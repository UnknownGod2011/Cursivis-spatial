using System;
using UnityEngine;

namespace Cursivis.Spatial.UI
{
    /// <summary>
    /// Design tokens ported from the original Cursivis WPF theme (CompanionThemeService.cs / PremiumTheme.xaml,
    /// see docs/UI_DESIGN_SPEC.md). Graphite-glass dark theme by default, white-glass light theme.
    /// Spatial adjustments: text scaled 1.4x for headset legibility, panel shells slightly more opaque over passthrough.
    /// </summary>
    public sealed class Palette
    {
        public Color ShellA, ShellB, ShellBorder, Header, Surface, SurfaceBorder;
        public Color TextMain, TextMuted, TextSoft, Heading, Bullet, CodeFg, CodeBg;
        public Color BtnBg, BtnBorder, BtnText, BtnHoverBorder, BtnPressedBg;
        public Color BtnSecondaryBg, BtnSecondaryBorder, BtnSecondaryText;
        public Color AccentBg, AccentBorder, AccentText;
        public Color ChipSelA, ChipSelB, ChipSelBorder, ChipSelText;
        public Color ChipA, ChipB, ChipBorder, ChipText;
        public Color OrbCore0, OrbCore1, OrbCore2, OrbInner0, OrbInner1, OrbInner2, OrbRim, OrbInnerStroke, OrbShadow;
        public Color OrbStatus, OrbBrand, TitleShineBase, TitleShine, StatusShineBase, StatusShine;
        public Color Highlight, HighlightBorder, Snip, SnipFill, Warning, Error, Success;
        public float ShadowAlpha;
        public bool IsDark;
    }

    public static class CursivisTheme
    {
        public static readonly Color BrandNavy = Hex("#121C2DFF");
        static readonly Color BrandCyanBright = Hex("#49E3FFFF");   // icon ring color (dark theme)
        static readonly Color BrandCyanDeep = Hex("#007A99FF");     // same hue, >= 4.5:1 on light glass
        public static Color BrandCyan => _dark ? BrandCyanBright : BrandCyanDeep;
        public static readonly Color BrandMagenta = Hex("#FF5BD6FF");

        // ---------- geometry (mm == canvas units at 0.001 scale) ----------
        public const float RadiusCard = 22.9f;
        public const float RadiusBody = 18f;
        public const float RadiusHeader = 14.7f;
        public const float RadiusButton = 12.3f;
        public const float PadCard = 9.8f;
        public const float Hairline = 1.0f;

        // ---------- type (mm, 1.4x of original px*0.817) ----------
        public const float TextH1 = 20.6f;
        public const float TextH2 = 19.4f;
        public const float TextH3 = 18.3f;
        public const float TextTitle = 19.4f;
        public const float TextBody = 14.9f;
        public const float TextSubtitle = 13.7f;
        public const float TextButton = 13.2f;
        public const float TextMeta = 12.0f;
        public const float TextChip = 13.0f; // orb chips enlarged for ray targeting (>= 3 deg at 0.7 m)
        public const float TextOrbBrand = 10.3f;

        // ---------- motion ----------
        public const float PanelOpenSec = 0.28f;
        public const float PanelCloseSec = 0.135f;
        public const float OrbScaleSec = 0.22f;
        public const float OrbPulseHalfSec = 0.62f;
        public const float ChipShowSec = 0.18f;
        public const float ChipHideSec = 0.22f;

        public static readonly Palette Dark = new Palette
        {
            IsDark = true,
            ShellA = Hex("#21262DE6"), ShellB = Hex("#1A1F25E6"), ShellBorder = Hex("#FFFFFF1A"),
            Header = Hex("#101418D0"), Surface = Hex("#101418D0"), SurfaceBorder = Hex("#FFFFFF10"),
            TextMain = Hex("#F5F7FAFF"), TextMuted = Hex("#A9B0B8FF"), TextSoft = Hex("#959DA6FF"),
            Heading = Hex("#F7FAFDFF"), Bullet = Hex("#D4DAE1FF"), CodeFg = Hex("#F2F5FAFF"), CodeBg = Hex("#1A1D2140"),
            BtnBg = Hex("#FFFFFF1A"), BtnBorder = Hex("#FFFFFF26"), BtnText = Hex("#F7FAFFFF"), BtnHoverBorder = Hex("#FFFFFF52"), BtnPressedBg = Hex("#171B20FF"),
            BtnSecondaryBg = Hex("#0000000E"), BtnSecondaryBorder = Hex("#FFFFFF18"), BtnSecondaryText = Hex("#A9B0B8FF"),
            AccentBg = Hex("#2C323AFF"), AccentBorder = Hex("#FFFFFF3A"), AccentText = Hex("#F7FAFFFF"),
            ChipSelA = Hex("#13171CE6"), ChipSelB = Hex("#1C2127D8"), ChipSelBorder = Hex("#F5F7FAFF"), ChipSelText = Hex("#FFFFFFFF"),
            ChipA = Hex("#11151AC8"), ChipB = Hex("#14181DB8"), ChipBorder = Hex("#FFFFFF48"), ChipText = Hex("#D9DEE4FF"),
            OrbCore0 = Hex("#161A1FDC"), OrbCore1 = Hex("#0F1317CC"), OrbCore2 = Hex("#090D11BC"),
            OrbInner0 = Hex("#141D28F6"), OrbInner1 = Hex("#0E151FEE"), OrbInner2 = Hex("#090E16E6"),
            OrbRim = Hex("#FFFFFF60"), OrbInnerStroke = Hex("#FFFFFF22"), OrbShadow = Hex("#08131FCC"),
            OrbStatus = Hex("#FFFFFFFF"), OrbBrand = Hex("#B7BFC8FF"),
            TitleShineBase = Hex("#C9D0D8FF"), TitleShine = Hex("#FFFFFFFF"),
            StatusShineBase = Hex("#C0C8D0FF"), StatusShine = Hex("#FFFFFFFF"),
            Highlight = Hex("#49E3FF38"), HighlightBorder = Hex("#49E3FFE6"), Snip = Hex("#4FC3F7FF"), SnipFill = Hex("#4FC3F720"),
            Warning = Hex("#FFC86BFF"), Error = Hex("#FF8A8AFF"), Success = Hex("#8BE3B5FF"),
            ShadowAlpha = 0.28f
        };

        public static readonly Palette Light = new Palette
        {
            IsDark = false,
            ShellA = Hex("#FFFFFFEB"), ShellB = Hex("#F3F4F5E6"), ShellBorder = Hex("#00000014"),
            Header = Hex("#FFFFFFC8"), Surface = Hex("#FFFFFFC4"), SurfaceBorder = Hex("#00000012"),
            TextMain = Hex("#111418FF"), TextMuted = Hex("#5E6670FF"), TextSoft = Hex("#6B737CFF"),
            Heading = Hex("#0F1419FF"), Bullet = Hex("#5D6670FF"), CodeFg = Hex("#1B232DFF"), CodeBg = Hex("#E9ECEFFF"),
            BtnBg = Hex("#FFFFFFED"), BtnBorder = Hex("#00000018"), BtnText = Hex("#12161AFF"), BtnHoverBorder = Hex("#0000002C"), BtnPressedBg = Hex("#F3F4F5FF"),
            BtnSecondaryBg = Hex("#FFFFFFE7"), BtnSecondaryBorder = Hex("#00000014"), BtnSecondaryText = Hex("#6A727AFF"),
            AccentBg = Hex("#14141AFF"), AccentBorder = Hex("#14141AFF"), AccentText = Hex("#FFFFFFFF"),
            ChipSelA = Hex("#FFFFFFF5"), ChipSelB = Hex("#F5F6F7EC"), ChipSelBorder = Hex("#1A1F24FF"), ChipSelText = Hex("#12161AFF"),
            ChipA = Hex("#FFFFFFEC"), ChipB = Hex("#F4F5F6E2"), ChipBorder = Hex("#20202030"), ChipText = Hex("#3A4048FF"),
            OrbCore0 = Hex("#FFFFFFEC"), OrbCore1 = Hex("#FBFCFEE6"), OrbCore2 = Hex("#F4F6F9DE"),
            OrbInner0 = Hex("#FFFFFFFC"), OrbInner1 = Hex("#FCFDFEF6"), OrbInner2 = Hex("#F5F7FAEE"),
            OrbRim = Hex("#00000024"), OrbInnerStroke = Hex("#00000014"), OrbShadow = Hex("#00000033"),
            OrbStatus = Hex("#2A3037FF"), OrbBrand = Hex("#6B727AFF"),
            TitleShineBase = Hex("#5A6067FF"), TitleShine = Hex("#D5DAE0FF"),
            StatusShineBase = Hex("#5F666EFF"), StatusShine = Hex("#B9C0C8FF"),
            Highlight = Hex("#2F7DFF2E"), HighlightBorder = Hex("#2F7DFFE6"), Snip = Hex("#2F7DFFFF"), SnipFill = Hex("#2F7DFF1C"),
            Warning = Hex("#B26A00FF"), Error = Hex("#C23B3BFF"), Success = Hex("#1F8A57FF"),
            ShadowAlpha = 0.16f
        };

        static bool _dark = true;
        static Palette _blend;
        static bool _transitioning;
        public static Palette P => _transitioning && _blend != null ? _blend : (_dark ? Dark : Light);
        public static bool IsDark => _dark;
        /// <summary>True while the 240 ms theme cross-fade runs (handlers can skip expensive work until it ends).</summary>
        public static bool IsTransitioning => _transitioning;
        public static event Action Changed;
        public const float ThemeFadeSec = 0.24f;

        public static void Load()
        {
            _dark = PlayerPrefs.GetInt("cursivis.theme.dark", 1) == 1;
        }

        public static void SetDark(bool dark)
        {
            if (_dark == dark) return;
            var from = P;
            _dark = dark;
            PlayerPrefs.SetInt("cursivis.theme.dark", dark ? 1 : 0);
            PlayerPrefs.Save();
            ThemeFader.Run(from, dark ? Dark : Light);
        }

        public static void Toggle() => SetDark(!_dark);

        internal static void SetBlend(Palette blend, bool transitioning)
        {
            _blend = blend;
            _transitioning = transitioning;
            Changed?.Invoke();
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    /// <summary>
    /// Cross-fades every themed surface between palettes (the original switched instantly; a short fade reads better
    /// in a headset where large panels suddenly flipping from dark to white is uncomfortable).
    /// </summary>
    public sealed class ThemeFader : MonoBehaviour
    {
        static ThemeFader _instance;
        static readonly System.Reflection.FieldInfo[] ColorFields = Array.FindAll(typeof(Palette).GetFields(), f => f.FieldType == typeof(Color));
        static readonly System.Reflection.FieldInfo[] FloatFields = Array.FindAll(typeof(Palette).GetFields(), f => f.FieldType == typeof(float));
        Palette _from, _to;
        readonly Palette _blend = new Palette();
        float _t = 1f;

        public static void Run(Palette from, Palette to)
        {
            if (_instance == null)
            {
                var go = new GameObject("CursivisThemeFader");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<ThemeFader>();
            }
            _instance._from = from == _instance._blend ? Copy(from) : from;
            _instance._to = to;
            _instance._t = 0f;
            _instance._blend.IsDark = to.IsDark;
            _instance.Step(0f);
        }

        static Palette Copy(Palette p)
        {
            var c = new Palette { IsDark = p.IsDark };
            foreach (var f in ColorFields) f.SetValue(c, f.GetValue(p));
            foreach (var f in FloatFields) f.SetValue(c, f.GetValue(p));
            return c;
        }

        void Step(float dt)
        {
            _t = Mathf.Min(1f, _t + dt / CursivisTheme.ThemeFadeSec);
            float e = Ease.OutCubic(_t);
            foreach (var f in ColorFields) f.SetValue(_blend, Color.Lerp((Color)f.GetValue(_from), (Color)f.GetValue(_to), e));
            foreach (var f in FloatFields) f.SetValue(_blend, Mathf.Lerp((float)f.GetValue(_from), (float)f.GetValue(_to), e));
            CursivisTheme.SetBlend(_blend, _t < 1f);
        }

        void Update()
        {
            if (_t < 1f) Step(Time.unscaledDeltaTime);
        }
    }

    /// <summary>Material Icons (Apache-2.0) codepoints used by the UI.</summary>
    public static class Icons
    {
        public const string Sparkle = "\ue65f";   // auto_awesome
        public const string Mic = "\ue029";
        public const string Speaker = "\ue050";   // volume_up
        public const string Stop = "\ue047";
        public const string Close = "\ue5cd";
        public const string LightMode = "\ue518";
        public const string DarkMode = "\ue51c";
        public const string Translate = "\ue8e2";
        public const string Copy = "\ue14d";
        public const string Refresh = "\ue5d5";
        public const string Pin = "\uf10d";
        public const string Snip = "\ue3c2";      // crop_free
        public const string Camera = "\ue412";
        public const string Summary = "\uf071";
        public const string Code = "\ue86f";
        public const string Bug = "\ue868";
        public const string Bolt = "\uea0b";
        public const string Doc = "\ue873";
        public const string Image = "\ue3f4";
        public const string Eye = "\ue8f4";
        public const string Text = "\ue262";
        public const string Nutrition = "\ue56c";  // restaurant
        public const string Leaf = "\uea35";       // eco
        public const string Question = "\ue887";
        public const string Tasks = "\ue2e6";      // task_alt
        public const string Shield = "\ue9e0";
        public const string Palette = "\ue40a";
        public const string Math = "\uea5f";
        public const string Book = "\uea19";
        public const string Chat = "\ue0b7";
        public const string Tag = "\ue892";
        public const string Warning = "\ue002";
        public const string Check = "\ue86c";
        public const string List = "\ue896";
        public const string Idea = "\ue0f0";
        public const string More = "\ue5d3";
        public const string Replay = "\ue042";
        public const string Wave = "\ue1b8";       // graphic_eq
        public const string Workspace = "\ue1a0";
        public const string Expand = "\uf1ce";
        public const string Collapse = "\uf1cf";
        public const string Drag = "\ue945";
        public const string Hand = "\ue769";

        public static string ForSuggestion(string icon)
        {
            switch (icon)
            {
                case "summary": return Summary;
                case "translate": return Translate;
                case "explain": return Idea;
                case "list": return List;
                case "check": return Check;
                case "alert": return Warning;
                case "code": return Code;
                case "bug": return Bug;
                case "bolt": return Bolt;
                case "doc": return Doc;
                case "image": return Image;
                case "caption": return Chat;
                case "eye": return Eye;
                case "text": return Text;
                case "nutrition": return Nutrition;
                case "leaf": return Leaf;
                case "question": return Question;
                case "tasks": return Tasks;
                case "shield": return Shield;
                case "palette": return Palette;
                case "math": return Math;
                case "book": return Book;
                case "chat": return Chat;
                case "tag": return Tag;
                default: return Sparkle;
            }
        }
    }

    public static class Ease
    {
        public static float OutCubic(float t) { t = Mathf.Clamp01(t) - 1f; return t * t * t + 1f; }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float OutQuad(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        public static float InQuad(float t) { t = Mathf.Clamp01(t); return t * t; }
        public static float OutBack(float t) { t = Mathf.Clamp01(t); const float c1 = 1.25f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
    }
}
