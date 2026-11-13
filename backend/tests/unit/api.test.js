import test from "node:test";
import assert from "node:assert/strict";
import { createApp } from "../../src/app.js";
import { normalizeSuggestions, cleanLabel, isUnsupportedAction, canonicalActionId } from "../../src/cursivis/actions.js";
import { pcmToWav, inspectWav, ensureWav } from "../../src/cursivis/wav.js";
import { KEY, fakeFetch, geminiOk, withServer, suggestJson, tinyWavBase64 } from "./helpers.js";

const TOKEN = "test-token-123";
const env = (extra = {}) => ({ GEMINI_API_KEYS: `${KEY(0)},${KEY(1)}`, CURSIVIS_APP_TOKENS: TOKEN, ...extra });
const headers = { "content-type": "application/json", "x-cursivis-token": TOKEN, "x-cursivis-install": "install-1" };
const textSelection = { id: "sel-1", kind: "text", source: "workspace", text: "Ingredients: wheat flour, sugar, whey powder (milk)." };
const imageSelection = { id: "sel-2", kind: "image", source: "camera", image: { mimeType: "image/jpeg", base64: Buffer.alloc(64, 7).toString("base64") } };

async function post(base, path, body, h = headers) {
  const res = await fetch(`${base}${path}`, { method: "POST", headers: h, body: JSON.stringify(body) });
  const type = res.headers.get("content-type") || "";
  return { status: res.status, body: type.includes("json") ? await res.json() : Buffer.from(await res.arrayBuffer()), headers: res.headers };
}

test("health reports configuration without secrets", async () => {
  const app = createApp({ env: env(), fetchImpl: fakeFetch(() => geminiOk("x")) });
  await withServer(app, async (base) => {
    const res = await fetch(`${base}/v1/health`);
    const json = await res.json();
    assert.equal(json.ok, true);
    assert.equal(json.ai.keys, 2);
    assert.ok(!JSON.stringify(json).includes("AQ."));
  });
});

test("rejects missing token, invalid payloads and oversized bodies", async () => {
  const app = createApp({ env: env({ CURSIVIS_BODY_LIMIT: "50kb" }), fetchImpl: fakeFetch(() => geminiOk(suggestJson())) });
  await withServer(app, async (base) => {
    let r = await post(base, "/v1/suggest", { selection: textSelection }, { "content-type": "application/json" });
    assert.equal(r.status, 401);
    r = await post(base, "/v1/suggest", { selection: { id: "bad id!", kind: "text", text: "x" } });
    assert.equal(r.status, 400);
    r = await post(base, "/v1/suggest", { selection: { id: "a", kind: "text", text: "   " } });
    assert.equal(r.status, 400);
    assert.match(r.body.error.message, /empty/);
    r = await post(base, "/v1/suggest", { selection: { ...textSelection, text: "x".repeat(60000) } });
    assert.equal(r.status, 413);
  });
});

test("suggest returns exactly four normalized suggestions and caches by selection content", async () => {
  const fetchImpl = fakeFetch(() => geminiOk(suggestJson()));
  const app = createApp({ env: env(), fetchImpl });
  await withServer(app, async (base) => {
    const r1 = await post(base, "/v1/suggest", { selection: textSelection });
    assert.equal(r1.status, 200);
    assert.equal(r1.body.suggestions.length, 4);
    assert.equal(r1.body.selectionId, "sel-1");
    assert.equal(r1.body.source, "ai");
    const r2 = await post(base, "/v1/suggest", { selection: { ...textSelection, id: "sel-9" } });
    assert.equal(r2.body.cached, true);
    assert.equal(r2.body.selectionId, "sel-9", "cached result is re-keyed to the new selection id");
    assert.equal(fetchImpl.calls.length, 1);
    const sent = fetchImpl.calls[0].body;
    assert.equal(sent.generationConfig.responseMimeType, "application/json");
    assert.ok(sent.systemInstruction.parts[0].text.includes("Exactly four"));
  });
});

test("suggest pads with labelled fallbacks when the model returns too few or unsafe options", async () => {
  const fetchImpl = fakeFetch(() => geminiOk(suggestJson({
    suggestions: [
      { id: "search_web", label: "Search the Web", intent: "x", icon: "sparkle" },
      { id: "talk", label: "Talk", intent: "x", icon: "chat" },
      { id: "summarize", label: "Summarize", intent: "x", icon: "summary" }
    ]
  })));
  const app = createApp({ env: env(), fetchImpl });
  await withServer(app, async (base) => {
    const r = await post(base, "/v1/suggest", { selection: imageSelection });
    assert.equal(r.status, 200);
    assert.equal(r.body.suggestions.length, 4);
    assert.equal(r.body.source, "ai+fallback");
    assert.ok(!r.body.suggestions.some((s) => s.id === "search_web" || s.id === "talk"));
  });
});

test("act executes the chosen action and returns a shaped result", async () => {
  const fetchImpl = fakeFetch(() => geminiOk(JSON.stringify({
    title: "Not vegan", answer: "**Not vegan.** Contains whey powder (milk).", evidence: "Ingredients list", uncertainty: "low", uncertaintyNote: "", followUps: ["Check Allergens"]
  })));
  const app = createApp({ env: env(), fetchImpl });
  await withServer(app, async (base) => {
    const r = await post(base, "/v1/act", { selection: textSelection, action: { id: "check_vegan", label: "Check Vegan", intent: "Is it vegan" } });
    assert.equal(r.status, 200);
    assert.equal(r.body.actionId, "check_vegan");
    assert.match(r.body.answer, /whey/);
    assert.equal(r.body.uncertaintyNote, "");
    const prompt = fetchImpl.calls[0].body.contents[0].parts.map((p) => p.text || "").join("\n");
    assert.match(prompt, /animal-derived/, "uses the specialised vegan guidance");
  });
});

test("talk validates WAV audio and supports general mode", async () => {
  const fetchImpl = fakeFetch(() => geminiOk(JSON.stringify({
    transcript: "is this vegan", title: "Vegan check", answer: "No.", evidence: "", uncertainty: "low", uncertaintyNote: "", followUps: [], clientAction: "none"
  })));
  const app = createApp({ env: env(), fetchImpl });
  await withServer(app, async (base) => {
    let r = await post(base, "/v1/talk", { mode: "context", selection: textSelection, audio: { mimeType: "audio/wav", base64: Buffer.alloc(100).toString("base64") } });
    assert.equal(r.status, 400, "non-WAV audio rejected");
    r = await post(base, "/v1/talk", { mode: "context", selection: textSelection, audio: { mimeType: "audio/wav", base64: tinyWavBase64(800) } });
    assert.equal(r.status, 200);
    assert.equal(r.body.transcript, "is this vegan");
    assert.equal(r.body.mode, "context");
    const parts = fetchImpl.calls[0].body.contents[0].parts;
    assert.ok(parts.some((p) => p.inlineData?.mimeType === "audio/wav"));
    r = await post(base, "/v1/talk", { mode: "general", instruction: "What can you do?" });
    assert.equal(r.status, 200);
    assert.equal(r.body.mode, "general");
    assert.equal(r.body.selectionId, null);
  });
});

test("speak returns WAV audio, wrapping raw PCM when needed", async () => {
  const pcm = Buffer.alloc(4800, 1);
  const fetchImpl = fakeFetch(() => ({ status: 200, body: { candidates: [{ content: { parts: [{ inlineData: { mimeType: "audio/L16;codec=pcm;rate=24000", data: pcm.toString("base64") } }] } }] } }));
  const app = createApp({ env: env(), fetchImpl });
  await withServer(app, async (base) => {
    const r = await post(base, "/v1/speak", { text: "Hello there." });
    assert.equal(r.status, 200);
    assert.equal(r.headers.get("content-type"), "audio/wav");
    const info = inspectWav(r.body);
    assert.equal(info.sampleRate, 24000);
    assert.equal(info.dataBytes, pcm.length);
  });
});

test("per-install rate limiting returns 429 with retry-after", async () => {
  const app = createApp({ env: env({ CURSIVIS_RATE_LIMIT_PER_MINUTE: "2" }), fetchImpl: fakeFetch(() => geminiOk(suggestJson())) });
  await withServer(app, async (base) => {
    await post(base, "/v1/suggest", { selection: textSelection });
    await post(base, "/v1/suggest", { selection: textSelection });
    const r = await post(base, "/v1/suggest", { selection: textSelection });
    assert.equal(r.status, 429);
    assert.ok(Number(r.headers.get("retry-after")) > 0);
  });
});

test("provider failures map to safe public errors", async () => {
  const app = createApp({ env: env(), fetchImpl: fakeFetch(() => ({ status: 403, body: { error: { status: "PERMISSION_DENIED", message: "secret detail" } } })) });
  await withServer(app, async (base) => {
    const r = await post(base, "/v1/act", { selection: textSelection, action: { id: "summarize", label: "Summarize" } });
    assert.equal(r.status, 503);
    assert.equal(r.body.error.code, "ai_unavailable");
    assert.ok(!JSON.stringify(r.body).includes("secret detail"));
  });
});

test("suggestion normalization: canonical ids, labels, unsupported filtering", () => {
  assert.equal(canonicalActionId("Generate Captions"), "generate_caption");
  assert.equal(cleanLabel("check what's allergens in this product label"), "Check What's Allergens in");
  assert.ok(isUnsupportedAction("search_web"));
  assert.ok(isUnsupportedAction("identify_person"));
  assert.ok(!isUnsupportedAction("translate_label"));
  const { suggestions } = normalizeSuggestions([
    { id: "summarize", label: "Summarize", intent: "a", icon: "summary" },
    { id: "summarise", label: "Summarize!", intent: "b", icon: "summary" }
  ], { kind: "text", text: "plain prose" });
  assert.equal(suggestions.length, 4);
  assert.equal(suggestions.filter((s) => s.id === "summarize").length, 1);
});

test("wav helpers round-trip", () => {
  const wav = pcmToWav(Buffer.alloc(3200), 16000);
  const info = inspectWav(wav);
  assert.equal(info.durationMs, 100);
  assert.equal(ensureWav(wav.toString("base64"), "audio/wav").length, wav.length);
});
