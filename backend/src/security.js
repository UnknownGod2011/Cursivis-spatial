// Abuse protection for the public backend: app token, per-install + per-IP rate limits, daily cap.
// State is per serverless instance (no external store), which bounds expense without adding infrastructure.
import crypto from "node:crypto";

function safeEqual(a, b) {
  const ab = Buffer.from(String(a));
  const bb = Buffer.from(String(b));
  return ab.length === bb.length && crypto.timingSafeEqual(ab, bb);
}

export function clientIp(req) {
  const fwd = String(req.headers["x-forwarded-for"] || "").split(",")[0].trim();
  return fwd || req.socket?.remoteAddress || "unknown";
}

export function createSecurity(config, { now = () => Date.now() } = {}) {
  const windows = new Map(); // key -> { start, count }
  let day = new Date(now()).toISOString().slice(0, 10);
  let dayCount = 0;

  function hit(key, limit) {
    const t = now();
    const w = windows.get(key);
    if (!w || t - w.start >= 60000) {
      windows.set(key, { start: t, count: 1 });
      if (windows.size > 5000) {
        for (const [k, v] of windows) if (t - v.start >= 60000) windows.delete(k);
      }
      return { ok: true };
    }
    w.count += 1;
    if (w.count > limit) return { ok: false, retryAfterSec: Math.ceil((60000 - (t - w.start)) / 1000) };
    return { ok: true };
  }

  function authenticate(req) {
    if (!config.requireToken) return { ok: true };
    if (config.appTokens.length === 0) {
      return { ok: false, status: 503, code: "not_configured", message: "Backend has no client tokens configured." };
    }
    const token = String(req.headers["x-cursivis-token"] || "");
    if (!token || !config.appTokens.some((t) => safeEqual(t, token))) {
      return { ok: false, status: 401, code: "unauthorized", message: "Missing or invalid client token." };
    }
    return { ok: true };
  }

  function rateLimit(req) {
    const today = new Date(now()).toISOString().slice(0, 10);
    if (today !== day) {
      day = today;
      dayCount = 0;
    }
    if (dayCount >= config.limits.dailyRequestCap) {
      return { ok: false, status: 429, code: "daily_cap", message: "Daily request limit reached. Please try again tomorrow.", retryAfterSec: 3600 };
    }
    const install = String(req.headers["x-cursivis-install"] || "").slice(0, 64);
    const ip = clientIp(req);
    const byIp = hit(`ip:${ip}`, config.limits.ipRatePerMinute);
    if (!byIp.ok) return { ok: false, status: 429, code: "rate_limited", message: "Too many requests. Please slow down.", retryAfterSec: byIp.retryAfterSec };
    if (install) {
      const byInstall = hit(`in:${install}`, config.limits.ratePerMinute);
      if (!byInstall.ok) return { ok: false, status: 429, code: "rate_limited", message: "Too many requests. Please slow down.", retryAfterSec: byInstall.retryAfterSec };
    }
    dayCount += 1;
    return { ok: true };
  }

  return { authenticate, rateLimit, stats: () => ({ day, dayCount }) };
}
