using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cursivis.Spatial.UI
{
    /// <summary>
    /// The orb and everything that surrounds it on one world-space canvas:
    ///  - Guided ring: four AI suggestions at the original Cursivis chip positions (+-0.907 D, +-0.339 D) and a fifth,
    ///    visually distinct Talk chip below the orb (always available, never AI-generated).
    ///  - Interpreted-selection title above the orb.
    ///  - Home dock (Snip / Workspace / Theme / Help) when no selection is active.
    /// Loading shows "..." placeholder chips exactly like the original Guided Mode (never fake results).
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

        RectTransform _root, _ringRoot, _dockRoot;
        TextMeshProUGUI _title, _hint;
        RoundedRect _titleBg;
        CanvasGroup _ringGroup, _dockGroup;
        readonly List<SuggestionDto> _current = new List<SuggestionDto>();
        float _ringAlpha, _ringTarget, _dockAlpha = 1f, _dockTarget = 1f;
        float _revealStart = -10f;
        bool _loading;

        static readonly Vector2[] ChipPositions =
        {
            new Vector2(-138f, 34f), new Vector2(138f, 34f), new Vector2(-138f, -30f), new Vector2(138f, -30f)
        };
        static readonly Vector2 ChipSize = new Vector2(176f, 50f);

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
            var status = UiKit.Text(_root, "Status", "", CursivisTheme.TextChip, Color.white, UiKit.SemiBold, TextAlignmentOptions.Center, new Vector2(300, 22), new Vector2(0, -OrbDiameter * 0.5f - 18f));
            status.textWrappingMode = TextWrappingModes.NoWrap;
            Orb = orbRoot.gameObject.AddComponent<OrbView>();
            Orb.Root = orbRoot;
            Orb.OrbGraphic = orbImg;
            Orb.Status = status;
            Orb.Brand = brand;
            Orb.BurstRings = rings;
            Orb.Init();

            // Invisible hit target over the orb (pinch the orb = Talk in general mode / toggle ring)
            OrbButton = CursivisButton.Create(_root, "OrbHit", null, null, new Vector2(OrbDiameter, OrbDiameter), Vector2.zero, ButtonStyle.Icon);
            OrbButton.Background.Set(Color.clear, Color.clear, Color.clear, OrbDiameter * 0.5f, 0f);
            OrbButton.Style = ButtonStyle.Icon;
            OrbButton.Clicked += () => OrbClicked?.Invoke();
            OrbButton.Pressed += () => OrbPressed?.Invoke();
            OrbButton.Released += () => OrbReleased?.Invoke();
            CursivisTheme.Changed -= OrbButton.Refresh; // keep it invisible

            // --- guided ring ---
            _ringRoot = UiKit.Rect(_root, "Ring", new Vector2(480, 300), Vector2.zero);
            _ringGroup = UiKit.Group(_ringRoot.gameObject);
            _titleBg = UiKit.Panel(_ringRoot, "TitleChip", new Vector2(330, 30), new Vector2(0, 92), Color.white, Color.white, Color.white, 15f, 1f);
            _title = UiKit.Text(_titleBg.transform, "Title", "", CursivisTheme.TextMeta, Color.white, UiKit.SemiBold, TextAlignmentOptions.Center);
            UiKit.Stretch(_title.rectTransform, 12, 12, 2, 2);
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var b = CursivisButton.Create(_ringRoot, "Suggestion" + i, "...", Icons.Sparkle, ChipSize, ChipPositions[i], ButtonStyle.Chip, CursivisTheme.TextChip);
                b.Clicked += () => { if (!_loading) SuggestionChosen?.Invoke(index); };
                SuggestionButtons.Add(b);
            }
            TalkButton = CursivisButton.Create(_ringRoot, "Talk", "Talk", Icons.Mic, new Vector2(150, 50), new Vector2(0, -98), ButtonStyle.Talk, CursivisTheme.TextChip);
            TalkButton.Pressed += () => TalkPressed?.Invoke();
            TalkButton.Released += () => TalkReleased?.Invoke();
            TalkButton.Clicked += () => TalkClicked?.Invoke();
            _hint = UiKit.Text(_ringRoot, "Hint", "", CursivisTheme.TextMeta, Color.white, UiKit.Regular, TextAlignmentOptions.Center, new Vector2(420, 20), new Vector2(0, -136));

            // --- home dock ---
            _dockRoot = UiKit.Rect(_root, "Dock", new Vector2(420, 60), new Vector2(0, -96));
            _dockGroup = UiKit.Group(_dockRoot.gameObject);
            var dockBg = UiKit.Panel(_dockRoot, "DockBg", new Vector2(404, 56), Vector2.zero, Color.white, Color.white, Color.white, 28f, 1f);
            dockBg.SetShadow(10f, 0.2f);
            SnipButton = CursivisButton.Create(dockBg.transform, "Snip", "Snip", Icons.Snip, new Vector2(118, 42), new Vector2(-133, 0), ButtonStyle.Primary);
            WorkspaceButton = CursivisButton.Create(dockBg.transform, "Workspace", "Workspace", Icons.Workspace, new Vector2(150, 42), new Vector2(5, 0), ButtonStyle.Primary);
            ThemeButton = CursivisButton.Create(dockBg.transform, "Theme", null, Icons.DarkMode, new Vector2(42, 42), new Vector2(110, 0), ButtonStyle.Icon);
            HelpButton = CursivisButton.Create(dockBg.transform, "Help", null, Icons.Question, new Vector2(42, 42), new Vector2(160, 0), ButtonStyle.Icon);
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
            var dockBg = _dockRoot.Find("DockBg").GetComponent<RoundedRect>();
            dockBg.Set(p.ShellA, p.ShellB, p.ShellBorder, 28f, 1f);
            dockBg.SetShadow(10f, p.ShadowAlpha);
            ThemeButton.SetGlyph(p.IsDark ? Icons.LightMode : Icons.DarkMode);
        }

        public void ShowRing(bool show, bool immediate = false)
        {
            _ringTarget = show ? 1f : 0f;
            _ringRoot.gameObject.SetActive(true);
            if (show) _revealStart = Time.time;
            if (immediate) { _ringAlpha = _ringTarget; ApplyRing(); }
        }

        public void ShowDock(bool show, bool immediate = false)
        {
            _dockTarget = show ? 1f : 0f;
            _dockRoot.gameObject.SetActive(true);
            if (immediate) { _dockAlpha = _dockTarget; ApplyDock(); }
        }

        public void SetLoading(string title)
        {
            _loading = true;
            _current.Clear();
            _title.text = string.IsNullOrEmpty(title) ? "Reading your selection" : title;
            foreach (var b in SuggestionButtons)
            {
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
            _revealStart = Time.time;
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
            for (int i = 0; i < SuggestionButtons.Count; i++) SuggestionButtons[i].SetSelected(i == index);
        }

        public SuggestionDto SuggestionAt(int index) => index >= 0 && index < _current.Count ? _current[index] : null;

        void Update()
        {
            float k = 1f - Mathf.Exp(-Time.deltaTime * 14f);
            if (!Mathf.Approximately(_ringAlpha, _ringTarget))
            {
                _ringAlpha = Mathf.MoveTowards(_ringAlpha, _ringTarget, Time.deltaTime / (_ringTarget > _ringAlpha ? CursivisTheme.ChipShowSec : CursivisTheme.ChipHideSec));
                ApplyRing();
            }
            if (!Mathf.Approximately(_dockAlpha, _dockTarget))
            {
                _dockAlpha = Mathf.MoveTowards(_dockAlpha, _dockTarget, Time.deltaTime / 0.2f);
                ApplyDock();
            }
            // staggered "expand from the orb" reveal for chips (40 ms stagger, ease-out)
            float since = Time.time - _revealStart;
            for (int i = 0; i < SuggestionButtons.Count; i++)
            {
                float t = Mathf.Clamp01((since - i * 0.04f) / 0.26f);
                var target = ChipPositions[i];
                var rt = SuggestionButtons[i].Background.rectTransform;
                rt.anchoredPosition = Vector2.Lerp(target * 0.55f, target, Ease.OutCubic(t));
            }
            _ = k;
        }

        void ApplyRing()
        {
            _ringGroup.alpha = _ringAlpha;
            _ringGroup.blocksRaycasts = _ringTarget > 0.5f;
            _ringGroup.interactable = _ringTarget > 0.5f;
            if (_ringAlpha <= 0.001f && _ringTarget <= 0f) _ringRoot.gameObject.SetActive(false);
        }

        void ApplyDock()
        {
            _dockGroup.alpha = _dockAlpha;
            _dockGroup.blocksRaycasts = _dockTarget > 0.5f;
            _dockGroup.interactable = _dockTarget > 0.5f;
            if (_dockAlpha <= 0.001f && _dockTarget <= 0f) _dockRoot.gameObject.SetActive(false);
        }
    }
}
