# Changes from the original Cursivis (Adapted division)

## Summary for the Devpost form (2 paragraphs)

Cursivis started as a Windows desktop assistant: select text or a screen region, press a Logitech MX button (or a
hotkey), and an orb offers AI actions for that selection. The original app depends on the Windows clipboard, desktop
screenshots, a Logitech Options+ plugin, WPF overlay windows and a localhost Node sidecar holding the Gemini key.
Cursivis Spatial is a ground-up rebuild of that idea for Meta Quest 3 and 3S. Selection now happens in the real
world: you pinch-drag a box around anything in your room and the app captures that region through the Passthrough
Camera API, or you pinch text and images on floating workspace documents. Gestures replace the mouse buttons: pinch
"Ask Cursivis", pinch one of four AI-generated suggestions, hold Talk to speak, pinch the orb to start a general
conversation. Results appear on a spatial card with a word-by-word reveal, evidence and uncertainty notes, follow-up
chips, read-aloud from the orb, regenerate, grab, pin and expand.

New since the original release (all added in the 2026 entry window): the hands-first Quest client (Unity 6, OpenXR,
Meta XR Core, Interaction SDK and MR Utility Kit 209); Passthrough Camera API snips with world-to-camera projection,
GPU crop and depth-based placement; app-owned workspace documents with word-range and image-region selection; push-to-
talk and tap-to-talk with silence detection; Gemini text-to-speech read-aloud; per-selection conversation memory and
voice commands that drive the app; a nine-mood orb, an animated suggestion ring and theme cross-fades; and a new
public, authenticated backend that turns one structured AI call into four suggestions (the original made four to six
calls), with a rotating key pool, model fallback and hedged requests, rate limits and no keys on the device. The
desktop-only parts (clipboard capture, Logitech triggers, browser "Take Action" automation, Live Mode desktop tools)
were not ported.

## Changelog

| Area | Original Cursivis (Windows) | Cursivis Spatial (Quest 3/3S) |
|---|---|---|
| Platform | WPF desktop companion + Logitech Options+ plugin + localhost Node sidecar | Native Unity 6000.4 app, OpenXR + Meta XR SDK 209, IL2CPP ARM64; Node 22 backend on Vercel |
| Selection | Clipboard copy of selected text, cursor-area screenshot, full-screen lasso | Room Snip through the Passthrough Camera API (pinch-drag a box in your room); workspace documents with word-range and image-region selection |
| Trigger | MX button tap / long press / dial, hotkeys, haptics | Hand pinch on "Ask Cursivis", suggestion chips and the orb; hold Talk; audible click instead of haptics |
| Suggestions | 2 AI actions + "Custom Voice Command" + 2 placeholders filled after 5 s; 4-6 AI calls per action | Exactly 4 AI suggestions from one structured call + a separate Talk chip that is never AI-generated; specific labels ("Check Allergens") |
| Results | Plain-text result, clipboard copy, insert into the source app with Undo | Spatial card: Markdown, word reveal, evidence, uncertainty, follow-up chips, read aloud, regenerate, more options, copy, grab, pin, expand |
| Voice | Live Mode WebSocket with the raw key on the client; transcription fallback chain | One-shot audio call (transcript + answer + app command) through the backend; push-to-talk or tap-to-talk with silence detection; Gemini TTS read-aloud |
| Memory | None | 8 turns per selection, general conversation history, "what did I just select?" summary |
| Orb | 4 states; errors looked like success | 9 moods with distinct error tint, breathing, pop and shake animations |
| Theme | Instant light/dark switch | 240 ms cross-fade, persisted, voice command |
| Backend | Unauthenticated localhost routes (including runtime key rewrite), no rate limits | Public HTTPS API with app token, per-install and per-IP limits, daily cap, schema validation, size limits; keys only in server environment variables |
| AI resilience | SDK retries with fixed backoff | Least-loaded key rotation with cooldowns, per-model health, model fallback chain, hedged second request, structured output validation, caches |
| Safety | Some meta-actions filtered | Filters buying, ordering, calling, sending, posting, sharing and person identification; grounding rules against fabrication |
| Not ported | — | Browser "Take Action" automation, Smart mode, insert/replace in the source app, Live Mode desktop tools, local LLM providers |

Full file-by-file audit with line references: [`docs/CURSIVIS_PORT_AUDIT.md`](../docs/CURSIVIS_PORT_AUDIT.md).
Original source: https://github.com/UnknownGod2011/MX--cursivis (read-only reference; no code was copied verbatim
except the condensed prompt templates and design tokens listed in the audit).
