// LIVE integration tests: real backend + real Gemini key pool + fixture selections.
// Requires backend/.env with GEMINI_API_KEYS. Run: npm run test:live
// Writes measured latencies to test-results/local/backend-live-metrics.json (repo root).
import test from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createApp } from "../../src/app.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../..");
const fixtures = path.join(root, "tests/fixtures");
for (const line of fs.readFileSync(path.join(root, ".env"), "utf8").split(/\r?\n/)) {
  const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/);
  if (m) process.env[m[1]] = m[2];
}
process.env.CURSIVIS_REQUIRE_TOKEN = "0";
process.env.CURSIVIS_LOG_REQUESTS = "1";

const metrics = [];
let base;
let server;

const img = (file, mime) => ({ mimeType: mime, base64: fs.readFileSync(path.join(fixtures, file)).toString("base64") });
const sel = (id, extra) => ({ id, source: "camera", ...extra });

async function call(route, body, label) {
  const started = Date.now();
  const res = await fetch(`${base}${route}`, { method: "POST", headers: { "content-type": "application/json", "x-cursivis-install": "live-test" }, body: JSON.stringify(body) });
  const ms = Date.now() - started;
  const type = res.headers.get("content-type") || "";
  const payload = type.includes("json") ? await res.json() : Buffer.from(await res.arrayBuffer());
  metrics.push({ label, route, status: res.status, ms, model: payload?.model || res.headers.get("x-cursivis-model") || null });
  if (res.status !== 200) console.log(`[live] ${label} -> ${res.status} ${JSON.stringify(payload).slice(0, 300)}`);
  return { status: res.status, body: payload };
}

const words = (s) => String(s || "").toLowerCase();
const anyOf = (text, list) => list.some((w) => words(text).includes(w));
const labels = (r) => r.body.suggestions.map((s) => `${s.label} ${s.id}`).join(" | ");

test.before(async () => {
  const app = createApp();
  server = await new Promise((resolve) => { const s = app.listen(0, "127.0.0.1", () => resolve(s)); });
  base = `http://127.0.0.1:${server.address().port}`;
});

test.after(async () => {
  await new Promise((resolve) => server.close(resolve));
  const out = path.resolve(root, "../test-results/local/backend-live-metrics.json");
  fs.mkdirSync(path.dirname(out), { recursive: true });
  fs.writeFileSync(out, JSON.stringify({ when: new Date().toISOString(), node: process.version, metrics }, null, 2));
  console.log(`[live] metrics -> ${out}`);
});

test("Scenario 1: food label -> relevant suggestions -> spoken 'is this vegan?' -> grounded verdict", async () => {
  const selection = sel("food-1", { kind: "image", image: img("food-label.png", "image/png") });
  const s = await call("/v1/suggest", { selection }, "suggest:food");
  assert.equal(s.status, 200);
  assert.equal(s.body.suggestions.length, 4);
  console.log(`[live] food suggestions: ${labels(s)} (${s.body.contentType}, ${s.body.title})`);
  const relevant = s.body.suggestions.filter((x) => anyOf(`${x.label} ${x.id} ${x.intent}`, ["ingredient", "allerg", "nutri", "additive", "vegan", "sugar", "diet", "calorie", "health"]));
  assert.ok(relevant.length >= 2, `expected food-specific suggestions, got: ${labels(s)}`);

  // Synthesize the spoken question with TTS (also exercises /v1/speak), then send it as Talk audio.
  const tts = await call("/v1/speak", { text: "Is this vegan?" }, "speak:question");
  assert.equal(tts.status, 200);
  assert.equal(tts.body.toString("ascii", 0, 4), "RIFF");
  const t = await call("/v1/talk", { mode: "context", selection, audio: { mimeType: "audio/wav", base64: tts.body.toString("base64") } }, "talk:food-vegan-audio");
  assert.equal(t.status, 200);
  console.log(`[live] heard: "${t.body.transcript}" -> ${t.body.title}: ${t.body.answer.slice(0, 200)}`);
  assert.ok(anyOf(t.body.transcript, ["vegan"]), "transcript should contain 'vegan'");
  assert.ok(anyOf(t.body.answer, ["not vegan", "isn't vegan", "is not vegan", "not suitable for vegans", "not be vegan"]), "verdict should be not vegan");
  assert.ok(anyOf(t.body.answer, ["milk", "whey"]), "should cite milk/whey evidence");

  const follow = await call("/v1/talk", {
    mode: "context", selection, instruction: "What exactly makes it not vegan, and is anything else uncertain?",
    history: [{ role: "user", text: t.body.transcript }, { role: "assistant", text: t.body.answer }]
  }, "talk:food-followup");
  assert.equal(follow.status, 200);
  assert.ok(anyOf(follow.body.answer, ["milk", "whey"]));
});

test("Scenario 1b: nutrition-only crop -> qualified answer, no fabricated ingredients", async () => {
  const selection = sel("food-2", { kind: "image", image: img("food-label-nutrition-only.png", "image/png") });
  const t = await call("/v1/talk", { mode: "context", selection, instruction: "Is this vegan?" }, "talk:nutrition-only-vegan");
  assert.equal(t.status, 200);
  console.log(`[live] nutrition-only: uncertainty=${t.body.uncertainty} ${t.body.answer.slice(0, 220)}`);
  assert.ok(["medium", "high"].includes(t.body.uncertainty), `expected qualified uncertainty, got ${t.body.uncertainty}`);
  assert.ok(anyOf(`${t.body.answer} ${t.body.uncertaintyNote}`, ["ingredient", "not visible", "cannot", "can't", "unable", "not enough", "doesn't show", "does not show", "no ingredient"]));
  assert.ok(!anyOf(t.body.answer, ["whey", "hazelnut"]), "must not invent ingredients that are not visible in the crop");
});

test("Scenario 2: printed French -> translate suggested -> translation -> follow-up", async () => {
  const selection = sel("fr-1", { kind: "image", image: img("printed-french.png", "image/png") });
  const s = await call("/v1/suggest", { selection }, "suggest:french");
  assert.equal(s.status, 200);
  console.log(`[live] french suggestions: ${labels(s)}`);
  const translate = s.body.suggestions.find((x) => anyOf(`${x.label} ${x.id}`, ["translat"]));
  assert.ok(translate, `expected a translate suggestion, got: ${labels(s)}`);
  const a = await call("/v1/act", { selection, action: translate }, "act:translate");
  assert.equal(a.status, 200);
  console.log(`[live] translation (${a.body.answer.length} chars): ${a.body.answer.slice(0, 200)}`);
  assert.ok(anyOf(a.body.answer, ["library"]) && anyOf(a.body.answer, ["tuesday"]) && anyOf(a.body.answer, ["three weeks", "3 weeks"]));
  const f = await call("/v1/talk", {
    mode: "context", selection, instruction: "Explain the borrowing rules more simply.",
    history: [{ role: "user", text: translate.label }, { role: "assistant", text: a.body.answer }]
  }, "talk:french-followup");
  assert.equal(f.status, 200);
  assert.ok(anyOf(f.body.answer, ["three weeks", "3 weeks", "once"]));
});

test("Scenario 3: workspace photo -> caption suggested -> caption -> regenerate differs", async () => {
  const selection = { id: "photo-1", source: "workspace", kind: "image", image: img("photo-footbridge.jpg", "image/jpeg"), meta: { title: "Footbridge photo" } };
  const s = await call("/v1/suggest", { selection }, "suggest:photo");
  assert.equal(s.status, 200);
  console.log(`[live] photo suggestions: ${labels(s)}`);
  const caption = s.body.suggestions.find((x) => anyOf(`${x.label} ${x.id}`, ["caption"])) || { id: "generate_caption", label: "Generate Caption" };
  const a = await call("/v1/act", { selection, action: caption }, "act:caption");
  assert.equal(a.status, 200);
  assert.ok(anyOf(a.body.answer, ["bridge"]), "caption should reflect the photo (bridge)");
  const b = await call("/v1/act", { selection, action: caption, regenerate: true }, "act:caption-regenerate");
  assert.equal(b.status, 200);
  assert.notEqual(a.body.answer, b.body.answer);
});

test("Scenario: code selection -> bug-focused suggestions -> debug finds the off-by-one", async () => {
  const code = fs.readFileSync(path.join(fixtures, "buggy-code.txt"), "utf8");
  const selection = { id: "code-1", source: "workspace", kind: "text", text: code, meta: { title: "reviews.js", docType: "code" } };
  const s = await call("/v1/suggest", { selection }, "suggest:code");
  assert.equal(s.status, 200);
  console.log(`[live] code suggestions: ${labels(s)}`);
  assert.ok(s.body.suggestions.some((x) => anyOf(`${x.label} ${x.id}`, ["bug", "debug", "fix", "error", "issue"])));
  const bug = s.body.suggestions.find((x) => anyOf(`${x.label} ${x.id}`, ["bug", "debug", "fix", "error", "issue"]));
  const a = await call("/v1/act", { selection, action: bug }, "act:debug");
  assert.equal(a.status, 200);
  assert.ok(anyOf(a.body.answer, ["< reviews.length", "<= reviews.length", "off-by-one", "out of bounds", "undefined"]));
});

test("Scenario: handwritten note -> tasks", async () => {
  const selection = sel("note-1", { kind: "image", image: img("handwritten-note.png", "image/png") });
  const s = await call("/v1/suggest", { selection }, "suggest:note");
  assert.equal(s.status, 200);
  console.log(`[live] note suggestions: ${labels(s)}`);
  const a = await call("/v1/act", { selection, action: { id: "create_tasks", label: "Create Tasks", intent: "Turn the note into a checklist" } }, "act:tasks");
  assert.equal(a.status, 200);
  assert.ok(anyOf(a.body.answer, ["plumber"]) && anyOf(a.body.answer, ["dentist"]));
});

test("Scenario 4: general orb conversation (no selection) + client actions", async () => {
  const t = await call("/v1/talk", { mode: "general", instruction: "What can you do?" }, "talk:general");
  assert.equal(t.status, 200);
  assert.equal(t.body.mode, "general");
  assert.ok(anyOf(t.body.answer, ["select"]));
  const c = await call("/v1/talk", { mode: "general", instruction: "Please read your last answer out loud.", history: [{ role: "user", text: "What can you do?" }, { role: "assistant", text: t.body.answer }] }, "talk:general-read");
  assert.equal(c.status, 200);
  assert.equal(c.body.clientAction, "read_aloud");
});
