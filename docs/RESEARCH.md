# Cursivis Spatial: research notes

Snapshot: 2026-11-13. Target: Meta Quest 3 / 3S. Engine: Unity 6000.4.6f1. The app is being built without a headset.

- Nothing in this document was tested on a Quest headset.
- "UNVERIFIED" means neither an official source nor a local test confirmed the statement.
- "Pending" means the item depends on the Meta XR Simulator actually running on this machine. That has not been verified yet.
- The detailed notes behind this summary are in `C:\Users\Admin\cursivis-ref\notes\`: `research-meta-sdk.md`, `research-xrsim.md`, `research-competition.md`, `backend-audit.md` and `ui-audit.md`.

## 1. Sources

`<docs>` stands for `https://developers.meta.com/horizon/documentation/unity`.

| Topic | Sources |
|---|---|
| Package versions | https://npm.developer.oculus.com/com.meta.xr.sdk.core (also `com.meta.xr.mrutilitykit`, `com.meta.xr.sdk.interaction`, `com.meta.xr.sdk.interaction.ovr`), https://packages.unity.com/com.unity.xr.openxr |
| XR backend, project setup | `<docs>/unity-xr-plugin/`, `<docs>/unity-and-openxr-compatibility/`, `<docs>/unity-project-setup/`, `<docs>/unity-openxr-settings-quest/`, `<docs>/unity-development-requirements/` |
| Passthrough Camera API (PCA) | `<docs>/unity-pca-overview/`, `<docs>/unity-pca-documentation/`, `<docs>/unity-pca-migration-from-webcamtexture/`, MRUK 209.0.0 source `Core/Scripts/PassthroughCameraAccess.cs`, https://github.com/oculus-samples/Unity-PassthroughCameraApiSamples |
| Interaction SDK (ISDK) | `<docs>/unity-isdk-quick-actions/`, `<docs>/unity-isdk-canvas-integration/`, `<docs>/unity-isdk-add-comprehensive-interaction-rig/`, `<docs>/unity-isdk-grabbable/`, `<docs>/unity-handtracking-interactions/`, ISDK 209.0.0 package source |
| Design and performance | https://developers.meta.com/horizon/design/hands-ui-best-practices/, https://developers.meta.com/horizon/design/styles_typography/, `<docs>/unity-perf/`, `<docs>/po-perf-opt-mobile/` |
| Manifest, VRCs, release channels | https://developers.meta.com/horizon/resources/publish-mobile-manifest/, https://developers.meta.com/horizon/resources/publish-common-vrc-failures/, https://developers.meta.com/horizon/resources/publish-release-channels/, https://developers.meta.com/horizon/resources/publish-release-channels-add-users/ |
| Meta XR Simulator | `<docs>/xrsim-intro/`, `<docs>/xrsim-getting-started/`, `<docs>/xrsim-heroscenes/`, `<docs>/xrsim-synthetic-environment-builder/`, `<docs>/xrsim-input-keyboard/`, `<docs>/xrsim-camera-tracking/`, `<docs>/xrsim-session-capture/`, https://developers.meta.com/horizon/downloads/package/meta-xr-simulator-windows/ |
| Meta VR CLI, agent tooling | https://developers.meta.com/horizon/essentials/metavr-environment, https://developers.meta.com/horizon/essentials/metavr-install, https://github.com/meta-quest/agentic-tools |
| Competition | https://start-developer-competition-26.devpost.com/ with `/rules`, `/details/faqs`, `/details/judging`, `/details/special-awards`, `/updates`. Devpost returned HTTP 403 to direct requests, so these pages were read through `https://r.jina.ai/<url>`. |
| Reference projects | https://developers.meta.com/horizon/blog/meta-horizon-start-developer-competition-meet-the-winners/, https://github.com/oculus-samples/Unity-SpatialLingo |
| Gemini API | https://aistudio.google.com/docs/models, `/docs/speech-generation`, `/docs/audio`, `/docs/structured-output`, `/docs/live-api/ephemeral-tokens`, `/docs/api-key`. Every `ai.google.dev/gemini-api/docs/*` URL now redirects (HTTP 301) to these pages. |
| Local measurements | `tools/probe-gemini.mjs`, `tools/probe-gemini-features.mjs`, `backend/tests/live/live.test.js`, `test-results/local/backend-live-metrics.json`, `test-results/local/unity/*.log` |

## 2. Meta SDK stack and versions

Pinned in `unity/CursivisSpatial/Packages/manifest.json`. The editor version is in `ProjectSettings/ProjectVersion.txt`.

| Package | Version | Notes |
|---|---|---|
| Unity Editor | 6000.4.6f1 | Meta's minimum is 6000.0.66f2. Meta does not state that it tested 6000.4 (UNVERIFIED). |
| `com.meta.xr.sdk.core` | 209.0.0 | Released 2026-11-10. Version numbers jumped from 85.0.0 (Feb 2026) to 201.0.0 (Apr 2026). |
| `com.meta.xr.sdk.interaction` / `.interaction.ovr` | 209.0.0 | ISDK Essentials plus the OVR integration. |
| `com.meta.xr.mrutilitykit` (MRUK) | 209.0.0 | Provides `PassthroughCameraAccess`. MRUK 205 or later is required for PCA preview in the simulator. |
| `com.unity.xr.openxr` | 1.19.0 | Meta's docs still list 1.15.1 as the "recommended version". |
| `com.unity.xr.management` | 4.5.4 | Same version as the PCA sample. |
| `com.unity.render-pipelines.universal` (URP) | 17.4.0 | Meta asks for projects created from the Universal 3D template. |
| `com.unity.nuget.newtonsoft-json`, `com.unity.ugui` | 3.2.1, 2.0.0 | |

- XR backend: Unity OpenXR Plugin with the "Meta XR" feature group on Standalone and Android. The Oculus XR Plugin is deprecated and supports only Meta XR SDK v73 or earlier. The simulator requires OpenXR.
- Packages left out on purpose:
  - `com.meta.xr.simulator` (81.0.1, deprecated; Meta says to remove it and use the standalone simulator).
  - `com.meta.xr.sdk.all` (404 on the Meta registry).
  - `com.meta.xr.sdk.voice` (85.0.1, Wit.ai cloud TTS, soft-deprecated).
  - `com.meta.xr.sdk.platform` (needed only for the store entitlement check, VRC.Quest.Security.1).
- The Meta packages also resolve from the Unity registry, so the manifest has no scoped registry.
- `Editor/ProjectSetup.cs:85-222` applies the settings, then runs the Meta Project Setup Tool `FixAllAsync` for both targets (`:236-275`, log in `test-results/local/unity/setup2.log`).
  - Android: minSdk 32 and targetSdk 34 (required for apps created after 2026-03-01), IL2CPP, ARM64 only (VRC.Quest.Packaging.6), Vulkan, single-pass instanced.
  - Windows: D3D11. Both targets: Linear color space, ASTC textures.

## 3. Passthrough Camera API

### 3.1 API, permission, devices
- Current API: MRUK `Meta.XR.PassthroughCameraAccess`, a MonoBehaviour. The older `WebCamTextureManager` sample code is superseded.
- Members used:
  - `CameraPosition` (Left/Right), `RequestedResolution` (default 1280×960), `CurrentResolution`, `IsPlaying`, `IsUpdatedThisFrame`.
  - `Intrinsics` (FocalLength, PrincipalPoint, SensorResolution, LensOffset).
  - `GetTexture()`, `GetCameraPose()`, `ViewportPointToRay()`, `WorldToViewportPoint()`, `IsSupported`.
- Permission:
  - Only `horizonos.permission.HEADSET_CAMERA` is needed (`OVRPermissionsRequester.Permission.PassthroughCameraAccess`), and passthrough must be enabled.
  - The component does not request the permission. It polls until the permission is granted.
  - Request it from one place, not combined with OVRManager "Permission Requests On Startup". Re-add it by hand after regenerating the manifest.
- Devices: Quest 3 and Quest 3S. `PassthroughCameraAccess` needs Horizon OS v81 or later. Editor preview through Link or the simulator needs v85 or later.

### 3.2 Resolutions, latency, cost
- Supported resolutions:
  - All OS versions: 320×240, 640×480, 800×600, 1280×960.
  - v83 adds 640×360, 720×480, 720×576, 1024×576, 1280×720, 1280×1080 and 1280×1280. v209 adds 1728×1728.
- An unsupported request falls back to a smaller size, so read `CurrentResolution` after start.
- Capture latency is 20-40 ms, at 60 Hz, internal format YUV420. Each streamed camera costs about 1-2% GPU and about 45 MB.
- The camera image covers less than the passthrough view. Even 1280×1280 does not cover all of it.

### 3.3 World ↔ camera mapping (MRUK 209 source)
- Viewport coordinates are normalized with the origin at the bottom-left: (0,0) is bottom-left, (1,1) is top-right. Pixel to viewport: `(px / CurrentResolution.x, py / CurrentResolution.y)`.
- The camera pose is the head pose at the image timestamp (`GetHeadsetPoseAtTime`) composed with `Intrinsics.LensOffset`, the sensor pose relative to the headset.
- The principal point is flipped to the bottom-left origin: `(ppx, SensorResolution.y − ppy)`.
- The current resolution is a centered crop of the sensor at its aspect ratio (`CalcSensorCropRegion`).
- `ViewportPointToRay(v)`:
  - Direction in camera space: `((crop.x + crop.w·v.x − ppx)/fx, (crop.y + crop.h·v.y − ppy)/fy, 1)`.
  - It is rotated by the camera pose. The ray starts at the camera position.
- `WorldToViewportPoint(P)`:
  1. `p = inverse(camRot)·(P − camPos)`
  2. `sensor = (p.x/p.z·fx + ppx, p.y/p.z·fy + ppy)`
  3. `viewport = (sensor − crop.min) / crop.size`
- Pitfalls found in the source:
  - `WorldToViewportPoint` never checks `p.z > 0`, so points behind the camera return mirrored values. Values outside [0,1] are off-image.
  - Pose, intrinsics and rays are valid only while `IsPlaying`. For async work, cache `GetCameraPose()` with the frame.
  - `GetTexture()` can be black for the first frames. `GetColors()` blocks on `AsyncGPUReadback.WaitForCompletion()`.
- The row order of read-back data compared with the viewport origin is UNVERIFIED and has to be checked on device.

### 3.4 Limitations
- The image shows only the physical world. Virtual content and UI are not in it (the Media Projection API captures those). The API never exposes other apps' pixels.
- It is a foreground API. The component stops the camera when the app pauses.
- Camera images are "Device User Data" under Meta's Developer Data Use Policy.
- Known v74 issue: RGB channels may be flipped after the first install on a newly flashed OS.

### 3.5 How Cursivis Spatial uses it (`Scripts/Capture/PassthroughCapture.cs`)
- Setup:
  - The scene creates the component disabled, with the Left camera at 1280×960 (`Editor/SceneBuilder.cs:83-88`).
  - It is enabled only while a snip is armed (`:69-72`, `:119-122`).
  - The permission is requested on the first Snip, together with the Scene permission used for depth (`:61-67`; `App/CursivisApp.cs:230-240`).
- Start waits for 3 fresh frames, with a 6 s timeout (`:75-118`).
- The camera's real field of view is outlined on the snip surface. The outline comes from casting rays through the four viewport corners onto the surface plane (`:125-139`).
- Crop pipeline (`:142-231`):
  1. The four world corners are projected with one cached pose. Every corner must be at least 5 cm in front of the camera.
  2. The rect gets 0.035 viewport units of padding, is clamped, and must cover at least 2% of the viewport.
  3. The GPU blits the crop and `AsyncGPUReadback` reads it back. The result is scaled to 1024 px or less and encoded as JPEG quality 82. Mostly black frames are rejected.

## 4. Interaction SDK

- Rig: `OVRCameraRig` plus `OVRComprehensiveInteractionRig` (prefab in `com.meta.xr.sdk.interaction.ovr/Runtime/Prefabs/`).
- Headless wiring:
  - The menu Quick Actions open a wizard window that needs a click.
  - ISDK 209 also ships a public editor API that is missing from the docs:
    - `Oculus.Interaction.Editor.QuickActions.QuickActionsAPI` (`AddRayCanvasInteraction`, `AddPokeCanvasInteraction`, `AddGrabInteraction`, `AddRayGrabInteraction`, and more).
    - `Oculus.Interaction.OVR.Editor.QuickActions.OVRQuickActionsAPI.AddOVRInteractionRig`.
  - `Editor/SceneBuilder.cs` builds the whole scene with these calls: the rig (`:60`), ray and poke on every canvas (`:165-183`), and ray grab plus hand grab on the result-card handle (`:117-142`).
- `PointableCanvasModule`:
  - Exactly one must exist per scene. On the EventSystem it must be the first module, or have `ExclusiveMode` on.
  - `PointableCanvas` turns ISDK pointer events into standard uGUI events (enter, exit, down, up, click, drag). The same `IPointer*Handler` code therefore serves hand-ray pinch, poke and controllers.
  - Drag events should drive `ScrollRect`, but no doc says so (UNVERIFIED).
- Grabbing a panel: `Grabbable` with a transformer, `RayInteractable` for far grab and `HandGrabInteractable` for near grab. Use a separate grab bar rather than making the whole canvas grabbable.
- Hand state: `OVRHand.GetFingerIsPinching`, `GetFingerPinchStrength`, `IsTracked`, `IsSystemGestureInProgress`. The system palm-pinch gesture is reserved.
- Design guidance:
  - Hit targets (hands-ui-best-practices): at least 48 dp, about 2.5-3° of visual angle, at least 12 mm spacing.
  - Text (styles_typography): at least 14 px, 18 px or more for comfortable reading.

## 5. Meta XR Simulator

| Topic | Facts |
|---|---|
| Install on this machine | Version 207.1 from the official MSI, signed by Meta Platforms, Inc. Installed by an unprivileged administrative extraction to `C:\Users\Admin\MetaXRSimulator`, plus a per-user (HKCU) `xrsim://` protocol registration. The Core SDK detects the simulator through that registry key. `tools/run-sim.ps1` starts the Windows player with `XR_RUNTIME_JSON`, `XR_SELECTED_RUNTIME_JSON` and `META_XRSIM_CONFIG_JSON` pointing into `...\v207.1\`, which overrides the OpenXR runtime for that process only. |
| Runs on Intel UHD 620? | **Pending.** The docs give no GPU or RAM minimum. 207.1 is the release that added AMD GPU support. Intel iGPUs are not mentioned. |
| 207.1 release notes | Adds grab and pinch from camera hand tracking. Fixes a memory leak that "could use up all system memory during Unity Play Mode". Fixes "the white screen in Passthrough Camera Access". Known issue: VRS recordings do not capture scene interactions. |
| PCA preview | The sources conflict. The migration doc says "MRUK v205 and later also support Passthrough Camera API preview in Meta XR Simulator", and MRUK 209 has an Editor/Windows code path for it. The PCA samples README and Meta's agentic PCA skill still say the simulator does not support PCA. On this machine: **pending**. The feed is probably a render of the synthetic room (inference); there is no documented way to inject a custom image. |
| Synthetic rooms | Hero rooms: game room, living room, bedroom. Feature rooms: Office, Trapezoidal, Corridor, Furniture-filled, L-shape, High-ceiling. Rooms supply passthrough imagery, scene data, anchors and depth. Depth works on Windows with the Quest 3/3S profiles. Changing the room drops connected apps. |
| Synthetic Environment Builder | Separate download, added to a separate Unity "server" project as `com.meta.xr.simulator.synthenvbuilder`. Label meshes with the Scene Annotation Tool (exactly one floor and one ceiling). Play mode or a built player serves gRPC on `localhost:33792`. This is the documented way to put custom posters, documents and objects in front of the virtual camera. |
| Hand simulation | Input per side: Controller, Hand or None. WASD to move, right mouse button to look. The pinch and gesture keys appear in the Input bindings panel; the docs do not list the defaults. Point & click makes the ray follow the mouse. An Xbox pad cannot trigger hand gestures. |
| Webcam hands (207 and later) | Up to 2 hands. 21 landmarks are retargeted to 26 OpenXR joints, with depth estimated from palm width and head rotation only. 207.1 adds pinch and grab. Depth is approximate, and overlapping hands confuse the tracker. |
| Meta VR CLI | Not installed on this machine. `metavr` 1.10.0 on npm (the `xrsim` group needs 1.7 or later): `xrsim runtime activate\|deactivate\|status\|logs --tail N`, `device list\|set --name`, `env list\|set --id`, `input point-and-click --active true`, `record start\|stop\|status`, `app quit`. Use `--format json` in scripts. No command injects hand poses. |
| Recording | `xrsim record` writes an MP4 of the composited output and needs a connected app. OBS window capture is the fallback. VRS "Record session" stores inputs for replay, not video, and auto-replay works only for standalone builds. Unity Recorder probably misses the composited passthrough (inference). |
| Limits compared with a Quest 3 | No OS image and no Android layer, so Android permission prompts, Camera2 and intents do not exist. Desktop GPU, so simulator FPS says nothing about Quest FPS. No thermals, no haptics, synthetic passthrough. Device-profile changes need a Play restart. Updating the OpenXR package needs an editor restart. Another active runtime (SteamVR, Link) blocks the simulator. |

## 6. Meta XR Operator

- Core SDK 209 ships "Meta XR Operator". It has two parts:
  - an OpenXR API layer, `XR_APILAYER_METAX_operator`;
  - `meta-xr-operator-mcp-proxy`, which serves MCP over HTTP at `http://127.0.0.1:8720/mcp` while an app runs.
- Its tools include `openxr_hand_gesture`, `openxr_set_head_pose`, `openxr_capture_composited_image` and `unity_find_interactables`.
- The layer is included only in Development builds.
  - `test-results/local/unity/build-win1.log` shows the Core SDK build processor disabling `XR_APILAYER_METAX_operator` for the Release build and re-enabling it afterwards.
  - `Editor/CursivisBuild.cs:69` makes a Development build when `CURSIVIS_DEVELOPMENT=1`.
- The Meta VR CLI has a matching command group, `metavr xroperator status|detect|enable|disable|call <TOOL>`. Its docs say it "connects the CLI to the Agentic XR layer running on a device". Its port variable `AGENTICXR_MCP_PORT` defaults to 8720, the same port as the MCP proxy.
- Use in this project: **planned, pending verification**.
  - The plan is to drive hand gestures and capture composited frames in the simulator for automated UI checks.
  - It has not been run. Whether the layer works with a simulator app is undocumented.

## 7. Competition rules that matter

Quotes from `research-competition.md` (Meta VR Start Developer Competition 2026).

| Rule | What the sources say |
|---|---|
| Deadline | Entry period ends "November 18, 2026 at 12:00:00 PM PT", which is 20:00 UTC or **01:30 IST on Nov 19**. "Projects that are modified after the submission deadline may be disqualified." |
| Simulator footage allowed | Video: "under three (3) minutes, showcasing footage of your Project as viewed on a Meta VR device, via XR Simulator, or another equivalent emulator". FAQ: "you are not required to own a Meta Quest headset to compete". |
| Judging device | "Judges will review apps on a Meta Quest 3 or Quest 3S." Builds "need to be playable on Meta Quest 3/3S". |
| Build access | "upload an APK to a new release channel named "Competition" in the Meta VR Developer Dashboard. Create and submit an Invite URL". FAQ: "APKs will not be considered." Turn on "Grant access to users by URL" with "Automatic approval: Users with this URL will be automatically added to the release channel up to the max limit." The default user limit is 200. |
| Video | "should be less than three (3) minutes". "Judges are not required to watch beyond three minutes". The video must be public on YouTube or Vimeo. Tip: "make your hands-first design clear in the first 30 seconds". "Don't lean on AI-generated video to carry the pitch." |
| Hands-first (mandatory) | "Fully usable with hands end-to-end. Controller support optional." Nov 10 update: "Test the full experience without controllers, including onboarding and menus." |
| Adapted division | "Ported experiences qualify for the Adapted/Significantly Updated Experience division of the competition, and should be submitted as such." Proof: "a summary (1-2 paragraphs) describing the new features or capabilities added since the app's original release". |
| Judging | Stage One is pass/fail viability. Stage Two uses four "equally weighted criteria" at 25% each: Innovation & Creativity, Experience Design, Technical Implementation, Polish & Presentation. Performance: "min 60 fps". |
| Brands and people | §4.19: "Projects must not contain any commercial or corporate advertising (including, without limitation, corporate logos, brand names, and slogans), recognizable branded products, or commercial artwork." §4.17: "Projects may not mention or depict any identifiable person other than Entrants." |
| Access and wrappers | Projects must be "made available free of charge and with sufficient access" for judges. No rule mentions API keys or logins; the notes infer that asking judges to enter a key would conflict with this rule. "Thin wrappers around existing platforms or services (YouTube, ChatGPT, etc.) are highly discouraged." |

## 8. Reference projects and lessons

| Project | Relevant lesson |
|---|---|
| Flows.xr (2025 winner, PCA with AI) | Make AI inputs tangible spatial objects. Ship presets so the first minute pays off. Keep content readable and grabbable in passthrough. |
| Language IRL (2025 winner, PCA with AI) | Started from Meta's PCA sample and validated one system at a time. Forgiving hand thresholds. A character with personality. Voice combined with gestures. |
| HandCraft XR (Judges' Choice) | One small, polished loop. Visual feedback on every hand action. Footage recorded on device. |
| MarketView VR (Best Android Utility) | Generous type, spacing and contrast took several iterations. They resisted feature creep. |
| Chores.gg (2025 winner, PCA with AI) | Dropped a feature because Gemini results were not consistent enough. Design for VLM inconsistency (confidence, fallbacks). |
| Unity-PassthroughCameraApiSamples | CameraToWorld scene for pixel ↔ world mapping. The sample predates MRUK 205, so bump it to 209 for simulator preview. |
| Unity-SpatialLingo | Meta's reference for camera, object recognition and LLM together. "Do not ship Quest apps with embedded API keys". Includes a face-blurring module. |

The 2025 winners' videos were not watched, so any statement about how those videos are structured is UNVERIFIED.

## 9. Gemini API (probed 2026-11-13)

### 9.1 Keys and models
- The key pool has 10 keys. 6 work.
  - 4 return HTTP 403 PERMISSION_DENIED "The Gemini API free tier is not available for this project".
  - The pool disables such keys automatically for 6 h: `backend/src/gemini/errors.js:35` classifies the error as `auth`, and `backend/src/gemini/keyPool.js:6` sets the 6 h.
- `generateContent` on `v1beta` works for these models. That settles two open points in `backend-audit.md`:
  - §0.7 and C4: `generateContent` with `responseJsonSchema` still works, so the Interactions API is not required.
  - C1: `gemini-3.5-flash` answers even though the models page does not list it.

| Model | Result on these keys (simple prompt) |
|---|---|
| gemini-3.6-flash | 200 OK, about 1.2-2.7 s, JSON-schema output works, `thinkingLevel` minimal and low supported |
| gemini-3.5-flash | 200 OK |
| gemini-3.5-flash-lite | 200 OK, about 0.9-1.4 s. `thinkingBudget: 0` is rejected with 400; `thinkingLevel` works |
| gemini-3.1-flash-lite | 200 OK, about 4.2 s |
| gemini-3.8-flash, gemini-3.7-flash, gemini-flash-latest | 503 "high demand" |
| gemini-4-hydrogen | 200 OK, but 16.8 s |
| gemini-omni-1.2-flash | 429 quota |
| Image generation models | Not usable on these keys |

### 9.2 Speech
- TTS:
  - `gemini-3.8-flash-lite-tts` returns `audio/wav` (24 kHz, mono, 16-bit) in about 3.8 s for one sentence.
  - `gemini-3.8-flash-tts` takes about 4.5 s.
  - `gemini-2.5-flash-preview-tts` and `gemini-3.1-flash-tts-preview` return raw `audio/L16` PCM at 24 kHz. The backend wraps it in a WAV header (`backend/src/cursivis/wav.js:23-32`).
- Audio understanding:
  - A 16-bit PCM WAV sent inline to `gemini-3.6-flash` or `gemini-3.5-flash-lite` is transcribed and answered in one `generateContent` call, in about 2.8 s.
  - `gemini-3.5-transcribe` does not support JSON mode.
- The Live API and ephemeral tokens were researched (`backend-audit.md` C2) but are not used.

### 9.3 Chosen model chains (`backend/src/config.js:25-36`, overridable with `CURSIVIS_MODELS_*`)
| Chain | Order |
|---|---|
| fast (suggestions) | gemini-3.5-flash-lite → gemini-3.6-flash → gemini-3.1-flash-lite → gemini-flash-lite-latest |
| quality (act, talk) | gemini-3.5-flash → gemini-3.6-flash → gemini-3.5-flash-lite → gemini-3.1-flash-lite |
| TTS | gemini-3.8-flash-lite-tts → gemini-3.8-flash-tts → gemini-2.5-flash-preview-tts |

`gemini-flash-lite-latest` was not in the probe set, so its availability is UNVERIFIED.

### 9.4 Live integration suite (`backend/tests/live/live.test.js`)
7/7 scenarios passed against real Gemini:
1. Food label → food-specific suggestions. Then the spoken question "Is this vegan?" (synthetic TTS speech sent as Talk audio) → a grounded "not vegan" answer citing whey and milk.
2. Nutrition-only crop → uncertainty "high", with no invented ingredients.
3. Printed French → Translate suggested → a correct translation → a follow-up.
4. Public-domain photo → caption → the regenerated caption differs.
5. Buggy JavaScript → a bug-finding suggestion → the off-by-one error is found.
6. Handwritten note → tasks.
7. General conversation, then "read your last answer out loud" → `clientAction: read_aloud`.

Per-call latency from `test-results/local/backend-live-metrics.json`:
- Measured end to end by the test client against the local backend, on a home internet connection from India, Nov 13 2026 (file time 12:57 UTC, Node v22.18.0).
- A model other than the first in its chain means the call fell back. The file does not record why.

| Call | Route | Model | ms |
|---|---|---|---|
| suggest:food | /v1/suggest | gemini-3.5-flash-lite | 3596 |
| speak:question | /v1/speak | gemini-3.8-flash-lite-tts | 3807 |
| talk:food-vegan-audio | /v1/talk | gemini-3.6-flash | 6618 |
| talk:food-followup | /v1/talk | gemini-3.6-flash | 3061 |
| talk:nutrition-only-vegan | /v1/talk | gemini-3.6-flash | 3847 |
| suggest:french | /v1/suggest | gemini-3.5-flash-lite | 3395 |
| act:translate | /v1/act | gemini-3.6-flash | 5725 |
| talk:french-followup | /v1/talk | gemini-3.5-flash | 4850 |
| suggest:photo | /v1/suggest | gemini-3.5-flash-lite | 2367 |
| act:caption | /v1/act | gemini-3.5-flash | 3355 |
| act:caption-regenerate | /v1/act | gemini-3.5-flash | 3100 |
| suggest:code | /v1/suggest | gemini-3.5-flash-lite | 1668 |
| act:debug | /v1/act | gemini-3.5-flash | 2296 |
| suggest:note | /v1/suggest | gemini-3.5-flash-lite | 2682 |
| act:tasks | /v1/act | gemini-3.5-flash | 3434 |
| talk:general | /v1/talk | gemini-3.5-flash | 2140 |
| talk:general-read | /v1/talk | gemini-3.5-flash-lite | 3593 |

Summary by route:

| Route | Calls | Range | Median |
|---|---|---|---|
| /v1/suggest | 5 | 1.7-3.6 s | 2.7 s |
| /v1/act | 5 | 2.3-5.7 s | 3.4 s |
| /v1/talk | 6 | 2.1-6.6 s | 3.7 s |
| /v1/speak | 1 | 3.8 s | 3.8 s |

## 10. Design decisions inspired by this research

| # | Decision | Reason | Code |
|---|---|---|---|
| 1 | Pin Meta SDK 209.0.0 with Unity OpenXR 1.19.0. Do not install a simulator UPM package. | MRUK 205 or later is needed for simulator PCA preview. The Oculus XR Plugin stops at SDK v73. The simulator needs OpenXR, and Meta says to remove `com.meta.xr.simulator`. | `Packages/manifest.json` |
| 2 | Configure the project and build the scene from code: `ProjectSetup`, `SceneBuilder` (QuickActionsAPI), `CursivisBuild` | The Quick Action menu items open wizards and are not headless. A repeatable setup removes hidden editor state. | `Editor/*.cs` |
| 3 | Use only world-space canvases at 1 unit = 1 mm, with ISDK ray and poke on each canvas and one button class driven by uGUI pointer events | Hands-first is mandatory. PointableCanvasModule turns ray pinch, poke and controller input into the same uGUI events. | `SceneBuilder.cs:165-183`, `UI/UiKit.cs:126-269` |
| 4 | Offer two selection sources: a built-in workspace of 4 original, unbranded panels, and a room Snip through PCA | Gives a payoff in the first minute without any permission. PCA cannot be verified without hardware. The rules ban logos and identifiable people in sample content. | `App/AppPanels.cs:15-139` |
| 5 | The camera is opt-in and off by default. It is requested at the first Snip and stopped after capture and on pause. | Minimum permissions (VRC.Quest.Security.2). The component does not request permission. Privacy. About 1-2% GPU and 45 MB per camera. | `PassthroughCapture.cs:49-72`, `CursivisApp.cs:222-270` |
| 6 | Project the user's rectangle with `WorldToViewportPoint` and one cached pose. Check that every corner is in front, add padding, and show the camera's real frame. | The camera FOV is smaller than the view. The API has no behind-camera check. Eye and camera positions differ (parallax). | `PassthroughCapture.cs:125-221` |
| 7 | Place the snip surface at the Depth API hit distance (0.35-2.5 m, 0.85 m default) and switch depth off right after | The drawn rectangle then lies on the real surface, so the corner projection is correct. Depth costs GPU. Depth raycasts in the simulator are UNVERIFIED. | `CursivisApp.cs:295-342` |
| 8 | Crop on the GPU, read back asynchronously, send a JPEG of at most 1024 px. Wait for 3 fresh frames and reject black frames. | `GetColors()` blocks. First frames can be black. `backend-audit.md` recommends about 1024 px JPEGs for serverless body limits. | `PassthroughCapture.cs:75-231` |
| 9 | Gemini runs only behind the backend, with an app token, per-install and per-IP rate limits, a daily cap and request validation. No key on the device. | Keys in an APK can be extracted (Spatial Lingo). Judges get free access without entering keys. | `backend/src/security.js`, `backend/src/cursivis/schemas.js:9-119` |
| 10 | Make one structured call for 4 suggestions and one for each action. Talk is always offered by the client. | The original made at least 4 Gemini calls per action, which hurts latency and free-tier quota (`backend-audit.md` §0.5). | `backend/src/cursivis/services.js:105-206` |
| 11 | Talk is one call with inline WAV that returns transcript, answer and `clientAction` | One `generateContent` call took about 2.8 s in the probe. No Live session or ephemeral token is needed. The transcribe model has no JSON mode. | `services.js:210-252`, `Voice/Voice.cs:90-245` |
| 12 | Read aloud with Gemini TTS through the backend, chunked so speech starts early | Voice SDK TTS is a soft-deprecated Wit.ai cloud service. Android `TextToSpeech` on Horizon OS is UNVERIFIED. The TTS probe returned WAV in about 3.8 s. | `services.js:255-269`, `Voice.cs:246-352` |
| 13 | Model chains with health cooldowns, a compat retry and hedged second attempts | Three models returned 503 "high demand". 4 of 10 keys return 403. Latency varies by model (0.9-16.8 s). | `backend/src/gemini/client.js`, `keyPool.js` |
| 14 | Grounding rules plus `evidence` and `uncertainty` fields shown on the card | VLM answers are inconsistent (Chores.gg). Food and allergen answers must not invent details. | `backend/src/cursivis/prompts.js:6-11`, `UI/ResultCard.cs:310-331` |
| 15 | Hit targets of 42-50 mm and text at 1.4× the desktop scale | Meta guidance: targets of about 2.5-3°, text of at least 14 px. Some gaps are still below 12 mm (see `UI_DESIGN_SPEC.md` §12.1). | `UI/CursivisTheme.cs:40-51` |
| 16 | Seated layout: orb 0.58 m, result card 0.78 m, chip at most 0.9 m, workspace 1.0 m, with lazy follow | The "airplane seat test" (2 ft radius) and the FoV guidance in the judging criteria | `CursivisApp.cs:859-961` |
| 17 | On pause, stop camera, microphone and speech and cancel requests. When hands are lost for 1.2 s, cancel the snip or send the held Talk. | VRC.Quest.Functional.2 (pause) and VRC.Quest.Input.6 (tracking loss). Hiding hand visuals is left to the ISDK rig (UNVERIFIED). | `CursivisApp.cs:963-999`, `:1021-1035` |
| 18 | URP with MSAA 4×, no HDR, no shadows, no post-processing, a transparent clear and a passthrough underlay | 72 FPS VRC and the 60 fps judging bar. Passthrough is composited underneath the app. | `ProjectSetup.cs:132-166`, `SceneBuilder.cs:60-81` |
| 19 | Fonts: Inter and IBM Plex Mono (OFL) plus Material Icons (Apache-2.0) | The original's Segoe and Bahnschrift are Windows system fonts and may not be licensed for bundling (`ui-audit.md` §4). | `Assets/Cursivis/Fonts/LICENSE-*` |
| 20 | Label simulator footage as simulator footage, and do not fake camera-selection shots | The rules forbid deceptive material and judges ask for honest gameplay. PCA in the simulator is pending. | Process |
