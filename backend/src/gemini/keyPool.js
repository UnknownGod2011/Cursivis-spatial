// Gemini API key pool with health tracking and least-loaded rotation.
// Adapted from the original Cursivis backend (backend/gemini-agent/src/apiKeyPool.js):
// same idea (per-key cooldowns, auth keys disabled, least in-flight first), rewritten for direct REST
// calls, model-level health, and serverless reuse via a module-level singleton.

const AUTH_DISABLE_MS = 6 * 60 * 60 * 1000;   // 403/401: retry the key after 6 h (e.g. project billing changes)
const QUOTA_COOLDOWN_MS = 60 * 1000;          // 429 without retryDelay
const TRANSIENT_COOLDOWN_MS = 4 * 1000;
const MODEL_OVERLOAD_COOLDOWN_MS = 45 * 1000; // 503 "high demand" is model-wide
const MODEL_MISSING_COOLDOWN_MS = 60 * 60 * 1000;

const isValidKeyShape = (k) => typeof k === "string" && k.length >= 30 && k.length <= 256 && /^[A-Za-z0-9._-]+$/.test(k);

export class KeyPool {
  constructor(keys, { now = () => Date.now() } = {}) {
    this.now = now;
    this.entries = keys.filter(isValidKeyShape).map((key, index) => ({
      key,
      index,
      inFlight: 0,
      lastUsedAt: 0,
      cooldownUntil: 0,
      disabledUntil: 0,
      successes: 0,
      failures: 0,
      lastCategory: null
    }));
    this.models = new Map(); // model -> { cooldownUntil, failures, successes }
  }

  get size() {
    return this.entries.length;
  }

  // Returns the best available key entry, excluding any in `exclude` (Set of indexes).
  acquire(exclude = new Set()) {
    const t = this.now();
    const candidates = this.entries
      .filter((e) => !exclude.has(e.index) && e.disabledUntil <= t && e.cooldownUntil <= t)
      .sort((a, b) => a.inFlight - b.inFlight || a.lastUsedAt - b.lastUsedAt);
    const entry = candidates[0];
    if (!entry) return null;
    entry.inFlight += 1;
    entry.lastUsedAt = t;
    return entry;
  }

  release(entry, outcome) {
    if (!entry) return;
    entry.inFlight = Math.max(0, entry.inFlight - 1);
    const t = this.now();
    if (!outcome || outcome.ok) {
      entry.successes += 1;
      entry.lastCategory = null;
      return;
    }
    entry.failures += 1;
    entry.lastCategory = outcome.category;
    switch (outcome.category) {
      case "auth":
        entry.disabledUntil = t + AUTH_DISABLE_MS;
        break;
      case "quota":
        entry.cooldownUntil = t + (outcome.retryAfterMs || QUOTA_COOLDOWN_MS);
        break;
      case "transient":
      case "timeout":
        entry.cooldownUntil = t + TRANSIENT_COOLDOWN_MS;
        break;
      default:
        break; // overloaded/model/invalid/safety are not the key's fault
    }
  }

  // Milliseconds until any key becomes usable again (0 if one is available now, Infinity if none ever).
  nextAvailableInMs() {
    const t = this.now();
    let best = Infinity;
    for (const e of this.entries) {
      const ready = Math.max(e.cooldownUntil, e.disabledUntil);
      if (ready <= t) return 0;
      if (e.disabledUntil <= t) best = Math.min(best, ready - t);
    }
    return best;
  }

  modelHealthy(model) {
    const m = this.models.get(model);
    return !m || m.cooldownUntil <= this.now();
  }

  markModel(model, category) {
    const m = this.models.get(model) || { cooldownUntil: 0, failures: 0, successes: 0 };
    if (category === "ok") {
      m.successes += 1;
      m.cooldownUntil = 0;
    } else {
      m.failures += 1;
      if (category === "overloaded") m.cooldownUntil = this.now() + MODEL_OVERLOAD_COOLDOWN_MS;
      if (category === "model") m.cooldownUntil = this.now() + MODEL_MISSING_COOLDOWN_MS;
    }
    this.models.set(model, m);
  }

  // Orders a model chain so healthy models come first while preserving preference order.
  orderModels(chain) {
    const healthy = chain.filter((m) => this.modelHealthy(m));
    const cooling = chain.filter((m) => !this.modelHealthy(m));
    return [...healthy, ...cooling];
  }

  stats() {
    const t = this.now();
    return {
      keys: this.entries.length,
      keysAvailable: this.entries.filter((e) => e.disabledUntil <= t && e.cooldownUntil <= t).length,
      keysDisabled: this.entries.filter((e) => e.disabledUntil > t).length,
      keysCoolingDown: this.entries.filter((e) => e.disabledUntil <= t && e.cooldownUntil > t).length,
      models: Object.fromEntries(
        [...this.models.entries()].map(([name, m]) => [name, { healthy: m.cooldownUntil <= t, successes: m.successes, failures: m.failures }])
      )
    };
  }
}

let singleton = null;
let singletonSignature = "";

export function getSharedPool(keys) {
  const signature = keys.join("|");
  if (!singleton || singletonSignature !== signature) {
    singleton = new KeyPool(keys);
    singletonSignature = signature;
  }
  return singleton;
}
