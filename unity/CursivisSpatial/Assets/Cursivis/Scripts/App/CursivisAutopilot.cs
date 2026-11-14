using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Cursivis.Spatial.App
{
    /// <summary>
    /// TEST HARNESS ONLY. Runs scripted end-to-end scenarios through the same public app methods the UI calls
    /// (selection -> trigger -> real backend suggestions -> action -> result -> voice/read-aloud), logging each step as
    /// [CursivisAuto] lines and writing persistentDataPath/cursivis-autopilot.json. It never fabricates AI output:
    /// every result comes from the live backend. It does not test gesture recognition (Meta XR Operator /
    /// simulator input does that). Enabled only with --cursivis-autopilot (desktop builds).
    /// </summary>
    public sealed class CursivisAutopilot : MonoBehaviour
    {
        sealed class Step { public string scenario, name, detail; public bool ok; public float ms; }
        readonly List<Step> _steps = new List<Step>();
        CursivisApp _app;
        string _scenario = "all";
        bool _quit;
        float _delay = 6f;  // --cursivis-autopilot-delay=<s>: time to set up simulator input / recording first
        float _pace = 1f;   // --cursivis-autopilot-pace=<x>: stretches the pauses between steps (readable footage)

        WaitForSeconds Pause(float seconds) => new WaitForSeconds(seconds * _pace);

        public static void InstallIfRequested(CursivisApp app)
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            var args = Environment.GetCommandLineArgs();
            if (!args.Contains("--cursivis-autopilot")) return;
            var pilot = app.gameObject.AddComponent<CursivisAutopilot>();
            pilot._app = app;
            foreach (var a in args)
            {
                if (a.StartsWith("--cursivis-scenario=")) pilot._scenario = a.Substring("--cursivis-scenario=".Length);
                if (a == "--cursivis-autopilot-quit") pilot._quit = true;
                if (a.StartsWith("--cursivis-autopilot-delay=") && float.TryParse(a.Substring("--cursivis-autopilot-delay=".Length), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)) pilot._delay = Mathf.Clamp(d, 0f, 600f);
                if (a.StartsWith("--cursivis-autopilot-pace=") && float.TryParse(a.Substring("--cursivis-autopilot-pace=".Length), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var p)) pilot._pace = Mathf.Clamp(p, 0.5f, 5f);
            }
#endif
        }

        IEnumerator Start()
        {
            Debug.Log($"[CursivisAuto] autopilot starting scenario={_scenario} delay={_delay:0}s pace={_pace:0.0}");
            yield return new WaitForSeconds(_delay); // let the XR session reach FOCUSED and the UI settle
            _app.AutomationDismissOnboarding();
            var scenarios = _scenario == "all" ? new[] { "workspace", "photo", "general", "snip" } : _scenario.Split(',');
            foreach (var s in scenarios)
            {
                switch (s)
                {
                    case "workspace": yield return Workspace(); break;
                    case "photo": yield return Photo(); break;
                    case "general": yield return General(); break;
                    case "snip": yield return Snip(); break;
                    case "spanish": yield return Spanish(); break;
                }
                _app.Dismiss();
                yield return Pause(2f);
            }
            WriteReport();
            if (_quit) Application.Quit();
        }

        IEnumerator WaitFor(string scenario, string name, Func<bool> condition, float timeout, Func<string> detail = null)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!condition() && Time.realtimeSinceStartup - t0 < timeout) yield return null;
            var ok = condition();
            var step = new Step { scenario = scenario, name = name, ok = ok, ms = (Time.realtimeSinceStartup - t0) * 1000f, detail = detail?.Invoke() };
            _steps.Add(step);
            Debug.Log($"[CursivisAuto] {scenario}/{name} {(ok ? "ok" : "FAIL")} {step.ms:0}ms {step.detail}");
        }

        string Suggestions() => _app.LastSuggestions == null ? "(none)" : string.Join(" | ", _app.LastSuggestions.suggestions.Select(s => s.label)) + $" [{_app.LastSuggestions.contentType} via {_app.LastSuggestions.model}]";
        string ResultSummary() => _app.LastResult == null ? "(none)" : $"\"{_app.LastResult.title}\" {Trim(_app.LastResult.answer, 140)} (uncertainty={_app.LastResult.uncertainty}, {_app.LastResult.latencyMs}ms via {_app.LastResult.model})";
        static string Trim(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "...").Replace("\n", " ");

        bool State(FlowState s) => _app.Flow.State == s;

        IEnumerator TriggerAndSuggest(string sc)
        {
            yield return WaitFor(sc, "selection_confirmed", () => State(FlowState.SelectionConfirmed), 5f, () => _app.Selection?.Describe());
            _ = _app.TriggerAsync();
            yield return WaitFor(sc, "understanding_shown", () => State(FlowState.Understanding) || State(FlowState.SuggestionsVisible), 2f);
            yield return WaitFor(sc, "suggestions_visible", () => State(FlowState.SuggestionsVisible) && _app.LastSuggestions != null, 30f, Suggestions);
        }

        IEnumerator RunSuggestion(string sc, int index, string label = null)
        {
            var before = _app.LastResult;
            if (label != null && _app.LastSuggestions != null)
            {
                var found = _app.LastSuggestions.suggestions.FindIndex(x => (x.label + " " + x.id).IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);
                if (found >= 0) index = found;
            }
            _ = _app.RunSuggestionAsync(index, false);
            yield return WaitFor(sc, $"result_{index}", () => State(FlowState.ResultVisible) && _app.LastResult != null && _app.LastResult != before, 60f, ResultSummary);
        }

        IEnumerator Workspace()
        {
            const string sc = "workspace";
            _app.AutomationSelectWorkspaceText(0);
            yield return TriggerAndSuggest(sc);
            yield return RunSuggestion(sc, 0, "summar");
            yield return Pause(2.5f);
            _app.Card.ScrollBy(0.6f);
            yield return Pause(1.5f);
            _app.AutomationReadAloud();
            yield return WaitFor(sc, "read_aloud_audio_playing", () => _app.IsAudioPlaying, 25f);
            yield return Pause(4f);
            _app.AutomationReadAloud(); // stop
            yield return WaitFor(sc, "read_aloud_stopped", () => !_app.IsSpeaking, 3f);
            var before = _app.LastResult;
            _ = _app.AutomationTalkTyped("Explain the main finding like I'm 12.", true);
            yield return WaitFor(sc, "follow_up", () => State(FlowState.ResultVisible) && _app.LastResult != before, 60f, ResultSummary);
            UI.CursivisTheme.Toggle();
            yield return Pause(2f);
            UI.CursivisTheme.Toggle();
        }

        IEnumerator Spanish()
        {
            const string sc = "spanish";
            _app.AutomationSelectWorkspaceText(1);
            yield return TriggerAndSuggest(sc);
            yield return RunSuggestion(sc, 0, "translat");
            yield return Pause(3f);
        }

        IEnumerator Photo()
        {
            const string sc = "photo";
            _app.AutomationSelectPhoto();
            yield return TriggerAndSuggest(sc);
            yield return RunSuggestion(sc, 0, "caption");
            yield return Pause(2f);
            var before = _app.LastResult;
            _app.Card.RegenButton.InvokeClick();
            yield return WaitFor(sc, "regenerate", () => State(FlowState.ResultVisible) && _app.LastResult != before, 60f, ResultSummary);
            yield return Pause(2f);
        }

        IEnumerator General()
        {
            const string sc = "general";
            var before = _app.LastResult;
            _ = _app.AutomationTalkTyped("What can you do?", false);
            yield return WaitFor(sc, "general_answer", () => State(FlowState.ResultVisible) && _app.LastResult != before && _app.LastResult.mode == "general", 60f, ResultSummary);
            yield return Pause(2f);
        }

        IEnumerator Snip()
        {
            const string sc = "snip";
            _ = _app.AutomationBeginSnip();
            yield return WaitFor(sc, "snip_armed", () => State(FlowState.SelectionReady) || State(FlowState.Idle), 4f, () => _app.Flow.State.ToString());
            yield return WaitFor(sc, "camera_ready", () => _app.Snip != null && State(FlowState.SelectionReady) && Time.frameCount > 0 && GameObject.FindFirstObjectByType<Meta.XR.PassthroughCameraAccess>() is var pca && pca != null && pca.IsPlaying, 10f,
                () => { var pca = GameObject.FindFirstObjectByType<Meta.XR.PassthroughCameraAccess>(); return pca == null ? "no component" : $"enabled={pca.enabled} playing={pca.IsPlaying} res={pca.CurrentResolution} state={_app.Flow.State}"; });
            if (!State(FlowState.SelectionReady)) yield break;
            _app.Snip.SimulateDrag(new Vector2(-170, -120), new Vector2(170, 120));
            yield return WaitFor(sc, "captured", () => State(FlowState.SelectionConfirmed), 8f, () => _app.Selection?.Describe());
            if (!State(FlowState.SelectionConfirmed)) yield break;
            SavePreview(_app.Selection?.Preview, "snip-preview.png");
            yield return TriggerAndSuggest(sc);
            var before = _app.LastResult;
            _ = _app.AutomationTalkTyped("What is this? Describe what you can see.", true);
            yield return WaitFor(sc, "talk_about_snip", () => State(FlowState.ResultVisible) && _app.LastResult != before, 60f, ResultSummary);
            yield return Pause(2f);
        }

        static void SavePreview(Texture2D tex, string file)
        {
            if (tex == null) return;
            try
            {
                var path = Path.Combine(Application.persistentDataPath, file);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[CursivisAuto] saved {path}");
            }
            catch (Exception e) { Debug.LogWarning($"[CursivisAuto] preview save failed: {e.Message}"); }
        }

        void WriteReport()
        {
            var sb = new StringBuilder();
            sb.Append("{\"when\":\"").Append(DateTime.UtcNow.ToString("o")).Append("\",\"platform\":\"").Append(Application.platform).Append("\",\"steps\":[");
            for (int i = 0; i < _steps.Count; i++)
            {
                var s = _steps[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"scenario\":\"").Append(s.scenario).Append("\",\"name\":\"").Append(s.name).Append("\",\"ok\":").Append(s.ok ? "true" : "false")
                  .Append(",\"ms\":").Append(((int)s.ms).ToString()).Append(",\"detail\":\"").Append((s.detail ?? "").Replace("\\", "/").Replace("\"", "'")).Append("\"}");
            }
            sb.Append("],\"metrics\":").Append(CursivisMetrics.Instance != null ? CursivisMetrics.Instance.Summary() : "{}").Append('}');
            var path = Path.Combine(Application.persistentDataPath, "cursivis-autopilot.json");
            File.WriteAllText(path, sb.ToString());
            int passed = _steps.Count(s => s.ok);
            Debug.Log($"[CursivisAuto] report {passed}/{_steps.Count} steps ok -> {path}");
        }
    }
}
