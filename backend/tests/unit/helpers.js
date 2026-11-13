// Test helpers: a scripted fake of the Gemini REST endpoint (no network) and an HTTP harness.
export const KEY = (n) => `AQ.TestKey${String(n).padStart(2, "0")}_abcdefghijklmnopqrstuvwxyz0123`;

export function geminiOk(text, extra = {}) {
  return {
    status: 200,
    body: { candidates: [{ content: { parts: [{ text }] }, finishReason: "STOP" }], usageMetadata: { totalTokenCount: 10 }, ...extra }
  };
}

export function geminiErr(status, statusText, message, details) {
  return { status, body: { error: { code: status, status: statusText, message, ...(details ? { details } : {}) } } };
}

// handler(call) -> { status, body, delayMs }. `call` = { model, key, body, index }
export function fakeFetch(handler) {
  const calls = [];
  const impl = async (url, init) => {
    const model = decodeURIComponent(String(url).match(/models\/([^:]+):/)[1]);
    const key = init.headers["x-goog-api-key"];
    const body = JSON.parse(init.body);
    const call = { model, key, body, index: calls.length };
    calls.push(call);
    const r = await handler(call);
    if (r.delayMs) {
      await new Promise((resolve, reject) => {
        const t = setTimeout(resolve, r.delayMs);
        init.signal?.addEventListener("abort", () => {
          clearTimeout(t);
          const e = new Error("aborted");
          e.name = "AbortError";
          reject(e);
        });
      });
    }
    return {
      ok: r.status >= 200 && r.status < 300,
      status: r.status,
      text: async () => JSON.stringify(r.body)
    };
  };
  impl.calls = calls;
  return impl;
}

export async function withServer(app, fn) {
  const server = await new Promise((resolve) => {
    const s = app.listen(0, "127.0.0.1", () => resolve(s));
  });
  const base = `http://127.0.0.1:${server.address().port}`;
  try {
    return await fn(base);
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
}

export function suggestJson(overrides = {}) {
  return JSON.stringify({
    contentType: "food_label",
    title: "Snack ingredients label",
    summary: "An ingredients list for a packaged snack.",
    quality: "clear",
    confidence: 0.9,
    suggestions: [
      { id: "analyze_ingredients", label: "Analyze Ingredients", intent: "Explain the ingredients.", icon: "list" },
      { id: "check_allergens", label: "Check Allergens", intent: "List allergens.", icon: "alert" },
      { id: "check_nutrition", label: "Check Nutrition", intent: "Explain nutrition.", icon: "nutrition" },
      { id: "explain_additives", label: "Explain Additives", intent: "Explain E-numbers.", icon: "explain" }
    ],
    ...overrides
  });
}

export function tinyWavBase64(ms = 600, rate = 16000) {
  const samples = Math.round((rate * ms) / 1000);
  const pcm = Buffer.alloc(samples * 2);
  for (let i = 0; i < samples; i++) pcm.writeInt16LE(Math.round(Math.sin(i / 8) * 3000), i * 2);
  const header = Buffer.alloc(44);
  header.write("RIFF", 0, "ascii");
  header.writeUInt32LE(36 + pcm.length, 4);
  header.write("WAVE", 8, "ascii");
  header.write("fmt ", 12, "ascii");
  header.writeUInt32LE(16, 16);
  header.writeUInt16LE(1, 20);
  header.writeUInt16LE(1, 22);
  header.writeUInt32LE(rate, 24);
  header.writeUInt32LE(rate * 2, 28);
  header.writeUInt16LE(2, 32);
  header.writeUInt16LE(16, 34);
  header.write("data", 36, "ascii");
  header.writeUInt32LE(pcm.length, 40);
  return Buffer.concat([header, pcm]).toString("base64");
}
