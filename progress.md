# Cursivis Spatial — progress

**Goal:** ship Cursivis Spatial, a hands-first Meta Quest 3/3S app (select anything → Cursivis orb → four AI suggestions
+ Talk → spatial result card) for the Meta Horizon Start Developer Competition 2026 (deadline 2026-11-19 01:30 IST),
Productivity track, Adapted division. No headset available: development and footage use the Meta XR Simulator.

## Current phase
Phase B/C (first complete flow + product UI) running in the simulator. APK build next.

## Done (with evidence)
- Backend (`backend/`): suggest/act/talk/speak, Gemini key pool + model fallback + hedging, abuse limits.
  Unit 21/21, live 7/7 (real Gemini), deployed: https://cursivis-spatial.vercel.app (health + auth + real suggest verified).
- Unity project (`unity/CursivisSpatial`): Unity 6000.4.6f1, OpenXR 1.19, Meta XR Core/ISDK/MRUK 209.0.0, URP.
  Headless setup (`ProjectSetup`), scene from code (`SceneBuilder`), builds (`CursivisBuild`).
- App code: orb (original graphite-glass identity), guided ring (4 AI + Talk), result card, workspace documents,
  passthrough snip (MRUK PassthroughCameraAccess), push-to-talk, read-aloud, theme, metrics, autopilot harness.
- Windows player runs in Meta XR Simulator 207.1 on this iGPU with **Vulkan** (D3D11 hung at swapchain creation);
  OpenXR session FOCUSED; orb + dock rendered (screenshot `test-results/local/shots/lr1.png`, local only).
- Meta XR Operator layer loads in the player (MCP on :8720, tool list retrieved) when
  `XR_API_LAYER_PATH`/`XR_ENABLE_API_LAYERS` are set; it does not yet see the XrSession (investigating).
- Docs: RESEARCH, CURSIVIS_PORT_AUDIT, UI_DESIGN_SPEC, INTERACTION_SPEC, ARCHITECTURE, TEST_PLAN, PREFLIGHT.

## In progress / next
1. Rebuild with passthrough support = Required (was None → black background) and tracked-head placement.
2. Simulator E2E via autopilot + simulated input; screenshots; fix UI issues.
3. Quest APK (IL2CPP ARM64) + manifest verification.
4. Demo footage in the simulator (labelled), submission docs, final audit.

## Known issues
- Simulator first run shows a consent dialog and a firewall prompt; dismiss before launching the app.
- `Hand Interaction Profile` bindings fail in the simulator (XR_ERROR_HANDLE_INVALID) — harmless; Meta hand tracking aim is used.

## Manual steps for the developer
- Meta Horizon Developer Dashboard: create the app, release channel **Competition**, upload APK, enable URL invites (auto-approve).
- Test the APK on a Quest 3/3S if one can be borrowed (all device behaviour is UNVERIFIED).
- Devpost: video upload (YouTube/Vimeo public), submit.

## Links
docs/RESEARCH.md · docs/CURSIVIS_PORT_AUDIT.md · docs/ARCHITECTURE.md · docs/INTERACTION_SPEC.md · docs/UI_DESIGN_SPEC.md · docs/TEST_PLAN.md · docs/PREFLIGHT.md

## Latest commit
See `git log -1` (updated at each milestone).
