// End-to-end API smoke test (suggest -> act -> talk follow-up -> general talk) against a running backend.
// Usage: node tools/smoke-flow.mjs [baseUrl]   (token read from backend/.env CURSIVIS_APP_TOKENS)
import fs from "node:fs";
import path from "node:path";

const base = (process.argv[2] || "http://127.0.0.1:8787").replace(/\/$/, "");
const env = Object.fromEntries(
  fs.readFileSync(path.resolve("backend/.env"), "utf8").split(/\r?\n/)
    .filter((l) => l.includes("=") && !l.trim().startsWith("#"))
    .map((l) => [l.slice(0, l.indexOf("=")).trim(), l.slice(l.indexOf("=") + 1).trim()])
);
const token = (env.CURSIVIS_APP_TOKENS || "").split(",")[0];
const research = "Urban heat islands raise night-time temperatures in dense neighbourhoods by up to 4 °C compared with surrounding rural areas. In a two-summer field study across 38 rooftops, we compared extensive green roofs with white reflective membranes. Green roofs reduced peak surface temperature by 21 °C and indoor top-floor temperature by 1.8 °C, while reflective roofs achieved a larger surface reduction (27 °C) but a smaller indoor effect (1.1 °C) because they provide no evapotranspiration. Benefits were strongest during heatwaves and declined when substrate moisture fell below 12%. We argue that irrigation-aware green roofs should be prioritised for buildings with vulnerable occupants.";
const selection = { id: "smoke-1", kind: "text", source: "workspace", text: research, meta: { title: "Research abstract", docType: "research_paper" } };

async function call(route, body) {
  const t0 = Date.now();
  const res = await fetch(`${base}${route}`, { method: "POST", headers: { "content-type": "application/json", "x-cursivis-token": token, "x-cursivis-install": "smoke" }, body: JSON.stringify(body) });
  const json = await res.json().catch(() => ({}));
  console.log(`${route} -> ${res.status} in ${Date.now() - t0} ms ${json.model || ""} ${json.error ? JSON.stringify(json.error) : ""}`);
  return json;
}

const s = await call("/v1/suggest", { selection });
console.log("  suggestions:", (s.suggestions || []).map((x) => x.label).join(" | "));
const key = (s.suggestions || []).find((x) => /finding/i.test(x.label)) || s.suggestions?.[0];
const a = await call("/v1/act", { selection, action: key });
console.log(`  answer ${a.answer?.length} chars`);
const f = await call("/v1/talk", { mode: "context", selection, instruction: "Explain the main finding like I'm 12.", history: [{ role: "user", text: key.label }, { role: "assistant", text: a.answer || "" }] });
console.log(`  follow-up: ${f.title} (${f.answer?.length} chars)`);
const g = await call("/v1/talk", { mode: "general", instruction: "What can you do?" });
console.log(`  general: ${g.title}`);
