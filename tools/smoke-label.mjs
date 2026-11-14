// Real-AI check of the demo's product-label flow: crop of the energy drink label -> suggestions -> "Is this vegan?"
// -> caffeine follow-up. Usage: node tools/smoke-label.mjs [baseUrl]   (token from backend/.env)
import fs from "node:fs";
import path from "node:path";
const base = (process.argv[2] || "https://cursivis-spatial.vercel.app").replace(/\/$/, "");
const env = Object.fromEntries(
  fs.readFileSync(path.resolve("backend/.env"), "utf8").split(/\r?\n/)
    .filter((l) => l.includes("=") && !l.trim().startsWith("#"))
    .map((l) => [l.slice(0, l.indexOf("=")).trim(), l.slice(l.indexOf("=") + 1).trim()])
);
const token = (env.CURSIVIS_APP_TOKENS || "").split(",")[0];
const file = process.argv[3] || "test-results/local/label-crop.jpg";
const image = fs.readFileSync(path.resolve(file)).toString("base64");
const selection = { id: "label-1", kind: "image", source: "workspace", image: { mimeType: "image/jpeg", base64: image }, meta: { title: "Back of an energy drink can (ingredients and nutrition label)", docType: "product_label" } };
async function call(route, body) {
  const t0 = Date.now();
  const res = await fetch(`${base}${route}`, { method: "POST", headers: { "content-type": "application/json", "x-cursivis-token": token, "x-cursivis-install": "smoke-label" }, body: JSON.stringify(body) });
  const json = await res.json().catch(() => ({}));
  console.log(`${route} -> ${res.status} in ${Date.now() - t0} ms ${json.model || ""} ${json.error ? JSON.stringify(json.error) : ""}`);
  return json;
}
const s = await call("/v1/suggest", { selection });
console.log("  type:", s.contentType, "|", s.title, "|", (s.suggestions || []).map((x) => x.label).join(" | "));
const v = await call("/v1/talk", { mode: "context", selection, instruction: "Is this vegan?" });
console.log(`  vegan: ${v.title}\n${v.answer}\n  uncertainty=${v.uncertainty}`);
const c = await call("/v1/talk", { mode: "context", selection, instruction: "How much caffeine is in one can, and how many cans a day are safe for an adult?", history: [{ role: "user", text: "Is this vegan?" }, { role: "assistant", text: v.answer || "" }] });
console.log(`  caffeine: ${c.title}\n${c.answer}`);
