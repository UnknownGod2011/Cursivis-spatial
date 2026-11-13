using UnityEngine;
using UnityEngine.UI;

namespace Cursivis.Spatial.UI
{
    /// <summary>
    /// Resolution-independent rounded rectangle for world-space uGUI, drawn with an SDF shader
    /// (Cursivis/UI/RoundedRect). Fill gradient, border, top rim highlight and a soft outer shadow are
    /// all computed per pixel from vertex data, so every panel in the app shares one material and batches.
    /// Replaces the WPF DropShadowEffect / translucent Border stack without real-time blur.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedRect : MaskableGraphic
    {
        public float Radius = 12f;
        public float BorderWidth = 1f;
        public Color FillTop = Color.white;
        public Color FillBottom = Color.white;
        public Color Border = new Color(1, 1, 1, 0.15f);
        public float ShadowSize = 0f;
        public float ShadowAlpha = 0f;
        public float RimHighlight = 0.6f; // 0..1, lighter top border (glass sheen)

        static Material _shared;

        public static Material SharedMaterial
        {
            get
            {
                if (_shared == null)
                {
                    var shader = Shader.Find("Cursivis/UI/RoundedRect");
                    _shared = new Material(shader) { name = "CursivisRoundedRect (shared)" };
                }
                return _shared;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            material = SharedMaterial;
            raycastTarget = false;
        }

        public void Set(Color top, Color bottom, Color border, float radius, float borderWidth = 1f)
        {
            FillTop = top;
            FillBottom = bottom;
            Border = border;
            Radius = radius;
            BorderWidth = borderWidth;
            SetVerticesDirty();
        }

        public void SetShadow(float size, float alpha)
        {
            ShadowSize = size;
            ShadowAlpha = alpha;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = GetPixelAdjustedRect();
            float pad = Mathf.Max(0f, ShadowSize) + 1.5f;
            var center = r.center;
            float hw = r.width * 0.5f, hh = r.height * 0.5f;
            float radius = Mathf.Min(Radius, Mathf.Min(hw, hh));
            // vertex color carries the global alpha (CanvasGroup / tint) multiplied by graphic color
            var tint = color;
            var top = FillTop * tint;
            var bottom = FillBottom * tint;
            var border = Border * tint;

            void Add(float x, float y)
            {
                var v = UIVertex.simpleVert;
                v.position = new Vector3(center.x + x, center.y + y, 0f);
                v.color = top;
                v.uv0 = new Vector4(x, y, hw, hh);
                v.uv1 = new Vector4(radius, BorderWidth, ShadowSize, ShadowAlpha * tint.a);
                v.uv2 = new Vector4(bottom.r, bottom.g, bottom.b, bottom.a);
                v.uv3 = new Vector4(border.r, border.g, border.b, border.a);
                v.tangent = new Vector4(RimHighlight, 0f, 0f, 0f);
                vh.AddVert(v);
            }

            Add(-hw - pad, -hh - pad);
            Add(-hw - pad, hh + pad);
            Add(hw + pad, hh + pad);
            Add(hw + pad, -hh - pad);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }

        public override bool Raycast(Vector2 sp, Camera eventCamera)
        {
            // Raycast against the visible rectangle only (not the shadow padding).
            return base.Raycast(sp, eventCamera);
        }
    }
}
