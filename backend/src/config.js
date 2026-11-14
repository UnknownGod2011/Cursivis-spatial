// Central configuration. Everything is read from environment variables so the same code runs
// locally (backend/.env via src/server.js) and on Vercel (project environment variables).

function list(value) {
  return String(value || "")
    .split(/[\s,;]+/)
    .map((v) => v.trim())
    .filter(Boolean);
}

function int(value, fallback) {
  const n = Number.parseInt(String(value ?? ""), 10);
  return Number.isFinite(n) && n > 0 ? n : fallback;
}

export function loadConfig(env = process.env) {
  const keys = [
    ...list(env.GEMINI_API_KEYS),
    ...list(env.GOOGLE_API_KEYS),
    ...list(env.GEMINI_API_KEY),
    ...list(env.GOOGLE_API_KEY)
  ].filter((v, i, a) => a.indexOf(v) === i);

  // Model order is driven by measured latency on the free tier (see docs/ARCHITECTURE.md, "Model choice"):
  // the flash-lite models answer act-sized prompts in ~1.2-2.2 s, while gemini-3.5-flash took 15-20 s and
  // gemini-3.6-flash 30-44 s under load (2026-11-14 probe). An interactive headset UI cannot wait that long,
  // so the larger models are kept only as last-resort fallbacks. The hedge goes to a different model family
  // (3.1 flash-lite) so one overloaded model does not stall a request.
  const models = {
    // Suggestions: low latency matters most.
    fast: list(env.CURSIVIS_MODELS_FAST).length
      ? list(env.CURSIVIS_MODELS_FAST)
      : ["gemini-3.5-flash-lite", "gemini-3.1-flash-lite", "gemini-flash-lite-latest", "gemini-3.5-flash"],
    // Action execution and Talk answers (thinkingLevel "low", longer output).
    quality: list(env.CURSIVIS_MODELS_QUALITY).length
      ? list(env.CURSIVIS_MODELS_QUALITY)
      : ["gemini-3.5-flash-lite", "gemini-3.1-flash-lite", "gemini-3.5-flash", "gemini-3.6-flash"],
    tts: list(env.CURSIVIS_MODELS_TTS).length
      ? list(env.CURSIVIS_MODELS_TTS)
      : ["gemini-3.8-flash-lite-tts", "gemini-3.8-flash-tts", "gemini-2.5-flash-preview-tts"]
  };

  return {
    version: "1.0.0",
    keys,
    appTokens: list(env.CURSIVIS_APP_TOKENS),
    requireToken: env.CURSIVIS_REQUIRE_TOKEN !== "0",
    models,
    ttsVoice: String(env.CURSIVIS_TTS_VOICE || "Kore").trim(),
    limits: {
      ratePerMinute: int(env.CURSIVIS_RATE_LIMIT_PER_MINUTE, 40),
      ipRatePerMinute: int(env.CURSIVIS_IP_RATE_LIMIT_PER_MINUTE, 80),
      dailyRequestCap: int(env.CURSIVIS_DAILY_REQUEST_CAP, 6000),
      maxImageBytes: int(env.CURSIVIS_MAX_IMAGE_BYTES, 2 * 1024 * 1024),
      maxAudioBytes: int(env.CURSIVIS_MAX_AUDIO_BYTES, 3 * 1024 * 1024),
      maxTextChars: int(env.CURSIVIS_MAX_TEXT_CHARS, 24000),
      maxTtsChars: int(env.CURSIVIS_MAX_TTS_CHARS, 1400),
      maxHistoryTurns: int(env.CURSIVIS_MAX_HISTORY_TURNS, 8),
      bodyLimit: env.CURSIVIS_BODY_LIMIT || "7mb"
    },
    timeouts: {
      suggestDeadlineMs: int(env.CURSIVIS_SUGGEST_DEADLINE_MS, 16000),
      actDeadlineMs: int(env.CURSIVIS_ACT_DEADLINE_MS, 50000),
      talkDeadlineMs: int(env.CURSIVIS_TALK_DEADLINE_MS, 50000),
      ttsDeadlineMs: int(env.CURSIVIS_TTS_DEADLINE_MS, 40000),
      attemptTimeoutMs: int(env.CURSIVIS_ATTEMPT_TIMEOUT_MS, 22000),
      suggestHedgeAfterMs: int(env.CURSIVIS_SUGGEST_HEDGE_MS, 3000),
      actHedgeAfterMs: int(env.CURSIVIS_ACT_HEDGE_MS, 3500)
    },
    logRequests: env.CURSIVIS_LOG_REQUESTS === "1",
    isProduction: Boolean(env.VERCEL) || env.NODE_ENV === "production"
  };
}
