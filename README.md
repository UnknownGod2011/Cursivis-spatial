# Cursivis Spatial

**Selection is Context. Gesture is Intent. Cursivis is Action.**

A hands-first AI assistant for Meta Quest 3 and 3S. Frame anything in your room (Passthrough Camera API) or pinch text
and images on floating workspace documents, pinch **Ask Cursivis**, and pick one of four AI-generated actions or
**Talk**. Answers appear on a spatial card with follow-ups, read-aloud, regenerate, pin and grab.

Built for the Meta Horizon Start Developer Competition 2026 (Productivity track, Adapted division). It is a rebuild of
the Windows app [Cursivis](https://github.com/UnknownGod2011/MX--cursivis) for mixed reality; see
[`submission/CHANGES_FROM_ORIGINAL.md`](submission/CHANGES_FROM_ORIGINAL.md).

> **Status, honestly.** There was no headset during development. The app was built and tested in the standalone
> Meta XR Simulator 207.1 (Quest 3 profile) with real AI responses. Every on-device behaviour (frame rate, hand
> tracking quality, camera and microphone permissions, comfort) is **UNVERIFIED** until someone runs the APK on a
> Quest 3/3S. Simulator numbers below are laptop numbers, not Quest numbers.

## What works (verified in the simulator)

| Flow | Evidence |
|---|---|
| Workspace text → 4 suggestions → action → result → read aloud → follow-up | Autopilot run auto4: 20/20 steps, actions in 1.7-2.8 s |
| Photo → suggestions → caption → regenerate | auto4 |
| Room Snip through MRUK `PassthroughCameraAccess` (1280×960 simulator camera) → suggestions → Talk about it | auto4, `snip-preview.png` |
| General conversation from the orb | auto4 |
| Simulated hand pinch selects workspace text and the photo | simulator, right-hand ray + pinch |
| Backend unit tests | `npm test`: 23/23 |
| Backend live tests (real Gemini) | `npm run test:live`: 7/7 |

Details and the remaining device checks: [`docs/TEST_PLAN.md`](docs/TEST_PLAN.md).

## Repository layout

| Path | What |
|---|---|
| `unity/CursivisSpatial/` | Unity 6000.4.6f1 project (OpenXR 1.19, Meta XR Core / Interaction SDK / MRUK 209, URP 17.4) |
| `unity/.../Assets/Cursivis/Scripts/` | App code: `App/` (flow, panels, autopilot), `UI/` (orb, ring, card, theme), `Capture/` (passthrough camera), `Selection/`, `Voice/`, `AI/`, `Core/` |
| `unity/.../Assets/Cursivis/Editor/` | Headless project setup, scene builder, build pipeline |
| `backend/` | Node 22 + Express 5 API for Vercel: `/v1/suggest`, `/v1/act`, `/v1/talk`, `/v1/speak`, `/v1/health` |
| `tools/` | Build, simulator, screenshot, input and smoke-test scripts |
| `docs/` | Research, port audit, architecture, interaction and UI specs, test plan, demo script, final audit |
| `submission/` | Devpost texts, release checklist, screenshot plan, media |

## Run it

### Backend (local)
```powershell
cd backend
npm install
copy .env.example .env   # add GEMINI_API_KEYS (comma separated) and CURSIVIS_APP_TOKENS
npm test                 # 23 unit tests, no network
npm start                # http://127.0.0.1:8787/v1/health
node ../tools/smoke-flow.mjs   # real suggest -> act -> talk round trip
```
Deploy: `cd backend && vercel deploy --prod` with `GEMINI_API_KEYS` and `CURSIVIS_APP_TOKENS` set as Vercel
environment variables. Production: https://cursivis-spatial.vercel.app/v1/health

### Quest APK
```powershell
powershell -File tools/build-apk.ps1 -VersionCode 1   # -> builds/android/CursivisSpatial.apk
```
The script points the build at the production backend, reads the client token from `backend/.env`, and signs with the
release keystore if it exists (see `submission/RELEASE_CHECKLIST.md` §8). Install for a quick test with
`adb install -r builds/android/CursivisSpatial.apk`; judges get it through the **Competition** release channel.

### Meta XR Simulator (no headset)
```powershell
$env:CURSIVIS_BACKEND_URL = "https://cursivis-spatial.vercel.app"; $env:CURSIVIS_DEVELOPMENT = "1"
powershell -File tools/unity-batch.ps1 -Method Cursivis.Spatial.EditorTools.CursivisBuild.BuildWindows -Target StandaloneWindows64 -Name build-win
powershell -File tools/run-sim.ps1 -LogName run1 -Autopilot all     # scripted end-to-end run with real AI
```
Simulator setup notes (Vulkan, JPEG passthrough, seated rig, hand input): [`docs/PREFLIGHT.md`](docs/PREFLIGHT.md).

## Privacy and safety
- Nothing leaves the headset until you pinch Ask Cursivis, a suggestion, Talk or Read aloud. Only the selected text or
  a JPEG crop of the selected region is sent; there is no continuous camera or microphone streaming.
- The camera runs only while you snip. Permission `horizonos.permission.HEADSET_CAMERA` is requested when you first tap
  Snip; `android.permission.CAMERA` is not requested.
- Gemini keys stay on the server. The backend stores nothing and logs timings and error categories only.
- Suggestions that would buy, order, call, send, post, share or identify a person are filtered out.

## Docs
[Research](docs/RESEARCH.md) · [Port audit](docs/CURSIVIS_PORT_AUDIT.md) · [Architecture](docs/ARCHITECTURE.md) ·
[Interaction spec](docs/INTERACTION_SPEC.md) · [UI design spec](docs/UI_DESIGN_SPEC.md) · [Test plan](docs/TEST_PLAN.md) ·
[Preflight](docs/PREFLIGHT.md) · [Demo script](docs/DEMO_SCRIPT.md) · [Final audit](docs/FINAL_AUDIT.md) ·
[Progress](progress.md)

## Credits and licences
- Fonts: Inter and IBM Plex Mono (SIL Open Font License 1.1), Material Icons (Apache 2.0).
- Workspace photo: "Floras Lake" by BLM Oregon & Washington, public domain (Wikimedia Commons).
- Meta XR SDK packages are used under the Meta Platform Technologies SDK licence; Unity packages under their licences.
