// Action catalog for Cursivis Spatial.
// Adapted from the original Cursivis backend (backend/gemini-agent/src/contentClassifier.js:
// ACTION_MAP aliases, ALTERNATIVES_BY_TYPE, META_TOOL_ACTION_PATTERN). Suggestions are AI-generated;
// this catalog provides id normalization, safety filtering, icons, and labelled deterministic fallbacks.

export const ICONS = [
  "sparkle", "summary", "translate", "explain", "list", "check", "alert", "code", "bug", "bolt",
  "doc", "image", "caption", "eye", "text", "nutrition", "leaf", "question", "tasks", "shield",
  "palette", "math", "book", "chat", "tag"
];

export const CONTENT_TYPES = [
  "food_label", "product", "printed_text", "handwritten_note", "document", "research_paper",
  "code", "photo", "screenshot", "sign", "chart", "math", "email", "question", "general_text", "other"
];

// Original Cursivis alias map (subset) so equivalent ids collapse to one canonical id.
const ALIASES = new Map([
  ["summarise", "summarize"],
  ["summary", "summarize"],
  ["expand", "expand_text"],
  ["elaborate", "expand_text"],
  ["key_insights", "extract_insights"],
  ["extract_key_insights", "extract_insights"],
  ["answer", "answer_question"],
  ["extract_bullet_points", "bullet_points"],
  ["convert_to_bullet_points", "bullet_points"],
  ["reply_email", "draft_reply"],
  ["reply_to_email", "draft_reply"],
  ["enhance_code", "improve_code"],
  ["product_details", "extract_product_info"],
  ["generate_captions", "generate_caption"],
  ["suggest_captions", "generate_caption"],
  ["write_caption", "generate_caption"],
  ["ocr_extract_text", "extract_text"],
  ["ocr", "extract_text"],
  ["read_text", "extract_text"],
  ["identify_image_colors", "extract_dominant_colors"],
  ["dominant_colors", "extract_dominant_colors"],
  ["color_palette", "extract_dominant_colors"]
]);

// Original Cursivis META_TOOL_ACTION_PATTERN plus spatial-specific capabilities we must never claim.
const UNSUPPORTED_ACTION_PATTERN =
  /^(search|search_web|web_search|google_search|lookup|look_up|browse|browse_web|open_browser|open_page|navigate|go_to|visit|click(?:_|$)|press(?:_|$)|fill(?:_|$)|select(?:_|$)|submit(?:_|$)|send(?:_|$)|schedule(?:_|$)|buy(?:_|$)|order(?:_|$)|purchase|call(?:_|$)|email_(?:to|someone)|share(?:_|$)|post(?:_|$)|take_action|execute_|control_|turn_on|turn_off|set_reminder|book(?:_|$)|identify_(?:person|people|face)|recognize_(?:person|face)|face_)/i;

export function toSnakeCase(value) {
  return String(value || "")
    .normalize("NFKD")
    .replace(/[^\w\s-]/g, " ")
    .trim()
    .toLowerCase()
    .replace(/[\s-]+/g, "_")
    .replace(/_+/g, "_")
    .replace(/^_|_$/g, "")
    .slice(0, 48);
}

export function canonicalActionId(value) {
  const id = toSnakeCase(value);
  return ALIASES.get(id) || id;
}

export function isUnsupportedAction(id, label = "") {
  if (UNSUPPORTED_ACTION_PATTERN.test(id)) return true;
  return /\b(search the web|browse|buy|purchase|order online|send|call|post to|share with|identify (this|the) person|who is this person)\b/i.test(label);
}

export function cleanLabel(label) {
  let text = String(label || "").replace(/[\r\n\t]+/g, " ").replace(/\s{2,}/g, " ").trim();
  text = text.replace(/^["'`•\-–\d.)\s]+/, "").replace(/["'`.!]+$/, "");
  if (text.length > 26) {
    const words = text.split(" ");
    while (words.length > 1 && words.join(" ").length > 26) words.pop();
    text = words.join(" ");
  }
  const words = text.split(" ").map((w, i) => {
    if (i > 0 && /^(a|an|the|of|in|to|for|and|or|on|vs)$/i.test(w)) return w.toLowerCase();
    if (/^[A-Z0-9]{2,}$/.test(w)) return w; // keep acronyms like OCR, ELI5
    return w.charAt(0).toUpperCase() + w.slice(1);
  });
  return words.join(" ");
}

// Deterministic, clearly-labelled fallback suggestions when the AI cannot produce valid suggestions.
// Mirrors the original ALTERNATIVES_BY_TYPE intent (four broadly useful actions per kind).
const FALLBACKS = {
  image: [
    { id: "describe_image", label: "Describe This", intent: "Describe what is visible in the selection.", icon: "eye" },
    { id: "extract_text", label: "Extract Text", intent: "Extract all readable text, preserving structure.", icon: "text" },
    { id: "extract_key_details", label: "Key Details", intent: "List the key details visible in the selection.", icon: "list" },
    { id: "explain", label: "Explain It", intent: "Explain what this shows in simple terms.", icon: "explain" }
  ],
  text: [
    { id: "summarize", label: "Summarize", intent: "Summarize the selected text concisely.", icon: "summary" },
    { id: "explain", label: "Explain Simply", intent: "Explain the selected text in plain language.", icon: "explain" },
    { id: "extract_insights", label: "Key Insights", intent: "Extract the most useful insights.", icon: "sparkle" },
    { id: "translate", label: "Translate", intent: "Translate the text to English (or clean it up if already English).", icon: "translate" }
  ],
  code: [
    { id: "explain_code", label: "Explain Code", intent: "Explain what this code does and its caveats.", icon: "code" },
    { id: "debug_code", label: "Find Bugs", intent: "Identify likely bugs and provide corrected code.", icon: "bug" },
    { id: "optimize_code", label: "Optimize", intent: "Optimize for readability and performance, preserving behavior.", icon: "bolt" },
    { id: "add_documentation", label: "Add Docs", intent: "Add clear documentation comments to the code.", icon: "doc" }
  ]
};

export function looksLikeCode(text) {
  const t = String(text || "");
  const signals = [
    /\b(function|const|let|var|def|class|return|import|public|private|static|void|async|await)\b/,
    /[{};]\s*$/m,
    /=>|::|\+\+|!==|===|->/,
    /^\s{2,}\S/m
  ];
  return signals.filter((r) => r.test(t)).length >= 2;
}

export function fallbackSuggestions(selection) {
  if (selection.kind === "image") return FALLBACKS.image;
  if (looksLikeCode(selection.text)) return FALLBACKS.code;
  return FALLBACKS.text;
}

// Normalizes model suggestions: canonical ids, safe labels, de-duplicated intentions, exactly four.
export function normalizeSuggestions(raw, selection) {
  const out = [];
  const seenIds = new Set();
  const seenLabels = new Set();
  for (const s of Array.isArray(raw) ? raw : []) {
    const id = canonicalActionId(s?.id || s?.label);
    const label = cleanLabel(s?.label || id.replace(/_/g, " "));
    const labelKey = label.toLowerCase().replace(/[^a-z0-9]/g, "");
    if (!id || !label || seenIds.has(id) || seenLabels.has(labelKey)) continue;
    if (/^(talk|custom|ask|voice|speak|other)(_|$)/.test(id)) continue; // Talk is always provided by the client
    if (isUnsupportedAction(id, label)) continue;
    seenIds.add(id);
    seenLabels.add(labelKey);
    out.push({
      id,
      label,
      intent: String(s?.intent || label).replace(/\s+/g, " ").trim().slice(0, 200),
      icon: ICONS.includes(s?.icon) ? s.icon : "sparkle"
    });
    if (out.length === 4) break;
  }
  let usedFallback = false;
  if (out.length < 4) {
    for (const f of fallbackSuggestions(selection)) {
      if (out.length === 4) break;
      const key = f.label.toLowerCase().replace(/[^a-z0-9]/g, "");
      if (seenIds.has(f.id) || seenLabels.has(key)) continue;
      out.push({ ...f });
      seenIds.add(f.id);
      seenLabels.add(key);
      usedFallback = true;
    }
  }
  return { suggestions: out, usedFallback };
}
