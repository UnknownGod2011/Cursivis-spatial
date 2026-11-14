// Gemini REST client with key rotation, model fallback, per-attempt timeouts and hedged requests.
//
// Retry policy (per logical request):
//   auth / quota / transient           -> same model, next healthy key
//   timeout                            -> next model in the chain (a slow model tends to stay slow), new key
//   overloaded (503) / model missing   -> next model in the chain (model is marked unhealthy for a while)
//   invalid (400)                      -> retry once in "compat" mode (no thinkingConfig / JSON schema), then next model
//   bad_output (schema validation)     -> next model once, then fail
//   safety                             -> fail immediately
// Hedging: if the first attempt has not finished after `hedgeAfterMs`, a second attempt is launched on a
// different key (and, if available, the next model). From then on two attempts stay in flight: when one
// fails while the other is still running, a replacement is launched straight away instead of waiting for
// the slow one. The first valid result wins; the others are aborted.

import { GeminiError, classify, parseRetryAfterMs } from "./errors.js";

const BASE_URL = "https://generativelanguage.googleapis.com/v1beta";

export class OutputValidationError extends Error {
  constructor(message) {
    super(message);
    this.name = "OutputValidationError";
  }
}

function supportsThinkingLevel(model) {
  return /^gemini-3|^gemini-4|latest$/.test(model) && !/tts|image|live/.test(model);
}

export function buildRequestBody(request, model, compat) {
  const generationConfig = { ...(request.generationConfig || {}) };
  if (compat) {
    delete generationConfig.responseJsonSchema;
    delete generationConfig.thinkingConfig;
  } else if (request.thinkingLevel && supportsThinkingLevel(model)) {
    generationConfig.thinkingConfig = { thinkingLevel: request.thinkingLevel };
  }
  const body = { contents: request.contents, generationConfig };
  if (request.systemInstruction) {
    body.systemInstruction = { parts: [{ text: request.systemInstruction }] };
  }
  if (request.safetySettings) body.safetySettings = request.safetySettings;
  return body;
}

export function extractText(json) {
  const parts = json?.candidates?.[0]?.content?.parts || [];
  return parts
    .filter((p) => !p.thought && typeof p.text === "string")
    .map((p) => p.text)
    .join("")
    .trim();
}

export function extractAudio(json) {
  const parts = json?.candidates?.[0]?.content?.parts || [];
  const part = parts.find((p) => p.inlineData?.data);
  return part ? { mimeType: part.inlineData.mimeType || "", base64: part.inlineData.data } : null;
}

function isSafetyBlocked(json) {
  const block = json?.promptFeedback?.blockReason;
  const finish = json?.candidates?.[0]?.finishReason;
  return Boolean(block) || finish === "SAFETY" || finish === "PROHIBITED_CONTENT" || finish === "BLOCKLIST" || finish === "SPII";
}

export function createGeminiClient({ pool, fetchImpl = globalThis.fetch, attemptTimeoutMs = 22000, logger = null }) {
  async function runAttempt({ model, entry, request, compat, outerSignal, extract, validate }) {
    const controller = new AbortController();
    const onOuterAbort = () => controller.abort();
    outerSignal?.addEventListener("abort", onOuterAbort, { once: true });
    const timer = setTimeout(() => controller.abort(), attemptTimeoutMs);
    const startedAt = Date.now();
    let status = 0;
    let json = null;
    try {
      const res = await fetchImpl(`${BASE_URL}/models/${encodeURIComponent(model)}:generateContent`, {
        method: "POST",
        headers: { "content-type": "application/json", "x-goog-api-key": entry.key },
        body: JSON.stringify(buildRequestBody(request, model, compat)),
        signal: controller.signal
      });
      status = res.status;
      const raw = await res.text();
      try {
        json = raw ? JSON.parse(raw) : {};
      } catch {
        json = { error: { message: "Non-JSON response from provider" } };
      }
      const latencyMs = Date.now() - startedAt;
      if (!res.ok) {
        const category = classify({ status, body: json });
        return { ok: false, category, status, retryAfterMs: category === "quota" ? parseRetryAfterMs(json) : 0, latencyMs, model, entry };
      }
      if (isSafetyBlocked(json)) {
        return { ok: false, category: "safety", status, latencyMs, model, entry };
      }
      const payload = extract(json);
      if (payload == null || payload === "") {
        return { ok: false, category: "bad_output", status, latencyMs, model, entry, detail: "empty" };
      }
      let value = payload;
      if (validate) {
        try {
          value = validate(payload);
        } catch (e) {
          return { ok: false, category: "bad_output", status, latencyMs, model, entry, detail: String(e?.message || e).slice(0, 200) };
        }
      }
      return { ok: true, value, status, latencyMs, model, entry, usage: json?.usageMetadata };
    } catch (networkError) {
      const category = outerSignal?.aborted ? "aborted" : classify({ networkError });
      return { ok: false, category, status, latencyMs: Date.now() - startedAt, model, entry };
    } finally {
      clearTimeout(timer);
      outerSignal?.removeEventListener("abort", onOuterAbort);
    }
  }

  // request: { contents, systemInstruction, generationConfig, thinkingLevel }
  // options: { models, deadlineMs, hedgeAfterMs, extract, validate, label }
  async function generate(request, options) {
    const { models: chain, deadlineMs = 30000, hedgeAfterMs = 0, extract = extractText, validate = null, label = "generate" } = options;
    if (pool.size === 0) {
      throw new GeminiError("No Gemini API keys configured", { category: "auth" });
    }
    const startedAt = Date.now();
    const deadline = startedAt + deadlineMs;
    const models = pool.orderModels(chain);
    const triedKeysByModel = new Map(models.map((m) => [m, new Set()]));
    const compatModels = new Set();
    const badOutputModels = new Set();
    // Models that failed model-wide during this request (overloaded, missing, timed out): skipped when moving on.
    const slowModels = new Set();
    let modelIndex = 0;
    let hedged = false;
    let lastFailure = null;
    const trace = [];
    const outer = new AbortController();
    const inflight = new Map(); // promise -> meta

    const nextModelIndex = () => {
      for (let i = modelIndex + 1; i < models.length; i++) {
        if (!badOutputModels.has(models[i]) && !slowModels.has(models[i])) return i;
      }
      return -1;
    };
    const advancePast = (model) => {
      if (models[modelIndex] !== model) return;
      const n = nextModelIndex();
      if (n >= 0) modelIndex = n;
    };

    const launch = (preferNextModel = false) => {
      let idx = modelIndex;
      if (preferNextModel) {
        const n = nextModelIndex();
        if (n >= 0) idx = n;
      }
      for (let tries = 0; tries < models.length; tries++) {
        const model = models[idx];
        const tried = triedKeysByModel.get(model);
        const entry = pool.acquire(tried);
        if (entry) {
          tried.add(entry.index);
          const compat = compatModels.has(model);
          const p = runAttempt({ model, entry, request, compat, outerSignal: outer.signal, extract, validate });
          inflight.set(p, { model, entry });
          return true;
        }
        idx = (idx + 1) % models.length;
      }
      return false;
    };

    const sleep = (ms, tag) => new Promise((resolve) => setTimeout(() => resolve(tag), Math.max(0, ms)));
    // Attempts still running when we return/throw must release their key slot when they settle.
    const drainInflight = () => {
      for (const p of inflight.keys()) {
        p.then((r) => pool.release(r.entry, r.ok ? { ok: true } : { ok: false, category: "aborted" }));
      }
      inflight.clear();
    };

    try {
      if (!launch()) {
        const wait = pool.nextAvailableInMs();
        if (wait === Infinity || wait > deadline - Date.now()) {
          throw new GeminiError("No Gemini key available", { category: "quota", retryAfterMs: Number.isFinite(wait) ? wait : 30000 });
        }
        await sleep(wait + 50);
        if (!launch()) throw new GeminiError("No Gemini key available", { category: "quota", retryAfterMs: 15000 });
      }

      while (inflight.size > 0) {
        const remaining = deadline - Date.now();
        if (remaining <= 0) throw new GeminiError("Deadline exceeded", { category: "deadline" });
        const racers = [...inflight.keys()];
        const timers = [sleep(remaining, "deadline")];
        if (hedgeAfterMs > 0 && !hedged && inflight.size === 1) {
          timers.push(sleep(hedgeAfterMs - (Date.now() - startedAt), "hedge"));
        }
        const winner = await Promise.race([...racers.map((p) => p.then((r) => ({ r, p }))), ...timers]);
        if (winner === "deadline") throw new GeminiError("Deadline exceeded", { category: "deadline" });
        if (winner === "hedge") {
          hedged = true;
          launch(true);
          trace.push("hedge");
          continue;
        }
        const { r, p } = winner;
        inflight.delete(p);
        pool.release(r.entry, r.ok ? { ok: true } : { ok: false, category: r.category, retryAfterMs: r.retryAfterMs });
        trace.push(`${r.model}#${r.entry.index}:${r.ok ? "ok" : r.category}:${r.latencyMs}ms`);
        if (r.ok) {
          pool.markModel(r.model, "ok");
          outer.abort();
          drainInflight();
          const result = { value: r.value, model: r.model, latencyMs: Date.now() - startedAt, attempts: trace.length, trace, usage: r.usage };
          logger?.({ label, ok: true, model: r.model, ms: result.latencyMs, trace });
          return result;
        }
        lastFailure = r;
        if (r.category === "aborted") continue;
        if (r.category === "safety") {
          throw new GeminiError("Blocked by safety filters", { category: "safety", model: r.model });
        }
        if (r.category === "overloaded" || r.category === "model") {
          pool.markModel(r.model, r.category);
          slowModels.add(r.model);
          advancePast(r.model);
        } else if (r.category === "timeout") {
          slowModels.add(r.model);
          advancePast(r.model);
        } else if (r.category === "invalid") {
          if (!compatModels.has(r.model)) {
            compatModels.add(r.model);
            triedKeysByModel.get(r.model).delete(r.entry.index);
          } else {
            const n = nextModelIndex();
            if (n >= 0) modelIndex = n;
          }
        } else if (r.category === "bad_output") {
          badOutputModels.add(r.model);
          const n = nextModelIndex();
          if (n >= 0) modelIndex = n;
          else if (badOutputModels.size >= 2) {
            throw new GeminiError(`Model output failed validation: ${r.detail || ""}`, { category: "bad_output", model: r.model });
          }
        }
        if (hedged && inflight.size === 1) {
          // Past the hedge point and one attempt just failed: keep a second attempt racing the survivor.
          if (launch(true)) trace.push("rehedge");
          continue;
        }
        if (inflight.size === 0) {
          if (!launch()) {
            // All keys for the current model tried; move down the chain.
            const n = nextModelIndex();
            if (n >= 0) {
              modelIndex = n;
              if (launch()) continue;
            }
            const wait = pool.nextAvailableInMs();
            if (Number.isFinite(wait) && wait < Math.min(3000, deadline - Date.now())) {
              await sleep(wait + 50);
              for (const s of triedKeysByModel.values()) s.clear();
              if (launch()) continue;
            }
            break;
          }
        }
      }
      const category = lastFailure?.category === "bad_output" ? "bad_output" : lastFailure?.category || "transient";
      throw new GeminiError(`All attempts failed (${trace.join(", ")})`, {
        category,
        retryAfterMs: lastFailure?.retryAfterMs || 0,
        model: lastFailure?.model || ""
      });
    } catch (error) {
      outer.abort();
      drainInflight();
      logger?.({ label, ok: false, ms: Date.now() - startedAt, category: error?.category, trace });
      throw error;
    }
  }

  return { generate };
}
