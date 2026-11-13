// HTTP API for Cursivis Spatial.
//   GET  /v1/health   -> service status (no secrets)
//   POST /v1/suggest  -> four contextual suggestions for a selection
//   POST /v1/act      -> execute a chosen action on the selection
//   POST /v1/talk     -> contextual or general voice/text instruction (audio is transcribed + answered in one call)
//   POST /v1/speak    -> read-aloud audio (WAV) for a text chunk
import express from "express";
import { loadConfig } from "./config.js";
import { getSharedPool, KeyPool } from "./gemini/keyPool.js";
import { createGeminiClient } from "./gemini/client.js";
import { toPublicError } from "./gemini/errors.js";
import { createServices } from "./cursivis/services.js";
import { buildRequestSchemas, firstError } from "./cursivis/schemas.js";
import { createSecurity } from "./security.js";
import { inspectWav } from "./cursivis/wav.js";

export function createApp({ env = process.env, fetchImpl, pool: injectedPool } = {}) {
  const config = loadConfig(env);
  const pool = injectedPool || (fetchImpl ? new KeyPool(config.keys) : getSharedPool(config.keys));
  const logger = config.logRequests
    ? (entry) => console.log(`[cursivis] ${entry.label} ok=${entry.ok} ${entry.model || entry.category || ""} ${entry.ms}ms ${entry.trace?.join(" ") || ""}`)
    : null;
  const gemini = createGeminiClient({ pool, fetchImpl, attemptTimeoutMs: config.timeouts.attemptTimeoutMs, logger });
  const services = createServices({ gemini, config });
  const schemas = buildRequestSchemas(config.limits);
  const security = createSecurity(config);

  const app = express();
  app.disable("x-powered-by");
  app.set("trust proxy", true);
  app.use(express.json({ limit: config.limits.bodyLimit }));

  app.use((req, res, next) => {
    res.setHeader("cache-control", "no-store");
    res.setHeader("x-content-type-options", "nosniff");
    next();
  });

  app.get(["/", "/v1/health"], (_req, res) => {
    res.json({
      ok: true,
      service: "cursivis-spatial-backend",
      version: config.version,
      ai: { configured: pool.size > 0, ...pool.stats() },
      limits: { ratePerMinute: config.limits.ratePerMinute, dailyRequestCap: config.limits.dailyRequestCap },
      time: new Date().toISOString()
    });
  });

  const guard = (validator) => (req, res, next) => {
    const auth = security.authenticate(req);
    if (!auth.ok) return res.status(auth.status).json({ error: { code: auth.code, message: auth.message } });
    if (!validator(req.body)) {
      return res.status(400).json({ error: { code: "invalid_request", message: firstError(validator) } });
    }
    const limited = security.rateLimit(req);
    if (!limited.ok) {
      res.setHeader("retry-after", String(limited.retryAfterSec || 30));
      return res.status(limited.status).json({ error: { code: limited.code, message: limited.message, retryAfterSec: limited.retryAfterSec } });
    }
    return next();
  };

  const selectionProblem = (selection) => {
    if (!selection) return "selection is required";
    if ((selection.kind === "text" || selection.kind === "text_image") && !String(selection.text || "").trim()) {
      return "selection text is empty";
    }
    if ((selection.kind === "image" || selection.kind === "text_image") && !selection.image) {
      return "selection image is missing";
    }
    if (selection.image?.base64) {
      const bytes = Buffer.byteLength(selection.image.base64, "base64");
      if (bytes > config.limits.maxImageBytes) return "selection image is too large";
    }
    return null;
  };

  const handle = (fn) => async (req, res) => {
    const started = Date.now();
    try {
      const result = await fn(req.body);
      res.setHeader("server-timing", `total;dur=${Date.now() - started}`);
      return res.json(result);
    } catch (error) {
      if (error?.status === 400) {
        return res.status(400).json({ error: { code: "invalid_request", message: error.message } });
      }
      const pub = toPublicError(error);
      if (pub.retryAfterSec) res.setHeader("retry-after", String(pub.retryAfterSec));
      if (config.logRequests) console.log(`[cursivis] ${req.path} failed category=${error?.category || "unknown"} ${String(error?.message || "").slice(0, 160)}`);
      return res.status(pub.httpStatus).json({ error: { code: pub.code, message: pub.message, retryAfterSec: pub.retryAfterSec } });
    }
  };

  const badRequest = (message) => Object.assign(new Error(message), { status: 400 });

  app.post("/v1/suggest", guard(schemas.suggest), handle(async (body) => {
    const problem = selectionProblem(body.selection);
    if (problem) throw badRequest(problem);
    return services.suggest(body);
  }));

  app.post("/v1/act", guard(schemas.act), handle(async (body) => {
    const problem = selectionProblem(body.selection);
    if (problem) throw badRequest(problem);
    return services.act(body);
  }));

  app.post("/v1/talk", guard(schemas.talk), handle(async (body) => {
    if (!body.audio && !body.instruction) throw badRequest("audio or instruction is required");
    const mode = body.mode === "general" || !body.selection ? "general" : "context";
    if (mode === "context") {
      const problem = selectionProblem(body.selection);
      if (problem) throw badRequest(problem);
    }
    if (body.audio) {
      const buf = Buffer.from(body.audio.base64, "base64");
      if (buf.length > config.limits.maxAudioBytes) throw badRequest("audio is too large");
      const info = inspectWav(buf);
      if (!info || info.format !== 1) throw badRequest("audio must be PCM WAV");
      if (info.durationMs < 250) throw badRequest("audio is too short");
    }
    return services.talk({ ...body, mode });
  }));

  app.post("/v1/speak", guard(schemas.speak), async (req, res) => {
    try {
      const { wav, model, latencyMs } = await services.speak(req.body);
      res.setHeader("content-type", "audio/wav");
      res.setHeader("x-cursivis-model", model);
      res.setHeader("server-timing", `total;dur=${latencyMs}`);
      return res.send(wav);
    } catch (error) {
      const pub = toPublicError(error);
      return res.status(pub.httpStatus).json({ error: { code: pub.code, message: pub.message, retryAfterSec: pub.retryAfterSec } });
    }
  });

  app.use((req, res) => res.status(404).json({ error: { code: "not_found", message: "Unknown endpoint" } }));

  // Body parser errors (too large / malformed JSON) -> clean JSON errors.
  // eslint-disable-next-line no-unused-vars
  app.use((err, _req, res, _next) => {
    if (err?.type === "entity.too.large") return res.status(413).json({ error: { code: "too_large", message: "Request is too large." } });
    if (err?.type === "entity.parse.failed") return res.status(400).json({ error: { code: "invalid_json", message: "Malformed JSON." } });
    return res.status(500).json({ error: { code: "internal", message: "Unexpected server error." } });
  });

  app.locals.config = config;
  app.locals.pool = pool;
  return app;
}
