// Cursivis Spatial AI services: SuggestionService, ActionExecutor, ConversationService (Talk), VoiceService (TTS).
import crypto from "node:crypto";
import { canonicalActionId, cleanLabel, normalizeSuggestions, fallbackSuggestions } from "./actions.js";
import {
  SUGGEST_SYSTEM, EXECUTE_SYSTEM, TALK_CONTEXT_SYSTEM, TALK_GENERAL_SYSTEM,
  guidanceFor, describeSelection, historyBlock
} from "./prompts.js";
import {
  SUGGEST_OUTPUT_SCHEMA, ACT_OUTPUT_SCHEMA, TALK_OUTPUT_SCHEMA, CLIENT_ACTIONS,
  validateSuggestOutput, validateActOutput, validateTalkOutput, firstError
} from "./schemas.js";
import { OutputValidationError, extractAudio } from "../gemini/client.js";
import { ensureWav } from "./wav.js";

// ---------- small TTL cache (per serverless instance) ----------
class TtlCache {
  constructor(limit = 200, ttlMs = 10 * 60 * 1000) {
    this.limit = limit;
    this.ttlMs = ttlMs;
    this.map = new Map();
  }
  get(key) {
    const hit = this.map.get(key);
    if (!hit) return undefined;
    if (hit.expires < Date.now()) {
      this.map.delete(key);
      return undefined;
    }
    return hit.value;
  }
  set(key, value) {
    if (this.map.size >= this.limit) this.map.delete(this.map.keys().next().value);
    this.map.set(key, { value, expires: Date.now() + this.ttlMs });
  }
}

export function selectionFingerprint(selection) {
  const h = crypto.createHash("sha256");
  h.update(selection.kind);
  h.update("\u0000");
  h.update(selection.text || "");
  h.update("\u0000");
  h.update(selection.image?.base64 || "");
  return h.digest("hex").slice(0, 32);
}

function selectionParts(selection) {
  const parts = [{ text: describeSelection(selection) }];
  if (selection.image?.base64) {
    parts.push({ inlineData: { mimeType: selection.image.mimeType, data: selection.image.base64 } });
  }
  return parts;
}

function parseJsonStrict(text) {
  let t = String(text || "").trim();
  const fence = t.match(/^```(?:json)?\s*([\s\S]*?)\s*```$/i);
  if (fence) t = fence[1];
  try {
    return JSON.parse(t);
  } catch {
    const start = t.indexOf("{");
    const end = t.lastIndexOf("}");
    if (start >= 0 && end > start) return JSON.parse(t.slice(start, end + 1));
    throw new OutputValidationError("Model did not return JSON");
  }
}

function jsonValidator(validator) {
  return (text) => {
    const value = parseJsonStrict(text);
    if (!validator(value)) throw new OutputValidationError(firstError(validator));
    return value;
  };
}

function cleanAnswer(markdown) {
  let text = String(markdown || "").replace(/\r\n/g, "\n").trim();
  text = text.replace(/^(sure|certainly|of course|here(?:'s| is| are))[^\n]*:\s*\n+/i, "");
  return text.slice(0, 12000);
}

function shapeResult(value, extra = {}) {
  const uncertainty = ["none", "low", "medium", "high"].includes(value.uncertainty) ? value.uncertainty : "low";
  return {
    title: cleanLabel(value.title || "Result").slice(0, 60) || "Result",
    answer: cleanAnswer(value.answer),
    evidence: String(value.evidence || "").trim().slice(0, 300),
    uncertainty,
    uncertaintyNote: uncertainty === "none" ? "" : String(value.uncertaintyNote || "").trim().slice(0, 300),
    followUps: (Array.isArray(value.followUps) ? value.followUps : [])
      .map((f) => cleanLabel(f))
      .filter((f) => f && f.length <= 30)
      .slice(0, 3),
    ...extra
  };
}

export function createServices({ gemini, config }) {
  const suggestCache = new TtlCache(200, 10 * 60 * 1000);
  const actCache = new TtlCache(200, 10 * 60 * 1000);
  const { models, timeouts } = config;

  // SuggestionService: one structured call -> interpreted type, summary, exactly four suggestions.
  async function suggest({ selection, history }) {
    const fingerprint = selectionFingerprint(selection);
    const cached = suggestCache.get(fingerprint);
    if (cached) return { ...cached, selectionId: selection.id, cached: true };

    const contents = [{ role: "user", parts: [
      ...selectionParts(selection),
      { text: [historyBlock(history), "Predict the four most useful next actions for this selection."].filter(Boolean).join("\n\n") }
    ] }];
    let outcome;
    try {
      outcome = await gemini.generate(
        {
          systemInstruction: SUGGEST_SYSTEM,
          contents,
          thinkingLevel: "minimal",
          generationConfig: {
            responseMimeType: "application/json",
            responseJsonSchema: SUGGEST_OUTPUT_SCHEMA,
            temperature: 0.4,
            maxOutputTokens: 900
          }
        },
        { models: models.fast, deadlineMs: timeouts.suggestDeadlineMs, hedgeAfterMs: timeouts.suggestHedgeAfterMs, validate: jsonValidator(validateSuggestOutput), label: "suggest" }
      );
    } catch (error) {
      if (error?.category === "bad_output") {
        // Structured output failed on every model: return clearly-labelled deterministic suggestions.
        const result = {
          selectionId: selection.id,
          contentType: selection.kind === "image" ? "other" : "general_text",
          title: selection.meta?.title || (selection.kind === "image" ? "Your selection" : "Selected text"),
          summary: "",
          quality: "partial",
          confidence: 0,
          suggestions: fallbackSuggestions(selection),
          source: "fallback",
          model: null,
          latencyMs: 0
        };
        return result;
      }
      throw error;
    }
    const v = outcome.value;
    const { suggestions, usedFallback } = normalizeSuggestions(v.suggestions, selection);
    const result = {
      selectionId: selection.id,
      contentType: v.contentType,
      title: cleanLabel(v.title).slice(0, 48) || "Selection",
      summary: String(v.summary || "").trim().slice(0, 280),
      quality: v.quality,
      confidence: Math.max(0, Math.min(1, Number(v.confidence) || 0)),
      suggestions,
      source: usedFallback ? "ai+fallback" : "ai",
      model: outcome.model,
      latencyMs: outcome.latencyMs
    };
    suggestCache.set(fingerprint, result);
    return result;
  }

  // ActionExecutor: runs the chosen action against the original selection.
  async function act({ selection, action, history, regenerate }) {
    const actionId = canonicalActionId(action.id);
    const label = cleanLabel(action.label) || action.label;
    const cacheKey = `${selectionFingerprint(selection)}:${actionId}:${action.instruction || ""}:${(history || []).length}`;
    if (!regenerate) {
      const cached = actCache.get(cacheKey);
      if (cached) return { ...cached, selectionId: selection.id, cached: true };
    }
    const task = [
      `Chosen action: ${label} (id: ${actionId}).`,
      `What to do: ${guidanceFor(actionId, label, action.intent, selection.kind)}`,
      action.instruction ? `Additional user instruction: ${action.instruction}` : "",
      regenerate ? "The user asked to regenerate: produce a fresh, different variant of the result." : "",
      historyBlock(history),
      "Respond with JSON: title, answer (Markdown), evidence, uncertainty, uncertaintyNote, followUps."
    ].filter(Boolean).join("\n\n");
    const outcome = await gemini.generate(
      {
        systemInstruction: EXECUTE_SYSTEM,
        contents: [{ role: "user", parts: [...selectionParts(selection), { text: task }] }],
        thinkingLevel: "low",
        generationConfig: {
          responseMimeType: "application/json",
          responseJsonSchema: ACT_OUTPUT_SCHEMA,
          temperature: regenerate ? 0.9 : 0.5,
          maxOutputTokens: 3000
        }
      },
      { models: models.quality, deadlineMs: timeouts.actDeadlineMs, hedgeAfterMs: timeouts.actHedgeAfterMs, validate: jsonValidator(validateActOutput), label: `act:${actionId}` }
    );
    const result = shapeResult(outcome.value, {
      selectionId: selection.id,
      actionId,
      actionLabel: label,
      model: outcome.model,
      latencyMs: outcome.latencyMs
    });
    actCache.set(cacheKey, result);
    return result;
  }

  // ConversationService: contextual Talk (selection) or General Mode (no selection), audio or text instruction.
  async function talk({ mode, selection, history, audio, instruction, recentSelectionSummary }) {
    const contextual = mode !== "general" && selection;
    const parts = [];
    if (contextual) parts.push(...selectionParts(selection));
    if (!contextual && recentSelectionSummary) {
      parts.push({ text: `Recent selection summary (for reference only): ${recentSelectionSummary}` });
    }
    const hist = historyBlock(history);
    if (hist) parts.push({ text: hist });
    if (audio) {
      parts.push({ text: "The user's spoken instruction is in the attached audio." });
      parts.push({ inlineData: { mimeType: "audio/wav", data: audio.base64 } });
    } else {
      parts.push({ text: `The user's instruction (typed): ${instruction}` });
    }
    parts.push({ text: `Respond with JSON: transcript${audio ? "" : " (repeat the typed instruction)"}, title, answer (Markdown), evidence, uncertainty, uncertaintyNote, followUps, clientAction (one of ${CLIENT_ACTIONS.join(", ")}).` });

    const outcome = await gemini.generate(
      {
        systemInstruction: contextual ? TALK_CONTEXT_SYSTEM : TALK_GENERAL_SYSTEM,
        contents: [{ role: "user", parts }],
        thinkingLevel: "low",
        generationConfig: {
          responseMimeType: "application/json",
          responseJsonSchema: TALK_OUTPUT_SCHEMA,
          temperature: 0.5,
          maxOutputTokens: 3000
        }
      },
      { models: models.quality, deadlineMs: timeouts.talkDeadlineMs, hedgeAfterMs: timeouts.actHedgeAfterMs, validate: jsonValidator(validateTalkOutput), label: contextual ? "talk:context" : "talk:general" }
    );
    const v = outcome.value;
    const transcript = String(v.transcript || (audio ? "" : instruction) || "").trim().slice(0, 600);
    return shapeResult(v, {
      selectionId: contextual ? selection.id : null,
      mode: contextual ? "context" : "general",
      transcript,
      heard: transcript.length > 0,
      clientAction: CLIENT_ACTIONS.includes(v.clientAction) ? v.clientAction : "none",
      model: outcome.model,
      latencyMs: outcome.latencyMs
    });
  }

  // VoiceService: text -> 24 kHz mono WAV via Gemini TTS.
  async function speak({ text, voice }) {
    const outcome = await gemini.generate(
      {
        // Only the text itself: TTS models may speak any instruction that is included in the prompt.
        contents: [{ role: "user", parts: [{ text }] }],
        generationConfig: {
          responseModalities: ["AUDIO"],
          speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName: voice || config.ttsVoice } } }
        }
      },
      { models: models.tts, deadlineMs: timeouts.ttsDeadlineMs, extract: extractAudio, label: "speak" }
    );
    const wav = ensureWav(outcome.value.base64, outcome.value.mimeType);
    return { wav, model: outcome.model, latencyMs: outcome.latencyMs };
  }

  return { suggest, act, talk, speak };
}
