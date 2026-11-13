// Error classification for Gemini REST responses.
// Adapted from the original Cursivis backend (backend/gemini-agent/src/apiKeyPool.js classifyGeminiError),
// extended for HTTP status codes because this backend calls the REST API directly.

export class GeminiError extends Error {
  constructor(message, { status = 0, code = "", category = "unknown", retryAfterMs = 0, model = "", keyIndex = -1 } = {}) {
    super(message);
    this.name = "GeminiError";
    this.status = status;
    this.code = code;
    this.category = category;
    this.retryAfterMs = retryAfterMs;
    this.model = model;
    this.keyIndex = keyIndex;
  }
}

// Categories:
//  auth       - key invalid/blocked/no free tier for the project -> disable that key for a long time
//  quota      - 429 / RESOURCE_EXHAUSTED -> cool down that key, try another key
//  overloaded - 503 UNAVAILABLE "high demand" -> model-wide; try next model
//  model      - 404 / model not found -> disable model for this instance
//  invalid    - 400 INVALID_ARGUMENT -> our request; retry once in compat mode, otherwise fail
//  safety     - blocked by safety filters -> do not retry
//  timeout    - attempt timed out -> try another key/model
//  transient  - 500/502/504/network -> retry elsewhere
export function classify({ status, body, networkError }) {
  if (networkError) {
    const msg = String(networkError?.message || networkError);
    if (networkError?.name === "AbortError" || /abort|timed? ?out/i.test(msg)) return "timeout";
    return "transient";
  }
  const errStatus = body?.error?.status || "";
  const message = String(body?.error?.message || "");
  if (status === 401 || status === 403 || /API_KEY_INVALID|PERMISSION_DENIED|UNAUTHENTICATED|api key not valid|free tier is not available/i.test(`${errStatus} ${message}`)) {
    return "auth";
  }
  if (status === 429 || errStatus === "RESOURCE_EXHAUSTED") return "quota";
  if (status === 404 || errStatus === "NOT_FOUND") return "model";
  if (status === 503 || errStatus === "UNAVAILABLE") return "overloaded";
  if (status === 400 || errStatus === "INVALID_ARGUMENT" || errStatus === "FAILED_PRECONDITION") return "invalid";
  if (status >= 500) return "transient";
  return "unknown";
}

export function parseRetryAfterMs(body) {
  const details = body?.error?.details;
  if (Array.isArray(details)) {
    for (const d of details) {
      const delay = d?.retryDelay;
      if (typeof delay === "string") {
        const m = delay.match(/^([0-9.]+)s$/);
        if (m) return Math.max(1000, Math.ceil(Number(m[1]) * 1000));
      }
    }
  }
  const msg = String(body?.error?.message || "");
  const m = msg.match(/retry in ([0-9.]+)s/i);
  if (m) return Math.max(1000, Math.ceil(Number(m[1]) * 1000));
  return 0;
}

// Maps an internal failure to a safe, user-presentable error (never leaks keys or raw provider text).
export function toPublicError(error) {
  const category = error?.category || "unknown";
  switch (category) {
    case "quota":
      return { httpStatus: 429, code: "ai_busy", message: "Cursivis is handling a lot of requests right now. Please try again in a moment.", retryAfterSec: Math.ceil((error.retryAfterMs || 15000) / 1000) };
    case "auth":
      return { httpStatus: 503, code: "ai_unavailable", message: "The AI service is not available right now. Please try again later." };
    case "safety":
      return { httpStatus: 422, code: "content_blocked", message: "The AI declined to process this selection." };
    case "timeout":
      return { httpStatus: 504, code: "ai_timeout", message: "The AI took too long to respond. Please retry." };
    case "invalid":
      return { httpStatus: 502, code: "ai_bad_request", message: "The AI could not process this request." };
    case "deadline":
      return { httpStatus: 504, code: "ai_timeout", message: "The AI took too long to respond. Please retry." };
    case "overloaded":
    case "transient":
    case "model":
    default:
      return { httpStatus: 503, code: "ai_unavailable", message: "The AI service is temporarily unavailable. Please retry." };
  }
}
