using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Cursivis.Spatial
{
    // ---------- DTOs (mirror backend/src/app.js responses) ----------
    public sealed class SuggestionDto
    {
        public string id;
        public string label;
        public string intent;
        public string icon;
    }

    public sealed class SuggestResponse
    {
        public string selectionId;
        public string contentType;
        public string title;
        public string summary;
        public string quality;
        public float confidence;
        public List<SuggestionDto> suggestions = new List<SuggestionDto>();
        public string source;
        public string model;
        public int latencyMs;
        public bool cached;
    }

    public sealed class ResultResponse
    {
        public string selectionId;
        public string actionId;
        public string actionLabel;
        public string title;
        public string answer;
        public string evidence;
        public string uncertainty;
        public string uncertaintyNote;
        public List<string> followUps = new List<string>();
        public string model;
        public int latencyMs;
        // talk-only
        public string mode;
        public string transcript;
        public bool heard;
        public string clientAction;
    }

    public sealed class CursivisApiException : Exception
    {
        public readonly long Status;
        public readonly string Code;
        public readonly int RetryAfterSec;
        public readonly bool IsNetwork;

        public CursivisApiException(string message, long status, string code, int retryAfterSec = 0, bool isNetwork = false) : base(message)
        {
            Status = status;
            Code = code;
            RetryAfterSec = retryAfterSec;
            IsNetwork = isNetwork;
        }

        public string UserMessage
        {
            get
            {
                if (IsNetwork) return "Can't reach Cursivis. Check the headset's internet connection and try again.";
                if (Code == "not_configured") return "This build has no AI backend configured.";
                return string.IsNullOrEmpty(Message) ? "Something went wrong. Please try again." : Message;
            }
        }
    }

    /// <summary>
    /// HTTP client for the Cursivis Spatial backend. Async/await on the main thread (UnityWebRequest), cancellable,
    /// with request timeouts. The selection image is sent only when the user explicitly triggers Cursivis.
    /// </summary>
    public sealed class CursivisApi
    {
        readonly CursivisConfig _config;
        static readonly JsonSerializerSettings Json = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        public CursivisApi(CursivisConfig config) { _config = config; }

        public static JObject SelectionJson(CursivisSelection s)
        {
            var o = new JObject
            {
                ["id"] = s.Id,
                ["kind"] = s.Kind == SelectionKind.Text ? "text" : s.Kind == SelectionKind.Image ? "image" : "text_image",
                ["source"] = s.Source == SelectionSource.Camera ? "camera" : "workspace"
            };
            if (!string.IsNullOrEmpty(s.Text)) o["text"] = s.Text;
            if (s.Jpeg != null && s.Jpeg.Length > 0)
            {
                o["image"] = new JObject
                {
                    ["mimeType"] = "image/jpeg",
                    ["base64"] = Convert.ToBase64String(s.Jpeg),
                    ["width"] = s.ImageWidth,
                    ["height"] = s.ImageHeight
                };
            }
            var meta = new JObject();
            if (!string.IsNullOrEmpty(s.Title)) meta["title"] = Truncate(s.Title, 120);
            if (!string.IsNullOrEmpty(s.DocType)) meta["docType"] = Truncate(s.DocType, 40);
            if (meta.Count > 0) o["meta"] = meta;
            return o;
        }

        static JArray HistoryJson(IReadOnlyList<Turn> history)
        {
            var arr = new JArray();
            if (history == null) return arr;
            int start = Math.Max(0, history.Count - 8);
            for (int i = start; i < history.Count; i++)
                arr.Add(new JObject { ["role"] = history[i].Role, ["text"] = Truncate(history[i].Text, 5800) });
            return arr;
        }

        static string Truncate(string s, int n) => string.IsNullOrEmpty(s) || s.Length <= n ? s : s.Substring(0, n);

        public Task<SuggestResponse> SuggestAsync(CursivisSelection s, IReadOnlyList<Turn> history, CancellationToken ct)
        {
            var body = new JObject { ["selection"] = SelectionJson(s) };
            if (history != null && history.Count > 0) body["history"] = HistoryJson(history);
            return PostJsonAsync<SuggestResponse>("/v1/suggest", body, 25, ct);
        }

        public Task<ResultResponse> ActAsync(CursivisSelection s, SuggestionDto action, IReadOnlyList<Turn> history, bool regenerate, string instruction, CancellationToken ct)
        {
            var a = new JObject { ["id"] = action.id, ["label"] = Truncate(action.label, 60) };
            if (!string.IsNullOrEmpty(action.intent)) a["intent"] = Truncate(action.intent, 240);
            if (!string.IsNullOrEmpty(instruction)) a["instruction"] = Truncate(instruction, 600);
            var body = new JObject { ["selection"] = SelectionJson(s), ["action"] = a };
            if (regenerate) body["regenerate"] = true;
            if (history != null && history.Count > 0) body["history"] = HistoryJson(history);
            return PostJsonAsync<ResultResponse>("/v1/act", body, 60, ct);
        }

        public Task<ResultResponse> TalkAsync(CursivisSelection contextSelection, byte[] wav, string typedInstruction, IReadOnlyList<Turn> history, string recentSelectionSummary, CancellationToken ct)
        {
            var body = new JObject { ["mode"] = contextSelection != null ? "context" : "general" };
            if (contextSelection != null) body["selection"] = SelectionJson(contextSelection);
            if (wav != null) body["audio"] = new JObject { ["mimeType"] = "audio/wav", ["base64"] = Convert.ToBase64String(wav) };
            else body["instruction"] = Truncate(typedInstruction, 2000);
            if (history != null && history.Count > 0) body["history"] = HistoryJson(history);
            if (contextSelection == null && !string.IsNullOrEmpty(recentSelectionSummary)) body["recentSelectionSummary"] = Truncate(recentSelectionSummary, 400);
            return PostJsonAsync<ResultResponse>("/v1/talk", body, 60, ct);
        }

        public async Task<byte[]> SpeakAsync(string text, CancellationToken ct)
        {
            var body = new JObject { ["text"] = Truncate(text, 1400) };
            using (var req = BuildRequest("/v1/speak", body, 45))
            {
                await SendAsync(req, ct);
                ThrowIfFailed(req);
                return req.downloadHandler.data;
            }
        }

        public async Task<bool> HealthAsync(CancellationToken ct)
        {
            if (!_config.IsBackendConfigured) return false;
            using (var req = UnityWebRequest.Get(_config.backendUrl + "/v1/health"))
            {
                req.timeout = 8;
                try
                {
                    await SendAsync(req, ct);
                    return req.result == UnityWebRequest.Result.Success;
                }
                catch (OperationCanceledException) { throw; }
                catch { return false; }
            }
        }

        UnityWebRequest BuildRequest(string path, JObject body, int timeoutSec)
        {
            if (!_config.IsBackendConfigured) throw new CursivisApiException("This build has no AI backend configured.", 0, "not_configured");
            var payload = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));
            var req = new UnityWebRequest(_config.backendUrl + path, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(payload) { contentType = "application/json" },
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSec
            };
            req.SetRequestHeader("content-type", "application/json");
            if (!string.IsNullOrEmpty(_config.appToken)) req.SetRequestHeader("x-cursivis-token", _config.appToken);
            req.SetRequestHeader("x-cursivis-install", _config.InstallId);
            req.SetRequestHeader("x-cursivis-build", _config.buildLabel);
            return req;
        }

        async Task<T> PostJsonAsync<T>(string path, JObject body, int timeoutSec, CancellationToken ct)
        {
            // One automatic retry for transient network failures (not for HTTP errors from the backend).
            for (int attempt = 0; ; attempt++)
            {
                using (var req = BuildRequest(path, body, timeoutSec))
                {
                    await SendAsync(req, ct);
                    bool network = req.result == UnityWebRequest.Result.ConnectionError;
                    if (network && attempt == 0)
                    {
                        await Task.Delay(600, ct);
                        continue;
                    }
                    ThrowIfFailed(req);
                    try
                    {
                        return JsonConvert.DeserializeObject<T>(req.downloadHandler.text, Json);
                    }
                    catch (Exception e)
                    {
                        throw new CursivisApiException("Cursivis received an unexpected response.", req.responseCode, "bad_response: " + e.GetType().Name);
                    }
                }
            }
        }

        static Task SendAsync(UnityWebRequest req, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            CancellationTokenRegistration reg = default;
            reg = ct.Register(() =>
            {
                try { req.Abort(); } catch { /* already disposed */ }
                tcs.TrySetCanceled();
            });
            op.completed += _ =>
            {
                reg.Dispose();
                tcs.TrySetResult(true);
            };
            return tcs.Task;
        }

        static void ThrowIfFailed(UnityWebRequest req)
        {
            if (req.result == UnityWebRequest.Result.Success) return;
            if (req.result == UnityWebRequest.Result.ConnectionError)
                throw new CursivisApiException(req.error, 0, "network", 0, true);
            string message = null, code = "http_" + req.responseCode;
            int retry = 0;
            try
            {
                var text = req.downloadHandler?.text;
                if (!string.IsNullOrEmpty(text) && text.TrimStart().StartsWith("{"))
                {
                    var err = JObject.Parse(text)["error"];
                    message = (string)err?["message"];
                    code = (string)err?["code"] ?? code;
                    retry = (int?)err?["retryAfterSec"] ?? 0;
                }
            }
            catch { /* non-JSON error body */ }
            if (req.responseCode == 0 || req.error == "Request timeout")
                throw new CursivisApiException("Cursivis took too long to respond. Please retry.", 0, "timeout", 0, true);
            throw new CursivisApiException(message ?? "The AI service is temporarily unavailable. Please retry.", req.responseCode, code, retry);
        }
    }
}
