// Reproducible builds.
//   Windows (Meta XR Simulator): -executeMethod Cursivis.Spatial.EditorTools.CursivisBuild.BuildWindows -buildTarget StandaloneWindows64
//   Quest APK:                   -executeMethod Cursivis.Spatial.EditorTools.CursivisBuild.BuildAndroid -buildTarget Android
// Environment: CURSIVIS_BACKEND_URL (required for a working build), CURSIVIS_APP_TOKEN (optional), CURSIVIS_BUILD_LABEL.
// The runtime config is written to Assets/Cursivis/Resources/CursivisRuntimeConfig.json (gitignored).
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Cursivis.Spatial.EditorTools
{
    public static class CursivisBuild
    {
        const string ConfigPath = "Assets/Cursivis/Resources/CursivisRuntimeConfig.json";

        [Serializable]
        sealed class RuntimeConfig
        {
            public string backendUrl;
            public string appToken;
            public string buildLabel;
        }

        static string RepoRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));

        static void WriteRuntimeConfig(string label)
        {
            var cfg = new RuntimeConfig
            {
                backendUrl = Environment.GetEnvironmentVariable("CURSIVIS_BACKEND_URL") ?? "",
                appToken = Environment.GetEnvironmentVariable("CURSIVIS_APP_TOKEN") ?? "",
                buildLabel = Environment.GetEnvironmentVariable("CURSIVIS_BUILD_LABEL") ?? label
            };
            if (string.IsNullOrEmpty(cfg.backendUrl)) Debug.LogWarning("[CursivisBuild] CURSIVIS_BACKEND_URL is not set: the build will show 'No AI backend configured'.");
            File.WriteAllText(ConfigPath, JsonUtility.ToJson(cfg, true));
            AssetDatabase.ImportAsset(ConfigPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[CursivisBuild] runtime config: backend={(string.IsNullOrEmpty(cfg.backendUrl) ? "<none>" : new Uri(cfg.backendUrl).Host)} token={(string.IsNullOrEmpty(cfg.appToken) ? "no" : "yes")} label={cfg.buildLabel}");
        }

        public static void BuildWindows()
        {
            GraphicsApiTool.WindowsVulkanFirst();
            Run(BuildTarget.StandaloneWindows64, Path.Combine(RepoRoot, "builds/windows/CursivisSpatial.exe"), "windows-sim");
        }

        public static void BuildAndroid()
        {
            var version = Environment.GetEnvironmentVariable("CURSIVIS_VERSION_CODE");
            if (int.TryParse(version, out var code)) PlayerSettings.Android.bundleVersionCode = code;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false;
            Run(BuildTarget.Android, Path.Combine(RepoRoot, "builds/android/CursivisSpatial.apk"), "quest");
        }

        static void Run(BuildTarget target, string output, string label)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                WriteRuntimeConfig(label);
                if (!File.Exists(SceneBuilder.ScenePath)) SceneBuilder.Build();
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { SceneBuilder.ScenePath },
                    locationPathName = output,
                    target = target,
                    targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                    options = Environment.GetEnvironmentVariable("CURSIVIS_DEVELOPMENT") == "1" ? BuildOptions.Development : BuildOptions.None
                };
                Debug.Log($"[CursivisBuild] building {target} -> {output}");
                var report = BuildPipeline.BuildPlayer(options);
                var s = report.summary;
                Debug.Log($"[CursivisBuild] result={s.result} size={s.totalSize / (1024 * 1024)}MB time={s.totalTime.TotalMinutes:0.0}min errors={s.totalErrors} warnings={s.totalWarnings}");
                foreach (var step in report.steps)
                foreach (var m in step.messages.Where(m => m.type == LogType.Error || m.type == LogType.Exception))
                    Debug.Log($"[CursivisBuild] error: {m.content}");
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 5);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(6);
            }
        }
    }
}
