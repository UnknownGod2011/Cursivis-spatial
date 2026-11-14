# Test plan and results

Legend: **PASS** (verified, evidence named), **FAIL**, **PENDING** (not run yet), **UNVERIFIED (device)** (needs a
Quest 3/3S; no headset was available). All AI results below are real Gemini responses through the real backend. No
mocked AI output is used to claim AI behaviour; the unit tests use a fake transport and only claim backend logic.

## 1. Automated tests

| Layer | Command | What it covers | Result |
|---|---|---|---|
| Backend unit | `cd backend && npm test` | key pool rotation and cooldowns, error classification, model fallback, compat retry, hedging, re-hedge after a fast hedge failure, timeout → next model, no slot leaks, schema-invalid fallback, safety stop, deadlines, auth / rate / size limits, suggestion normalization, WAV helpers, endpoint contracts | **PASS** 23/23 (2026-11-14) |
| Backend live | `cd backend && npm run test:live` | 7 real scenarios against Gemini with fixture selections (§3) | **PASS** 7/7 (2026-11-13), `test-results/local/backend-live-metrics.json` |
| API smoke, local | `node tools/smoke-flow.mjs` | suggest → act → contextual talk → general talk | **PASS** (2026-11-14): 3.1 s / 2.2 s / 1.3 s / 2.0 s |
| API smoke, production | `node tools/smoke-flow.mjs https://cursivis-spatial.vercel.app` | same against the deployed backend | **PASS** (2026-11-14): 2.3 s / 2.1 s / 2.9 s / 1.8 s |
| Unity compile + scene | `tools/unity-batch.ps1 -Method ...SceneBuilder.BuildBatch` | runtime + editor assemblies against SDK 209; scene generated from code | **PASS** |
| Windows player | `...CursivisBuild.BuildWindows` | Vulkan-first desktop player for the simulator | **PASS** (incremental 1.0-1.3 min) |
| Quest APK | `tools/build-apk.ps1 -VersionCode 1` | IL2CPP ARM64, Vulkan, OpenXR (Meta Quest feature), release-signed | see §5 |
| Android manifest | `aapt dump badging` / `aapt dump xmltree` on the APK | package, permissions, `supportedDevices`, VR category, hand tracking | see §5 |

## 2. Simulator end-to-end (Meta XR Simulator 207.1, Quest 3 profile, Living Room)

Run: `tools/run-sim.ps1 -LogName auto4 -Autopilot all` (Development build `sim-win9`, local backend with request
logs). The autopilot calls the same app methods as the buttons and waits for real state changes and real AI results.

| Scenario | Steps (all real AI) | Result |
|---|---|---|
| workspace | select research abstract → Understanding → 4 suggestions ("Key Findings, Simplify Text, Explain Terms, Research Questions") → Key Findings result → scroll → read aloud (audio playing) → stop → typed follow-up "Explain the main finding like I'm 12" → theme dark → light → dark | **PASS** 7/7 |
| photo | select photo → 4 suggestions ("Describe Details, Extract Sign Text, Write Caption, Identify Elements") → Write Caption → Redo returns a different caption | **PASS** 5/5 |
| general | "What can you do?" with no selection | **PASS** 1/1 |
| snip | Snip armed → `PassthroughCameraAccess` playing 1280×960 → pinch-drag (simulated drag on the snip surface) → 479×344 crop → 4 suggestions ("Describe Scene, Generate Caption, Analyze Palette, List Elements") → Talk "What is this?" → grounded answer about the curtains and lamp | **PASS** 7/7 |
| **Total** | | **PASS 20/20** (auto3 before the fixes: 19/20, auto2: 13/16) |

Simulated hands (not the autopilot): with the simulator's right input set to **Hand**, the pinch key selected the code
document text and the workspace photo through the Interaction SDK hand ray (`[CursivisSelect] ... [Workspace/Text]`,
`[Workspace/Image]`). **PASS**. Aiming is done by turning the simulated head, so chip-by-chip hand navigation is slow
but works.

Bugs found and fixed through these runs: rig falling under gravity (locomotion disabled), black passthrough (JPEG
texture path), snip "behind the camera" (tracking-space conversion), typed Talk rejected in some states (transition
table), NaN button scale at low frame rates (exact spring step), UI placed off to the side (placement rewrite), 7.7 s
actions (model order and hedging).

## 3. Functional scenarios

| # | Scenario | Backend + real AI | Simulator | Quest 3/3S |
|---|---|---|---|---|
| 1 | Food packaging: suggestions, Talk "Is this vegan?", grounded verdict, read aloud, follow-up; cropped label → qualified answer | **PASS** (fixture image; spoken question synthesized with TTS) | not in the room (no packaging in the synthetic environment) | UNVERIFIED (device) |
| 2 | Printed text (French): Translate suggested, translation, follow-up | **PASS** | PENDING (Spanish document available in the workspace) | UNVERIFIED (device) |
| 3 | Workspace photo: caption suggested, caption, regenerate differs | **PASS** | **PASS** (§2) | UNVERIFIED (device) |
| 4 | General orb conversation + "read your last answer out loud" (clientAction) | **PASS** | **PASS** (typed) | UNVERIFIED (device) |
| 5 | Hands-only flow: select, trigger, choose, scroll, move, dismiss | n/a | selection by simulated pinch **PASS**; full flow by autopilot **PASS** | UNVERIFIED (device) |
| 6 | Error handling (below) | partly (unit tests) | partly | UNVERIFIED (device) |

### Error handling

| Case | Expected | Where verified |
|---|---|---|
| No internet / backend down | "Offline" orb + toast; card error with Retry; no crash | PENDING (simulator with backend stopped) |
| API timeout | `ai_timeout` 504 → card error + Retry | unit test **PASS**; client UNVERIFIED |
| Invalid / no-free-tier keys | key disabled 6 h, next key used; all bad → `ai_unavailable` 503 | unit tests **PASS**; live: 4 of 10 keys return 403 and are skipped **PASS** |
| Overloaded model | hedge or next model | unit tests **PASS**; live: auto3 log shows 503 → fallback |
| Camera permission denied | toast, workspace and Talk keep working | UNVERIFIED (device; the simulator has no Android permission flow) |
| Microphone permission denied | toast, suggestions still work | UNVERIFIED (device) |
| Empty selection | rejected locally / 400 `selection text is empty` | unit test **PASS** |
| Hand tracking lost mid-drag | drag cancelled, held Talk sent | UNVERIFIED (device) |
| Large image | ≤1024 px JPEG on device; backend rejects > 2 MB | unit test **PASS** |
| Long AI result | scrolls; reveal; read aloud in chunks | **PASS** (simulator, Key Findings) |
| New selection during request | old request cancelled, stale response dropped | code path present; PENDING explicit test |
| Pause / resume, fresh restart | camera, mic and speech stop; "Paused … Retry" card | UNVERIFIED (device) |

## 4. Ten-minute device check (for whoever first runs the APK on a Quest 3/3S)

1. Install from the Competition invite URL (or `adb install -r CursivisSpatial.apk`). Launch from the library.
2. With **controllers put down**, confirm hand rays appear and the onboarding panel responds to a pinch.
3. Pinch the research document → "Ask Cursivis" → four suggestions in ≤ 5 s → pinch one → answer card.
4. Read aloud plays from the orb; Talk (hold pinch) asks for the microphone once, then answers a spoken question.
5. Tap Snip → allow the headset camera → pinch-drag a box around a real object → suggestions about that object.
6. Turn 90° and back; grab the card by its handle and move it; pin it.
7. Watch for frame drops (target ≥ 72 fps; the rules ask for 60+), text legibility at arm's length, and comfort.
8. Take the headset off and on (pause/resume), then retry an action.
Record results here with the date and headset model.

## 5. Quest APK

| Check | Result |
|---|---|
| Build | PENDING |
| Signing | release keystore (outside the repo) |
| Manifest | PENDING |

## 6. Performance measurements (measured only)

Simulator figures come from a laptop (Intel UHD 620 integrated GPU, 7.8 GB RAM) and say nothing about Quest
performance. Network: home internet in India.

| Metric | Value | Conditions |
|---|---|---|
| Time to suggestions (trigger → chips filled) | p50 3.6 s (2.8-5.0 s, n=3) | auto4, simulator client, local backend |
| Action latency (pinch → result) | p50 2.2 s (1.3-2.6 s, n=3) | auto4 |
| Talk answer (typed) | p50 1.9 s (1.8-2.0 s, n=3) | auto4 |
| Read-aloud first audio | 3.9 s | auto4 |
| Gesture → visual feedback | p50 17 ms (max 264 ms) | auto4, one frame at simulator frame rate |
| Camera start / crop | 495 ms / 365 ms | auto4, simulator camera |
| Simulator frame rate | mean 7.0 fps | UHD 620; not a Quest number |
| Managed heap | 9-15 MB | auto4 |
| Backend, production | suggest 2.3 s, act 2.1 s, talk 1.8-2.9 s | smoke flow 2026-11-14 |

Quest frame rate, thermals and battery: **UNVERIFIED (device)**.
