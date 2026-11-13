// Probe which Gemini models the configured key pool can use, and their latency.
// Never prints key material: keys are referenced by index only.
import fs from "node:fs";
import path from "node:path";

const envPath = path.resolve(process.argv[2] || "backend/.env");
const env = Object.fromEntries(
  fs.readFileSync(envPath, "utf8")
    .split(/\r?\n/)
    .filter((l) => l.includes("=") && !l.trim().startsWith("#"))
    .map((l) => [l.slice(0, l.indexOf("=")).trim(), l.slice(l.indexOf("=") + 1).trim()])
);
const keys = (env.GEMINI_API_KEYS || "").split(/[,\s;]+/).filter(Boolean);
console.log(`keys configured: ${keys.length}`);

const BASE = "https://generativelanguage.googleapis.com/v1beta";

async function listModels(key) {
  const res = await fetch(`${BASE}/models?pageSize=200`, { headers: { "x-goog-api-key": key } });
  const body = await res.json().catch(() => ({}));
  return { status: res.status, body };
}

async function generate(key, model, text) {
  const started = Date.now();
  const res = await fetch(`${BASE}/models/${model}:generateContent`, {
    method: "POST",
    headers: { "x-goog-api-key": key, "content-type": "application/json" },
    body: JSON.stringify({
      contents: [{ role: "user", parts: [{ text }] }],
      generationConfig: { maxOutputTokens: 64 }
    })
  });
  const body = await res.json().catch(() => ({}));
  return {
    status: res.status,
    ms: Date.now() - started,
    text: body?.candidates?.[0]?.content?.parts?.map((p) => p.text).join("")?.slice(0, 80),
    error: body?.error ? `${body.error.status}: ${String(body.error.message).slice(0, 160)}` : undefined,
    usage: body?.usageMetadata
  };
}

const mode = process.argv[3] || "list";
if (mode === "list") {
  const { status, body } = await listModels(keys[0]);
  console.log(`list status ${status}`);
  if (body.error) console.log(body.error.status, body.error.message?.slice(0, 200));
  for (const m of body.models || []) {
    const methods = (m.supportedGenerationMethods || []).join(",");
    console.log(`${m.name.replace("models/", "")}\t${methods}\tin=${m.inputTokenLimit} out=${m.outputTokenLimit}`);
  }
} else if (mode === "gen") {
  const models = process.argv.slice(4);
  for (const model of models) {
    const r = await generate(keys[0], model, "Reply with exactly: ok");
    console.log(`${model}\tstatus=${r.status}\t${r.ms}ms\t${r.text ?? ""}\t${r.error ?? ""}\tthoughts=${r.usage?.thoughtsTokenCount ?? 0}`);
  }
} else if (mode === "keys") {
  const model = process.argv[4] || "gemini-3.6-flash";
  for (let i = 0; i < keys.length; i++) {
    const r = await generate(keys[i], model, "Reply with exactly: ok");
    console.log(`key#${i}\tstatus=${r.status}\t${r.ms}ms\t${r.error ?? r.text}`);
  }
}
