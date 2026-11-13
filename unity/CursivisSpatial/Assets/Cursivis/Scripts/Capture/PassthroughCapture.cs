using System;
using System.Threading;
using System.Threading.Tasks;
using Meta.XR;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cursivis.Spatial.Capture
{
    public sealed class CaptureResult
    {
        public byte[] Jpeg;
        public int Width, Height;
        public Texture2D Preview;
        public Rect Viewport;        // normalized camera viewport rect that was captured (bottom-left origin)
        public Vector2Int CameraResolution;
        public float CaptureMs;
    }

    /// <summary>
    /// Physical-world capture through Meta's Passthrough Camera API (MRUK PassthroughCameraAccess).
    /// - The camera runs only while a snip is armed (privacy + battery); nothing is streamed or uploaded continuously.
    /// - The user's rectangle is defined by world-space corners; each corner is projected into the camera image with
    ///   WorldToViewportPoint using the camera pose of the current frame (intrinsics + lens offset), so the crop follows
    ///   the real camera geometry instead of assuming the headset view maps 1:1 to the camera texture.
    /// - The crop is done on the GPU (Blit with scale/offset), read back asynchronously, resized to <= 1024 px and
    ///   JPEG-encoded. The exact crop is shown back to the user as a frozen preview before anything is sent.
    /// </summary>
    public sealed class PassthroughCapture : MonoBehaviour
    {
        public PassthroughCameraAccess Camera;
        public const int MaxSide = 1024;
        public const float ParallaxPad = 0.035f; // viewport padding to absorb eye/camera parallax and depth error

        public bool IsStarting { get; private set; }
        public string LastError { get; private set; }

        public static bool PlatformSupported
        {
            get
            {
                try { return PassthroughCameraAccess.IsSupported; }
                catch (Exception) { return false; }
            }
        }

        public bool IsReady => Camera != null && Camera.enabled && Camera.IsPlaying;

        public bool HasPermission
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.PassthroughCameraAccess);
#else
                return true;
#endif
            }
        }

        public void RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Scene permission enables the Depth API used to place the snip surface on real surfaces (optional).
            OVRPermissionsRequester.Request(new[] { OVRPermissionsRequester.Permission.PassthroughCameraAccess, OVRPermissionsRequester.Permission.Scene });
#endif
        }

        void Awake()
        {
            if (Camera != null) Camera.enabled = false; // never run the camera until the user arms a snip
        }

        /// <summary>Starts the camera and waits until frames arrive. Returns false with LastError on failure.</summary>
        public async Task<bool> StartCameraAsync(CancellationToken ct, float timeoutSec = 6f)
        {
            LastError = null;
            if (Camera == null) { LastError = "Camera component missing."; return false; }
            if (!PlatformSupported)
            {
                LastError = "The passthrough camera isn't available on this device or simulator.";
                return false;
            }
            if (!HasPermission) { LastError = "Camera permission not granted."; return false; }
            if (Camera.IsPlaying) return true;
            IsStarting = true;
            var t0 = Time.realtimeSinceStartup;
            Camera.enabled = true;
            try
            {
                int freshFrames = 0;
                while (Time.realtimeSinceStartup - t0 < timeoutSec)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!Camera.enabled)
                    {
                        // MRUK disables the component when CameraPlay fails (no camera source on this runtime).
                        LastError = "The passthrough camera could not be started on this device or simulator.";
                        return false;
                    }
                    if (Camera.IsPlaying && Camera.IsUpdatedThisFrame) freshFrames++;
                    if (freshFrames >= 3) // first frames can be black right after start
                    {
                        CursivisMetrics.Instance?.Sample("camera_start_ms", (Time.realtimeSinceStartup - t0) * 1000f);
                        Debug.Log($"[CursivisCapture] camera playing {Camera.CurrentResolution.x}x{Camera.CurrentResolution.y} focal={Camera.Intrinsics.FocalLength} pp={Camera.Intrinsics.PrincipalPoint} sensor={Camera.Intrinsics.SensorResolution}");
                        return true;
                    }
                    await Task.Yield();
                }
                LastError = "The passthrough camera didn't start in time.";
                return false;
            }
            finally
            {
                IsStarting = false;
            }
        }

        public void StopCamera()
        {
            if (Camera != null && Camera.enabled) Camera.enabled = false;
        }

        /// <summary>Projects the four camera-image corners onto a plane so the UI can show what the camera can see.</summary>
        public bool TryGetCameraFrameOnPlane(Plane plane, out Vector3[] worldCorners)
        {
            worldCorners = null;
            if (!IsReady) return false;
            var pose = Camera.GetCameraPose();
            var vps = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            worldCorners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                var ray = Camera.ViewportPointToRay(vps[i], pose);
                if (!plane.Raycast(ray, out var enter)) return false;
                worldCorners[i] = ray.GetPoint(enter);
            }
            return true;
        }

        /// <summary>Converts world-space selection corners to a padded, clamped camera viewport rect.</summary>
        public static bool TryProjectToViewport(Vector3[] corners, Func<Vector3, Vector2> worldToViewport, Func<Vector3, bool> inFront, float pad, out Rect viewport, out string error)
        {
            viewport = default;
            error = null;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (var c in corners)
            {
                if (!inFront(c)) { error = "Part of that selection is behind the camera."; return false; }
                var vp = worldToViewport(c);
                minX = Mathf.Min(minX, vp.x); maxX = Mathf.Max(maxX, vp.x);
                minY = Mathf.Min(minY, vp.y); maxY = Mathf.Max(maxY, vp.y);
            }
            minX -= pad; minY -= pad; maxX += pad; maxY += pad;
            if (maxX <= 0f || maxY <= 0f || minX >= 1f || minY >= 1f)
            {
                error = "That area is outside what the headset camera can see. Look at it and try again.";
                return false;
            }
            minX = Mathf.Clamp01(minX); minY = Mathf.Clamp01(minY); maxX = Mathf.Clamp01(maxX); maxY = Mathf.Clamp01(maxY);
            if ((maxX - minX) < 0.02f || (maxY - minY) < 0.02f)
            {
                error = "That selection is too small for the camera to read.";
                return false;
            }
            viewport = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return true;
        }

        public static Vector2Int TargetSize(Vector2 cropPixels, int maxSide)
        {
            float w = Mathf.Max(8f, cropPixels.x), h = Mathf.Max(8f, cropPixels.y);
            float s = Mathf.Min(1f, maxSide / Mathf.Max(w, h)); // never upscale beyond the real camera pixels
            return new Vector2Int(Mathf.Max(8, Mathf.RoundToInt(w * s)), Mathf.Max(8, Mathf.RoundToInt(h * s)));
        }

        public async Task<CaptureResult> CaptureAsync(Vector3[] worldCorners, CancellationToken ct)
        {
            var t0 = Time.realtimeSinceStartup;
            if (!IsReady) throw new InvalidOperationException(LastError ?? "The passthrough camera is not running.");
            var pose = Camera.GetCameraPose();
            var forward = pose.rotation * Vector3.forward;
            if (!TryProjectToViewport(worldCorners, w => Camera.WorldToViewportPoint(w, pose), w => Vector3.Dot(w - pose.position, forward) > 0.05f, ParallaxPad, out var vp, out var err))
                throw new InvalidOperationException(err);

            var res = Camera.CurrentResolution;
            var size = TargetSize(new Vector2(vp.width * res.x, vp.height * res.y), MaxSide);
            var src = Camera.GetTexture();
            if (src == null) throw new InvalidOperationException("No camera frame is available yet.");

            var rt = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(src, rt, new Vector2(vp.width, vp.height), new Vector2(vp.x, vp.y));
                var tcs = new TaskCompletionSource<AsyncGPUReadbackRequest>();
                AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, r => tcs.TrySetResult(r));
                using (ct.Register(() => tcs.TrySetCanceled()))
                {
                    var req = await tcs.Task;
                    if (req.hasError) throw new InvalidOperationException("Reading the camera frame failed.");
                    var data = req.GetData<byte>();
                    var preview = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, false) { name = "SnipPreview", wrapMode = TextureWrapMode.Clamp };
                    preview.LoadRawTextureData(data);
                    preview.Apply(false, false);
                    if (IsMostlyBlack(preview))
                    {
                        Destroy(preview);
                        throw new InvalidOperationException("The camera frame was empty. Please try again.");
                    }
                    var jpg = preview.EncodeToJPG(82);
                    var ms = (Time.realtimeSinceStartup - t0) * 1000f;
                    CursivisMetrics.Instance?.Sample("capture_ms", ms);
                    Debug.Log($"[CursivisCapture] crop vp=({vp.x:0.000},{vp.y:0.000},{vp.width:0.000},{vp.height:0.000}) -> {size.x}x{size.y} jpg={jpg.Length / 1024}KB in {ms:0}ms");
                    return new CaptureResult { Jpeg = jpg, Width = size.x, Height = size.y, Preview = preview, Viewport = vp, CameraResolution = res, CaptureMs = ms };
                }
            }
            finally
            {
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        static bool IsMostlyBlack(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int bright = 0, step = Mathf.Max(1, px.Length / 512);
            for (int i = 0; i < px.Length; i += step)
                if (px[i].r + px[i].g + px[i].b > 24) bright++;
            return bright < (px.Length / step) * 0.02f;
        }
    }

    /// <summary>Exact crops of Cursivis-owned images (no camera): reads source pixels and JPEG-encodes the region.</summary>
    public static class WorkspaceCapture
    {
        public static CaptureResult CropTexture(Texture2D source, Rect uv, int maxSide = PassthroughCapture.MaxSide)
        {
            var t0 = Time.realtimeSinceStartup;
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.xMin * source.width), 0, source.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.yMin * source.height), 0, source.height - 1);
            int w = Mathf.Clamp(Mathf.CeilToInt(uv.width * source.width), 1, source.width - x);
            int h = Mathf.Clamp(Mathf.CeilToInt(uv.height * source.height), 1, source.height - y);
            var size = PassthroughCapture.TargetSize(new Vector2(w, h), maxSide);
            var rt = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt, new Vector2((float)w / source.width, (float)h / source.height), new Vector2((float)x / source.width, (float)y / source.height));
                RenderTexture.active = rt;
                var tex = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false, false) { name = "WorkspaceCrop" };
                tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0, false);
                tex.Apply(false, false);
                var jpg = tex.EncodeToJPG(85);
                return new CaptureResult { Jpeg = jpg, Width = size.x, Height = size.y, Preview = tex, Viewport = uv, CaptureMs = (Time.realtimeSinceStartup - t0) * 1000f };
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
