using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cursivis.Spatial.UI
{
    /// <summary>Code-first UI factory so every panel is reviewable, reproducible and theme-aware.</summary>
    public static class UiKit
    {
        static TMP_FontAsset _regular, _semibold, _bold, _mono, _icons;
        // TMP resolves <font="Name"> tags from Resources/"Fonts & Materials/", so the assets live there.
        const string FontDir = "Fonts & Materials/";

        public static TMP_FontAsset Regular => _regular ? _regular : (_regular = LoadFont(FontDir + "Inter-Regular SDF"));
        public static TMP_FontAsset SemiBold => _semibold ? _semibold : (_semibold = LoadFont(FontDir + "Inter-SemiBold SDF") ?? Regular);
        public static TMP_FontAsset Bold => _bold ? _bold : (_bold = LoadFont(FontDir + "Inter-Bold SDF") ?? SemiBold);
        public static TMP_FontAsset Mono => _mono ? _mono : (_mono = LoadFont(FontDir + "IBMPlexMono-Regular SDF") ?? Regular);
        public static TMP_FontAsset IconFont => _icons ? _icons : (_icons = LoadFont(FontDir + "MaterialIcons-Regular SDF"));

        static TMP_FontAsset LoadFont(string path)
        {
            var f = Resources.Load<TMP_FontAsset>(path);
            if (f == null) Debug.LogWarning($"[CursivisUI] font missing: {path}");
            return f;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 size, Vector2? pos = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 0;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos ?? Vector2.zero;
            return rt;
        }

        public static void Stretch(RectTransform rt, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        public static RoundedRect Panel(Transform parent, string name, Vector2 size, Vector2 pos, Color top, Color bottom, Color border, float radius, float borderWidth = 1f)
        {
            var rt = Rect(parent, name, size, pos);
            var rr = rt.gameObject.AddComponent<RoundedRect>();
            rr.Set(top, bottom, border, radius, borderWidth);
            return rr;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TMP_FontAsset font = null, TextAlignmentOptions align = TextAlignmentOptions.Left, Vector2? boxSize = null, Vector2? pos = null)
        {
            var rt = Rect(parent, name, boxSize ?? new Vector2(200, size * 1.6f), pos);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font ?? Regular;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.text = text;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        public static TextMeshProUGUI Icon(Transform parent, string glyph, float size, Color color, Vector2? pos = null)
        {
            var t = Text(parent, "Icon", glyph, size, color, IconFont, TextAlignmentOptions.Center, new Vector2(size * 1.3f, size * 1.3f), pos);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var g = go.GetComponent<CanvasGroup>();
            return g != null ? g : go.AddComponent<CanvasGroup>();
        }

        /// <summary>Shared soft "click" so hand presses have audible confirmation (no haptics on hands).</summary>
        static AudioClip _click, _confirm;
        static AudioSource _uiAudio;

        public static void PlayClick(bool confirm = false, Vector3? at = null)
        {
            if (_uiAudio == null)
            {
                var go = new GameObject("CursivisUiAudio");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _uiAudio = go.AddComponent<AudioSource>();
                _uiAudio.playOnAwake = false;
                _uiAudio.spatialBlend = 0.6f;
                _uiAudio.volume = 0.35f;
                _click = Tone("click", 1850f, 0.028f, 0.5f);
                _confirm = Tone("confirm", 1320f, 0.075f, 0.35f, 1760f);
            }
            if (at.HasValue) _uiAudio.transform.position = at.Value;
            _uiAudio.PlayOneShot(confirm ? _confirm : _click);
        }

        static AudioClip Tone(string name, float freq, float seconds, float gain, float freq2 = 0f)
        {
            int rate = 48000, n = Mathf.CeilToInt(rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Exp(-t * (freq2 > 0 ? 38f : 140f)) * Mathf.Clamp01(t * 2000f);
                float f = freq2 > 0 && t > seconds * 0.45f ? freq2 : freq;
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * gain;
            }
            var clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }

    public enum ButtonStyle { Primary, Secondary, Accent, Chip, Talk, Icon, Danger }

    /// <summary>
    /// Hand-friendly button driven by standard uGUI pointer events (Interaction SDK PointableCanvasModule emits these
    /// for hand-ray pinch, poke and controllers). Exposes press/release for push-to-talk and click for everything else.
    /// Hover/pressed visuals follow the original Cursivis button states (instant border/background swaps).
    /// </summary>
    public sealed class CursivisButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public RoundedRect Background;
        public TextMeshProUGUI Label;
        public TextMeshProUGUI Glyph;
        public ButtonStyle Style;
        public bool Selected;
        public bool Interactable = true;
        public event Action Clicked;
        public event Action Pressed;
        public event Action Released;
        public object Tag;

        bool _hover, _down;
        float _pressScale = 1f;
        float _lastClick;

        public static CursivisButton Create(Transform parent, string name, string label, string glyph, Vector2 size, Vector2 pos, ButtonStyle style, float fontSize = CursivisTheme.TextButton)
        {
            float radius = Mathf.Min(size.y * 0.5f, style == ButtonStyle.Icon ? size.y * 0.5f : CursivisTheme.RadiusButton * 1.6f);
            var bg = UiKit.Panel(parent, name, size, pos, Color.white, Color.white, Color.white, radius, 1.2f);
            bg.raycastTarget = true;
            var b = bg.gameObject.AddComponent<CursivisButton>();
            b.Background = bg;
            b.Style = style;
            bool hasLabel = !string.IsNullOrEmpty(label);
            bool hasGlyph = !string.IsNullOrEmpty(glyph);
            float iconSize = style == ButtonStyle.Icon ? size.y * 0.52f : fontSize * 1.35f;
            if (hasGlyph)
            {
                var gx = hasLabel ? -size.x * 0.5f + 12f + iconSize * 0.5f : 0f;
                b.Glyph = UiKit.Icon(bg.transform, glyph, iconSize, Color.white, new Vector2(gx, 0));
            }
            if (hasLabel)
            {
                float left = hasGlyph ? 12f + iconSize + 6f : 12f;
                var t = UiKit.Text(bg.transform, "Label", label, fontSize, Color.white, UiKit.SemiBold, hasGlyph ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
                UiKit.Stretch(t.rectTransform, left, 12f, 2f, 2f);
                t.textWrappingMode = TextWrappingModes.Normal;
                t.enableAutoSizing = true;
                t.fontSizeMax = fontSize;
                t.fontSizeMin = fontSize * 0.78f;
                t.lineSpacing = -12f;
                b.Label = t;
            }
            b.Refresh();
            CursivisTheme.Changed += b.Refresh;
            return b;
        }

        void OnDestroy() => CursivisTheme.Changed -= Refresh;

        public void SetLabel(string label) { if (Label != null) Label.text = label; }
        public void SetGlyph(string glyph) { if (Glyph != null) Glyph.text = glyph; }
        public void SetSelected(bool s) { Selected = s; Refresh(); }
        public void SetInteractable(bool on) { Interactable = on; Refresh(); }

        public void Refresh()
        {
            if (Background == null) return;
            var p = CursivisTheme.P;
            Color a, bcol, border, text;
            switch (Style)
            {
                case ButtonStyle.Chip:
                    if (Selected || _down) { a = p.ChipSelA; bcol = p.ChipSelB; border = p.ChipSelBorder; text = p.ChipSelText; }
                    else { a = p.ChipA; bcol = p.ChipB; border = _hover ? p.ChipSelBorder : p.ChipBorder; text = _hover ? p.ChipSelText : p.ChipText; }
                    break;
                case ButtonStyle.Talk:
                    a = Selected || _down ? CursivisTheme.WithAlpha(CursivisTheme.BrandCyan, p.IsDark ? 0.30f : 0.22f) : p.ChipA;
                    bcol = Selected || _down ? CursivisTheme.WithAlpha(CursivisTheme.BrandCyan, p.IsDark ? 0.18f : 0.14f) : p.ChipB;
                    border = _hover || Selected || _down ? CursivisTheme.BrandCyan : CursivisTheme.WithAlpha(CursivisTheme.BrandCyan, 0.65f);
                    text = p.TextMain;
                    break;
                case ButtonStyle.Accent:
                    a = bcol = p.AccentBg; border = _hover ? p.BtnHoverBorder : p.AccentBorder; text = p.AccentText;
                    break;
                case ButtonStyle.Secondary:
                    a = bcol = _down ? p.BtnPressedBg : p.BtnSecondaryBg; border = _hover ? p.BtnHoverBorder : p.BtnSecondaryBorder; text = p.BtnSecondaryText;
                    break;
                case ButtonStyle.Danger:
                    a = bcol = _down ? p.BtnPressedBg : p.BtnBg; border = _hover ? p.Error : p.BtnBorder; text = p.Error;
                    break;
                default:
                    a = bcol = _down ? p.BtnPressedBg : p.BtnBg; border = _hover ? p.BtnHoverBorder : p.BtnBorder; text = p.BtnText;
                    if (Selected) { border = p.ChipSelBorder; }
                    break;
            }
            if (!Interactable)
            {
                a.a *= 0.42f; bcol.a *= 0.42f; border.a *= 0.42f; text.a *= 0.42f;
            }
            Background.Set(a, bcol, border, Background.Radius, _hover ? 1.6f : 1.2f);
            if (Label != null) Label.color = text;
            if (Glyph != null) Glyph.color = Style == ButtonStyle.Talk ? CursivisTheme.BrandCyan : text;
        }

        void Update()
        {
            float target = _down ? 0.965f : (_hover ? 1.02f : 1f);
            _pressScale = Mathf.Lerp(_pressScale, target, 1f - Mathf.Exp(-Time.deltaTime * 22f));
            transform.localScale = Vector3.one * _pressScale;
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; Refresh(); }
        public void OnPointerExit(PointerEventData e) { _hover = false; if (_down) { _down = false; Released?.Invoke(); } Refresh(); }

        public void OnPointerDown(PointerEventData e)
        {
            if (!Interactable) return;
            _down = true;
            Refresh();
            UiKit.PlayClick(false, transform.position);
            Pressed?.Invoke();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_down) return;
            _down = false;
            Refresh();
            Released?.Invoke();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable) return;
            if (Time.unscaledTime - _lastClick < 0.25f) return; // debounce double events
            _lastClick = Time.unscaledTime;
            Clicked?.Invoke();
        }

        /// <summary>Programmatic click used by the automation harness and voice client actions.</summary>
        public void InvokeClick()
        {
            if (!Interactable) return;
            Clicked?.Invoke();
        }
    }

    /// <summary>Small fade/rise animator for panels (original: open 280 ms rise 18px, close 135 ms).</summary>
    public sealed class PanelAnimator : MonoBehaviour
    {
        CanvasGroup _group;
        RectTransform _content;
        Vector2 _restPos;
        float _t = 1f, _dur, _fromA, _toA, _fromY, _toY;
        Action _done;
        public bool Visible { get; private set; }

        public void Init(RectTransform content)
        {
            _content = content;
            _restPos = content.anchoredPosition;
            _group = UiKit.Group(gameObject);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            Visible = false;
            gameObject.SetActive(false);
        }

        public void Show(Action done = null)
        {
            gameObject.SetActive(true);
            Visible = true;
            _group.blocksRaycasts = true;
            _group.interactable = true;
            Animate(_group.alpha, 1f, -14.7f, 0f, CursivisTheme.PanelOpenSec, done);
        }

        public void Hide(Action done = null)
        {
            if (!gameObject.activeSelf) { done?.Invoke(); return; }
            Visible = false;
            _group.blocksRaycasts = false;
            _group.interactable = false;
            Animate(_group.alpha, 0f, 0f, -8.2f, CursivisTheme.PanelCloseSec, () => { gameObject.SetActive(false); done?.Invoke(); });
        }

        public void HideImmediate()
        {
            Visible = false;
            _t = 1f;
            if (_group != null) { _group.alpha = 0f; _group.blocksRaycasts = false; }
            gameObject.SetActive(false);
        }

        void Animate(float fromA, float toA, float fromY, float toY, float dur, Action done)
        {
            _fromA = fromA; _toA = toA; _fromY = fromY; _toY = toY; _dur = dur; _t = 0f; _done = done;
        }

        void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.deltaTime / Mathf.Max(0.01f, _dur));
            bool opening = _toA > _fromA;
            float e = opening ? Ease.OutCubic(_t) : Ease.InCubic(_t);
            _group.alpha = Mathf.Lerp(_fromA, _toA, e);
            if (_content != null) _content.anchoredPosition = _restPos + new Vector2(0, Mathf.Lerp(_fromY, _toY, e));
            if (_t >= 1f)
            {
                var d = _done;
                _done = null;
                d?.Invoke();
            }
        }
    }
}
