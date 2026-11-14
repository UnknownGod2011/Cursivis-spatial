using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cursivis.Spatial.UI
{
    public enum OrbMood { Idle, Acknowledged, Understanding, Listening, ProcessingVoice, Processing, Speaking, Completed, Error }

    /// <summary>
    /// The Cursivis orb. Motion follows the original OrbOverlayWindow storyboards:
    /// idle scale 0.88, expanded 1.0, completed 0.98 (220 ms ease-out cubic), +0.07 ping-pong pulse every 620 ms
    /// while working/listening, a 4-ring completion burst (80 ms stagger), and a silver shine on the status text.
    /// Spatial additions: brand-colored thinking arc, voice-level ring, selection glow, error tint.
    /// </summary>
    public sealed class OrbView : MonoBehaviour
    {
        public RectTransform Root;      // scaled element
        public Graphic OrbGraphic;      // RawImage using Cursivis/UI/Orb
        public TextMeshProUGUI Status;  // label below the orb
        public TextMeshProUGUI Brand;   // "CURSIVIS" inside the orb
        public RoundedRect[] BurstRings;

        Material _mat;
        OrbMood _mood = OrbMood.Idle;
        float _baseScale = 0.88f, _fromScale = 0.88f, _targetScale = 0.88f, _scaleT = 1f;
        float _spin, _arc, _arcTarget, _listen, _listenTarget, _level, _err, _errTarget, _glow, _glowTarget;
        float _burstStart = -10f, _popStart = -10f, _shakeStart = -10f;
        string _shineText;
        int _shineFrame;
        float _shineT;
        public float VoiceLevel { get; set; }
        public OrbMood Mood => _mood;

        static readonly int Core0 = Shader.PropertyToID("_Core0"), Core1 = Shader.PropertyToID("_Core1"), Core2 = Shader.PropertyToID("_Core2");
        static readonly int Inner0 = Shader.PropertyToID("_Inner0"), Inner1 = Shader.PropertyToID("_Inner1"), Inner2 = Shader.PropertyToID("_Inner2");
        static readonly int RimId = Shader.PropertyToID("_Rim"), InnerStroke = Shader.PropertyToID("_InnerStroke"), ShadowId = Shader.PropertyToID("_Shadow");
        static readonly int ArcId = Shader.PropertyToID("_Arc"), SpinId = Shader.PropertyToID("_Spin"), LevelId = Shader.PropertyToID("_Level");
        static readonly int ListenId = Shader.PropertyToID("_Listen"), ErrorId = Shader.PropertyToID("_ErrorTint"), GlowId = Shader.PropertyToID("_Glow");

        public void Init()
        {
            _mat = new Material(Shader.Find("Cursivis/UI/Orb")) { name = "CursivisOrb (instance)" };
            OrbGraphic.material = _mat;
            ApplyTheme();
            CursivisTheme.Changed += ApplyTheme;
            SetMood(OrbMood.Idle, null);
        }

        void OnDestroy()
        {
            CursivisTheme.Changed -= ApplyTheme;
            if (_mat != null) Destroy(_mat);
        }

        public void ApplyTheme()
        {
            if (_mat == null) return;
            var p = CursivisTheme.P;
            _mat.SetColor(Core0, p.OrbCore0);
            _mat.SetColor(Core1, p.OrbCore1);
            _mat.SetColor(Core2, p.OrbCore2);
            _mat.SetColor(Inner0, p.OrbInner0);
            _mat.SetColor(Inner1, p.OrbInner1);
            _mat.SetColor(Inner2, p.OrbInner2);
            _mat.SetColor(RimId, p.OrbRim);
            _mat.SetColor(InnerStroke, p.OrbInnerStroke);
            var sh = p.OrbShadow;
            _mat.SetColor(ShadowId, new Color(sh.r, sh.g, sh.b, p.IsDark ? 0.30f : 0.10f));
            if (Brand != null) Brand.color = p.OrbBrand;
            if (Status != null) Status.color = p.TextMain;
            if (BurstRings != null)
                for (int i = 0; i < BurstRings.Length; i++)
                    BurstRings[i].Border = i % 2 == 0 ? (p.IsDark ? CursivisTheme.Hex("#F5F7FAFF") : CursivisTheme.Hex("#2A3037FF")) : (p.IsDark ? CursivisTheme.Hex("#B6BEC6FF") : CursivisTheme.Hex("#626973FF"));
        }

        public void SetMood(OrbMood mood, string status)
        {
            _mood = mood;
            if (Status != null)
            {
                Status.text = status ?? "";
                Status.gameObject.SetActive(!string.IsNullOrEmpty(status));
            }
            switch (mood)
            {
                case OrbMood.Idle: ScaleTo(0.88f); _arcTarget = 0f; _listenTarget = 0f; _errTarget = 0f; _glowTarget = 0f; break;
                case OrbMood.Acknowledged: ScaleTo(1.0f); _glowTarget = 1f; _arcTarget = 0f; _listenTarget = 0f; _errTarget = 0f; _popStart = Time.unscaledTime; break;
                case OrbMood.Understanding: ScaleTo(1.0f); _arcTarget = 1f; _glowTarget = 0.35f; _listenTarget = 0f; _errTarget = 0f; break;
                case OrbMood.Listening: ScaleTo(1.0f); _arcTarget = 0f; _listenTarget = 1f; _glowTarget = 0f; _errTarget = 0f; break;
                case OrbMood.ProcessingVoice:
                case OrbMood.Processing: ScaleTo(1.0f); _arcTarget = 1f; _listenTarget = 0f; _glowTarget = 0.2f; _errTarget = 0f; break;
                case OrbMood.Speaking: ScaleTo(1.0f); _arcTarget = 0f; _listenTarget = 0.55f; _glowTarget = 0f; _errTarget = 0f; break;
                case OrbMood.Completed: ScaleTo(0.98f); _arcTarget = 0f; _listenTarget = 0f; _glowTarget = 0f; _errTarget = 0f; _burstStart = Time.time; break;
                case OrbMood.Error: ScaleTo(0.98f); _arcTarget = 0f; _listenTarget = 0f; _glowTarget = 0f; _errTarget = 1f; _shakeStart = Time.unscaledTime; break;
            }
        }

        void ScaleTo(float s)
        {
            _fromScale = _baseScale;
            _targetScale = s;
            _scaleT = 0f;
        }

        bool Pulsing => _mood == OrbMood.Understanding || _mood == OrbMood.Processing || _mood == OrbMood.ProcessingVoice || _mood == OrbMood.Listening;

        void Update()
        {
            float dt = Time.deltaTime;
            if (_scaleT < 1f)
            {
                _scaleT = Mathf.Min(1f, _scaleT + dt / CursivisTheme.OrbScaleSec);
                _baseScale = Mathf.Lerp(_fromScale, _targetScale, Ease.OutCubic(_scaleT));
            }
            float scale = _baseScale;
            if (Pulsing)
            {
                float ph = Mathf.PingPong(Time.time / CursivisTheme.OrbPulseHalfSec, 1f);
                scale = Mathf.Max(_baseScale, 0.96f) + 0.07f * (_mood == OrbMood.Listening ? Mathf.Lerp(0.35f, 1f, VoiceLevel) * ph : ph);
            }
            else if (_mood == OrbMood.Idle || _mood == OrbMood.Completed)
                scale += 0.012f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / 4.6f)); // calm idle breathing
            float pop = Time.unscaledTime - _popStart;
            if (pop < 0.45f) scale *= 1f + 0.09f * Mathf.Sin(Mathf.Clamp01(pop / 0.45f) * Mathf.PI) * (1f - pop / 0.45f * 0.4f);
            if (Root != null)
            {
                Root.localScale = Vector3.one * scale;
                float shake = Time.unscaledTime - _shakeStart;
                Root.anchoredPosition = shake < 0.42f ? new Vector2(Mathf.Sin(shake * 52f) * 3.2f * (1f - shake / 0.42f), 0f) : Vector2.zero;
            }

            float k = 1f - Mathf.Exp(-dt * 8f);
            _arc = Mathf.Lerp(_arc, _arcTarget, k);
            _listen = Mathf.Lerp(_listen, _listenTarget, k);
            _err = Mathf.Lerp(_err, _errTarget, k);
            _glow = Mathf.Lerp(_glow, _glowTarget, 1f - Mathf.Exp(-dt * 5f));
            _level = Mathf.Lerp(_level, VoiceLevel, 1f - Mathf.Exp(-dt * 18f));
            _spin -= dt * 2.4f;
            if (_mat != null)
            {
                _mat.SetFloat(ArcId, _arc);
                _mat.SetFloat(SpinId, _spin);
                _mat.SetFloat(ListenId, _listen);
                _mat.SetFloat(LevelId, _level);
                _mat.SetFloat(ErrorId, _err);
                _mat.SetFloat(GlowId, _glow);
            }
            if (_mood == OrbMood.Acknowledged && _glowTarget > 0.5f && Time.time - _burstStart > 0f)
            {
                // glow decays after the acknowledgement flash
                _glowTarget = Mathf.Max(0.45f, _glowTarget - dt * 1.2f);
            }
            UpdateBurst();
            UpdateShine();
        }

        void UpdateBurst()
        {
            if (BurstRings == null) return;
            float t = (Time.time - _burstStart) * 1000f;
            for (int i = 0; i < BurstRings.Length; i++)
            {
                var ring = BurstRings[i];
                float local = t - i * 80f;
                bool on = local >= 0f && local <= 860f;
                if (ring.gameObject.activeSelf != on) ring.gameObject.SetActive(on);
                if (!on) continue;
                float a = local < 160f ? Mathf.Lerp(0f, 0.9f, local / 160f) : Mathf.Lerp(0.9f, 0f, (local - 160f) / 660f);
                float s = Mathf.Lerp(0.72f, 1.24f + 0.05f * i, Ease.OutCubic(local / 860f));
                ring.rectTransform.localScale = Vector3.one * s;
                var c = ring.color;
                c.a = a;
                ring.color = c;
            }
        }

        void UpdateShine()
        {
            if (Status == null || !Status.gameObject.activeSelf) return;
            bool shining = _mood == OrbMood.Understanding || _mood == OrbMood.Processing || _mood == OrbMood.ProcessingVoice || _mood == OrbMood.Listening;
            var p = CursivisTheme.P;
            if (!shining)
            {
                Status.color = p.TextMain;
                return;
            }
            float period = _mood == OrbMood.Listening ? 2.0f : 2.35f;
            _shineT = (Time.time % period) / period;
            // Animated silver sweep via vertex colors (cheap, no extra material). The mesh is rebuilt only when the
            // text changes (or occasionally, in case TMP regenerated it); otherwise only colors are re-uploaded.
            if (_shineText != Status.text || ++_shineFrame % 30 == 0) { Status.ForceMeshUpdate(); _shineText = Status.text; }
            var info = Status.textInfo;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                minX = Mathf.Min(minX, ch.bottomLeft.x);
                maxX = Mathf.Max(maxX, ch.topRight.x);
            }
            if (maxX <= minX) return;
            float sweep = Mathf.Lerp(-0.3f, 1.3f, _shineT);
            Color32 baseC = p.StatusShineBase, shineC = p.StatusShine;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var colors = info.meshInfo[ch.materialReferenceIndex].colors32;
                for (int v = 0; v < 4; v++)
                {
                    var vx = info.meshInfo[ch.materialReferenceIndex].vertices[ch.vertexIndex + v].x;
                    float u = (vx - minX) / (maxX - minX);
                    float w = Mathf.Clamp01(1f - Mathf.Abs(u - sweep) / 0.22f);
                    colors[ch.vertexIndex + v] = Color32.Lerp(baseC, shineC, w * 0.9f);
                }
            }
            Status.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
