// Minimal client for the Meta XR Operator in-app MCP server (HTTP, http://127.0.0.1:8720/mcp).
// Usage:
//   node tools/xrop.mjs list                         -> list tool names
//   node tools/xrop.mjs describe <tool>              -> tool schema
//   node tools/xrop.mjs call <tool> '<json args>'    -> call a tool (image results saved to test-results/local/shots/)
//   node tools/xrop.mjs script <file.json>           -> run a sequence [{tool,args,waitMs}]
import fs from "node:fs";
import path from "node:path";

const URL_ = process.env.XROP_URL || "http://127.0.0.1:8720/mcp";
let sessionId = null;
let nextId = 1;

async function rpc(method, params, isNotification = false) {
  const body = isNotification ? { jsonrpc: "2.0", method, params } : { jsonrpc: "2.0", id: nextId++, method, params };
  const headers = { "content-type": "application/json", accept: "application/json, text/event-stream" };
  if (sessionId) headers["mcp-session-id"] = sessionId;
  const res = await fetch(URL_, { method: "POST", headers, body: JSON.stringify(body) });
  const sid = res.headers.get("mcp-session-id");
  if (sid) sessionId = sid;
  if (isNotification) return null;
  const text = await res.text();
  // Streamable HTTP may answer with SSE frames
  const dataLines = text.split(/\r?\n/).filter((l) => l.startsWith("data:")).map((l) => l.slice(5).trim());
  const payload = dataLines.length ? dataLines[dataLines.length - 1] : text;
  const json = JSON.parse(payload);
  if (json.error) throw new Error(`${method}: ${JSON.stringify(json.error)}`);
  return json.result;
}

async function init() {
  const r = await rpc("initialize", { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "cursivis-xrop", version: "1.0" } });
  await rpc("notifications/initialized", {}, true);
  return r;
}

function saveContent(result, label) {
  const out = [];
  for (const c of result?.content || []) {
    if (c.type === "image" && c.data) {
      const dir = path.resolve("test-results/local/shots");
      fs.mkdirSync(dir, { recursive: true });
      const file = path.join(dir, `${label}-${Date.now()}.${(c.mimeType || "image/png").split("/")[1] || "png"}`);
      fs.writeFileSync(file, Buffer.from(c.data, "base64"));
      out.push(`[image saved ${file}]`);
    } else if (c.type === "text") {
      out.push(c.text.length > 1500 ? c.text.slice(0, 1500) + "..." : c.text);
    }
  }
  return out.join("\n");
}

const [cmd, a1, a2] = process.argv.slice(2);
await init();
if (cmd === "list") {
  const r = await rpc("tools/list", {});
  for (const t of r.tools || []) console.log(`${t.name}\t${(t.description || "").replace(/\s+/g, " ").slice(0, 110)}`);
} else if (cmd === "describe") {
  const r = await rpc("tools/list", {});
  const t = (r.tools || []).find((x) => x.name === a1);
  console.log(JSON.stringify(t, null, 2));
} else if (cmd === "call") {
  const args = a2 ? JSON.parse(a2) : {};
  const r = await rpc("tools/call", { name: a1, arguments: args });
  console.log(saveContent(r, a1));
} else if (cmd === "script") {
  const steps = JSON.parse(fs.readFileSync(a1, "utf8"));
  for (const s of steps) {
    if (s.tool) {
      const r = await rpc("tools/call", { name: s.tool, arguments: s.args || {} });
      console.log(`> ${s.tool} ${JSON.stringify(s.args || {}).slice(0, 160)}\n${saveContent(r, s.label || s.tool)}`);
    }
    if (s.waitMs) await new Promise((res) => setTimeout(res, s.waitMs));
  }
}
