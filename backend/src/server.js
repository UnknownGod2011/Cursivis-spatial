// Local development server. Loads backend/.env (if present) without extra dependencies.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createApp } from "./app.js";

const here = path.dirname(fileURLToPath(import.meta.url));
const envPath = path.resolve(here, "..", ".env");
if (fs.existsSync(envPath)) {
  for (const line of fs.readFileSync(envPath, "utf8").split(/\r?\n/)) {
    const m = line.match(/^\s*([A-Z0-9_]+)\s*=\s*(.*)\s*$/);
    if (m && process.env[m[1]] === undefined) process.env[m[1]] = m[2];
  }
}

const port = Number(process.env.PORT || 8787);
const host = process.env.HOST || "127.0.0.1";
const app = createApp();
app.listen(port, host, () => {
  const cfg = app.locals.config;
  console.log(`[cursivis] listening on http://${host}:${port} keys=${cfg.keys.length} tokenRequired=${cfg.requireToken && cfg.appTokens.length > 0}`);
});
