// Generates a fixture image with a Gemini image model (used only for original, unbranded test/demo content).
// Usage: node tools/gen-image.mjs <out.png> "<prompt>" [model...]
import fs from "node:fs";
import path from "node:path";

const env = Object.fromEntries(
  fs.readFileSync(path.resolve("backend/.env"), "utf8").split(/\r?\n/)
    .filter((l) => l.includes("=") && !l.trim().startsWith("#"))
    .map((l) => [l.slice(0, l.indexOf("=")).trim(), l.slice(l.indexOf("=") + 1).trim()])
);
const keys = env.GEMINI_API_KEYS.split(/[,\s;]+/).filter(Boolean);
const [out, prompt, ...models] = process.argv.slice(2);
const chain = models.length ? models : ["gemini-3.1-flash-image", "gemini-2.5-flash-image", "gemini-3-pro-image"];

for (const model of chain) {
  for (const key of keys) {
    const started = Date.now();
    const res = await fetch(`https://generativelanguage.googleapis.com/v1beta/models/${model}:generateContent`, {
      method: "POST",
      headers: { "content-type": "application/json", "x-goog-api-key": key },
      body: JSON.stringify({
        contents: [{ role: "user", parts: [{ text: prompt }] }],
        generationConfig: { responseModalities: ["IMAGE"] }
      })
    });
    const json = await res.json().catch(() => ({}));
    if (res.status === 403 || res.status === 429) continue; // try next key
    const part = json?.candidates?.[0]?.content?.parts?.find((p) => p.inlineData?.data);
    if (part) {
      fs.mkdirSync(path.dirname(out), { recursive: true });
      fs.writeFileSync(out, Buffer.from(part.inlineData.data, "base64"));
      console.log(`ok ${model} ${Date.now() - started}ms ${part.inlineData.mimeType} -> ${out}`);
      process.exit(0);
    }
    console.log(`${model} status=${res.status} ${json?.error?.message?.slice(0, 140) || "no image"}`);
    break; // non-key error: try next model
  }
}
process.exit(1);
