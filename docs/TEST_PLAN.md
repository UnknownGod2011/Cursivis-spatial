# Test plan and results

Legend: **PASS** (verified, evidence linked), **FAIL**, **PENDING** (not run yet), **UNVERIFIED (device)** (needs a Quest 3/3S).
All AI results below are real Gemini responses through the real backend; no mocked AI is used to claim AI behaviour.

## 1. Automated tests

| Layer | Command | What it covers | Result |
|---|---|---|---|
| Backend unit | `cd backend && npm test` | key pool rotation/cooldowns, error classification, model fallback, compat retry, hedging + no slot leaks, schema-invalid fallback, safety stop, timeouts, auth/limits/size limits, suggestion normalization, WAV helpers, endpoint contracts (fake Gemini transport) | **PASS** 21/21 (2026-11-13) |
| Backend live | `cd backend && npm run test:live` | 7 real scenarios against Gemini with fixture selections (see §2) | **PASS** 7/7 (2026-11-13), metrics in `test-results/local/backend-live-metrics.json` |
| Deployed backend smoke | health + 401 without token + real `/v1/suggest` | Vercel deployment, token enforcement, keys present | **PASS**: suggestions in 1.9 s via gemini-3.5-flash-lite (2026-11-13) |
| Unity compile | `tools/unity-batch.ps1 -Method ...SceneBuilder.BuildBatch` | runtime + editor assemblies compile against SDK 209 | **PASS** |
| Windows player build | `...CursivisBuild.BuildWindows` | full player build (Vulkan, D3D11, D3D12) | **PASS** (300 MB, 18 min) |
| Quest APK build | `...CursivisBuild.BuildAndroid` | IL2CPP ARM64 APK | PENDING |
| Android manifest check | `aapt dump badging` / manifest diff | package, permissions (`HEADSET_CAMERA`, `RECORD_AUDIO`, `INTERNET`), `com.oculus.supportedDevices=quest3|quest3s`, VR intent category, hand tracking | PENDING |
| Simulator E2E (autopilot) | `tools/run-sim.ps1 -Autopilot all` | selection → suggestions → action → result → scroll → read aloud → follow-up → theme; photo caption + regenerate; general talk; camera snip | PENDING |

## 2. Functional scenarios

| # | Scenario | Backend + real AI | Simulator | Quest 3/3S |
|---|---|---|---|---|
| 1 | Food packaging: suggestions, Talk "Is this vegan?", grounded verdict, read aloud, follow-up; cropped label → qualified answer | **PASS** (fixture image; spoken question synthesized with TTS) | PENDING | UNVERIFIED (device) |
| 2 | Printed text (French): Translate suggested, translation, follow-up | **PASS** | PENDING | UNVERIFIED (device) |
| 3 | Workspace photo: caption suggested, caption, regenerate differs | **PASS** | PENDING | UNVERIFIED (device) |
| 4 | General orb conversation + "read your last answer out loud" (clientAction) | **PASS** | PENDING | UNVERIFIED (device) |
| 5 | Hands-only flow: select, trigger, choose, scroll, move, dismiss | n/a | PENDING (simulated hands / Operator) | UNVERIFIED (device) |
| 6 | Error handling (below) | partly (unit tests) | PENDING | UNVERIFIED (device) |

### Scenario 6 checklist

| Case | Expected | Where verified |
|---|---|---|
| No internet | "Offline" orb + toast; card error with Retry; no crash | PENDING (simulator, backend stopped) |
| API timeout | `ai_timeout` 504 → card error + Retry | unit test (deadline) **PASS**; client PENDING |
| Invalid/expired credentials | key disabled, next key used; all bad → `ai_unavailable` 503 | unit tests **PASS** |
| Camera permission denied | toast, workspace/Talk keep working | UNVERIFIED (device; no Android permissions in simulator) |
| Microphone permission denied | toast, suggestions still work | UNVERIFIED (device) |
| Empty selection | rejected locally / 400 `selection text is empty` | unit test **PASS** |
| Hand tracking lost | drag cancelled, held Talk sent | UNVERIFIED (device); simulator PENDING |
| Large image | ≤1024 px JPEG on device; backend rejects > 2 MB (413/400) | unit test **PASS** |
| Long AI result | scrolls; reveal; read aloud chunks | PENDING (simulator) |
| New selection during request | old request cancelled, stale response dropped (`stale_response_dropped`) | PENDING (simulator) |
| Pause/resume, fresh restart | camera/mic/speech stop; "Paused … Retry" card | UNVERIFIED (device) |

## 3. Performance measurements (measured only)

| Metric | Value | Conditions |
|---|---|---|
| Suggestions (backend, live) | see `backend-live-metrics.json` | local backend, home internet (India), 2026-11-13 |
| Suggestions (deployed) | 1.9 s | Vercel production, one text selection |
| Action latency, Talk latency, TTS first audio | see `backend-live-metrics.json` | as above |
| Frame rate, memory, capture time, gesture→feedback | PENDING (simulator numbers are desktop numbers, not Quest numbers) | — |

Quest frame rate and thermal behaviour cannot be measured without a headset: **UNVERIFIED (device)**.
