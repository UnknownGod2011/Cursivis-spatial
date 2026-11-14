using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cursivis.Spatial.Capture;
using Cursivis.Spatial.Selection;
using Cursivis.Spatial.UI;
using Cursivis.Spatial.Voice;
using Meta.XR;
using Meta.XR.EnvironmentDepth;
using UnityEngine;

namespace Cursivis.Spatial.App
{
    /// <summary>
    /// Composition root and interaction orchestrator for Cursivis Spatial.
    /// Selection (camera snip or workspace) -> "Ask Cursivis" -> orb + four AI suggestions + Talk -> action / voice ->
    /// result card -> follow-up or dismiss. All transitions go through FlowStateMachine; every request is cancellable and
    /// tagged with the selection id so stale responses can never overwrite newer state.
    /// </summary>
    public sealed class CursivisApp : MonoBehaviour
    {
        [Header("Scene references (assigned by the scene builder)")]
        public Transform Head;
        public Canvas OrbCanvas, ResultCanvas, ChipCanvas, SnipCanvas, OnboardingCanvas, ToastCanvas;
        public Canvas[] WorkspaceCanvases;
        public Transform ResultRoot;
        public PassthroughCameraAccess CameraAccess;
        public EnvironmentRaycastManager EnvironmentRaycast;
        public MeshRenderer PreviewQuad;

        public FlowStateMachine Flow { get; } = new FlowStateMachine();
        public CursivisSelection Selection => _selection;
        public ResultResponse LastResult => _lastResult;
        public SuggestResponse LastSuggestions => _suggest;
        public OrbHud Hud => _hud;
        public ResultCard Card => _card;
        public Workspace WorkspacePanels => _workspace;
        public SnipSurface Snip => _snip;
        public SelectionChip Chip => _chip;
        public bool Online { get; private set; } = true;
        public static CursivisApp Instance { get; private set; }

        CursivisConfig _config;
        CursivisApi _api;
        readonly ConversationSession _conv = new ConversationSession();
        PassthroughCapture _capture;
        VoiceCapture _voice;
        SpeechPlayer _speech;
        OrbHud _hud;
        ResultCard _card;
        SelectionChip _chip;
        SnipSurface _snip;
        Workspace _workspace;
        Onboarding _onboarding;
        Toast _toast;

        CursivisSelection _selection;
        SuggestResponse _suggest;
        SuggestionDto _lastAction;
        string _lastInstruction;
        ResultResponse _lastResult;
        Func<Task> _retry;
        CancellationTokenSource _requestCts, _cameraCts, _lifetimeCts;
        bool _talkContext, _talkHolding, _talkToggleMode;
        float _talkPressedAt;
        FlowState _stateBeforeTalk;
        bool _pinned = true, _expanded;
        Vector3 _orbTarget;
        Quaternion _orbRotTarget;
        bool _orbHome = true;
        float _homeYaw;
        Vector3 _homeAnchor;
        float _noTrackingSince = -1f;
        bool _pendingFeedbackMeasure;
        OVRCameraRig _rig;

        void Awake()
        {
            Instance = this;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Desktop simulator builds only: integrated GPUs can't render Quest 3 resolution x2 eyes with 4x MSAA.
            // Quest builds keep full resolution and MSAA. The simulator shows the eye view itself, so the desktop
            // mirror window is not rendered.
            float eyeScale = 0.5f;
            foreach (var arg in Environment.GetCommandLineArgs())
                if (arg.StartsWith("--cursivis-eye-scale=") && float.TryParse(arg.Substring(21), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s)) eyeScale = Mathf.Clamp(s, 0.25f, 1f);
            UnityEngine.XR.XRSettings.eyeTextureResolutionScale = eyeScale;
            UnityEngine.XR.XRSettings.gameViewRenderMode = UnityEngine.XR.GameViewRenderMode.None;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp) urp.msaaSampleCount = 1;
#endif
            CursivisTheme.Load();
            // Safety net for the seated experience: never let rig locomotion (gravity locomotor, teleport, turning) run.
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var tn = mb.GetType().Name;
                if (tn == "FirstPersonLocomotor" || tn.Contains("Teleport") || tn.Contains("Turner")) mb.enabled = false;
            }
            foreach (var cc in FindObjectsByType<CharacterController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) cc.enabled = false;
            _config = CursivisConfig.Instance;
            _api = new CursivisApi(_config);
            _lifetimeCts = new CancellationTokenSource();
            if (FindFirstObjectByType<CursivisMetrics>() == null) gameObject.AddComponent<CursivisMetrics>();
            if (Head == null && Camera.main != null) Head = Camera.main.transform;

            _capture = gameObject.AddComponent<PassthroughCapture>();
            _capture.Camera = CameraAccess;
            // MRUK reports camera and raycast poses in tracking space; our selection corners live in world space.
            var rig = FindFirstObjectByType<OVRCameraRig>();
            _rig = rig;
            if (rig != null)
            {
                _capture.TrackingSpace = rig.trackingSpace;
                if (EnvironmentRaycast != null) EnvironmentRaycast.CustomTrackingSpace = rig.trackingSpace;
            }
            if (CameraAccess != null) CameraAccess.enabled = false;
            _voice = gameObject.AddComponent<VoiceCapture>();
            _voice.AutoStopped += OnVoiceAutoStopped;
            var speechSource = new GameObject("SpeechSource").AddComponent<AudioSource>();
            speechSource.transform.SetParent(OrbCanvas.transform, false);
            speechSource.playOnAwake = false;
            speechSource.spatialBlend = 0.65f; // voice comes from the orb
            speechSource.minDistance = 0.6f;
            _speech = gameObject.AddComponent<SpeechPlayer>();
            _speech.Source = speechSource;
            _speech.Synthesize = (text, ct) => _api.SpeakAsync(text, ct);
            _speech.Started += () => { _card?.SetSpeaking(true); if (_hud.Orb.Mood != OrbMood.Listening) _hud.Orb.SetMood(OrbMood.Speaking, "Speaking\u2026"); };
            _speech.Finished += () => { _card?.SetSpeaking(false); if (_hud.Orb.Mood == OrbMood.Speaking) _hud.Orb.SetMood(OrbMood.Idle, null); };
            _speech.Failed += msg => Toast(msg);

            BuildUi();
            Flow.Changed += (from, to) => OnFlowChanged(from, to);
            // Changed fires every frame of the cross-fade; log once when the fade has finished.
            CursivisTheme.Changed += () => { if (Debug.isDebugBuild && !CursivisTheme.IsTransitioning) LogDiagnostics("theme " + (CursivisTheme.IsDark ? "dark" : "light")); };
        }

        System.Collections.IEnumerator Start()
        {
            _hud.Orb.SetMood(OrbMood.Idle, null);
            _ = HealthLoop(_lifetimeCts.Token);
            // Wait for a tracked head pose before placing anything (the first frames report the origin).
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 4f && (Head == null || Head.position.sqrMagnitude < 0.01f || Time.frameCount < 20)) yield return null;
            RecenterHome(true);
            PlaceWorkspace();
            if (PlayerPrefs.GetInt("cursivis.onboarded", 0) == 0) ShowOnboarding();
            CursivisAutopilot.InstallIfRequested(this);
            Debug.Log($"[Cursivis] started v{CursivisVersion.Version} backend={(string.IsNullOrEmpty(_config.backendUrl) ? "<none>" : new Uri(_config.backendUrl).Host)} platform={Application.platform}");
        }

        void OnDestroy()
        {
            _lifetimeCts?.Cancel();
            _requestCts?.Cancel();
            _cameraCts?.Cancel();
        }

        // ------------------------------------------------------------------ UI wiring
        void BuildUi()
        {
            _hud = OrbCanvas.gameObject.AddComponent<OrbHud>();
            _hud.Build();
            _hud.SnipClicked += () => _ = BeginSnipAsync();
            _hud.WorkspaceClicked += ToggleWorkspace;
            _hud.ThemeClicked += CursivisTheme.Toggle;
            _hud.HelpClicked += ShowOnboarding;
            _hud.SuggestionChosen += i => _ = RunSuggestionAsync(i, false);
            _hud.TalkPressed += () => OnTalkPressed(true);
            _hud.TalkReleased += OnTalkReleased;
            _hud.TalkClicked += OnTalkClicked;
            _hud.OrbPressed += OnOrbPressed;
            _hud.OrbReleased += OnTalkReleased;
            _hud.OrbClicked += OnOrbClicked;

            _card = ResultCanvas.gameObject.AddComponent<ResultCard>();
            _card.Build();
            _card.CloseClicked += Dismiss;
            _card.SpeakClicked += ToggleReadAloud;
            _card.ThemeClicked += CursivisTheme.Toggle;
            _card.TalkPressed += () => OnTalkPressed(_selection != null && (_lastResult == null || _lastResult.mode != "general"));
            _card.TalkReleased += OnTalkReleased;
            _card.TalkClicked += OnTalkClicked;
            _card.MoreOptionsClicked += () => _ = MoreOptionsAsync();
            _card.RegenerateClicked += () => _ = RegenerateAsync();
            _card.CopyClicked += CopyResult;
            _card.RetryClicked += () => { if (_retry != null) _ = _retry(); };
            _card.FollowUpClicked += text => _ = TalkTypedAsync(text, _selection != null);
            _card.PinClicked += TogglePin;
            _card.ExpandClicked += ToggleExpand;
            _card.SetPinned(_pinned);

            _chip = ChipCanvas.gameObject.AddComponent<SelectionChip>();
            _chip.Build();
            _chip.AskButton.Clicked += () => _ = TriggerAsync();
            _chip.RetakeButton.Clicked += Retake;
            _chip.CancelButton.Clicked += () => ClearSelection(true);

            _snip = SnipCanvas.gameObject.AddComponent<SnipSurface>();
            _snip.Build();
            _snip.Started += () => Flow.Go(FlowState.Selecting, "pinch down");
            _snip.Completed += corners => _ = OnSnipCompletedAsync(corners);
            _snip.Rejected += msg => { Toast(msg); Flow.Go(FlowState.SelectionReady, "rejected"); };
            SnipCanvas.gameObject.SetActive(false);

            _workspace = gameObject.AddComponent<Workspace>();
            _workspace.Build(WorkspaceCanvases);
            _workspace.TextSelected += OnWorkspaceText;
            _workspace.ImageSelected += OnWorkspaceImage;

            _onboarding = OnboardingCanvas.gameObject.AddComponent<Onboarding>();
            _onboarding.Build();
            _onboarding.GotIt.Clicked += CloseOnboarding;
            _onboarding.TryWorkspace.Clicked += () => { CloseOnboarding(); _workspace.SetShown(true); Toast("Pinch a paragraph or the photo to select it.", false); };
            _onboarding.TrySnip.Clicked += () => { CloseOnboarding(); _ = BeginSnipAsync(); };

            _toast = ToastCanvas.gameObject.AddComponent<Toast>();
            _toast.Build();

            if (PreviewQuad != null) PreviewQuad.gameObject.SetActive(false);
            ChipCanvas.gameObject.SetActive(true);
        }

        void Toast(string message, bool warning = true) => _toast?.Show(message, warning);

        void ShowOnboarding()
        {
            PlaceInFront(OnboardingCanvas.transform, 0.78f, 0.02f, 0f);
            _onboarding.Show();
        }

        void CloseOnboarding()
        {
            PlayerPrefs.SetInt("cursivis.onboarded", 1);
            PlayerPrefs.Save();
            _onboarding.Hide();
        }

        void ToggleWorkspace()
        {
            bool show = !_workspace.Shown;
            if (show) PlaceWorkspace();
            _workspace.SetShown(show);
        }

        // ------------------------------------------------------------------ state reactions
        void OnFlowChanged(FlowState from, FlowState to)
        {
            bool home = to == FlowState.Idle || to == FlowState.SelectionReady || to == FlowState.Selecting || to == FlowState.SelectionConfirmed || to == FlowState.Dismissed;
            _hud.ShowDock(to == FlowState.Idle || to == FlowState.Dismissed);
            if (home && !_orbHome) { _orbHome = true; RecenterHome(false); }
            if (to == FlowState.Idle && _hud.Orb.Mood != OrbMood.Speaking && _hud.Orb.Mood != OrbMood.Error) _hud.Orb.SetMood(OrbMood.Idle, Online ? null : "Offline");
        }

        // ------------------------------------------------------------------ selection: camera snip
        async Task BeginSnipAsync()
        {
            if (!(Flow.State == FlowState.Idle || Flow.State == FlowState.SelectionConfirmed || Flow.State == FlowState.SuggestionsVisible || Flow.State == FlowState.ResultVisible || Flow.State == FlowState.Dismissed))
                return;
            CancelRequests();
            _speech.Stop();
            if (_card.Visible && !_pinned) _card.Hide();
            ClearSelection(false);
            if (!_capture.HasPermission)
            {
                Toast("Cursivis needs camera access to see what you select in your room.", false);
                _capture.RequestPermission();
                float t0 = Time.realtimeSinceStartup;
                while (!_capture.HasPermission && Time.realtimeSinceStartup - t0 < 30f) await Task.Yield();
                if (!_capture.HasPermission)
                {
                    Toast("Camera permission was denied. You can still use the workspace and Talk, or allow camera access in Settings.");
                    return;
                }
            }
            if (!Flow.Go(FlowState.SelectionReady, "snip armed")) return;
            CursivisMetrics.Count("snip_armed");
            _workspace.SetShown(false);
            await EnsureDepthAsync();
            if (Flow.State != FlowState.SelectionReady) return;
            PlaceSnipSurface();
            SetDepth(false); // depth is only needed to place the surface; keep GPU cost low
            SnipCanvas.gameObject.SetActive(true);
            _snip.ResetDrawing();
            _snip.SetHint("Starting the camera\u2026");
            _snip.SetCameraFrame(default, false);
            _hud.Orb.SetMood(OrbMood.Acknowledged, "Snip");
            _cameraCts?.Cancel();
            _cameraCts = new CancellationTokenSource();
            bool ok;
            try { ok = await _capture.StartCameraAsync(_cameraCts.Token); }
            catch (OperationCanceledException) { return; }
            if (Flow.State != FlowState.SelectionReady && Flow.State != FlowState.Selecting) return;
            if (!ok)
            {
                Toast(_capture.LastError ?? "The passthrough camera is unavailable.");
                CursivisMetrics.Count("camera_unavailable");
                EndSnipMode();
                Flow.Go(FlowState.Idle, "camera unavailable");
                return;
            }
            _snip.SetHint("Pinch and drag to frame what you want Cursivis to see");
            UpdateCameraFrame();
        }

        void UpdateCameraFrame()
        {
            var plane = new Plane(-SnipCanvas.transform.forward, SnipCanvas.transform.position);
            if (_capture.TryGetCameraFrameOnPlane(plane, out var c))
            {
                var rt = (RectTransform)SnipCanvas.transform;
                Vector2 a = rt.InverseTransformPoint(c[0]), b = rt.InverseTransformPoint(c[2]);
                var min = Vector2.Min(a, b);
                var max = Vector2.Max(a, b);
                _snip.SetCameraFrame(Rect.MinMaxRect(min.x, min.y, max.x, max.y), true);
            }
        }

        void SetDepth(bool on)
        {
            if (EnvironmentRaycast == null) return;
            var depth = FindFirstObjectByType<EnvironmentDepthManager>(FindObjectsInactive.Include);
            if (depth != null) depth.enabled = on;
            EnvironmentRaycast.enabled = on;
        }

        float? _depthHit;

        async Task EnsureDepthAsync()
        {
            _depthHit = null;
            if (EnvironmentRaycast == null) return;
            bool supported;
            try { supported = EnvironmentRaycastManager.IsSupported; } catch (Exception) { supported = false; }
            if (!supported) return;
            SetDepth(true);
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.8f)
            {
                var head = HeadPose();
                EnvironmentRaycastHit hit = default;
                bool ok = false;
                try { ok = EnvironmentRaycast.Raycast(new Ray(head.position, head.rotation * Vector3.forward), out hit, 4f); }
                catch (Exception) { ok = false; }
                if (ok && hit.status == EnvironmentRaycastHitStatus.Hit)
                {
                    _depthHit = Vector3.Distance(head.position, hit.point);
                    return;
                }
                await Task.Yield();
            }
        }

        void PlaceSnipSurface()
        {
            var head = HeadPose();
            float depth = 0.85f;
            if (_depthHit.HasValue)
            {
                depth = Mathf.Clamp(_depthHit.Value - 0.01f, 0.35f, 2.5f);
                Debug.Log($"[CursivisCapture] depth from environment raycast: {depth:0.00} m");
            }
            var t = SnipCanvas.transform;
            t.position = head.position + head.forward * depth;
            t.rotation = Quaternion.LookRotation(head.forward, Vector3.up);
            // keep the angular size of the surface constant (designed at 0.85 m)
            t.localScale = Vector3.one * 0.001f * (depth / 0.85f);
        }

        void EndSnipMode()
        {
            _cameraCts?.Cancel();
            _capture.StopCamera();
            SnipCanvas.gameObject.SetActive(false);
            _snip.ResetDrawing();
        }

        async Task OnSnipCompletedAsync(Vector3[] corners)
        {
            if (Flow.State != FlowState.Selecting && Flow.State != FlowState.SelectionReady) return;
            CursivisMetrics.Since("snip_down", "snip_gesture_ms");
            _snip.SetHint("Capturing\u2026");
            CaptureResult capture;
            try
            {
                capture = await _capture.CaptureAsync(corners, _lifetimeCts.Token);
            }
            catch (Exception e)
            {
                CursivisMetrics.Count("capture_failed");
                Toast(e is InvalidOperationException ? e.Message : "Capture failed. Please try again.");
                _snip.ResetDrawing();
                _snip.SetHint("Pinch and drag to frame what you want Cursivis to see");
                Flow.Go(FlowState.SelectionReady, "capture failed");
                return;
            }
            var sel = new CursivisSelection(SelectionKind.Image, SelectionSource.Camera, null, capture.Jpeg, capture.Width, capture.Height, "Room snip", null)
            {
                WorldCorners = corners,
                Preview = capture.Preview
            };
            EndSnipMode();
            _workspace.SetShown(true);
            SetSelection(sel);
            ShowPreview(sel);
            Flow.Go(FlowState.SelectionConfirmed, "snip captured");
            ShowChip(sel, "Room snip \u00B7 nothing is sent until you ask", "Retake");
            CursivisMetrics.Count("snip_captured");
        }

        void ShowPreview(CursivisSelection sel)
        {
            if (PreviewQuad == null || sel.Preview == null || sel.WorldCorners == null) return;
            var c = sel.WorldCorners;
            var center = sel.Center;
            var up = (c[1] - c[0]).normalized;
            var right = (c[3] - c[0]).normalized;
            var normal = Vector3.Cross(right, up);
            var head = HeadPose();
            var toHead = (head.position - center).normalized;
            PreviewQuad.transform.position = center + toHead * 0.006f;
            PreviewQuad.transform.rotation = Quaternion.LookRotation(-toHead, up);
            PreviewQuad.transform.localScale = new Vector3(Vector3.Distance(c[0], c[3]), Vector3.Distance(c[0], c[1]), 1f);
            var mat = PreviewQuad.material;
            mat.mainTexture = sel.Preview;
            PreviewQuad.gameObject.SetActive(true);
            var pop = PreviewQuad.GetComponent<PreviewPop>() ?? PreviewQuad.gameObject.AddComponent<PreviewPop>();
            pop.Play(PreviewQuad.transform.localScale);
            _ = normal;
        }

        void HidePreview()
        {
            if (PreviewQuad != null) PreviewQuad.gameObject.SetActive(false);
        }

        void Retake()
        {
            bool camera = _selection != null && _selection.Source == SelectionSource.Camera;
            ClearSelection(false);
            if (camera)
            {
                Flow.Go(FlowState.SelectionReady, "retake");
                Flow.Go(FlowState.Idle, "retake reset");
                _ = BeginSnipAsync();
            }
            else Flow.Go(FlowState.Idle, "clear");
        }

        // ------------------------------------------------------------------ selection: workspace
        void OnWorkspaceText(WorkspaceText item, string text, Vector3[] corners)
        {
            if (!CanStartNewSelection()) return;
            CancelRequests();
            _speech.Stop();
            ClearSelection(false, keepHighlightOf: item);
            var sel = new CursivisSelection(SelectionKind.Text, SelectionSource.Workspace, text, null, 0, 0, item.Title, item.DocType) { WorldCorners = corners };
            SetSelection(sel);
            Flow.Go(FlowState.SelectionConfirmed, "workspace text");
            ShowChip(sel, text.Length == item.SourceText.Length ? "Whole paragraph selected" : $"{CountWords(text)} words selected", "Clear");
            CursivisMetrics.Since("select_down", "select_gesture_ms");
            CursivisMetrics.Count("select_text");
        }

        void OnWorkspaceImage(WorkspaceImage item, Rect uv, Vector3[] corners)
        {
            if (!CanStartNewSelection()) return;
            CancelRequests();
            _speech.Stop();
            ClearSelection(false, keepHighlightOf: item);
            var src = item.Image.texture as Texture2D;
            if (src == null) { Toast("This image can't be selected."); return; }
            var crop = WorkspaceCapture.CropTexture(src, uv);
            bool whole = uv.width > 0.98f && uv.height > 0.98f;
            var sel = new CursivisSelection(SelectionKind.Image, SelectionSource.Workspace, null, crop.Jpeg, crop.Width, crop.Height, item.Title, "photo")
            {
                WorldCorners = corners,
                Preview = crop.Preview
            };
            SetSelection(sel);
            Flow.Go(FlowState.SelectionConfirmed, "workspace image");
            ShowChip(sel, whole ? "Photo selected" : "Photo region selected", "Clear");
            CursivisMetrics.Since("select_down", "select_gesture_ms");
            CursivisMetrics.Count("select_image");
        }

        bool CanStartNewSelection()
        {
            // New selections are always allowed except while recording voice; in-flight requests get cancelled.
            return Flow.State != FlowState.VoiceListening && Flow.State != FlowState.Selecting;
        }

        static int CountWords(string s) => s.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

        void SetSelection(CursivisSelection sel)
        {
            _selection?.Dispose();
            _selection = sel;
            _suggest = null;
            _lastAction = null;
            _lastResult = null;
            _retry = null;
            Debug.Log($"[CursivisSelect] {sel.Describe()}");
        }

        void ShowChip(CursivisSelection sel, string caption, string retakeLabel)
        {
            var head = HeadPose();
            var c = sel.WorldCorners;
            var bottomCenter = (c[0] + c[3]) * 0.5f;
            var toHead = (head.position - bottomCenter).normalized;
            var pos = bottomCenter + Vector3.down * 0.045f + toHead * 0.03f;
            // keep the chip comfortably reachable: no further than 0.9 m, no lower than 0.45 m below the eyes
            var d = Vector3.Distance(head.position, pos);
            if (d > 0.9f) pos = head.position + (pos - head.position).normalized * 0.9f;
            pos.y = Mathf.Max(pos.y, head.position.y - 0.45f);
            ChipCanvas.transform.position = pos;
            ChipCanvas.transform.rotation = Quaternion.LookRotation(pos - head.position, Vector3.up);
            _chip.Show(caption, retakeLabel);
            _hud.Orb.SetMood(OrbMood.Acknowledged, null);
            UiKit.PlayClick(true, pos);
        }

        void ClearSelection(bool toIdle, object keepHighlightOf = null)
        {
            if (_chip.Visible) _chip.Hide();
            HidePreview();
            _workspace.ClearHighlights();
            if (keepHighlightOf is WorkspaceText t) { /* highlight re-applied by the item itself */ }
            if (_selection != null) _conv.RememberSelection(_selection);
            _selection?.Dispose();
            _selection = null;
            _suggest = null;
            _hud.ShowRing(false);
            if (toIdle)
            {
                CancelRequests();
                if (Flow.State != FlowState.Idle)
                {
                    if (!Flow.Go(FlowState.Idle, "cleared")) Flow.Reset();
                }
            }
        }

        // ------------------------------------------------------------------ trigger + suggestions
        public async Task TriggerAsync()
        {
            if (_selection == null) return;
            if (Flow.State != FlowState.SelectionConfirmed && Flow.State != FlowState.ResultVisible && Flow.State != FlowState.SuggestionsVisible) return;
            CursivisMetrics.Mark("trigger");
            _pendingFeedbackMeasure = true;
            _chip.Hide();
            Flow.Go(FlowState.Understanding, "ask cursivis");
            _orbHome = false;
            PlaceForSelection(_selection);
            _hud.ShowRing(true);
            _hud.SetLoading(_selection.Source == SelectionSource.Camera ? "Reading your snip\u2026" : $"Reading \u201C{_selection.Title}\u201D\u2026");
            _hud.Orb.SetMood(OrbMood.Understanding, "Understanding\u2026");
            UiKit.PlayClick(true, OrbCanvas.transform.position);
            await LoadSuggestionsAsync(_selection);
        }

        async Task LoadSuggestionsAsync(CursivisSelection sel)
        {
            var ct = NewRequestToken();
            _retry = () => LoadSuggestionsAsync(sel);
            try
            {
                var r = await _api.SuggestAsync(sel, _conv.HistoryFor(sel.Id), ct);
                if (_selection == null || r.selectionId != _selection.Id) { CursivisMetrics.Count("stale_response_dropped"); return; }
                _suggest = r;
                sel.SuggestTitle = r.title;
                sel.SuggestSummary = r.summary;
                CursivisMetrics.Since("trigger", "time_to_suggestions_ms");
                CursivisMetrics.Count(r.source == "fallback" ? "suggestions_fallback" : "suggestions_ai");
                _hud.SetSuggestions(r);
                _hud.Orb.SetMood(OrbMood.Completed, null);
                if (Flow.State == FlowState.Understanding) Flow.Go(FlowState.SuggestionsVisible, "suggestions ready");
                SetOnline(true);
            }
            catch (OperationCanceledException) { }
            catch (CursivisApiException e)
            {
                CursivisMetrics.Count("suggest_error_" + e.Code);
                if (_selection != sel) return;
                _hud.SetError(e.UserMessage);
                _hud.Orb.SetMood(OrbMood.Error, "Couldn\u2019t read it");
                if (e.IsNetwork) SetOnline(false);
                if (Flow.State == FlowState.Understanding) Flow.Go(FlowState.SuggestionsVisible, "suggestions failed");
            }
        }

        async Task MoreOptionsAsync()
        {
            if (_selection == null) { Toast("Select something first, then ask Cursivis.", false); return; }
            _speech.Stop();
            Flow.Go(FlowState.SuggestionsVisible, "more options");
            _hud.ShowRing(true);
            if (_suggest != null && _suggest.selectionId == _selection.Id)
            {
                _hud.SetSuggestions(_suggest);
                return;
            }
            Flow.Go(FlowState.Understanding, "reload suggestions");
            _hud.SetLoading("Reading your selection\u2026");
            _hud.Orb.SetMood(OrbMood.Understanding, "Understanding\u2026");
            await LoadSuggestionsAsync(_selection);
        }

        // ------------------------------------------------------------------ actions
        public async Task RunSuggestionAsync(int index, bool regenerate)
        {
            if (_selection == null) return;
            var s = _hud.SuggestionAt(index);
            if (s == null) return;
            if (Flow.State != FlowState.SuggestionsVisible && Flow.State != FlowState.ResultVisible) return;
            Flow.Go(FlowState.ActionSelected, s.label);
            _hud.MarkChosen(index);
            await RunActionAsync(s, regenerate, null);
        }

        async Task RunActionAsync(SuggestionDto action, bool regenerate, string instruction)
        {
            var sel = _selection;
            if (sel == null) return;
            _lastAction = action;
            _lastInstruction = null;
            CursivisMetrics.Mark("action");
            Flow.Go(FlowState.Processing, "act " + action.id);
            _hud.Orb.SetMood(OrbMood.Processing, "Working\u2026");
            PlaceCard();
            _card.ShowLoading(action.label, null);
            var ct = NewRequestToken();
            _retry = () => RunActionAsync(action, regenerate, instruction);
            try
            {
                var r = await _api.ActAsync(sel, action, _conv.HistoryFor(sel.Id), regenerate, instruction, ct);
                if (_selection != sel || r.selectionId != sel.Id) { CursivisMetrics.Count("stale_response_dropped"); return; }
                CursivisMetrics.Since("action", "action_latency_ms");
                CursivisMetrics.Count("action_ok");
                _lastResult = r;
                _conv.Add(sel.Id, "user", action.label);
                _conv.Add(sel.Id, "assistant", r.answer);
                _card.ShowResult(r, action.label, false);
                _hud.ShowRing(false);
                _hud.Orb.SetMood(OrbMood.Completed, null);
                Flow.Go(FlowState.ResultVisible, "result");
                SetOnline(true);
            }
            catch (OperationCanceledException) { }
            catch (CursivisApiException e)
            {
                CursivisMetrics.Count("action_error_" + e.Code);
                if (_selection != sel) return;
                _card.ShowError(action.label, e.UserMessage, true);
                _hud.Orb.SetMood(OrbMood.Error, "Couldn\u2019t finish");
                if (e.IsNetwork) SetOnline(false);
                Flow.Go(FlowState.ResultVisible, "action failed");
            }
        }

        async Task RegenerateAsync()
        {
            if (_lastAction != null && _selection != null) await RunActionAsync(_lastAction, true, null);
            else if (!string.IsNullOrEmpty(_lastInstruction)) await TalkTypedAsync(_lastInstruction, _selection != null);
        }

        // ------------------------------------------------------------------ voice (push-to-talk, toggle, general mode)
        void OnOrbPressed()
        {
            // The orb is the general-conversation entry point when nothing is selected.
            if (Flow.State == FlowState.Idle || Flow.State == FlowState.Dismissed) OnTalkPressed(false);
        }

        void OnOrbClicked()
        {
            if (Flow.State == FlowState.SuggestionsVisible && _suggest == null && _selection != null) { _ = MoreOptionsAsync(); return; }
            if (Flow.State == FlowState.VoiceListening && _talkToggleMode) StopTalkAndSend(StopReason.Tapped);
        }

        void OnTalkPressed(bool context)
        {
            if (Flow.State == FlowState.VoiceListening)
            {
                if (_talkToggleMode) StopTalkAndSend(StopReason.Tapped);
                return;
            }
            if (Flow.State == FlowState.Processing || Flow.State == FlowState.Selecting || Flow.State == FlowState.SelectionReady || Flow.State == FlowState.FollowUp) return;
            _speech.Stop();
            if (!_voice.HasPermission)
            {
                Toast("Cursivis needs the microphone for Talk. It only listens while you hold Talk.", false);
                _voice.RequestPermission(granted => { if (!granted) Toast("Microphone permission denied. Suggestions still work without voice."); });
                return;
            }
            _talkContext = context && _selection != null;
            _stateBeforeTalk = Flow.State;
            if (!Flow.Go(FlowState.VoiceListening, _talkContext ? "talk (context)" : "talk (general)")) return;
            if (!_voice.Begin(false, out var err))
            {
                Toast(err);
                ReturnFromTalk();
                return;
            }
            CursivisMetrics.Mark("talk_down");
            _talkPressedAt = Time.realtimeSinceStartup;
            _talkHolding = true;
            _talkToggleMode = false;
            if (!_talkContext && _orbHome) { /* orb stays home for general talk */ }
            _hud.Orb.SetMood(OrbMood.Listening, "Listening\u2026");
            _hud.SetHint("Release to send \u00B7 or tap and speak, I'll stop when you pause");
            _card.SetListening(true);
        }

        void OnTalkReleased()
        {
            if (!_talkHolding || Flow.State != FlowState.VoiceListening) return;
            _talkHolding = false;
            float held = Time.realtimeSinceStartup - _talkPressedAt;
            if (held >= 0.45f) StopTalkAndSend(StopReason.Released);
            else
            {
                // quick tap: keep listening hands-free until a pause in speech (or tap again)
                _talkToggleMode = true;
                _voice.SetAutoStop(true);
                _hud.Orb.SetMood(OrbMood.Listening, "Listening\u2026 tap to stop");
            }
        }

        void OnTalkClicked()
        {
            // handled by press/release; clicks while toggle-listening stop the recording
            if (Flow.State == FlowState.VoiceListening && _talkToggleMode && Time.realtimeSinceStartup - _talkPressedAt > 0.6f) StopTalkAndSend(StopReason.Tapped);
        }

        void OnVoiceAutoStopped(StopReason reason)
        {
            if (Flow.State == FlowState.VoiceListening) StopTalkAndSend(reason);
        }

        void StopTalkAndSend(StopReason reason)
        {
            _talkHolding = false;
            _talkToggleMode = false;
            _card.SetListening(false);
            var wav = _voice.End(out var err, out var seconds);
            CursivisMetrics.Instance?.Sample("talk_record_s", seconds);
            if (wav == null)
            {
                Toast(err);
                ReturnFromTalk();
                return;
            }
            _ = SendTalkAsync(wav, null, _talkContext);
        }

        void ReturnFromTalk()
        {
            _voice.Cancel();
            _card.SetListening(false);
            var back = _stateBeforeTalk == FlowState.VoiceListening ? FlowState.Idle : _stateBeforeTalk;
            if (!Flow.Go(back, "talk cancelled")) Flow.Go(FlowState.Idle, "talk cancelled");
            _hud.Orb.SetMood(back == FlowState.Idle ? OrbMood.Idle : OrbMood.Completed, null);
            _hud.SetHint("");
        }

        async Task TalkTypedAsync(string instruction, bool context)
        {
            if (Flow.State == FlowState.Processing || Flow.State == FlowState.VoiceListening) return;
            _stateBeforeTalk = Flow.State;
            await SendTalkAsync(null, instruction, context && _selection != null);
        }

        async Task SendTalkAsync(byte[] wav, string typed, bool context)
        {
            var sel = context ? _selection : null;
            CursivisMetrics.Mark("talk_sent");
            var sending = Flow.State == FlowState.ResultVisible && typed != null ? FlowState.FollowUp : FlowState.Processing;
            if (!Flow.Go(sending, wav != null ? "voice sent" : "typed follow-up"))
            {
                // Never run a request the state machine does not know about (it would leave the UI out of sync).
                Toast("Cursivis is busy. Try again in a moment.");
                return;
            }
            _hud.Orb.SetMood(OrbMood.ProcessingVoice, wav != null ? "Thinking about what you said\u2026" : "Working\u2026");
            _hud.SetHint("");
            if (!context) { _orbHome = true; }
            PlaceCard();
            _card.ShowLoading(context ? "Talk" : "Cursivis", wav != null ? "Listening to your request and preparing an answer\u2026" : $"Working on \u201C{typed}\u201D\u2026");
            var ct = NewRequestToken();
            _retry = () => SendTalkAsync(wav, typed, context);
            try
            {
                var history = _conv.HistoryFor(sel?.Id);
                var r = await _api.TalkAsync(sel, wav, typed, history, _conv.LastSelectionSummary, ct);
                if (context && (_selection != sel || r.selectionId != sel?.Id)) { CursivisMetrics.Count("stale_response_dropped"); return; }
                CursivisMetrics.Since("talk_sent", "voice_response_ms");
                if (wav != null && !r.heard)
                {
                    Toast("I didn't catch that. Hold Talk and try again.");
                    _card.ShowError("Talk", "I didn\u2019t catch that. Hold Talk and try again.", false);
                    _hud.Orb.SetMood(OrbMood.Idle, null);
                    Flow.Go(FlowState.ResultVisible, "not heard");
                    return;
                }
                _lastResult = r;
                _lastAction = null;
                _lastInstruction = string.IsNullOrEmpty(r.transcript) ? typed : r.transcript;
                _conv.Add(sel?.Id, "user", _lastInstruction);
                _conv.Add(sel?.Id, "assistant", r.answer);
                _card.ShowResult(r, context ? "Talk" : "Conversation", wav != null);
                _hud.ShowRing(false);
                _hud.Orb.SetMood(OrbMood.Completed, null);
                if (Flow.State == FlowState.FollowUp) Flow.Go(FlowState.ResultVisible, "follow-up result");
                else Flow.Go(FlowState.ResultVisible, "talk result");
                SetOnline(true);
                CursivisMetrics.Count(context ? "talk_context_ok" : "talk_general_ok");
                ApplyClientAction(r.clientAction);
            }
            catch (OperationCanceledException) { }
            catch (CursivisApiException e)
            {
                CursivisMetrics.Count("talk_error_" + e.Code);
                _card.ShowError("Talk", e.UserMessage, true);
                _hud.Orb.SetMood(OrbMood.Error, "Couldn\u2019t answer");
                if (e.IsNetwork) SetOnline(false);
                if (Flow.State == FlowState.FollowUp) Flow.Go(FlowState.ResultVisible, "follow-up failed");
                else Flow.Go(FlowState.ResultVisible, "talk failed");
            }
        }

        void ApplyClientAction(string action)
        {
            switch (action)
            {
                case "read_aloud": ToggleReadAloud(); break;
                case "toggle_theme": CursivisTheme.Toggle(); break;
                case "show_options": _ = MoreOptionsAsync(); break;
                case "clear_context": ClearSelection(false); Toast("Selection cleared.", false); break;
                case "new_conversation": _conv.ClearAll(); ClearSelection(false); Toast("Started a new conversation.", false); break;
            }
        }

        // ------------------------------------------------------------------ result card actions
        void ToggleReadAloud()
        {
            if (_speech.IsSpeaking) { _speech.Stop(); return; }
            var text = _card.CurrentPlainAnswer;
            if (string.IsNullOrWhiteSpace(text)) return;
            CursivisMetrics.Count("read_aloud");
            _speech.Speak(text);
        }

        void CopyResult()
        {
            if (_lastResult == null) return;
            GUIUtility.systemCopyBuffer = _lastResult.answer ?? "";
            Toast("Copied to clipboard.", false);
        }

        void TogglePin()
        {
            _pinned = !_pinned;
            _card.SetPinned(_pinned);
            Toast(_pinned ? "Pinned here in your space." : "The card will follow your view.", false);
        }

        void ToggleExpand()
        {
            _expanded = !_expanded;
            _card.SetExpanded(_expanded);
            ResultRoot.localScale = Vector3.one * (_expanded ? 1.28f : 1f);
        }

        public void Dismiss()
        {
            CancelRequests();
            _speech.Stop();
            _voice.Cancel();
            _card.Hide();
            _hud.ShowRing(false);
            if (Flow.State == FlowState.SelectionReady || Flow.State == FlowState.Selecting) EndSnipMode();
            if (!_workspace.Shown) _workspace.SetShown(true);
            ClearSelection(false);
            if (!Flow.Go(FlowState.Dismissed, "dismiss")) Flow.Reset();
            Flow.Go(FlowState.Idle, "dismissed");
            _hud.Orb.SetMood(OrbMood.Idle, Online ? null : "Offline");
            if (Debug.isDebugBuild) LogDiagnostics("dismiss");
        }

        // ------------------------------------------------------------------ placement
        public Pose HeadPose()
        {
            if (Head == null && Camera.main != null) Head = Camera.main.transform;
            return Head != null ? new Pose(Head.position, Head.rotation) : new Pose(new Vector3(0, 1.4f, 0), Quaternion.identity);
        }

        static Quaternion YawOnly(Quaternion q)
        {
            var f = q * Vector3.forward;
            f.y = 0;
            return f.sqrMagnitude < 1e-4f ? Quaternion.identity : Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        void RecenterHome(bool snap)
        {
            var head = HeadPose();
            _homeYaw = YawOnly(head.rotation).eulerAngles.y;
            _homeAnchor = head.position;
            var yaw = Quaternion.Euler(0, _homeYaw, 0);
            _orbTarget = head.position + yaw * new Vector3(0.21f, -0.235f, 0.58f); // ~20 deg right, ~22 deg down
            _orbRotTarget = Quaternion.LookRotation(_orbTarget - head.position, Vector3.up);
            if (snap) OrbCanvas.transform.SetPositionAndRotation(_orbTarget, _orbRotTarget);
            var toastPos = head.position + yaw * new Vector3(0.0f, -0.08f, 0.62f);
            ToastCanvas.transform.SetPositionAndRotation(toastPos, Quaternion.LookRotation(toastPos - head.position, Vector3.up));
        }

        void PlaceWorkspace()
        {
            if (WorkspaceCanvases == null) return;
            var head = HeadPose();
            var baseYaw = YawOnly(head.rotation).eulerAngles.y;
            // 2x2 grid of 400x300 mm cards at 1 m (each ~23 x 17 degrees). Rows sit slightly high so the home orb and
            // dock (lower right, about -22 degrees) never overlap the bottom-right card.
            var cells = new[] { new Vector2(-13.5f, 11.5f), new Vector2(13.5f, 11.5f), new Vector2(-13.5f, -7f), new Vector2(13.5f, -7f) };
            for (int i = 0; i < WorkspaceCanvases.Length && i < cells.Length; i++)
            {
                var rot = Quaternion.Euler(-cells[i].y, baseYaw + cells[i].x, 0);
                var pos = head.position + rot * Vector3.forward * 1.0f + Vector3.down * 0.06f;
                WorkspaceCanvases[i].transform.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - head.position, Vector3.up));
            }
            AnchorLayout(head.position);
        }

        void PlaceInFront(Transform t, float distance, float up, float right)
        {
            var head = HeadPose();
            var yaw = YawOnly(head.rotation);
            var pos = head.position + yaw * new Vector3(right, up, distance);
            t.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - head.position, Vector3.up));
        }

        /// <summary>Yaw (degrees, + = right) and pitch (+ = up) of a world point around the head, relative to the head's heading.</summary>
        static Vector2 HeadAngles(Pose head, Vector3 point, out float distance)
        {
            var local = Quaternion.Inverse(YawOnly(head.rotation)) * (point - head.position);
            distance = local.magnitude;
            float flat = new Vector2(local.x, local.z).magnitude;
            return new Vector2(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, Mathf.Atan2(local.y, Mathf.Max(flat, 1e-4f)) * Mathf.Rad2Deg);
        }

        static Vector3 FromHeadAngles(Pose head, float yaw, float pitch, float distance)
            => head.position + YawOnly(head.rotation) * (Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward) * distance;

        void PlaceForSelection(CursivisSelection sel)
        {
            // The orb hugs the edge of the selection that faces the centre of the view, slightly in front of it, so the
            // selection, the orb and its chip ring stay inside a comfortable +-30 degree cone.
            var head = HeadPose();
            var a = HeadAngles(head, sel.Center, out var dist);
            float halfAngle = Mathf.Atan2(sel.WorldWidth * 0.5f, Mathf.Max(dist, 0.05f)) * Mathf.Rad2Deg;
            float side = a.x > 3f ? -1f : 1f;
            float orbYaw = Mathf.Clamp(a.x + side * Mathf.Max(halfAngle - 2f, 3f), -24f, 24f);
            float orbPitch = Mathf.Clamp(a.y - 2f, -24f, 6f);
            float orbDist = Mathf.Clamp(dist * 0.62f, 0.5f, 0.66f);
            _orbTarget = FromHeadAngles(head, orbYaw, orbPitch, orbDist);
            _orbRotTarget = Quaternion.LookRotation(_orbTarget - head.position, Vector3.up);
        }

        void PlaceCard()
        {
            if (_card.Visible && _pinned) return;
            // The card goes on the far side of the orb from the selection (so the selection stays visible), clamped
            // near the centre of the view: the card is ~38 degrees wide at 0.8 m.
            var head = HeadPose();
            float orbYaw = HeadAngles(head, _orbTarget, out _).x;
            float cardYaw = _orbHome ? orbYaw - 22f : (orbYaw >= 0f ? orbYaw - 21f : orbYaw + 21f);
            cardYaw = Mathf.Clamp(cardYaw, -18f, 18f);
            var pos = FromHeadAngles(head, cardYaw, 0f, 0.8f) + Vector3.down * 0.05f;
            ResultRoot.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - head.position, Vector3.up));
        }

        // Seated layout safety net: if the head ends up far from where the layout was built (the user moved, a
        // recenter, or a simulator pose jump), carry the head-relative UI along so it stays within reach.
        Vector3 _layoutAnchor;
        bool _layoutAnchored;

        void AnchorLayout(Vector3 headPosition)
        {
            _layoutAnchor = headPosition;
            _layoutAnchored = true;
        }

        void FollowHeadTranslation(Pose head)
        {
            if (!_layoutAnchored) return;
            var delta = head.position - _layoutAnchor;
            if (delta.sqrMagnitude < 0.45f * 0.45f) return;
            _layoutAnchor = head.position;
            if (WorkspaceCanvases != null) foreach (var c in WorkspaceCanvases) if (c != null) c.transform.position += delta;
            if (_orbHome) RecenterHome(false); // re-derives the home orb and toast from the current head pose
            else
            {
                OrbCanvas.transform.position += delta;
                _orbTarget += delta;
                _homeAnchor += delta;
                if (ToastCanvas != null) ToastCanvas.transform.position += delta;
            }
            ResultRoot.position += delta;
            if (ChipCanvas != null) ChipCanvas.transform.position += delta;
            if (OnboardingCanvas != null) OnboardingCanvas.transform.position += delta;
            if (_selection != null && _selection.Source == SelectionSource.Workspace && _selection.WorldCorners != null)
                for (int i = 0; i < _selection.WorldCorners.Length; i++) _selection.WorldCorners[i] += delta;
            CursivisMetrics.Count("layout_follow");
            if (Debug.isDebugBuild) Debug.Log($"[CursivisDiag] layout followed head by ({delta.x:0.00},{delta.y:0.00},{delta.z:0.00})");
        }

        void LateUpdate()
        {
            if (_pendingFeedbackMeasure)
            {
                _pendingFeedbackMeasure = false;
                CursivisMetrics.Since("trigger", "trigger_feedback_ms");
            }
            var head = HeadPose();
            FollowHeadTranslation(head);
            if (_orbHome && Flow.State != FlowState.VoiceListening)
            {
                // lazy follow: re-center when the user turns more than 30 degrees or moves 35 cm
                float yaw = YawOnly(head.rotation).eulerAngles.y;
                if (Mathf.Abs(Mathf.DeltaAngle(yaw, _homeYaw)) > 30f || Vector3.Distance(head.position, _homeAnchor) > 0.35f) RecenterHome(false);
            }
            var t = OrbCanvas.transform;
            float k = 1f - Mathf.Exp(-Time.deltaTime * 7f);
            t.position = Vector3.Lerp(t.position, _orbTarget, k);
            t.rotation = Quaternion.Slerp(t.rotation, _orbRotTarget, k);
            if (!_pinned && _card.Visible)
            {
                var dir = ResultRoot.position - head.position;
                var fwd = YawOnly(head.rotation) * Vector3.forward;
                if (Vector3.Angle(new Vector3(dir.x, 0, dir.z), fwd) > 35f)
                {
                    var target = head.position + Quaternion.Euler(0, -18f, 0) * fwd * 0.78f + Vector3.down * 0.06f;
                    ResultRoot.position = Vector3.Lerp(ResultRoot.position, target, 1f - Mathf.Exp(-Time.deltaTime * 3f));
                    ResultRoot.rotation = Quaternion.Slerp(ResultRoot.rotation, Quaternion.LookRotation(ResultRoot.position - head.position, Vector3.up), 1f - Mathf.Exp(-Time.deltaTime * 3f));
                }
            }
            if (Flow.State == FlowState.SelectionReady && Time.frameCount % 10 == 0) UpdateCameraFrame();
            _hud.Orb.VoiceLevel = _voice.IsRecording ? _voice.Level : 0f;
            MonitorTracking();
            if (Debug.isDebugBuild && Time.realtimeSinceStartup - _lastDiag > 3f) LogDiagnostics("tick");
        }

        float _lastDiag;

        /// <summary>Development builds: where things are relative to the head (for simulator/automation debugging).</summary>
        public void LogDiagnostics(string reason)
        {
            _lastDiag = Time.realtimeSinceStartup;
            var head = HeadPose();
            var fwd = head.rotation * Vector3.forward;
            string Describe(Transform t)
            {
                if (t == null) return "null";
                var d = t.position - head.position;
                return $"{(t.gameObject.activeInHierarchy ? "on" : "off")} d={d.magnitude:0.00} ang={Vector3.Angle(fwd, d):0}";
            }
            // rig = OVRCameraRig root (moves only through locomotion), local = head inside the tracking space (runtime pose)
            string rigInfo = _rig == null ? "" : $"rig=({_rig.transform.position.x:0.00},{_rig.transform.position.y:0.00},{_rig.transform.position.z:0.00}) " +
                (_rig.trackingSpace != null && Head != null ? $"local=({_rig.trackingSpace.InverseTransformPoint(Head.position).x:0.00},{_rig.trackingSpace.InverseTransformPoint(Head.position).y:0.00},{_rig.trackingSpace.InverseTransformPoint(Head.position).z:0.00}) " : "");
            Debug.Log($"[CursivisDiag] {reason} state={Flow.State} head=({head.position.x:0.00},{head.position.y:0.00},{head.position.z:0.00}) yaw={head.rotation.eulerAngles.y:0} {rigInfo}" +
                      $"orb[{Describe(OrbCanvas.transform)}] ws0[{Describe(WorkspaceCanvases != null && WorkspaceCanvases.Length > 0 ? WorkspaceCanvases[0].transform : null)}] " +
                      $"card[{(Card != null && Card.Visible ? "visible" : "hidden")} {Describe(ResultRoot)}] fps={(CursivisMetrics.Instance != null ? CursivisMetrics.Instance.CurrentFps : 0f):0.0}");
        }

        void MonitorTracking()
        {
            bool anyInput = OVRInput.IsControllerConnected(OVRInput.Controller.Hands) || OVRInput.IsControllerConnected(OVRInput.Controller.LTouch) || OVRInput.IsControllerConnected(OVRInput.Controller.RTouch);
#if UNITY_EDITOR || UNITY_STANDALONE
            anyInput = true; // simulator and editor drive input through the runtime; OVRInput may not report it
#endif
            if (anyInput) { _noTrackingSince = -1f; return; }
            if (_noTrackingSince < 0f) _noTrackingSince = Time.time;
            if (Time.time - _noTrackingSince > 1.2f)
            {
                if (Flow.State == FlowState.Selecting) { _snip.ResetDrawing(); Flow.Go(FlowState.SelectionReady, "tracking lost"); Toast("Hands not visible. Raise your hand and pinch-drag again."); }
                if (Flow.State == FlowState.VoiceListening && _talkHolding) StopTalkAndSend(StopReason.Released);
                _noTrackingSince = Time.time + 5f;
            }
        }

        // ------------------------------------------------------------------ infrastructure
        CancellationToken NewRequestToken()
        {
            _requestCts?.Cancel();
            _requestCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetimeCts.Token);
            return _requestCts.Token;
        }

        void CancelRequests()
        {
            _requestCts?.Cancel();
            _requestCts = null;
        }

        void SetOnline(bool online)
        {
            if (Online == online) return;
            Online = online;
            if (!online) Toast("Cursivis is offline. Check the internet connection.");
            else Toast("Back online.", false);
        }

        async Task HealthLoop(CancellationToken ct)
        {
            if (!_config.IsBackendConfigured)
            {
                Online = false;
                _hud.Orb.SetMood(OrbMood.Error, "No AI backend configured");
                Toast("This build has no AI backend configured.");
                return;
            }
            while (!ct.IsCancellationRequested)
            {
                bool ok;
                try { ok = await _api.HealthAsync(ct); }
                catch (OperationCanceledException) { return; }
                SetOnline(ok);
                try { await Task.Delay(ok ? 60000 : 12000, ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) return;
            // headset removed / system menu: stop camera, microphone, speech and any in-flight request
            CancelRequests();
            _voice.Cancel();
            _speech.Stop();
            if (Flow.State == FlowState.SelectionReady || Flow.State == FlowState.Selecting) { EndSnipMode(); Flow.Go(FlowState.Idle, "paused"); }
            if (Flow.State == FlowState.VoiceListening) ReturnFromTalk();
            if (Flow.State == FlowState.Processing || Flow.State == FlowState.Understanding || Flow.State == FlowState.FollowUp)
            {
                _card.ShowError(_lastAction?.label ?? "Cursivis", "Paused before the answer arrived. Tap Retry.", _retry != null);
                Flow.Go(FlowState.ResultVisible, "paused");
            }
        }

        // ------------------------------------------------------------------ automation hooks (test harness only)
        public void AutomationSelectWorkspaceText(int index) => _workspace.Texts[index].SelectAll();
        public void AutomationSelectPhoto(Rect? uv = null) { if (uv.HasValue) _workspace.Images[0].SelectUv(uv.Value); else _workspace.Images[0].SelectAll(); }
        public Task AutomationTalkTyped(string instruction, bool context) => TalkTypedAsync(instruction, context);
        public Task AutomationBeginSnip() => BeginSnipAsync();
        public void AutomationReadAloud() => ToggleReadAloud();
        public bool IsSpeaking => _speech.IsSpeaking;
        public bool IsAudioPlaying => _speech.Source != null && _speech.Source.isPlaying;
        public void AutomationDismissOnboarding() { if (_onboarding.Visible) CloseOnboarding(); }
        public void AutomationTalkPress(bool context) => OnTalkPressed(context);
        public void AutomationTalkRelease() => OnTalkReleased();
    }
}
