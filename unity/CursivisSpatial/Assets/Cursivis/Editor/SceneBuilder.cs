// Builds the Cursivis Spatial scene from code (repeatable, reviewable, no manual editor work):
// OVRCameraRig + Interaction SDK comprehensive rig, passthrough underlay, MRUK passthrough camera access,
// world-space canvases wired with Interaction SDK ray/poke (QuickActionsAPI), grabbable result card, app root.
// Run: Unity.exe -batchmode -projectPath <proj> -executeMethod Cursivis.Spatial.EditorTools.SceneBuilder.BuildBatch -quit
using System;
using System.IO;
using System.Linq;
using Cursivis.Spatial.App;
using Meta.XR;
using Meta.XR.EnvironmentDepth;
using Oculus.Interaction;
using Oculus.Interaction.Editor.QuickActions;
using Oculus.Interaction.OVR.Editor.QuickActions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Cursivis.Spatial.EditorTools
{
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/Cursivis/Scenes/CursivisSpatial.unity";
        const string FontSrc = "Assets/Cursivis/Fonts";
        const string FontDst = "Assets/Cursivis/Resources/Fonts & Materials";

        [MenuItem("Cursivis/Setup/Build Scene")]
        public static void BuildMenu() => Build();

        public static void BuildBatch()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(4);
            }
        }

        public static void Build()
        {
            Log("Preparing assets");
            EnsureTmpEssentials();
            EnsureFonts();
            EnsureWorkspaceAssets();
            ConfigureOvrProject();

            Log("Creating scene");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = Color.white;

            var rigObjects = OVRQuickActionsAPI.AddOVRInteractionRig(false);
            var cameraRig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>();
            if (cameraRig == null) throw new Exception("OVRCameraRig was not created");
            var manager = cameraRig.GetComponent<OVRManager>() ?? cameraRig.gameObject.AddComponent<OVRManager>();
            manager.isInsightPassthroughEnabled = true;
            // Core SDK 209 always composites OVRPassthroughLayer as an underlay.
            var pass = cameraRig.gameObject.GetComponent<OVRPassthroughLayer>() ?? cameraRig.gameObject.AddComponent<OVRPassthroughLayer>();
            pass.textureOpacity = 1f;
            Log($"rig created ({rigObjects.Count} objects), passthrough underlay added");

            var center = cameraRig.centerEyeAnchor;
            var cam = center.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 50f;
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = false;
            camData.antialiasing = AntialiasingMode.None;
            camData.renderShadows = false;
            var listener = center.GetComponent<AudioListener>() ?? center.gameObject.AddComponent<AudioListener>();
            listener.enabled = true;

            // Passthrough Camera API (MRUK). Disabled until the user arms a snip.
            var pcaGo = new GameObject("PassthroughCamera");
            var pca = pcaGo.AddComponent<PassthroughCameraAccess>();
            pca.CameraPosition = PassthroughCameraAccess.CameraPositionType.Left;
            pca.RequestedResolution = new Vector2Int(1280, 960);
            pca.enabled = false;

            // Depth API raycasts to place the snip surface on real surfaces (enabled only while snipping).
            var depthGo = new GameObject("EnvironmentDepth");
            var depthMgr = depthGo.AddComponent<EnvironmentDepthManager>();
            depthMgr.enabled = false;
            var envRay = depthGo.AddComponent<EnvironmentRaycastManager>();
            envRay.enabled = false;

            // App root and canvases
            var root = new GameObject("Cursivis");
            var app = root.AddComponent<CursivisApp>();
            app.Head = center;
            app.CameraAccess = pca;
            app.EnvironmentRaycast = envRay;

            app.OrbCanvas = MakeCanvas(root.transform, "OrbCanvas", new Vector2(500, 330), ray: true, poke: true);
            app.ChipCanvas = MakeCanvas(root.transform, "ChipCanvas", new Vector2(380, 110), ray: true, poke: true);
            app.SnipCanvas = MakeCanvas(root.transform, "SnipCanvas", new Vector2(1600, 1100), ray: true, poke: false);
            app.OnboardingCanvas = MakeCanvas(root.transform, "OnboardingCanvas", new Vector2(600, 380), ray: true, poke: true);
            app.ToastCanvas = MakeCanvas(root.transform, "ToastCanvas", new Vector2(460, 54), ray: false, poke: false);
            app.WorkspaceCanvases = new[]
            {
                MakeCanvas(root.transform, "WS_Research", new Vector2(400, 300), true, true),
                MakeCanvas(root.transform, "WS_Spanish", new Vector2(400, 300), true, true),
                MakeCanvas(root.transform, "WS_Photo", new Vector2(400, 300), true, true),
                MakeCanvas(root.transform, "WS_Code", new Vector2(400, 300), true, true)
            };

            // Result card: grabbable root holding the canvas and a grab handle under the card
            var resultRoot = new GameObject("ResultRoot").transform;
            resultRoot.SetParent(root.transform, false);
            app.ResultRoot = resultRoot;
            app.ResultCanvas = MakeCanvas(resultRoot, "ResultCanvas", new Vector2(540, 430), ray: true, poke: true);
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "GrabHandle";
            handle.transform.SetParent(resultRoot, false);
            handle.transform.localPosition = new Vector3(0, -0.208f, 0.002f);
            handle.transform.localScale = new Vector3(0.14f, 0.026f, 0.012f);
            UnityEngine.Object.DestroyImmediate(handle.GetComponent<MeshRenderer>());
            UnityEngine.Object.DestroyImmediate(handle.GetComponent<MeshFilter>());
            var grabObjects = QuickActionsAPI.AddRayGrabInteraction(handle);
            grabObjects.AddRange(QuickActionsAPI.AddGrabInteraction(handle));
            foreach (var g in handle.GetComponentsInChildren<Grabbable>(true).Concat(handle.GetComponents<Grabbable>()))
            {
                g.InjectOptionalTargetTransform(resultRoot);
                g.InjectOptionalKinematicWhileSelected(true);
                EditorUtility.SetDirty(g);
            }
            foreach (var rb in handle.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            Log($"grab interaction added ({grabObjects.Count} objects)");

            // Frozen snip preview quad
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "SnipPreview";
            quad.transform.SetParent(root.transform, false);
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetFloat("_Surface", 0);
            mat.name = "SnipPreview";
            Directory.CreateDirectory("Assets/Cursivis/Materials");
            AssetDatabase.CreateAsset(mat, "Assets/Cursivis/Materials/SnipPreview.mat");
            quad.GetComponent<MeshRenderer>().sharedMaterial = mat;
            quad.SetActive(false);
            app.PreviewQuad = quad.GetComponent<MeshRenderer>();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Log($"Scene saved: {ScenePath}");
        }

        static Canvas MakeCanvas(Transform parent, string name, Vector2 size, bool ray, bool poke)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * 0.001f; // 1 canvas unit = 1 mm
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 |
                                              AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Tangent | AdditionalCanvasShaderChannels.Normal;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 4;
            scaler.referencePixelsPerUnit = 100;
            go.AddComponent<GraphicRaycaster>();
            if (ray) QuickActionsAPI.AddRayCanvasInteraction(go, true);
            if (poke) QuickActionsAPI.AddPokeCanvasInteraction(go, true);
            return canvas;
        }

        static void ConfigureOvrProject()
        {
            var cfg = OVRProjectConfig.CachedProjectConfig;
            cfg.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
            cfg.handTrackingFrequency = OVRProjectConfig.HandTrackingFrequency.HIGH;
            cfg.insightPassthroughEnabled = true;
            cfg.isPassthroughCameraAccessEnabled = true;
            cfg.sceneSupport = OVRProjectConfig.FeatureSupport.Supported;
            cfg.focusAware = true;
            cfg.targetDeviceTypes = new System.Collections.Generic.List<OVRProjectConfig.DeviceType> { OVRProjectConfig.DeviceType.Quest3, OVRProjectConfig.DeviceType.Quest3S };
            OVRProjectConfig.CommitProjectConfig(cfg);
        }

        static void EnsureTmpEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return;
            var pkg = Directory.GetFiles("Packages/com.unity.ugui", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault()
                      ?? Directory.GetFiles(Path.GetFullPath("Library/PackageCache"), "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
            if (pkg == null) throw new Exception("TMP Essential Resources package not found");
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            Log("TMP Essential Resources imported");
        }

        static void EnsureFonts()
        {
            Directory.CreateDirectory(FontDst);
            var latinExt = MakeFont("Inter-LatinExt-Regular", null);
            var regular = MakeFont("Inter-Regular", latinExt);
            MakeFont("Inter-SemiBold", latinExt);
            MakeFont("Inter-Bold", latinExt);
            MakeFont("IBMPlexMono-Regular", regular);
            MakeFont("MaterialIcons-Regular", null, 64);
            // default font for any TMP text that doesn't set one explicitly
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null && regular != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultFontAsset").objectReferenceValue = regular;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
        }

        static TMP_FontAsset MakeFont(string name, TMP_FontAsset fallback, int sampling = 72)
        {
            var path = $"{FontDst}/{name} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontSrc}/{name}.ttf");
            if (font == null) throw new Exception($"Font not found: {name}.ttf");
            var asset = TMP_FontAsset.CreateFontAsset(font, sampling, 7, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = $"{name} SDF";
            if (fallback != null) asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { fallback };
            AssetDatabase.CreateAsset(asset, path);
            foreach (var tex in asset.atlasTextures)
            {
                tex.name = $"{name} Atlas";
                AssetDatabase.AddObjectToAsset(tex, asset);
            }
            asset.material.name = $"{name} Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            Log($"font asset created: {path}");
            return asset;
        }

        static void EnsureWorkspaceAssets()
        {
            const string dir = "Assets/Cursivis/Resources/Workspace";
            Directory.CreateDirectory(dir);
            var src = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../backend/tests/fixtures/photo-footbridge.jpg"));
            var dst = $"{dir}/photo-footbridge.jpg";
            if (!File.Exists(dst) && File.Exists(src))
            {
                File.Copy(src, dst);
                File.Copy(src + ".credit.json", $"{dir}/photo-footbridge.credit.json", true);
                AssetDatabase.ImportAsset(dst);
            }
            var importer = AssetImporter.GetAtPath(dst) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        static void Log(string msg) => Debug.Log($"[CursivisScene] {msg}");
    }
}
