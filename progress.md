# Cursivis Spatial: progress

**Goal:** ship Cursivis Spatial, a hands-first Meta Quest 3/3S app (select anything → Cursivis orb → four AI
suggestions + Talk → spatial result card) for the Meta Horizon Start Developer Competition 2026 (deadline
2026-11-19 01:30 IST), Productivity track, Adapted division. No headset was available: development, tests and the
demo video use the Meta XR Simulator.

## Status (2026-11-14 19:00 IST)
All buildable deliverables are done. What is left needs the developer's accounts (Meta Dashboard, YouTube, Devpost)
or a physical headset. See `submission/RELEASE_CHECKLIST.md`.

| Deliverable | Where | Evidence |
|---|---|---|
| Quest APK 1.0.1 (code 2), release-signed | `builds/android/CursivisSpatial.apk`, GitHub release v1.0.1 | `aapt` + `apksigner` checks, `docs/TEST_PLAN.md` §5 |
| Backend | https://cursivis-spatial.vercel.app | unit 23/23, live 7/7, production smoke flows |
| Simulator end-to-end | `tools/demo-run.ps1` | 26/26 autopilot steps with live AI (2026-11-14 18:33) |
| Demo video, 1:59, labelled simulator footage | `submission/media/cursivis-spatial-demo.mp4` (also on the GitHub release) | cut list `submission/media/demo-segments.tsv` |
| Devpost stills | `submission/media/0*.png` | frames of the same recording |
| Docs | `docs/`, `submission/`, `README.md` | |

## Done
- Backend: suggest / act / talk / speak; Gemini key pool with cooldowns, model fallback, hedging (3-3.5 s), re-hedge,
  timeout fallback; grounding rules (clear "Not vegan" verdicts when a visible ingredient decides it, general
  knowledge such as the 400 mg/day caffeine guidance marked as such); abuse limits.
- Quest client: orb (9 moods), guided ring with four AI chips + Talk, result card (reveal, scroll, follow-ups, read
  aloud, regenerate, pin, grab), workspace documents (research abstract, Spanish notice, energy drink label, code),
  room Snip through MRUK `PassthroughCameraAccess`, push-to-talk and tap-to-talk, light/dark cross-fade, onboarding.
- Fixes from simulator testing: locomotion off (rig fell), JPEG passthrough, tracking-space conversion, transition
  table for typed Talk, exact spring step (NaN scale), layout clear of the workspace, panels hidden at startup now
  become hand-ray targets, reachability check that does not raise false "offline" toasts, short first-attempt timeout
  with retry for stalled connections.

## Not verified (needs a Quest 3/3S)
Frame rate, hand-pinch reliability on chips, camera and microphone permission flows, real voice input, comfort.

## Manual steps for the developer
`submission/RELEASE_CHECKLIST.md`: Meta Dashboard app + "Competition" channel + invite URL, upload the APK, YouTube
upload of the video, Devpost form, Start membership.

## Links
docs/RESEARCH.md · docs/CURSIVIS_PORT_AUDIT.md · docs/ARCHITECTURE.md · docs/INTERACTION_SPEC.md ·
docs/UI_DESIGN_SPEC.md · docs/TEST_PLAN.md · docs/PREFLIGHT.md · docs/FINAL_AUDIT.md · docs/DEMO_SCRIPT.md

## Latest commit
See `git log -1`.
