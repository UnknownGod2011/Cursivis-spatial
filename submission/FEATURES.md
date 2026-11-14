# Cursivis Spatial: features

Status legend:
- **Sim**: verified in the standalone Meta XR Simulator 207.1 (Quest 3 profile, synthetic living room) with real AI.
- **Backend**: verified by unit or live tests against the real Gemini API.
- **Device**: needs a physical Quest 3/3S. Nothing has run on a headset yet, so every device column says UNVERIFIED.

## Core loop: select → Ask Cursivis → four actions or Talk → result

| Feature | What the user does | Sim | Device |
|---|---|---|---|
| Workspace selection | Pinch a document card to select the whole block, or pinch-drag across words to select a range; pinch the photo, or pinch-drag a region of it | Verified with a simulated hand pinch and by the test autopilot | UNVERIFIED |
| Room Snip (Passthrough Camera API) | Tap **Snip**, then pinch-drag a box around anything in the room. Cursivis freezes that crop as the selection | Verified: MRUK `PassthroughCameraAccess` streamed 1280×960 frames from the simulator room; crop captured and sent | UNVERIFIED |
| Selection chip | "Ask Cursivis" appears under the selection with a caption ("Whole paragraph selected", "12 words selected", "Photo region selected") and Clear / Retake | Verified | UNVERIFIED |
| Four AI suggestions | One AI call reads the selection and returns exactly four short, specific actions (for example "Key Findings", "Explain Terms", "Describe Room", "Write Caption") | Verified; 2.1-4.6 s in the simulator runs | UNVERIFIED |
| Talk (never AI-generated) | A fifth chip, always present. Hold to talk, or tap and speak and Cursivis stops when you pause | Typed Talk verified; microphone path UNVERIFIED (the simulator has no Android mic permission flow) | UNVERIFIED |
| Result card | Title, formatted answer that reveals word by word, evidence and uncertainty notes, up to three follow-up chips, scroll | Verified | UNVERIFIED |
| Follow-ups | Pinch a follow-up chip or Talk again; the conversation remembers the last 8 turns for that selection | Verified ("Explain the main finding like I'm 12") | UNVERIFIED |
| Regenerate, More options, Copy | Redo gives a different answer; More options reopens the four suggestions without a new AI call | Regenerate and More options verified | Copy UNVERIFIED |
| Read aloud | Speaks the answer from the orb with Gemini TTS, in chunks so audio starts quickly | Verified; first audio in 3.2-4.4 s | UNVERIFIED |
| General conversation | Pinch the orb with nothing selected and ask anything ("What can you do?") | Verified (typed) | UNVERIFIED |
| Voice commands for the app | "Read that out loud", "switch to light mode", "show me the options", "start over" | Backend verified (`clientAction`) | UNVERIFIED |

## Spatial UI

| Feature | Details | Sim | Device |
|---|---|---|---|
| Orb | Graphite-glass orb (original Cursivis identity) with nine moods: idle, acknowledged, understanding, listening, processing voice, processing, speaking, completed, error. Breathing idle, acknowledgement pop, error shake | Verified | UNVERIFIED |
| Guided ring | The four suggestion chips fly out of the orb with a stagger and shimmer while the AI is thinking; the Talk chip glows and shows a live mic level | Verified | UNVERIFIED |
| Placement | Seated, comfortable cone: the orb hugs the edge of the selection that faces you, the card sits on the other side within ±18°, nothing behind you. If you move more than 45 cm, the layout follows | Verified | UNVERIFIED |
| Grab and pin | Grab the card by its handle to move it; pin it in place or let it follow your view | Not exercised in the simulator | UNVERIFIED |
| Light and dark theme | 240 ms cross-fade (the desktop app switched instantly), remembered between sessions | Verified | UNVERIFIED |
| Buttons | Spring scale on hover and press, eased colours, a confirm flash and a soft click sound | Verified | UNVERIFIED |
| Onboarding | First-run panel: "Snip my room", "Use workspace", "Got it" | Verified | UNVERIFIED |

## Reliability and privacy

| Feature | Details | Verified by |
|---|---|---|
| Nothing leaves the headset until you ask | Only the selected text, or a JPEG crop of the selected region (max 1024 px), is sent, and only when you pinch Ask Cursivis, a suggestion, Talk or Read aloud. No continuous camera or microphone streaming | Code review |
| Camera on only while snipping | `PassthroughCameraAccess` is enabled when you tap Snip and disabled after the capture | Simulator logs |
| No keys on the device | Gemini keys live only on the server (Vercel encrypted environment variables). The APK carries a client token that is used for rate limits only | Code review, secret scan |
| Fast, resilient AI backend | Pool of Gemini keys with least-loaded rotation and cooldowns, per-model health, model fallback chain, a hedged second request after 3-3.5 s, structured JSON output validated with a schema | 23 unit tests, 7 live tests, simulator runs |
| Safe actions only | Suggestions that would buy, order, call, send, post, share or identify a person are filtered out | Unit tests |
| Abuse limits | 40 requests per minute per install, 80 per IP, a daily cap and size limits | Unit tests |
| Graceful failure | Offline orb state and toast, card errors with Retry, stale responses dropped when you select something new, requests cancelled on a new selection, pause and resume handling | Unit tests and simulator; pause/resume UNVERIFIED (device) |
