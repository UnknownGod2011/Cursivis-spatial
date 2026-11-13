import test from "node:test";
import assert from "node:assert/strict";
import { KeyPool } from "../../src/gemini/keyPool.js";
import { createGeminiClient, buildRequestBody } from "../../src/gemini/client.js";
import { classify, parseRetryAfterMs, toPublicError } from "../../src/gemini/errors.js";
import { KEY, fakeFetch, geminiOk, geminiErr } from "./helpers.js";

test("key pool prefers least-loaded, least-recently-used keys and skips cooling keys", () => {
  let t = 1000;
  const pool = new KeyPool([KEY(0), KEY(1), KEY(2)], { now: () => t });
  const a = pool.acquire();
  t += 1;
  const b = pool.acquire();
  assert.notEqual(a.index, b.index);
  pool.release(a, { ok: false, category: "quota", retryAfterMs: 5000 });
  pool.release(b, { ok: true });
  t += 1;
  const c = pool.acquire();
  assert.notEqual(c.index, a.index, "cooling key must not be selected");
  pool.release(c, { ok: false, category: "auth" });
  assert.equal(pool.stats().keysDisabled, 1);
  assert.equal(pool.stats().keysCoolingDown, 1);
  t += 6000;
  assert.equal(pool.stats().keysCoolingDown, 0, "quota cooldown expires");
});

test("invalid key shapes are dropped and never exposed in stats", () => {
  const pool = new KeyPool(["short", KEY(1), "has spaces in it which is not allowed at all ok"]);
  assert.equal(pool.size, 1);
  assert.ok(!JSON.stringify(pool.stats()).includes("AQ."));
});

test("error classification covers auth, quota, overload, invalid and network", () => {
  assert.equal(classify({ status: 403, body: { error: { status: "PERMISSION_DENIED", message: "The Gemini API free tier is not available for this project." } } }), "auth");
  assert.equal(classify({ status: 429, body: { error: { status: "RESOURCE_EXHAUSTED" } } }), "quota");
  assert.equal(classify({ status: 503, body: { error: { status: "UNAVAILABLE", message: "high demand" } } }), "overloaded");
  assert.equal(classify({ status: 400, body: { error: { status: "INVALID_ARGUMENT" } } }), "invalid");
  assert.equal(classify({ networkError: Object.assign(new Error("x"), { name: "AbortError" }) }), "timeout");
  assert.equal(parseRetryAfterMs({ error: { details: [{ "@type": "type.googleapis.com/google.rpc.RetryInfo", retryDelay: "7s" }] } }), 7000);
  const pub = toPublicError({ category: "quota", retryAfterMs: 9000 });
  assert.equal(pub.httpStatus, 429);
  assert.equal(pub.retryAfterSec, 9);
});

test("rotates to the next key on 403 and 429, then succeeds", async () => {
  const pool = new KeyPool([KEY(0), KEY(1), KEY(2)]);
  const fetchImpl = fakeFetch((call) => {
    if (call.index === 0) return geminiErr(403, "PERMISSION_DENIED", "The Gemini API free tier is not available for this project.");
    if (call.index === 1) return geminiErr(429, "RESOURCE_EXHAUSTED", "quota", [{ retryDelay: "3s" }]);
    return geminiOk("hello");
  });
  const client = createGeminiClient({ pool, fetchImpl, attemptTimeoutMs: 2000 });
  const r = await client.generate({ contents: [] }, { models: ["m1"], deadlineMs: 5000 });
  assert.equal(r.value, "hello");
  assert.equal(fetchImpl.calls.length, 3);
  assert.equal(new Set(fetchImpl.calls.map((c) => c.key)).size, 3, "each attempt used a different key");
  const stats = pool.stats();
  assert.equal(stats.keysDisabled, 1);
  assert.equal(stats.keysCoolingDown, 1);
});

test("falls back to the next model when a model is overloaded and remembers it", async () => {
  const pool = new KeyPool([KEY(0), KEY(1)]);
  const fetchImpl = fakeFetch((call) => (call.model === "busy-model" ? geminiErr(503, "UNAVAILABLE", "high demand") : geminiOk(`from ${call.model}`)));
  const client = createGeminiClient({ pool, fetchImpl, attemptTimeoutMs: 2000 });
  const r1 = await client.generate({ contents: [] }, { models: ["busy-model", "backup-model"], deadlineMs: 5000 });
  assert.equal(r1.model, "backup-model");
  const before = fetchImpl.calls.length;
  const r2 = await client.generate({ contents: [] }, { models: ["busy-model", "backup-model"], deadlineMs: 5000 });
  assert.equal(r2.model, "backup-model");
  assert.equal(fetchImpl.calls.length - before, 1, "overloaded model is skipped while cooling down");
});

test("retries once in compat mode (no thinkingConfig / schema) after INVALID_ARGUMENT", async () => {
  const pool = new KeyPool([KEY(0), KEY(1)]);
  const fetchImpl = fakeFetch((call) =>
    call.body.generationConfig.thinkingConfig ? geminiErr(400, "INVALID_ARGUMENT", "thinking not supported") : geminiOk("compat ok"));
  const client = createGeminiClient({ pool, fetchImpl, attemptTimeoutMs: 2000 });
  const r = await client.generate(
    { contents: [], thinkingLevel: "minimal", generationConfig: { responseMimeType: "application/json", responseJsonSchema: { type: "object" } } },
    { models: ["gemini-3.6-flash"], deadlineMs: 5000 }
  );
  assert.equal(r.value, "compat ok");
  assert.equal(fetchImpl.calls.length, 2);
  assert.ok(!fetchImpl.calls[1].body.generationConfig.responseJsonSchema);
});

test("hedges a slow attempt onto another key and returns the first result", async () => {
  const pool = new KeyPool([KEY(0), KEY(1)]);
  const fetchImpl = fakeFetch((call) => (call.index === 0 ? { ...geminiOk("slow"), delayMs: 1500 } : geminiOk("fast")));
  const client = createGeminiClient({ pool, fetchImpl, attemptTimeoutMs: 3000 });
  const started = Date.now();
  const r = await client.generate({ contents: [] }, { models: ["m1"], deadlineMs: 5000, hedgeAfterMs: 100 });
  assert.equal(r.value, "fast");
  assert.ok(Date.now() - started < 1200, "did not wait for the slow attempt");
  await new Promise((resolve) => setTimeout(resolve, 50));
  assert.equal(pool.entries.reduce((n, e) => n + e.inFlight, 0), 0, "no leaked in-flight slots");
});

test("schema-invalid output moves to the next model; safety blocks are not retried", async () => {
  const pool = new KeyPool([KEY(0), KEY(1)]);
  const fetchImpl = fakeFetch((call) => (call.model === "bad" ? geminiOk("not json") : geminiOk("{\"a\":1}")));
  const client = createGeminiClient({ pool, fetchImpl, attemptTimeoutMs: 2000 });
  const validate = (t) => {
    const v = JSON.parse(t);
    return v;
  };
  const r = await client.generate({ contents: [] }, { models: ["bad", "good"], deadlineMs: 5000, validate });
  assert.deepEqual(r.value, { a: 1 });

  const safetyFetch = fakeFetch(() => ({ status: 200, body: { promptFeedback: { blockReason: "SAFETY" } } }));
  const client2 = createGeminiClient({ pool: new KeyPool([KEY(0), KEY(1)]), fetchImpl: safetyFetch, attemptTimeoutMs: 2000 });
  await assert.rejects(() => client2.generate({ contents: [] }, { models: ["m"], deadlineMs: 5000 }), (e) => e.category === "safety");
  assert.equal(safetyFetch.calls.length, 1);
});

test("times out attempts and reports a deadline", async () => {
  const pool = new KeyPool([KEY(0)]);
  const fetchImpl = fakeFetch(() => ({ ...geminiOk("late"), delayMs: 5000 }));
  const client = createGeminiClient({ pool, fetchImpl, attemptTimeoutMs: 200 });
  await assert.rejects(() => client.generate({ contents: [] }, { models: ["m"], deadlineMs: 600 }), (e) => ["deadline", "timeout", "quota"].includes(e.category));
});

test("thinkingLevel is only sent to models that support it", () => {
  const req = { contents: [], thinkingLevel: "minimal", generationConfig: {} };
  assert.deepEqual(buildRequestBody(req, "gemini-3.6-flash", false).generationConfig.thinkingConfig, { thinkingLevel: "minimal" });
  assert.equal(buildRequestBody(req, "gemini-2.5-flash-preview-tts", false).generationConfig.thinkingConfig, undefined);
});
