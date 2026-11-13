// JSON schemas: (1) request validation at the trust boundary, (2) model structured-output contracts.
import Ajv from "ajv";
import { CONTENT_TYPES, ICONS } from "./actions.js";

export const CLIENT_ACTIONS = ["none", "read_aloud", "toggle_theme", "show_options", "clear_context", "new_conversation"];

const ajv = new Ajv({ allErrors: false, strict: false, removeAdditional: false });

export function buildRequestSchemas(limits) {
  const base64Max = (bytes) => Math.ceil((bytes * 4) / 3) + 8;
  const selection = {
    type: "object",
    additionalProperties: false,
    required: ["id", "kind"],
    properties: {
      id: { type: "string", minLength: 1, maxLength: 64, pattern: "^[A-Za-z0-9_.:-]+$" },
      kind: { enum: ["text", "image", "text_image"] },
      source: { enum: ["camera", "workspace", "unknown"] },
      text: { type: "string", maxLength: limits.maxTextChars },
      image: {
        type: "object",
        additionalProperties: false,
        required: ["mimeType", "base64"],
        properties: {
          mimeType: { enum: ["image/jpeg", "image/png"] },
          base64: { type: "string", minLength: 16, maxLength: base64Max(limits.maxImageBytes) },
          width: { type: "integer", minimum: 1, maximum: 8192 },
          height: { type: "integer", minimum: 1, maximum: 8192 }
        }
      },
      meta: {
        type: "object",
        additionalProperties: false,
        properties: {
          title: { type: "string", maxLength: 120 },
          docType: { type: "string", maxLength: 40 },
          language: { type: "string", maxLength: 16 }
        }
      }
    }
  };
  const history = {
    type: "array",
    maxItems: limits.maxHistoryTurns,
    items: {
      type: "object",
      additionalProperties: false,
      required: ["role", "text"],
      properties: {
        role: { enum: ["user", "assistant"] },
        text: { type: "string", maxLength: 6000 }
      }
    }
  };
  const audio = {
    type: "object",
    additionalProperties: false,
    required: ["mimeType", "base64"],
    properties: {
      mimeType: { enum: ["audio/wav", "audio/x-wav", "audio/wave"] },
      base64: { type: "string", minLength: 64, maxLength: base64Max(limits.maxAudioBytes) },
      durationMs: { type: "integer", minimum: 0, maximum: 120000 }
    }
  };
  return {
    suggest: ajv.compile({
      type: "object",
      additionalProperties: false,
      required: ["selection"],
      properties: { selection, history, clientRequestId: { type: "string", maxLength: 64 } }
    }),
    act: ajv.compile({
      type: "object",
      additionalProperties: false,
      required: ["selection", "action"],
      properties: {
        selection,
        history,
        clientRequestId: { type: "string", maxLength: 64 },
        regenerate: { type: "boolean" },
        action: {
          type: "object",
          additionalProperties: false,
          required: ["id", "label"],
          properties: {
            id: { type: "string", minLength: 1, maxLength: 64 },
            label: { type: "string", minLength: 1, maxLength: 60 },
            intent: { type: "string", maxLength: 240 },
            icon: { type: "string", maxLength: 24 },
            instruction: { type: "string", maxLength: 600 }
          }
        }
      }
    }),
    talk: ajv.compile({
      type: "object",
      additionalProperties: false,
      properties: {
        mode: { enum: ["context", "general"] },
        selection,
        history,
        audio,
        instruction: { type: "string", minLength: 1, maxLength: 2000 },
        recentSelectionSummary: { type: "string", maxLength: 400 },
        clientRequestId: { type: "string", maxLength: 64 }
      }
    }),
    speak: ajv.compile({
      type: "object",
      additionalProperties: false,
      required: ["text"],
      properties: {
        text: { type: "string", minLength: 1, maxLength: limits.maxTtsChars },
        voice: { type: "string", maxLength: 40, pattern: "^[A-Za-z]+$" },
        format: { enum: ["wav"] }
      }
    })
  };
}

// ---- Model output contracts (sent as responseJsonSchema, then re-validated with ajv) ----

export const SUGGEST_OUTPUT_SCHEMA = {
  type: "object",
  properties: {
    contentType: { type: "string", enum: CONTENT_TYPES },
    title: { type: "string", description: "2-5 word title of what was selected" },
    summary: { type: "string", description: "One sentence describing what is in the selection" },
    quality: { type: "string", enum: ["clear", "partial", "unreadable"] },
    confidence: { type: "number", minimum: 0, maximum: 1 },
    suggestions: {
      type: "array",
      minItems: 4,
      maxItems: 4,
      items: {
        type: "object",
        properties: {
          id: { type: "string", description: "snake_case action id" },
          label: { type: "string", description: "1-3 words, verb-first Title Case, max 22 chars" },
          intent: { type: "string", description: "one short sentence: what will be produced" },
          icon: { type: "string", enum: ICONS }
        },
        required: ["id", "label", "intent", "icon"]
      }
    }
  },
  required: ["contentType", "title", "summary", "quality", "confidence", "suggestions"]
};

const RESULT_PROPERTIES = {
  title: { type: "string", description: "2-6 word title for the result panel" },
  answer: { type: "string", description: "Markdown answer for the panel" },
  evidence: { type: "string", description: "One short line: what in the selection the answer is based on" },
  uncertainty: { type: "string", enum: ["none", "low", "medium", "high"] },
  uncertaintyNote: { type: "string", description: "What is missing or unclear; empty if none" },
  followUps: { type: "array", maxItems: 3, items: { type: "string" }, description: "Up to 3 short follow-up actions (max 4 words each)" }
};

export const ACT_OUTPUT_SCHEMA = {
  type: "object",
  properties: RESULT_PROPERTIES,
  required: ["title", "answer", "uncertainty"]
};

export const TALK_OUTPUT_SCHEMA = {
  type: "object",
  properties: {
    transcript: { type: "string", description: "Exact transcription of the user's spoken instruction" },
    ...RESULT_PROPERTIES,
    clientAction: { type: "string", enum: CLIENT_ACTIONS }
  },
  required: ["transcript", "title", "answer", "uncertainty", "clientAction"]
};

export const validateSuggestOutput = ajv.compile({
  ...SUGGEST_OUTPUT_SCHEMA,
  properties: {
    ...SUGGEST_OUTPUT_SCHEMA.properties,
    suggestions: { ...SUGGEST_OUTPUT_SCHEMA.properties.suggestions, minItems: 3, maxItems: 8 }
  }
});
export const validateActOutput = ajv.compile(ACT_OUTPUT_SCHEMA);
export const validateTalkOutput = ajv.compile(TALK_OUTPUT_SCHEMA);

export function firstError(validator) {
  const e = validator.errors?.[0];
  if (!e) return "invalid request";
  return `${e.instancePath || "(root)"} ${e.message}`.trim().slice(0, 200);
}
