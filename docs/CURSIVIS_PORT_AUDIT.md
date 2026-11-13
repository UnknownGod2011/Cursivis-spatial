# Cursivis → Cursivis Spatial port audit

What came from the original Windows app "Cursivis", what changed, and what was left behind.

- Snapshot: 2026-11-13. Nothing here was tested on a Quest headset.
- Original source: read-only clone at `C:\Users\Admin\cursivis-ref\MX--cursivis`. Line numbers are from that clone as of this date.
  - `C/` = `desktop/cursivis-companion/src/Cursivis.Companion/`
  - `B/` = `backend/gemini-agent/src/`
- New source paths are relative to this repository.
  - `U/` = `unity/CursivisSpatial/Assets/Cursivis/`
  - `S/` = `backend/src/`
- Background notes: `C:\Users\Admin\cursivis-ref\notes\ui-audit.md` and `backend-audit.md`.

## 1. Original architecture

| Component | Files | Role |
|---|---|---|
| Logitech Options+ plugin | `plugin/logitech-plugin/src/CursivisPlugin/src/Actions/` (`CursivisTriggerCommand.cs`, `CursivisLongPressCommand.cs`, `CursivisDialAdjustment.cs`, `CursivisSnipItCommand.cs`, `CursivisTakeActionCommand.cs`, `CursivisPromptOptimizerCommand.cs`, `CursivisLiveModeCommand.cs`), `Helpers/TriggerIpcClient.cs`, `Helpers/CompanionHapticClient.cs` | Maps MX buttons, the dial and the Actions Ring to trigger events. Plays haptics. |
| Trigger IPC | `C/Services/TriggerIpcServer.cs:17` (`http://127.0.0.1:48711/cursivis-trigger/`), `shared/ipc-protocol/schema/trigger-event.schema.json` | Local HTTP listener for trigger events. |
| Hotkey host, trigger launcher | `desktop/cursivis-hotkey-host/`, `desktop/cursivis-trigger-launcher/` | Keyboard triggers for users without Logitech hardware. |
| WPF companion | `C/App.xaml.cs` (startup, theme at `:69`), `C/Controllers/TriggerController.cs` (2,799 lines, orchestration), `C/Views/*.xaml`, `C/Services/*.cs`, `C/Themes/PremiumTheme.xaml`, `C/Infrastructure/UiPresentation.cs` | Selection capture, orb, menus, result panel, voice. |
| Live Mode | `C/LiveMode/*.cs`, e.g. `GeminiLiveConversationService.cs`, `LiveModeAutomationService.cs`, `AppResolverService.cs` | Voice agent with desktop tools. Connects straight to the Gemini Live WebSocket with the raw key (`GeminiLiveConversationService.cs:87-90`). |
| Backend sidecar | `B/server.js:10` (binds `127.0.0.1:51880`), `B/app.js`, `B/contentClassifier.js`, `B/geminiService.js`, `B/apiKeyPool.js`, `B/geminiCredentials.js`, `B/liveGateway.js` (`WS /live`), `B/aiProviderFactory.js` (Gemini, Ollama, OpenAI, hosted) | Intent routing, generation, transcription. No auth, CORS or rate limits (`backend-audit.md` §0). |
| Contracts | `shared/ipc-protocol/schema/agent-request.schema.json`, `agent-response.schema.json`, `C/Models/AgentContracts.cs`, `C/Models/SuggestionResponse.cs` | AgentRequest and AgentResponse (protocol 1.0.0). |
| Browser automation ("Take Action") | `desktop/browser-extension-chromium/`, `desktop/browser-native-host/`, `desktop/browser-action-agent/`, `C/Services/ActiveBrowserAutomationService.cs`, `C/Services/ExtensionAutomationClient.cs`, `C/Views/ActionPlanPreviewWindow.xaml`, `B/browserActionPlanner.js`, `B/browserTaskPacks.js` | Plans and runs clicks and form fills in the user's browser. |

A Guided-mode tap ran through these steps:
1. The plugin or hotkey posts to `TriggerIpcServer`, which calls `TriggerController.HandleTapAsync` (`TriggerController.cs:589-682`). One run is allowed at a time.
2. `SelectionDetector.CaptureSelectionAsync` (`C/Services/SelectionDetector.cs:16`) copies the selection through the clipboard and retries at 260 and 520 ms. With no text and no image, it falls back to the lasso (`TriggerController.cs:775`).
3. `POST /suggest-actions` (`B/app.js:1216-1302`) makes a router call and an option-generator call.
4. `ResolveActionDecisionAsync` (`TriggerController.cs:1076-1122`) decides the next step. Guided mode shows the orb menu. Smart mode sends `bestAction` directly.
5. `POST /analyze` (`B/app.js:896-1211`) routes the request again, builds the per-action prompt, generates, sanitizes the text and may retry once.
6. `CompleteSuccessfulRunAsync` (`TriggerController.cs:839-866`) copies the result to the clipboard, may replace the selection in the source app (with Undo), and opens the result panel.

## 2. The 16 items in the brief

### 2.1 Orb
- **Original:**
  - `C/Views/OrbOverlayWindow.xaml`: a 320×320 window. Parts: OrbCore (118 px, `:234-253`), inner surface (`:275-286`), state badge, status and brand (`:288-337`), compact idle panel (`:339-418`), stop button (`:255-273`).
  - `C/Views/OrbOverlayWindow.xaml.cs`: `SetState` (`:194-232`), `ApplyPalette` (`:722-763`), `CreateOrbBrush` (`:857-872`), `ApplyThemeChrome` (`:927-954`).
  - `C/Models/OrbState.cs:3-9` defines 4 states.
- **New:**
  - `U/Scripts/UI/OrbView.cs` has 9 moods (`SetMood` `:74-94`).
  - `U/Resources/Shaders/CursivisOrb.shader` draws the whole orb on one quad.
  - `U/Scripts/UI/OrbHud.cs:47-79` sets up the orb, brand text, burst rings, status label and hit target.

### 2.2 Orb animations
- **Original** (`OrbOverlayWindow.xaml.cs` unless noted):
  - pulse (`:458-476`: 620 ms, +0.07)
  - base scale (`:703-720`: 220 ms)
  - 4-ring completion burst (`:785-839`)
  - invisible glow and orbit layers (`:478-520`, `:765-783`)
  - shiny status text (`C/Infrastructure/UiPresentation.cs:10-57`)
- **New:** `OrbView.cs` `Update` (`:105-144`, scale, pulse and shader blends), `UpdateBurst` (`:146-164`), `UpdateShine` (`:166-206`). The shader adds a thinking arc, a voice ring, a selection glow and an error tint.

### 2.3 Guided Mode
- **Original:** `TriggerController.cs`:
  - `ResolveActionDecisionAsync` (`:1076-1122`), `BuildActionMenuOptionsAsync` (`:1124-1228`), `BuildInitialOrbMenuOptions` (`:1230-1255`)
  - deferred options after a fixed 5 s (`:1288-1313`, delay at `:1297`), `ShowActionMenuAsync` (`:1315-1366`)
  - navigation: dial (`:297`, `:337`), `NavigateGuidedMenu` (`:2536`), 2 s auto-commit (`:99-103`)
  - constants `"AI: "`, `"Custom Voice Command"` and `"__deferred__"` (`:14-16`)
- **New:** There is no Smart mode. Every trigger shows suggestions:
  - `U/Scripts/App/CursivisApp.cs`: `TriggerAsync` (`:510-525`), `LoadSuggestionsAsync` (`:527-555`), `RunSuggestionAsync` (`:575-584`).
  - `OrbHud.SetLoading` / `SetSuggestions` (`OrbHud.cs:150-189`).

### 2.4 Suggestions UI
- **Original:**
  - `OrbOverlayWindow.xaml:15-108`: 5 chip slots of 94×32 px in a fixed "crown".
  - `OrbOverlayWindow.xaml.cs`: `UpdateActionRing` (`:254-287`), `CreateChipBrush` (`:899-910`), `CompactLabel` (`:581-603`), show/hide (`:605-628`), click (`:659-670`).
  - Labels built on the client: `TriggerController.cs` `NormalizeActionHint` (`:2355`) and `ToDisplayAction` (`:2413-2442`).
- **New:**
  - `OrbHud.cs:38-42` (positions, chip size) and `:81-99` (title chip, 4 chips, Talk chip, hint).
  - `Chip` and `Talk` button styles in `U/Scripts/UI/UiKit.cs:188-226`.
  - The model returns label and icon. The backend normalizes the label (`S/cursivis/actions.js:69-83`) and the client maps the icon (`U/Scripts/UI/CursivisTheme.cs:184-214`).

### 2.5 Result box
- **Original:**
  - `C/Views/ResultPanelWindow.xaml`: 612×312 window; RootCard (`:15-30`), header (`:37-100`), body (`:102-149`).
  - `C/Views/ResultPanelWindow.xaml.cs`: Markdown into a FlowDocument (`:473-630`), button events (`:156-298`), drag and resize (`:1077-1128`).
- **New:** `U/Scripts/UI/ResultCard.cs`: layout (`Build` `:41-183`), `ShowResult` (`:266-306`). Markdown conversion in `U/Scripts/UI/MarkdownTmp.cs:47-125`.

### 2.6 Light/dark theme
- **Original:**
  - `C/Services/CompanionThemeService.cs`: Dark is the default (`:9`), `Apply` (`:13-82`), palettes (`:172-223` dark, `:225-276` light).
  - `C/Models/CompanionThemeMode.cs:3-7`.
  - Toggles: `App.xaml.cs:114` and `:604`, `MainWindow.xaml.cs:1868`.
- **New:**
  - `CursivisTheme.cs`: palettes (`:61-82` dark, `:84-105` light), `Load`/`SetDark`/`Toggle` (`:112-126`). The choice persists in PlayerPrefs `cursivis.theme.dark`.
  - Toggles: dock Theme button (`OrbHud.cs:108`), card header Theme button (`ResultCard.cs:60`), voice `toggle_theme` (`CursivisApp.cs:790-800`).

### 2.7 Translucency
- **Original:** Translucency comes from brush alpha only. Example: ResultPanelShell is dark `#D021262D` and light `#D0FFFFFF` (`CompanionThemeService.cs:197`, `:250`). There is no blur, acrylic or Mica anywhere (`ui-audit.md` §0).
- **New:** Alpha lives in the palette tokens. Shells are α 0.90-0.92. `U/Resources/Shaders/CursivisRoundedRect.shader:33` uses a separate alpha blend so the passthrough compositor receives coverage. There is no blur.

### 2.8 Shadows, gradients, borders
- **Original:**
  - DropShadowEffect on the result panel (`ResultPanelWindow.xaml:15-30`: 24 / `#66000000` / 0.24), the orb (`OrbOverlayWindow.xaml:246-249`) and the buttons (`C/Themes/PremiumTheme.xaml:297-354`).
  - Radial orb brushes (`OrbOverlayWindow.xaml.cs:857-872`) and diagonal chip brushes (`:899-910`).
  - 1 px borders.
- **New:**
  - `U/Scripts/UI/RoundedRect.cs` with `CursivisRoundedRect.shader`: SDF fill, a 2-stop vertical gradient, a border with a top rim highlight, an analytic outer shadow and dithering.
  - The orb's gradient, rim and shadow are in `CursivisOrb.shader`.

### 2.9 Popup animations
- **Original:**
  - `UiPresentation.AnimateEntrance` (`UiPresentation.cs:75-99`).
  - Result panel opens in 280 ms with an 18 px rise (`ResultPanelWindow.xaml.cs:351-383`) and closes in 135 ms (`:201-251`).
  - Chips fade in 180 ms and out 220 ms (`OrbOverlayWindow.xaml.cs:605-628`).
- **New:**
  - `PanelAnimator` (`UiKit.cs:271-339`) for the result card, selection chip and onboarding.
  - Ring fade and staggered chip reveal (`OrbHud.cs:208-239`).
  - Toast fade (`U/Scripts/App/AppPanels.cs:223-228`).

### 2.10 Branding and header
- **Original:**
  - Icon `C/Assets/cursivis-icon.png`: navy `#121C2D` background, cyan `#49E3FF` ring, magenta `#FF5BD6` arc, white "c".
  - Header title "Cursivis" at 17 px with a silver shine (`ResultPanelWindow.xaml:48-52`, `.cs:798-813`). Header buttons at `ResultPanelWindow.xaml:54-98`.
  - Orb wordmark "CURSIVIS" in Bahnschrift 9 (`OrbOverlayWindow.xaml:329-336`).
- **New:**
  - Card wordmark with shine (`ResultCard.cs:49-55`, `UpdateWordmarkShine` `:404-437`).
  - Orb brand text (`OrbHud.cs:51-52`).
  - Brand colors (`CursivisTheme.cs:29-31`, orb arc defaults `CursivisOrb.shader:20-21`).
  - Onboarding title and tagline (`AppPanels.cs:151-152`, `U/Scripts/Core/CursivisVersion.cs:6`).
  - `ProjectSetup.cs` sets no launcher icon.

### 2.11 Voice and Talk
- **Original:**
  - A long press runs `HandleLongPressAsync` (`TriggerController.cs:205-231`). It records voice through `PromptVoiceCommandFromOrbAsync` (`:240-267`) or opens the text bar (`:269-295`), then reruns the current selection with the command.
  - Capture and transcription:
    - `C/Services/VoiceCommandPromptService.cs`: limits (`:31-35`), streaming to `/live` (`:85-161`), `/transcribe` fallback (`:241`, `:359-372`), typed fallback (`:277`).
    - `C/Services/VoiceCaptureService.cs`: WASAPI (`:447-459`), WaveIn (`:484-501`), VAD (`:57-58`, `:351-356`).
    - Backend: `B/app.js` `/transcribe` (`:1304-1392`) and `B/liveGateway.js`.
  - The orb shows a stop button while listening (`OrbOverlayWindow.xaml:255-273`).
- **New:**
  - Capture and playback in `U/Scripts/Voice/Voice.cs`: `VoiceCapture` (`:90-245`: push-to-talk, 16 kHz WAV, VAD, mute detection) and `SpeechPlayer` (`:246-352`: read-aloud).
  - Talk flow in `CursivisApp.cs:633-788`: holding 0.45 s or longer is push-to-talk, and a quick tap listens hands-free until 1.2 s of silence.
  - Backend: `/v1/talk` (`S/app.js:110-125`, `S/cursivis/services.js:210-252`) and `/v1/speak` (`S/app.js:127-138`, `services.js:255-269`).

### 2.12 Image-region selection (Snip-it, lasso)
- **Original:**
  - The orb command "Snip-it" (`OrbOverlayWindow.xaml.cs:20`) goes to `TriggerController.cs:185-186` and then `HandleImageSelectionAsync` (`:442`).
  - Lasso fallback: `HandleImageFallbackAsync` (`:775-837`, hint at `:801`).
  - `C/Views/LassoOverlayWindow.xaml:1-30`: a full-screen rectangle, `#4FC3F7`, with the hint "Drag to select an area. Press Esc to cancel." `LassoOverlayWindow.xaml.cs` cancels anything under 8×8 px (`:72-83`) and on Esc (`:143-158`).
  - `C/Services/LassoSelectionService.cs:8` and `C/Services/ScreenCaptureService.cs:34`.
- **New** (all in `U/Scripts/`):
  - Room snip: `SnipSurface` (`Selection/SelectionInput.cs:32-188`), `Capture/PassthroughCapture.cs`, `CursivisApp.cs` `BeginSnipAsync` (`:222-270`) and `OnSnipCompletedAsync` (`:344-375`).
  - Workspace: `WorkspaceImage` region drag (`SelectionInput.cs:335-436`) with `WorkspaceCapture.CropTexture` (`PassthroughCapture.cs:234-262`), and `WorkspaceText` word ranges (`SelectionInput.cs:194-329`).

### 2.13 Suggestion routing
- **Original:**
  - `B/app.js`: `/suggest-actions` (`:1216-1302`), `toSuggestionResponse` (`:407`).
  - `B/geminiService.js`: `buildIntentRouterRequest` (`:381-480`), `createGeminiIntentRouter` (`:508`), `createGeminiOptionGenerator` (`:633`).
  - `B/contentClassifier.js`: `ACTION_MAP` (`:51`), `ALTERNATIVES_BY_TYPE` (`:147`), `DEFAULT_ACTION_BY_TYPE` (`:173`), `META_TOOL_ACTION_PATTERN` (`:186`), `buildIntentRouterPrompt` (`:714`), `normalizeIntentDecision` (`:814`).
- **New:**
  - `S/cursivis/services.js` `suggest` (`:105-165`).
  - `S/cursivis/prompts.js` `SUGGEST_SYSTEM` (`:21-35`).
  - `S/cursivis/schemas.js` `SUGGEST_OUTPUT_SCHEMA` (`:123-148`).
  - `S/cursivis/actions.js`: `canonicalActionId` (`:59-62`), `isUnsupportedAction` (`:64-67`), `FALLBACKS` (`:87-106`), `normalizeSuggestions` (`:126-160`).

### 2.14 Result generation
- **Original:**
  - `B/app.js` `analyzeHandler` (`:896-1211`), with `resolveAction` (`:372-405`), `buildTextImagePrompt` (`:151-182`), `buildImagePrompt` (`:184`), `sanitizeResultText` (`:1509-1555`) and retries for weak answers.
  - `B/contentClassifier.js` `buildPrompt` (`:869` to the end of the file).
  - `B/geminiService.js`: `CONTEXTUAL_EXECUTION_SYSTEM_INSTRUCTION` (`:29-42`), `createGeminiTextGenerator` (`:100`), Google Search grounding (`:135`), `generateWithFallbackModels` (`:775-880`).
  - `TriggerController.cs` `CompleteSuccessfulRunAsync` (`:839-866`).
- **New:**
  - Backend: `services.js` `act` (`:168-206`) uses `EXECUTE_SYSTEM` (`prompts.js:37-44`), `ACTION_GUIDANCE` (`:69-108`) and `guidanceFor` (`:110-117`). The output follows `ACT_OUTPUT_SCHEMA` (`schemas.js:150-163`) and is shaped by `shapeResult` and `cleanAnswer` (`services.js:77-97`).
  - Client: `CursivisApp.cs` `RunActionAsync` (`:586-624`).

### 2.15 Follow-ups and context
- **Original:**
  - More Options (`TriggerController.cs:2312-2350`) calls `ReRunLastSelectionAsync` (`:2126`) on the stored selection.
  - The text bar ("Type a refinement or follow-up...", `C/Views/TextCommandBarWindow.xaml:88`).
  - Talk applies to the current selection.
  - There is no conversation memory: every `/analyze` call is stateless (`backend-audit.md` B3).
- **New:**
  - `ConversationSession` keeps one history per selection id, at most 8 turns (`U/Scripts/Core/CursivisTypes.cs:82-127`). The history is sent with each request (`U/Scripts/AI/CursivisApi.cs:120-129`). The client keeps up to 4,000 chars per turn, the server accepts at most 8 turns of 6,000 chars (`schemas.js:42-54`), and the prompt uses the first 1,500 chars of each turn (`prompts.js:140-146`).
  - The card shows follow-up chips from `followUps` (`ResultCard.cs:288-299`) and an "Ask follow-up" button (Talk with the selection as context).
  - More options (`CursivisApp.cs:557-584`), Redo (`:626-630`).
  - General mode receives a `recentSelectionSummary` (`CursivisTypes.cs:108-114`).

### 2.16 Error, loading and empty states
- **Original:**
  - Loading shows only on the orb, as the Processing pulse with shiny text.
  - Errors reuse the Completed state and the same burst: "Request timed out" (`TriggerController.cs:668`), "Failed to process request" (`:675`).
  - Messages appear through `ResultPanelWindow.ShowInfo` (`.xaml.cs:188-199`). Formatting: `GeminiClient.FormatBackendError` (`C/Services/GeminiClient.cs:997-1025`) and `sendClassifiedProviderError` (`B/app.js:61-118`).
  - Empty result: "No useful response returned." (`B/app.js:1511`).
  - The legacy list menu has a "Gemini is thinking..." progress bar (`C/Views/ActionMenuWindow.xaml:96-102`).
- **New:**
  - Card loading skeleton (`ResultCard.cs:134-142`, `ShowLoading` `:233-247`) and error view with Retry (`:144-150`, `ShowError` `:249-264`).
  - Guided ring: "..." placeholders (`OrbHud.cs:150-164`) and an error title (`:191-197`).
  - A distinct `OrbMood.Error`.
  - Toasts (`AppPanels.cs:191-229`), an offline health loop (`CursivisApp.cs:1001-1019`) and "I didn't catch that" for silent audio (`:758`).
  - Error text: `toPublicError` on the backend (`S/gemini/errors.js:64-85`) and `UserMessage` on the client (`CursivisApi.cs:72-80`).
  - Fallback suggestions are labelled "basic options".

## 3. Reused

Same idea and the same data, re-implemented in the new stack.

| What | Original | Cursivis Spatial | What stayed the same |
|---|---|---|---|
| Key pool design | `B/apiKeyPool.js` (`withGoogleGenAiClient` `:27-77`, `markEntryFailure` `:79-89`) | `S/gemini/keyPool.js` (`acquire` `:36-46`, `release` `:48-73`) | Least in-flight first, then least recently used. Per-key cooldowns. Auth failures disable the key. Quota cooldowns honor `retryDelay`. |
| Error classification | `B/apiKeyPool.js` `classifyGeminiError` (`:126-150`) | `S/gemini/errors.js` `classify` (`:27-44`) | Same categories (auth, quota, model, malformed/invalid, transient). Auth and quota rotate keys; model errors move down the chain. |
| Action alias map | `B/contentClassifier.js` `ACTION_MAP` (`:51`) | `S/cursivis/actions.js` `ALIASES` (`:18-41`) | Equivalent ids collapse to one canonical id (summarise→summarize, reply_email→draft_reply, …). A subset was carried over. |
| Unsupported-action filter | `META_TOOL_ACTION_PATTERN` (`B/contentClassifier.js:186`) | `UNSUPPORTED_ACTION_PATTERN` (`actions.js:44-45`), `isUnsupportedAction` (`:64-67`) | Search, browse, click, fill, submit, send, schedule and take_action are blocked. The original replaced them with a default action; the new code drops them from the suggestions. |
| Per-action prompt templates | `buildPrompt` (`B/contentClassifier.js:869`), `buildImagePrompt` (`B/app.js:184`) | `ACTION_GUIDANCE` (`S/cursivis/prompts.js:69-108`) | Condensed wording of the original templates (summarize, extract_insights, bullet_points, rewrite, translate, explain, answer_question, draft_reply, polish_email, the code and image actions). The generic fallback "Perform this action on the selection: …" mirrors the original "Perform this operation on the text: …" (`contentClassifier.js:1112`). |
| Grounding rules lineage | `CONTEXTUAL_EXECUTION_SYSTEM_INSTRUCTION` (`B/geminiService.js:29-42`) | `EXECUTE_SYSTEM` (`prompts.js:37-44`) | Kept: "Selection is the context…", "Honor the chosen action, but execute it intelligently rather than mechanically", no internal reasoning. The `answer_question` date qualifier ("avoid fake certainty") is kept in `prompts.js:79`. The anti-fabrication rules in `GROUNDING_RULES` (`prompts.js:6-11`) are new. |
| Talk = selection + spoken instruction | `HandleLongPressAsync` → `HandleTapAsync(voiceCommand)` on the current selection (`TriggerController.cs:205-231`); "Apply the spoken command to the selected text…" (`contentClassifier.js:883`) | `/v1/talk` in context mode (`services.js:210-252`, `TALK_CONTEXT_SYSTEM` `prompts.js:46-55`) | The spoken instruction always applies to the active selection ("this", "that", "it"). |
| More Options | `ResultPanelWindowOnMoreOptionsRequested` (`TriggerController.cs:2312-2350`) reopens the menu from the stored suggestion without a new `/suggest-actions` call | `MoreOptionsAsync` (`CursivisApp.cs:557-584`) reuses the cached suggestions when the selection id matches | Same behavior. |
| Word-step reveal | `PresentBodyAsync` (`ResultPanelWindow.xaml.cs:385-471`) | `ResultCard.ShowResult` and `Update` (`ResultCard.cs:266-306`, `:363-387`) | Same thresholds: ≤110 words 1 word every 20 ms, >110 2/17 ms, >200 3/14 ms, >320 4/12 ms. |
| Theme tokens | `CompanionThemeService.cs:172-276`, orb palette code (`OrbOverlayWindow.xaml.cs:722-763`) | `CursivisTheme.cs:61-105` | Same graphite and silver tokens, orb gradient stops, chip colors and shine colors and timings. Deviations are listed in `UI_DESIGN_SPEC.md` §2. |

## 4. Adapted

| What | Original | Cursivis Spatial | Why |
|---|---|---|---|
| Suggest and analyze | `/suggest-actions` makes a router call and an options call, then `/analyze` routes again and generates. That is at least 4 Gemini calls per action, up to 6 with retries. | `/v1/suggest` is one structured call that returns content type, title, summary, quality, confidence and exactly 4 suggestions. `/v1/act` executes the chosen action directly. | Latency and free-tier quota. |
| Voice | Stream to `/live`, wait 4 s for a transcript, fall back to `/transcribe` and then typed input, then run `/suggest-actions` and `/analyze` with the transcript. | `/v1/talk` sends the WAV inline in one call and gets transcript, answer, uncertainty and `clientAction` back. | One round trip. The probe measured about 2.8 s. `gemini-3.5-transcribe` has no JSON mode. |
| API surface | Unauthenticated localhost routes, including `/runtime/*` (rewrites `process.env`, SSRF), browser planning routes and `WS /live` (`B/app.js:565-894`, `:1394-1504`) | `/v1/health`, `/v1/suggest`, `/v1/act`, `/v1/talk`, `/v1/speak` (`S/app.js:39-138`) behind an app token, per-install and per-IP rate limits, a daily cap, ajv validation with `additionalProperties: false`, and size limits (`S/security.js`, `S/cursivis/schemas.js:9-119`). | The backend is public now. Desktop-only endpoints were removed for security. |
| Models | `gemini-3.6-flash` with appended fallbacks `gemini-3.5-flash` and `gemini-3.5-flash-lite` (`B/geminiService.js:20-21`). `.env.example` uses the deprecated `gemini-2.5-flash`. | Three chains (fast, quality, TTS) in `S/config.js:25-36`, overridable by environment variable, with per-model health cooldowns. | Probe results (`RESEARCH.md` §9). |
| Gemini client | `@google/genai` SDK; 110 s operation budget, 25 s per attempt, 500/1500 ms backoff | Direct REST calls to `v1beta generateContent` (`S/gemini/client.js`). `thinkingLevel` minimal for suggest and low for act and talk. On a 400, one compat retry without `thinkingConfig` or schema. A hedged second attempt after 4.5 s (suggest) or 14 s (act, talk). Deadlines 16, 50 and 40 s. | Two runtime dependencies (`ajv`, `express`). Timeouts fit a 60 s Vercel function (`backend/vercel.json`). |
| Output contract | Plain-text `result`, `confidence`, `alternatives`, cleaned by `sanitizeResultText` | JSON with `title`, Markdown `answer`, `evidence`, `uncertainty`, `uncertaintyNote` and up to 3 `followUps` (`S/cursivis/schemas.js:150-173`). `cleanAnswer` strips a leading "Sure/Here is" line (`services.js:77-81`). | The card shows grounding and uncertainty. Follow-ups become chips. |
| Content types | 10 desktop types (question, mcq, code, email, report, product, social_caption, prompt, general_text, image) | 16 spatial types, e.g. food_label, printed_text, handwritten_note, sign, chart (`actions.js:12-15`) | Camera selections are physical objects. |
| Labels | snake_case ids from the backend. The client builds the labels: `ToDisplayAction`, the "AI: " prefix, abbreviations. | The model returns a 1-3 word label (22 chars or fewer) and an icon id. `cleanLabel` trims to 26 chars in Title Case (`actions.js:69-83`). | Labels fit the chip and name the specific content (for example "Check Allergens"). |
| Guided menu | 5 slots: 2 actions, "Custom Voice Command" and 2 "..." placeholders filled after a fixed 5 s | 4 AI chips plus a separate Talk chip. The "..." placeholders stay until the single response arrives. | One call. Talk is always available, even while loading. |
| Fallback suggestions | Heuristic `fallbackIntentDecision`, `ALTERNATIVES_BY_TYPE`, client `FallbackTextSuggestion` | `FALLBACKS` (`actions.js:87-106`) pad or replace the AI list, with `source` "fallback" or "ai+fallback". The UI shows "basic options". | Fallbacks are labelled honestly. |
| Unsupported-action filter | Includes `check_` | Drops `check_` and adds buy, order, call, share, post and identify-person or face (`actions.js:44-45`) | `check_allergens` and `check_vegan` are core food actions. People must not be identified. |
| Canonical ids | `generate_captions` / `suggest_captions`, `ocr_extract_text` | `generate_caption`, `extract_text` (`actions.js:33-37`) | Fewer near-duplicate ids. |
| Caching | 10 min / 200 entries, text-only prompts | 10 min / 200 entries keyed by a SHA-256 of kind, text and image, so images are cached too. Act cache includes action, instruction and history length. Regenerate bypasses the cache (`services.js:16-35`, `:168-174`). | Repeated taps on the same snip cost nothing. |
| Conversation memory | None | Per-selection history (8 turns), general-mode history, `recentSelectionSummary` | Follow-ups need the previous answer. |
| Orb states | 4 states. Errors look like success (Completed plus the burst). | 9 moods with a distinct error tint (`OrbView.cs:7`) | Clear feedback for each phase of a spatial interaction. |
| Result panel buttons | Undo, Insert, Take Action, More Options, Theme, Settings | Header: Read aloud, Talk, Theme, Expand, Pin, Close. Footer: Ask follow-up, More options, Redo, Copy. | There is no target app to insert into. Spatial controls were added. |
| Spoken output | Only in Live Mode, which plays native-audio Live sessions at 24 kHz | Read-aloud of any result through `/v1/speak` (Gemini TTS), chunked at 160 then 360 chars (`Voice.cs:259-288`) | Hands-free use and accessibility. |
| Theme switching | `CompanionThemeService.Apply` plus `ThemeChanged`, instant | `CursivisTheme.SetDark` plus `Changed`, instant, persisted in PlayerPrefs. Also available by voice. | Same behavior, extra entry points. |

## 5. Replaced

| Original | Replacement | Why |
|---|---|---|
| Windows selection capture: clipboard copy with retries (`SelectionDetector.cs:16-44`), cursor-area screenshots (`ScreenCaptureService.cs:11`), full-screen lasso (`LassoOverlayWindow`) | Room Snip through the Passthrough Camera API (`SnipSurface`, `PassthroughCapture`) and workspace selection (`WorkspaceText` word ranges, `WorkspaceImage` regions) | The headset has no clipboard or foreground app to copy from. The camera cannot see virtual content, so app-owned content is selected directly. |
| Logitech triggers: tap, long press, dial ticks and press, Snip-it and Take Action buttons, plus haptics (`plugin/logitech-plugin/...`) | Hand pinch gestures through ISDK ray and poke: pinch "Ask Cursivis", pinch a suggestion chip, hold Talk (push-to-talk), pinch the orb for general Talk. An audible click replaces haptics (`UiKit.cs:90-122`). | Hands-first is mandatory, and there is no third-party hardware. |
| WPF: XAML windows, DropShadowEffect, RadialGradientBrush, Storyboards, Segoe and Bahnschrift fonts | Unity uGUI and TextMeshPro, built in code (`UiKit.cs`), drawn with SDF shaders (`CursivisRoundedRect.shader`, `CursivisOrb.shader`). Fonts: Inter, IBM Plex Mono, Material Icons. | Quest runtime. One shared material instead of per-element effects. Licensed fonts. |
| Mouse cursor | Hand ray (ISDK ray interactor through `PointableCanvasModule`) for far targets, poke for near ones | Hand input. |
| Desktop overlays: topmost transparent windows placed at the cursor or the work-area corner | World-space canvases at 1 mm per unit (`SceneBuilder.cs:165-183`). The orb sits at 0.58 m or next to the selection, the card at 0.78 m and grabbable (ISDK `Grabbable` on a handle), with pin and follow modes. | Spatial placement and comfort. |
| IPC: localhost HTTP sidecar (`127.0.0.1:51880`), trigger IPC (`:48711`), native messaging host | HTTPS backend (Vercel function `backend/api/index.js`, `backend/vercel.json` maxDuration 60 s) with an app token. Local development uses `http://127.0.0.1:8787` (`S/server.js`). | The headset cannot host a Node sidecar, and keys must stay off the device. |
| Live WebSocket: `wss://…BidiGenerateContent?key=<raw key>` from the desktop, and the unauthenticated `WS /live` proxy over the key pool | Not shipped. Talk uses one-shot audio calls. | A raw key on the client is extractable. The proxy had no auth. Ephemeral tokens would work but were not needed for push-to-talk. |
| Browser "Take Action" (extension, native host, action agent, `browserActionPlanner.js`) | Not ported | It drives a Chromium extension through a native messaging host on a PC, and there is no such browser session on the headset. It also clicks and submits on third-party sites, which the unsupported-action filter forbids. |

## 6. What was not ported

- **Smart mode (AutoPerform).** Every trigger shows suggestions.
- **Mode selection window.** Not needed without Smart mode.
- **Insert / auto-replace into the source app, Undo, automatic clipboard copy.** There is no source app. The card has a Copy button (`CursivisApp.cs:812-817`).
- **Typed input.** No text command bar and no system keyboard, to stay hands-first. Typed instructions exist only as follow-up chips and automation hooks.
- **Live Mode:** desktop automation, app resolver, file search, screen guidance, webcam capture.
- **Browser Take Action** and its plan preview (`ActionPlanPreviewWindow`).
- **Google Search grounding** for time-sensitive questions (`B/geminiService.js:135`). Answers are grounded in the selection only.
- **Local providers** (Ollama, OpenAI-compatible, hosted) and the `/runtime/*` configuration routes.
- **The local `extract_dominant_colors` PNG decoder** (`B/imageAnalysis.js`). It also crashed on JPEG input (`backend-audit.md` §0.6a). The action is now answered by the model.
- **The dedicated Prompt Optimizer command** (`optimize_prompt`). The model may still suggest an equivalent action.
- **Desktop app infrastructure:** intent memory (`IntentMemoryService.cs`), settings window, tray icon, startup registration, installer, the Logitech plugin package.
