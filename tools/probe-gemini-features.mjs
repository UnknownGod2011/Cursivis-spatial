// Probe Gemini features the backend depends on: structured JSON output, thinking controls,
// TTS output format, and inline audio input. Never prints key material.
import fs from "node:fs";
import path from "node:path";

const env = Object.fromEntries(
  fs.readFileSync(path.resolve("backend/.env"), "utf8")
    .split(/\r?\n/)
    .filter((l) => l.includes("=") && !l.trim().startsWith("#"))
    .map((l) => [l.slice(0, l.indexOf("=")).trim(), l.slice(l.indexOf("=") + 1).trim()])
);
const allKeys = (env.GEMINI_API_KEYS || "").split(/[,\s;]+/).filter(Boolean);
const allow = (process.env.KEYS_ALLOW || "").split(",").filter(Boolean).map(Number);
const keys = allow.length ? allKeys.filter((_, i) => allow.includes(i)) : allKeys;
let keyIndex = Number(process.env.KEY_INDEX || 0);
const BASE = "https://generativelanguage.googleapis.com/v1beta";

async function call(model, body) {
  const started = Date.now();
  const key = keys[keyIndex++ % keys.length];
  const res = await fetch(`${BASE}/models/${model}:generateContent`, {
    method: "POST",
    headers: { "x-goog-api-key": key, "content-type": "application/json" },
    body: JSON.stringify(body)
  });
  const json = await res.json().catch(() => ({}));
  return { status: res.status, ms: Date.now() - started, json };
}

function textOf(json) {
  return (json?.candidates?.[0]?.content?.parts || [])
    .filter((p) => !p.thought && typeof p.text === "string")
    .map((p) => p.text)
    .join("");
}

const schema = {
  type: "object",
  properties: {
    contentType: { type: "string" },
    summary: { type: "string" },
    suggestions: {
      type: "array",
      minItems: 4,
      maxItems: 4,
      items: {
        type: "object",
        properties: { id: { type: "string" }, label: { type: "string" } },
        required: ["id", "label"]
      }
    }
  },
  required: ["contentType", "summary", "suggestions"]
};

const which = process.argv[2] || "json";

if (which === "json") {
  const text = "Ingredients: wheat flour, sugar, palm oil, whey powder (milk), cocoa, soy lecithin (E322), raising agent (E500), salt, natural flavouring.";
  for (const model of process.argv.slice(3)) {
    for (const thinking of [undefined, { thinkingLevel: "minimal" }, { thinkingLevel: "low" }, { thinkingBudget: 0 }]) {
      const generationConfig = {
        responseMimeType: "application/json",
        responseJsonSchema: schema,
        ...(thinking ? { thinkingConfig: thinking } : {})
      };
      const r = await call(model, {
        systemInstruction: { parts: [{ text: "You suggest the four most useful distinct actions for a user's selected content. Labels max 3 words." }] },
        contents: [{ role: "user", parts: [{ text: `Selected text:\n${text}` }] }],
        generationConfig
      });
      const out = textOf(r.json);
      console.log(`${model}\tthinking=${JSON.stringify(thinking)}\tstatus=${r.status}\t${r.ms}ms\tthoughtTokens=${r.json?.usageMetadata?.thoughtsTokenCount ?? 0}\t${r.json?.error ? r.json.error.message.slice(0, 140) : out.slice(0, 220)}`);
    }
  }
}

if (which === "tts") {
  for (const model of process.argv.slice(3)) {
    const r = await call(model, {
      contents: [{ role: "user", parts: [{ text: "Say calmly: This product contains whey powder, which comes from milk, so it is not vegan." }] }],
      generationConfig: {
        responseModalities: ["AUDIO"],
        speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName: "Kore" } } }
      }
    });
    const part = r.json?.candidates?.[0]?.content?.parts?.find((p) => p.inlineData);
    const bytes = part ? Buffer.from(part.inlineData.data, "base64") : null;
    console.log(`${model}\tstatus=${r.status}\t${r.ms}ms\tmime=${part?.inlineData?.mimeType}\tbytes=${bytes?.length ?? 0}\t${r.json?.error ? r.json.error.message.slice(0, 160) : ""}`);
    if (bytes && process.env.SAVE_TTS) {
      fs.mkdirSync("backend/test-artifacts", { recursive: true });
      fs.writeFileSync(`backend/test-artifacts/tts-${model}.pcm`, bytes);
    }
  }
}

if (which === "audio") {
  // Uses a WAV file path given as argv[3]; model ids follow.
  const wav = fs.readFileSync(process.argv[3]).toString("base64");
  for (const model of process.argv.slice(4)) {
    const r = await call(model, {
      contents: [{ role: "user", parts: [
        { text: "Transcribe the spoken instruction exactly, then answer it briefly. Return JSON {\"transcript\":string,\"answer\":string}." },
        { inlineData: { mimeType: "audio/wav", data: wav } }
      ] }],
      generationConfig: { responseMimeType: "application/json" }
    });
    console.log(`${model}\tstatus=${r.status}\t${r.ms}ms\t${r.json?.error ? r.json.error.message.slice(0, 160) : textOf(r.json).slice(0, 240)}`);
  }
}
