using System;
using UnityEngine;

namespace Cursivis.Spatial
{
    /// <summary>
    /// Runtime configuration. The backend URL and client token are injected at build time into
    /// Resources/CursivisRuntimeConfig.json (gitignored) by the build pipeline from environment variables.
    /// The client token is not a secret (it ships in the APK); it only lets the backend apply per-app limits.
    /// No Gemini key ever exists on the device.
    /// </summary>
    [Serializable]
    public sealed class CursivisConfig
    {
        public string backendUrl = "";
        public string appToken = "";
        public string buildLabel = "dev";
        public bool autopilot;

        [NonSerialized] public string InstallId;

        static CursivisConfig _instance;

        public static CursivisConfig Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = new CursivisConfig();
                var asset = Resources.Load<TextAsset>("CursivisRuntimeConfig");
                if (asset != null)
                {
                    try { JsonUtility.FromJsonOverwrite(asset.text, _instance); }
                    catch (Exception e) { Debug.LogError($"[Cursivis] invalid runtime config: {e.Message}"); }
                }
#if UNITY_EDITOR || UNITY_STANDALONE
                // Desktop/simulator overrides via environment variables or command line.
                var envUrl = Environment.GetEnvironmentVariable("CURSIVIS_BACKEND_URL");
                if (!string.IsNullOrEmpty(envUrl)) _instance.backendUrl = envUrl;
                var envToken = Environment.GetEnvironmentVariable("CURSIVIS_APP_TOKEN");
                if (!string.IsNullOrEmpty(envToken)) _instance.appToken = envToken;
                foreach (var arg in Environment.GetCommandLineArgs())
                {
                    if (arg.StartsWith("--cursivis-backend=")) _instance.backendUrl = arg.Substring("--cursivis-backend=".Length);
                    if (arg == "--cursivis-autopilot") _instance.autopilot = true;
                }
#endif
                _instance.backendUrl = (_instance.backendUrl ?? "").TrimEnd('/');
                _instance.InstallId = PlayerPrefs.GetString("cursivis.installId", "");
                if (string.IsNullOrEmpty(_instance.InstallId))
                {
                    _instance.InstallId = Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString("cursivis.installId", _instance.InstallId);
                    PlayerPrefs.Save();
                }
                return _instance;
            }
        }

        public bool IsBackendConfigured => !string.IsNullOrEmpty(backendUrl);
    }
}
