// Headless, repeatable project configuration for Cursivis Spatial.
// Run:  Unity.exe -batchmode -projectPath <proj> -executeMethod Cursivis.Spatial.EditorTools.ProjectSetup.RunBatch -logFile <log>
// (no -quit: the method exits the editor itself once the async Meta Project Setup Tool fixes complete)
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace Cursivis.Spatial.EditorTools
{
    public static class ProjectSetup
    {
        public const string PackageName = "com.cursivis.spatial";
        public const string ProductName = "Cursivis Spatial";
        public const string Version = "1.0.0";
        const string SettingsDir = "Assets/Cursivis/Settings";
        const string MetaFeatureSetId = "com.meta.openxr.featureset.metaxr";

        // Features we want enabled for each target, matched by OpenXR feature id (exact) or type name (fallback).
        static readonly string[] AndroidFeatureIds =
        {
            "com.unity.openxr.feature.metaquest",            // Meta Quest Support (OpenXR 1.13+; replaces the deprecated "oculusquest" feature)
            "com.meta.openxr.feature.metaxr",                // Meta XR Feature (Core SDK)
            "com.meta.openxr.feature.foveation",
            "com.unity.openxr.feature.input.handtracking",   // XR Hands
            "com.unity.openxr.feature.input.metahandtrackingaim",
            "com.unity.openxr.feature.input.handinteraction",
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.input.metaquestplus",
            "com.unity.openxr.feature.input.metaquestpro",
        };
        static readonly string[] DeprecatedFeatureIds = { "com.unity.openxr.feature.oculusquest" };

        static readonly string[] StandaloneFeatureIds =
        {
            "com.meta.openxr.feature.metaxr",
            "com.unity.openxr.feature.input.handtracking",
            "com.unity.openxr.feature.input.metahandtrackingaim",
            "com.unity.openxr.feature.input.handinteraction",
            "com.unity.openxr.feature.input.oculustouch",
            "com.unity.openxr.feature.input.metaquestplus",
        };

        [MenuItem("Cursivis/Setup/Configure Project (all targets)")]
        public static void ConfigureMenu() => ConfigureCore();

        public static void RunBatch()
        {
            try
            {
                ConfigureCore();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(2);
                return;
            }
            // The Meta Project Setup Tool processes its queue on editor ticks, so keep the editor alive.
            RunUpstThenExit();
        }

        static void ConfigureCore()
        {
            Log("Configuring player settings");
            ConfigurePlayer();
            Log("Configuring URP");
            ConfigureUrp();
            Log("Configuring XR plug-in management (OpenXR) for Standalone + Android");
            ConfigureXr(BuildTargetGroup.Standalone, StandaloneFeatureIds);
            ConfigureXr(BuildTargetGroup.Android, AndroidFeatureIds);
            AssetDatabase.SaveAssets();
            Log("Core configuration complete");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Cursivis";
            PlayerSettings.productName = ProductName;
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, PackageName);
            PlayerSettings.Android.bundleVersionCode = Math.Max(PlayerSettings.Android.bundleVersionCode, 1);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            // Meta XR Simulator accepts D3D11/D3D12/Vulkan. Ship all three so the player can be launched with
            // -force-vulkan / -force-d3d12 when a GPU driver has trouble with the simulator's D3D11 interop.
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Direct3D12 });
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.runInBackground = true; // Windows simulator builds keep running when the sim window has focus
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.visibleInBackground = true;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 4;

            // Active input handling = Both (Interaction SDK + Input System packages coexist).
            var ps = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
            if (ps != null)
            {
                var so = new SerializedObject(ps);
                var prop = so.FindProperty("activeInputHandler");
                if (prop != null && prop.intValue != 2)
                {
                    prop.intValue = 2;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Log("activeInputHandler set to Both (takes effect after editor restart)");
                }
            }
        }

        static void ConfigureUrp()
        {
            Directory.CreateDirectory(SettingsDir);
            var rendererPath = $"{SettingsDir}/CursivisURP_Renderer.asset";
            var assetPath = $"{SettingsDir}/CursivisURP_Quest.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(assetPath);
            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(urp, assetPath);
            }
            // Quest-friendly defaults: MSAA 4x, no HDR, no depth/opaque textures, no shadows, no post.
            urp.msaaSampleCount = 4;
            urp.supportsHDR = false;
            urp.supportsCameraDepthTexture = false;
            urp.supportsCameraOpaqueTexture = false;
            urp.shadowDistance = 0f;
            urp.renderScale = 1f;
            EditorUtility.SetDirty(urp);

            GraphicsSettings.defaultRenderPipeline = urp;
            var names = QualitySettings.names;
            for (int i = 0; i < names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = urp;
            }
            QualitySettings.SetQualityLevel(names.Length - 1, false);
        }

        /// <summary>
        /// Called by CursivisBuild.BuildAndroid: re-applies the Android OpenXR feature selection, then runs Unity's
        /// OpenXR project validation, applies every automatic fix and logs anything that is left.
        /// Returns the number of remaining validation errors.
        /// </summary>
        public static int PrepareAndroidXr()
        {
            ConfigureXr(BuildTargetGroup.Android, AndroidFeatureIds);
            AssetDatabase.SaveAssets();
            var issues = new System.Collections.Generic.List<OpenXRFeature.ValidationRule>();
            for (int pass = 0; pass < 3; pass++)
            {
                issues.Clear();
                UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
                if (issues.Count == 0) break;
                bool fixedAny = false;
                foreach (var rule in issues)
                {
                    Log($"[Android] OpenXR validation {(rule.error ? "ERROR" : "warning")}: {rule.message}{(rule.fixItAutomatic && rule.fixIt != null ? " -> auto-fix" : "")}");
                    if (rule.fixItAutomatic && rule.fixIt != null)
                    {
                        try { rule.fixIt(); fixedAny = true; }
                        catch (Exception e) { Log($"[Android]   fix failed: {e.Message}"); }
                    }
                }
                AssetDatabase.SaveAssets();
                if (!fixedAny) break;
            }
            issues.Clear();
            UnityEditor.XR.OpenXR.OpenXRProjectValidation.GetCurrentValidationIssues(issues, BuildTargetGroup.Android);
            int errors = issues.Count(r => r.error);
            foreach (var rule in issues) Log($"[Android] OpenXR validation remaining ({(rule.error ? "ERROR" : "warning")}): {rule.message}");
            return errors;
        }

        static void ConfigureXr(BuildTargetGroup group, string[] wantedFeatureIds)
        {
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
            {
                Directory.CreateDirectory("Assets/XR");
                perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perTarget, true);
            }
            if (!perTarget.HasSettingsForBuildTarget(group)) perTarget.CreateDefaultSettingsForBuildTarget(group);
            if (!perTarget.HasManagerSettingsForBuildTarget(group)) perTarget.CreateDefaultManagerSettingsForBuildTarget(group);
            var general = perTarget.SettingsForBuildTarget(group);
            general.InitManagerOnStart = true;
            var assigned = XRPackageMetadataStore.AssignLoader(general.AssignedSettings, typeof(OpenXRLoader).FullName, group);
            Log($"[{group}] OpenXR loader assigned: {assigned}; loaders = {string.Join(",", general.AssignedSettings.activeLoaders.Select(l => l.name))}");
            EditorUtility.SetDirty(general);
            EditorUtility.SetDirty(perTarget);

            FeatureHelpers.RefreshFeatures(group);
            var featureSet = OpenXRFeatureSetManager.GetFeatureSetWithId(group, MetaFeatureSetId);
            if (featureSet != null)
            {
                featureSet.isEnabled = true;
                OpenXRFeatureSetManager.SetFeaturesFromEnabledFeatureSets(group);
                Log($"[{group}] Meta XR feature set enabled");
            }
            else
            {
                Log($"[{group}] Meta XR feature set NOT found");
            }

            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null)
            {
                Log($"[{group}] OpenXRSettings missing!");
                return;
            }
            foreach (var feature in settings.GetFeatures())
            {
                var id = GetFeatureId(feature);
                bool wanted = wantedFeatureIds.Contains(id) || (group == BuildTargetGroup.Android && feature.GetType().Name == "MetaQuestFeature");
                if (wanted && !feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                }
                else if (DeprecatedFeatureIds.Contains(id) && feature.enabled)
                {
                    // The OpenXR build validator fails Android builds while the deprecated Oculus Quest feature is on.
                    feature.enabled = false;
                    EditorUtility.SetDirty(feature);
                }
            }
            if (group == BuildTargetGroup.Android)
            {
                settings.renderMode = OpenXRSettings.RenderMode.MultiPass == settings.renderMode
                    ? OpenXRSettings.RenderMode.SinglePassInstanced
                    : settings.renderMode;
            }
            EditorUtility.SetDirty(settings);
            Log($"[{group}] enabled OpenXR features: {string.Join(", ", settings.GetFeatures().Where(f => f.enabled).Select(GetFeatureId))}");
        }

        static string GetFeatureId(OpenXRFeature feature)
        {
            // OpenXRFeature exposes featureIdInternal (internal); read it via serialization to stay on public API.
            var so = new SerializedObject(feature);
            var p = so.FindProperty("featureIdInternal");
            return p != null ? p.stringValue : feature.GetType().FullName;
        }

        static Task _upstTask;
        static int _upstStage;
        static double _stageStarted;

        static void RunUpstThenExit()
        {
            _upstStage = 0;
            _stageStarted = EditorApplication.timeSinceStartup;
            EditorApplication.update += UpstTick;
        }

        static void UpstTick()
        {
            try
            {
                if (_upstTask == null)
                {
                    var group = _upstStage == 0 ? BuildTargetGroup.Standalone : BuildTargetGroup.Android;
                    Log($"Running Meta Project Setup Tool FixAll for {group}");
                    _upstTask = OVRProjectSetup.FixAllAsync(group);
                    _stageStarted = EditorApplication.timeSinceStartup;
                    return;
                }
                var timedOut = EditorApplication.timeSinceStartup - _stageStarted > 240;
                if (!_upstTask.IsCompleted && !timedOut) return;
                Log($"FixAll stage {_upstStage} finished (completed={_upstTask.IsCompleted}, faulted={_upstTask.IsFaulted}, timedOut={timedOut})");
                if (_upstTask.IsFaulted) Debug.LogException(_upstTask.Exception);
                _upstTask = null;
                _upstStage++;
                if (_upstStage < 2) return;
                EditorApplication.update -= UpstTick;
                // Re-apply our explicit choices in case a fix changed them, then persist.
                ConfigureCore();
                AssetDatabase.SaveAssets();
                Log("Project setup finished. Exiting.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.update -= UpstTick;
                EditorApplication.Exit(3);
            }
        }

        static void Log(string msg) => Debug.Log($"[CursivisSetup] {msg}");
    }
}
