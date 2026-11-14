# Final audit (2026-11-14)

Reviewed as a first-time judge would see it, against the four equally weighted Stage Two criteria. Evidence is named
for every claim. **No physical headset was available.** Everything below that says "verified" was verified in the
Meta XR Simulator 207.1 (Quest 3 profile, synthetic Living Room) on a laptop with an Intel UHD 620, or by automated
tests. Device behaviour is marked UNVERIFIED.

## 1. Readiness verdict

| Area | Status |
|---|---|
| Quest APK | Built and release-signed: `builds/android/CursivisSpatial.apk` (70.4 MB, `com.cursivis.spatial` 1.0.1 / code 2, arm64-v8a, Vulkan, OpenXR Meta Quest feature). Attached to GitHub release v1.0.1. **Never installed on a headset.** |
| Manifest | Verified with `aapt`: HEADSET_CAMERA, RECORD_AUDIO, INTERNET, HAND_TRACKING, USE_SCENE; `com.oculus.supportedDevices=quest3\|quest3s`; VR launcher category; hand tracking frequency HIGH; `android.permission.CAMERA` not requested. Extra permissions merged by SDKs: MODIFY_AUDIO_SETTINGS, BLUETOOTH, USE_ANCHOR_API (harmless) |
| Backend | Live at https://cursivis-spatial.vercel.app (Vercel Hobby, free). Unit 23/23, live 7/7, production smoke 4/4 (suggest 2.3 s, act 2.1 s, talk 1.8-2.9 s) |
| Core loop in the simulator | 20/20 (auto4) and 26/26 (final demo run) autopilot steps with live AI: code bug, Spanish notice translation, energy drink 'Is this vegan?' + caffeine follow-up, room snip, research findings |
| Hands-only operation | Simulated-hand ray + pinch selects workspace documents (verified). Pinching the chips and buttons with the simulated hand could not be aimed reliably in the simulator (the hand ray starts at the hand, so it misses near panels the cursor points at); those steps were driven by the test autopilot through the same handlers. **Device check required** (see `docs/TEST_PLAN.md` §4) |
| Submission materials | `submission/` complete; video `submission/media/cursivis-spatial-demo.mp4` (1:59, simulator footage, labelled) and 8 stills |
| Human-only steps | Meta Dashboard app + "Competition" channel + invite URL, Devpost form, YouTube upload, Start membership (see `submission/RELEASE_CHECKLIST.md`) |

Verdict: **ready to submit as a simulator-verified build, with an explicit device-verification gap.** The single most
valuable remaining action is a 10-minute run on any Quest 3/3S.

## 2. Scores against the judging criteria (honest self-assessment)

| Criterion (25% each) | Score /10 | Why | Biggest risk |
|---|---|---|---|
| Innovation & Creativity | 8 | Selection-first agent: the user frames real-world content or app content and the AI proposes the next actions; no prompt typing. Camera snips grounded with uncertainty notes | Judges may compare with other "camera + LLM" entries; the four-suggestion ring and snip flow are the differentiators |
| Experience Design | 6 | Seated, comfortable cone (orb below the workspace, card within ±18°), onboarding, clear states and errors, Talk always available | Hand-pinch targeting of small chips is UNVERIFIED on device; simulator frame rate hides real motion quality |
| Technical Implementation | 7 | ISDK hands, MRUK Passthrough Camera API with world→camera projection, depth raycast fallback, keyless-client backend with key pool, model fallback and hedging, 23 unit tests, reproducible headless builds | No on-device frame-rate or thermal data; Passthrough Camera API path only exercised in the simulator |
| Polish & Presentation | 7 | Original Cursivis identity (graphite orb, silver shine, chips, result card), animation pass, light/dark cross-fade, labelled video, complete docs | Simulator footage at ~6 fps looks choppy; a headset recording would be far stronger |

## 3. Weaknesses found in this audit and what was done

| # | Weakness | Cause | Fix | Verified by |
|---|---|---|---|---|
| 1 | Actions took 7.7 s | Quality chain started with gemini-3.5-flash (15-20 s on the free tier) and hedged only after 6 s | Flash-lite first, hedge at 3-3.5 s onto another model family, re-hedge on fast failure, timeouts move down the chain | auto4: 1.3-2.6 s; unit tests 23/23 |
| 2 | Typed Talk ignored after suggestions appeared | Transition table had no SuggestionsVisible → Processing | Table extended; requests never run when a transition is rejected | auto4 snip/talk_about_snip ok |
| 3 | Buttons froze with NaN scale at low frame rates | Explicit Euler spring step unstable when ω·dt > 2 | Exact closed-form critically damped step + NaN guard | No NaN warnings in later runs |
| 4 | Orb and dock covered the bottom-right document; dock clipped at the view edge | Home orb at −17° pitch, 19° yaw overlapped the 2×2 grid | Grid raised (rows +12.5° / −5.5°), orb at ~15° right, ~23° down | Simulator screenshot after the fix |
| 5 | Red errors in the desktop window of development builds | Operator API layer enabled for Standalone (tool registration failed), depth manager enabled where depth is unsupported, Microsoft hand-interaction bindings rejected by the simulator | API layers and Microsoft hand interaction off for Standalone; depth enabled only when `EnvironmentDepthManager.IsSupported` | Release Windows build shows no console; Android settings unchanged |
| 6 | "Offline" / "Back online" toasts on startup | First health check from the simulator player often times out (curl error 28 after 8 s) and every result toasted | No toast for the first result, a failed check is re-tried after 3 s, idle offline needs two failed rounds; real request failures still report immediately | Code review; simulator log |
| 7 | Layout could drift away from the user | Head jumped in 1 m steps in one simulator run (cause unknown) | Head-relative layout follows the head when it moves more than 45 cm | auto4 `layout_follow` counter |
| 9 | Health check raised false 'offline' toasts and the first request after launch took up to 20 s | GET /v1/health from the desktop player timed out ('0 bytes received') while POSTs answered in 2-3 s | Reachability uses the POST path (any HTTP answer counts), never overrides a recent successful request; first attempt of each request times out at 9 s and retries | Final demo run: no offline toasts, suggestions 1.9-7.1 s |
| 8 | Panels hidden at startup might never become hand-ray targets | They were deactivated inside `Awake` before Interaction SDK components ran `Start` | Panels now hide after the first frame (alpha 0, raycasts off until then) | Defensive; simulator aiming could not prove either way. Device check item |

## 4. Outstanding limitations (not fixed)

- **No device run.** Frame rate (target 72 fps, minimum 60), hand-tracking pinch reliability on chips, Passthrough
  Camera API permission flow and image quality, microphone permission, comfort and legibility are all UNVERIFIED.
- **Voice was not exercised with a real microphone** in any test. The backend Talk path was verified with synthesized
  speech (live test), and typed Talk through the app.
- **Live API not shipped.** Talk is push-to-talk or tap-to-talk with silence detection; there is no barge-in or
  streaming conversation.
- **Room snips are rectangles**, not object segmentation. The AI is told it sees a rectangular crop.
- **Simulator footage** is choppy (about 6 fps on the laptop GPU) and silent (no audio capture), so read-aloud is shown
  by the orb's speaking state only.
- **Dev-agent token in `Assets/Resources/DevAgentSettings.asset`.** The Meta Immersive Debugger build step writes a
  local LAN dev-agent token and the build machine's LAN IP into this asset on every build. The dev agent is disabled
  (`enabled: 0`), the token only works against the Unity editor's local dev-agent server, and it grants nothing on any
  cloud service. Rotating it: delete the asset's `accessToken` before a build on a different machine.
- **Free-tier AI capacity.** 6 of the 10 Gemini keys work; the free tier can rate-limit under heavy judging traffic.
  The pool rotates keys and the app shows a clear "busy, try again" message rather than failing silently.
- Four of the supplied Gemini keys return "free tier not available"; they are disabled automatically.

## 5. Measured metrics (simulator, laptop, India home internet; not Quest numbers)

| Metric | Value | Source |
|---|---|---|
| Gesture → visual feedback | p50 17 ms (one frame at simulator rate) | auto4 |
| Trigger → four suggestions | p50 3.6 s (2.8-5.0 s) | auto4 |
| Action latency | p50 2.2 s (1.3-2.6 s) | auto4 |
| Talk answer (typed) | p50 1.9 s | auto4 |
| Read-aloud first audio | 3.9 s | auto4 |
| Camera start / crop | 495 ms / 365 ms | auto4 |
| Frame rate | ~7 fps (UHD 620 + simulator) | auto4; meaningless for Quest |
| Managed memory | 9-15 MB | auto4 |

## 6. Security and privacy review

- No API keys in the repo or the APK (pattern scan of every committed file: 0 hits for Google key, client-token and
  private-key patterns). The APK carries only the public client token used for rate limiting.
- Backend: token check, per-install (40/min) and per-IP (80/min) limits, daily cap (6000), body and media size limits,
  schema validation with `additionalProperties: false`, no storage, logs without content.
- Camera runs only while Snip is armed; only the selected crop (≤1024 px JPEG) is uploaded. Microphone records only while
  Talk is held or until the pause in tap mode. The onboarding panel states what is sent.
- Release keystore lives outside the repo; its password is stored only DPAPI-encrypted for the build user.
