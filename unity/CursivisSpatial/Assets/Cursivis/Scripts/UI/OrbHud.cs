using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cursivis.Spatial.UI
{
    /// <summary>
    /// The orb and everything that surrounds it on one world-space canvas:
    ///  - Guided ring: four AI suggestions around the orb (two each side, mirroring the original Cursivis guided
    ///    layout) and a fifth, visually distinct Talk chip below the orb (always available, never AI-generated).
    ///  - Interpreted-selection title above the orb.
    ///  - Home dock (Snip / Workspace / Theme / Help) when no selection is active.
    /// Motion: chips fly out of the orb with a staggered spring, loading placeholders shimmer ("..." like the original
    /// Guided Mode; never fake results), the Talk chip breathes to invite voice and shows a live level meter while
    /// listening, the dock slides in/out.
    /// </summary>
    public sealed class OrbHud : MonoBehaviour
    {
        public const float OrbDiameter = 78f;
        public OrbView Orb { get; private set; }
        public CursivisButton OrbButton { get; private set; }
        public CursivisButton TalkButton { get; private set; }
        public readonly List<CursivisButton> SuggestionButtons = new List<CursivisButton>();
        public CursivisButton SnipButton, WorkspaceButton, ThemeButton, HelpButton;
        public event Action<int> SuggestionChosen;
        public event Action TalkPressed, TalkReleased, TalkClicked, OrbClicked, OrbPressed, OrbReleased;
        public event Action SnipClicked, WorkspaceClicked, ThemeClicked, HelpClicked;

        RectTransform _root, _ringRoot, _dockRoot, _titleRt, _talkRt;
        TextMeshProUGUI _title, _hint;
        RoundedRect _titleBg, _talkGlow, _dockBg;
        CanvasGroup _ringGroup, _dockGroup, _titleGroup, _hintGroup;
        readonly List<CanvasGroup> _chipGroups = new List<CanvasGroup>();
        CanvasGroup _talkGroup;
        readonly List<RoundedRect> _eqBars = new List<RoundedRect>();
        readonly List<SuggestionDto> _current = new List<SuggestionDto>();
        float _ringT, _dockT = 1f;
        bool _ringShown, _dockShown = true;
        float _revealStart = -10f, _popStart = -10f;
        bool _loading;

        static readonly Vector2[] ChipPositions =
        {
            new Vector2(-138f, 34f), new Vector2(138f, 34f), new Vector2(-138f, -30f), new Vector2(138f, -30f)
        };
        static readonly Vector2 ChipSize = new Vector2(176f, 50f);
        static readonly Vector2 TalkPos = new Vector2(0, -98f);
        static readonly Vector2 TitlePos = new Vector2(0, 92f);
        const float Stagger = 0.045f, FlyDur = 0.34f;

        public void Build()
        {
            _root = (RectTransform)transform;
            // --- orb ---
            var orbRoot = UiKit.Rect(_root, "OrbRoot", new Vector2(OrbDiameter * 1.6f, OrbDiameter * 1.6f), Vector2.zero);
            var orbImg = orbRoot.gameObject.AddComponent<RawImage>();
            orbImg.raycastTarget = false;
            var brand = UiKit.Text(orbRoot, "Brand", "CURSIVIS", CursivisTheme.TextOrbBrand, Color.white, UiKit.SemiBold, TextAlignmentOptions.Center, new Vector2(80, 14), new Vector2(0, -2));
            brand.characterSpacing = 18f;
            var rings = new RoundedRect[4];
            for (int i = 0; i < 4; i++)
            {
                float d = OrbDiameter * (1.186f + 0.085f * i);
                var ring = UiKit.Panel(orbRoot, "BurstRing" + i, new Vector2(d, d), Vector2.zero, Color.clear, Color.clear, Color.white, d * 0.5f, i == 3 ? 1.1f : 1.6f);
                ring.RimHighlight = 0f;
                ring.gameObject.SetActive(false);
                rings[i] = ring;
            }
            var status = UiKit.Text(_root, "Status", "", CursivisTheme.TextChip, Color.white, UiKit.SemiBold, TextAlignmentOptions.Center, new Vector2(320, 22), new Vector2(0, -OrbDiameter * 0.5f - 18f));
            status.textWrappingMode = TextWrappingModes.NoWrap;
            Orb = orbRoot.gameObject.AddComponent<OrbView>();
            Orb.Root = orbRoot;
            Orb.OrbGraphic = orbImg;
            Orb.Status = status;
            Orb.Brand = brand;
            Orb.BurstRings = rings;
            Orb.Init();

            // Invisible hit target over the orb (pinch the orb = Talk in general mode)
            OrbButton = CursivisButton.Create(_root, "OrbHit", null, null, new Vector2(OrbDiameter, OrbDiameter), Vector2.zero, ButtonStyle.Icon);
            OrbButton.Style = ButtonStyle.Icon;
            CursivisTheme.Changed -= OrbButton.Refresh; // keep it invisible
            OrbButton.Background.Set(Color.clear, Color.clear, Color.clear, OrbDiameter * 0.5f, 0f);
            OrbButton.Invisible = true; // no hover scale/colour animation on the invisible target
            OrbButton.Clicked += () => OrbClicked?.Invoke();
            OrbButton.Pressed += () => OrbPressed?.Invoke();
            OrbButton.Released += () => OrbReleased?.Invoke();

            // --- guided ring ---
            _ringRoot = UiKit.Rect(_root, "Ring", new Vector2(480, 320), Vector2.zero);
            _ringGroup = UiKit.Group(_ringRoot.gameObject);
            _titleBg = UiKit.Panel(_ringRoot, "TitleChip", new Vector2(330, 30), TitlePos, Color.white, Color.white, Color.white, 15f, 1f);
            _titleRt = _titleBg.rectTransform;
            _titleGroup = UiKit.Group(_titleBg.gameObject);
            _title = UiKit.Text(_titleBg.transform, "Title", "", CursivisTheme.TextMeta, Color.white, UiKit.SemiBold, TextAlignmentOptions.Center);
            UiKit.Stretch(_title.rectTransform, 12, 12, 2, 2);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var b = CursivisButton.Create(_ringRoot, "Suggestion" + i, "...", Icons.Sparkle, ChipSize, ChipPositions[i], ButtonStyle.Chip, CursivisTheme.TextChip);
                b.Clicked += () => { if (!_loading) SuggestionChosen?.Invoke(index); };
                SuggestionButtons.Add(b);
                _chipGroups.Add(UiKit.Group(b.gameObject));
            }
            // soft glow behind the Talk chip (breathes to invite voice)
            _talkGlow = UiKit.Panel(_ringRoot, "TalkGlow", new Vector2(150, 50), TalkPos, Color.clear, Color.clear, Color.white, 25f, 2f);
            _talkGlow.RimHighlight = 0f;
            TalkButton = CursivisButton.Create(_ringRoot, "Talk", "Talk", Icons.Mic, new Vector2(150, 50), TalkPos, ButtonStyle.Talk, CursivisTheme.TextChip);
            _talkRt = TalkButton.Background.rectTransform;
            _talkGroup = UiKit.Group(TalkButton.gameObject);
            TalkButton.Pressed += () => TalkPressed?.Invoke();
            TalkButton.Released += () => TalkReleased?.Invoke();
            TalkButton.Clicked += () => TalkClicked?.Invoke();
            // live microphone level meter inside the Talk chip (visible only while listening)
            for (int i = 0; i < 4; i++)
            {
                var bar = UiKit.Panel(TalkButton.transform, "Level" + i, new Vector2(4f, 8f), new Vector2(40f + i * 8f, 0f), Color.white, Color.white, Color.clear, 2f, 0f);
                bar.RimHighlight = 0f;
                bar.gameObject.SetActive(false);
                _eqBars.Add(bar);
            }
            _hint = UiKit.Text(_ringRoot, "Hint", "", CursivisTheme.TextMeta, Color.white, UiKit.Regular, TextAlignmentOptions.Center, new Vector2(440, 20), new Vector2(0, -138));
            _hintGroup = UiKit.Group(_hint.gameObject);

            // --- home dock ---
            _dockRoot = UiKit.Rect(_root, "Dock", new Vector2(420, 60), new Vector2(0, -96));
            _dockGroup = UiKit.Group(_dockRoot.gameObject);
            _dockBg = UiKit.Panel(_dockRoot, "DockBg", new Vector2(404, 56), Vector2.zero, Color.white, Color.white, Color.white, 28f, 1f);
            SnipButton = CursivisButton.Create(_dockBg.transform, "Snip", "Snip", Icons.Snip, new Vector2(118, 42), new Vector2(-133, 0), ButtonStyle.Primary);
            WorkspaceButton = CursivisButton.Create(_dockBg.transform, "Workspace", "Workspace", Icons.Workspace, new Vector2(150, 42), new Vector2(5, 0), ButtonStyle.Primary);
            ThemeButton = CursivisButton.Create(_dockBg.transform, "Theme", null, Icons.DarkMode, new Vector2(42, 42), new Vector2(110, 0), ButtonStyle.Icon);
            HelpButton = CursivisButton.Create(_dockBg.transform, "Help", null, Icons.Question, new Vector2(42, 42), new Vector2(160, 0), ButtonStyle.Icon);
            SnipButton.Clicked += () => SnipClicked?.Invoke();
            WorkspaceButton.Clicked += () => WorkspaceClicked?.Invoke();
            ThemeButton.Clicked += () => ThemeClicked?.Invoke();
            HelpButton.Clicked += () => HelpClicked?.Invoke();

            ApplyTheme();
            CursivisTheme.Changed += ApplyTheme;
            ShowRing(false, immediate: true);
            ShowDock(true, immediate: true);
        }

        void OnDestroy() => CursivisTheme.Changed -= ApplyTheme;

        void ApplyTheme()
        {
            var p = CursivisTheme.P;
            _titleBg.Set(p.ChipA, p.ChipB, p.ChipBorder, 15f, 1f);
            _title.color = p.TextMuted;
            _hint.color = p.TextSoft;
            _dockBg.Set(p.ShellA, p.ShellB, p.ShellBorder, 28f, 1f);
            _dockBg.SetShadow(10f, p.ShadowAlpha);
            ThemeButton.SetGlyph(p.IsDark ? Icons.LightMode : Icons.DarkMode);
            foreach (var bar in _eqBars) bar.Set(CursivisTheme.BrandCyan, CursivisTheme.BrandCyan, Color.clear, 2f, 0f);
        }

        public bool RingVisible => _ringShown;

        public void ShowRing(bool show, bool immediate = false)
        {
            if (show && !_ringShown) _revealStart = Time.unscaledTime;
            _ringShown = show;
            _ringRoot.gameObject.SetActive(true);
            _ringGroup.blocksRaycasts = show;
            _ringGroup.interactable = show;
            if (immediate)
            {
                _ringT = show ? 1f : 0f;
                _revealStart = show ? Time.unscaledTime - 10f : -10f;
                ApplyRing();
            }
        }

        public void ShowDock(bool show, bool immediate = false)
        {
            _dockShown = show;
            _dockRoot.gameObject.SetActive(true);
            _dockGroup.blocksRaycasts = show;
            _dockGroup.interactable = show;
            if (immediate) { _dockT = show ? 1f : 0f; ApplyDock(); }
        }

        public void SetLoading(string title)
        {
            _loading = true;
            _current.Clear();
            _title.text = string.IsNullOrEmpty(title) ? "Reading your selection" : title;
            foreach (var b in SuggestionButtons)
            {
                b.gameObject.SetActive(true);
                b.SetLabel("...");
                b.SetGlyph(Icons.More);
                b.SetInteractable(false);
                b.SetSelected(false);
            }
            TalkButton.SetInteractable(true);
            _hint.text = "Talk is ready while suggestions load";
        }

        public void SetSuggestions(SuggestResponse r)
        {
            _loading = false;
            _current.Clear();
            _current.AddRange(r.suggestions);
            var title = string.IsNullOrEmpty(r.title) ? "Your selection" : r.title;
            if (r.quality == "unreadable") title += "  \u00B7  hard to read";
            if (r.source == "fallback") title += "  \u00B7  basic options";
            _title.text = title;
            for (int i = 0; i < SuggestionButtons.Count; i++)
            {
                var b = SuggestionButtons[i];
                if (i < r.suggestions.Count)
                {
                    b.SetLabel(r.suggestions[i].label);
                    b.SetGlyph(Icons.ForSuggestion(r.suggestions[i].icon));
                    b.SetInteractable(true);
                    b.gameObject.SetActive(true);
                }
                else b.gameObject.SetActive(false);
            }
            _hint.text = "Pinch an action, or hold Talk and ask anything about it";
            _popStart = Time.unscaledTime; // each chip "lands" with a small pop as its real label arrives
        }

        public void SetError(string message)
        {
            _loading = false;
            _title.text = message;
            foreach (var b in SuggestionButtons) b.SetInteractable(false);
            _hint.text = "Pinch the orb to retry, or hold Talk";
        }

        public void SetHint(string text) => _hint.text = text ?? "";

        public void MarkChosen(int index)
        {
            for (int i = 0; i < SuggestionButtons.Count; i++)
            {
                SuggestionButtons[i].SetSelected(i == index);
                if (i == index) SuggestionButtons[i].Flash();
            }
        }

        public SuggestionDto SuggestionAt(int index) => index >= 0 && index < _current.Count ? _current[index] : null;

        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float now = Time.unscaledTime;
            // ring master fade: fast in (the per-chip flight carries the motion), quick out
            float ringTarget = _ringShown ? 1f : 0f;
            if (!Mathf.Approximately(_ringT, ringTarget))
                _ringT = Mathf.MoveTowards(_ringT, ringTarget, dt / (_ringShown ? 0.12f : CursivisTheme.ChipHideSec));
            ApplyRing();

            float dockTarget = _dockShown ? 1f : 0f;
            if (!Mathf.Approximately(_dockT, dockTarget))
            {
                _dockT = Mathf.MoveTowards(_dockT, dockTarget, dt / (_dockShown ? 0.26f : 0.16f));
                ApplyDock();
            }
            if (!_ringRoot.gameObject.activeSelf) return;

            // per-chip flight out of the orb (staggered spring), shimmer while loading, pop when labels land
            float since = now - _revealStart;
            for (int i = 0; i < SuggestionButtons.Count; i++)
            {
                float t = Mathf.Clamp01((since - i * Stagger) / FlyDur);
                float e = Ease.OutBack(t);
                var rt = SuggestionButtons[i].Background.rectTransform;
                rt.anchoredPosition = Vector2.LerpUnclamped(ChipPositions[i] * 0.18f, ChipPositions[i], e);
                float alpha = Mathf.Clamp01(t * 2.2f);
                if (_loading) alpha *= 0.55f + 0.3f * (0.5f + 0.5f * Mathf.Sin(now * 4.2f - i * 0.9f));
                _chipGroups[i].alpha = alpha;
                float pop = Mathf.Clamp01((now - _popStart - i * 0.05f) / 0.3f);
                float popScale = pop < 1f ? Mathf.LerpUnclamped(0.9f, 1f, Ease.OutBack(pop)) : 1f;
                SuggestionButtons[i].ExternalScale = Mathf.LerpUnclamped(0.6f, 1f, e) * popScale;
            }
            {
                float t = Mathf.Clamp01((since - 4 * Stagger) / FlyDur);
                float e = Ease.OutBack(t);
                _talkRt.anchoredPosition = Vector2.LerpUnclamped(TalkPos * 0.25f, TalkPos, e);
                TalkButton.ExternalScale = Mathf.LerpUnclamped(0.6f, 1f, e);
                _talkGroup.alpha = Mathf.Clamp01(t * 2.2f);
                var tt = Mathf.Clamp01((since - 0.02f) / 0.3f);
                _titleRt.anchoredPosition = Vector2.Lerp(TitlePos + new Vector2(0, -26f), TitlePos, Ease.OutCubic(tt));
                _titleGroup.alpha = tt;
                _hintGroup.alpha = Mathf.Clamp01((since - 0.35f) / 0.3f);
            }
            UpdateTalkVisuals(now, dt);
        }

        float _eqPhase;

        void UpdateTalkVisuals(float now, float dt)
        {
            bool listening = Orb != null && Orb.Mood == OrbMood.Listening;
            var p = CursivisTheme.P;
            // breathing glow behind Talk: invites voice while the ring is up, brighter while listening
            float breath = 0.5f + 0.5f * Mathf.Sin(now * (listening ? 6.5f : 2.6f));
            float glowA = listening ? 0.35f + 0.45f * Orb.VoiceLevel : (_ringShown ? 0.10f + 0.18f * breath : 0f);
            float grow = listening ? 6f + 10f * Orb.VoiceLevel : 4f + 4f * breath;
            _talkGlow.rectTransform.anchoredPosition = _talkRt.anchoredPosition;
            _talkGlow.rectTransform.sizeDelta = new Vector2(150f + grow * 2f, 50f + grow * 2f);
            var cyan = CursivisTheme.BrandCyan;
            _talkGlow.Set(CursivisTheme.WithAlpha(cyan, glowA * 0.18f), CursivisTheme.WithAlpha(cyan, glowA * 0.08f), CursivisTheme.WithAlpha(cyan, glowA), 25f + grow, 2f);
            _talkGlow.SetShadow(listening ? 10f : 6f, glowA * (p.IsDark ? 0.6f : 0.3f));
            // level meter
            _eqPhase += dt * (listening ? 9f : 0f);
            for (int i = 0; i < _eqBars.Count; i++)
            {
                var bar = _eqBars[i];
                if (bar.gameObject.activeSelf != listening) bar.gameObject.SetActive(listening);
                if (!listening) continue;
                float wobble = 0.5f + 0.5f * Mathf.Sin(_eqPhase + i * 1.7f);
                float h = Mathf.Lerp(6f, 26f, Mathf.Clamp01(Orb.VoiceLevel * (0.55f + 0.6f * wobble) + 0.08f * wobble));
                bar.rectTransform.sizeDelta = new Vector2(4f, h);
            }
        }

        void ApplyRing()
        {
            _ringGroup.alpha = _ringT;
            if (_ringT <= 0.001f && !_ringShown) _ringRoot.gameObject.SetActive(false);
        }

        void ApplyDock()
        {
            float e = _dockShown ? Ease.OutCubic(_dockT) : Ease.InCubic(_dockT);
            _dockGroup.alpha = e;
            _dockRoot.anchoredPosition = new Vector2(0, -96f - (1f - e) * 14f);
            float s = Mathf.Lerp(0.94f, 1f, e);
            _dockRoot.localScale = new Vector3(s, s, 1f);
            if (_dockT <= 0.001f && !_dockShown) _dockRoot.gameObject.SetActive(false);
        }
    }
}
