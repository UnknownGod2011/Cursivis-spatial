using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cursivis.Spatial.UI
{
    /// <summary>
    /// Spatial port of the original Cursivis result panel (ResultPanelWindow.xaml): rounded graphite glass card,
    /// header pill with the Cursivis wordmark (silver shine sweep), "Output / action" meta line, rich body with
    /// word-step reveal, plus spatial controls: read aloud, talk follow-up, theme, pin, expand, scroll.
    /// </summary>
    public sealed class ResultCard : MonoBehaviour
    {
        public const float Width = 520f, Height = 404f;
        public event Action CloseClicked, SpeakClicked, TalkPressed, TalkReleased, TalkClicked, ThemeClicked, PinClicked, ExpandClicked;
        public event Action MoreOptionsClicked, RegenerateClicked, CopyClicked, RetryClicked;
        public event Action<string> FollowUpClicked;

        public CursivisButton CloseButton, SpeakButton, TalkButton, ThemeButton, PinButton, ExpandButton;
        public CursivisButton MoreButton, RegenButton, CopyButton, FollowUpButton, RetryButton;
        public ScrollRect Scroll { get; private set; }
        public string CurrentPlainAnswer { get; private set; } = "";
        public bool IsShowingResult { get; private set; }

        RectTransform _root, _shellRt, _content;
        RoundedRect _shell, _header, _body, _handle, _uncertaintyChip, _evidenceChip, _scrollThumb;
        TextMeshProUGUI _wordmark, _title, _meta, _timing, _text, _status, _uncertaintyText, _evidenceText, _errorIcon;
        RectTransform _followRow, _skeleton, _errorRoot;
        readonly List<RoundedRect> _skeletonBars = new List<RoundedRect>();
        readonly List<CursivisButton> _followChips = new List<CursivisButton>();
        PanelAnimator _anim;
        int _revealWords, _totalWords;
        float _revealTimer, _revealInterval = 0.016f;
        int _revealStep = 1;
        float _shineT;
        bool _speaking;

        public void Build()
        {
            _root = (RectTransform)transform;
            _shellRt = UiKit.Rect(_root, "Shell", new Vector2(Width, Height), new Vector2(0, 8));
            _shell = _shellRt.gameObject.AddComponent<RoundedRect>();
            _shell.raycastTarget = true; // blocks rays from passing through the card
            var shell = _shellRt.transform;

            // header
            _header = UiKit.Panel(shell, "Header", new Vector2(Width - 19.6f, 46f), new Vector2(0, Height * 0.5f - 9.8f - 23f), Color.white, Color.white, Color.white, CursivisTheme.RadiusHeader);
            var h = _header.transform;
            _wordmark = UiKit.Text(h, "Wordmark", "Cursivis", CursivisTheme.TextTitle, Color.white, UiKit.SemiBold, TextAlignmentOptions.Left, new Vector2(110, 30), new Vector2(-Width * 0.5f + 9.8f + 14f + 55f, 0));
            _wordmark.textWrappingMode = TextWrappingModes.NoWrap;
            _title = UiKit.Text(h, "Title", "", CursivisTheme.TextSubtitle, Color.white, UiKit.Regular, TextAlignmentOptions.Left, new Vector2(170, 26), new Vector2(-54, 0));
            _title.textWrappingMode = TextWrappingModes.NoWrap;
            float x = (Width - 19.6f) * 0.5f - 22f;
            CloseButton = HeaderIcon(h, "Close", Icons.Close, ref x);
            PinButton = HeaderIcon(h, "Pin", Icons.Pin, ref x);
            ExpandButton = HeaderIcon(h, "Expand", Icons.Expand, ref x);
            ThemeButton = HeaderIcon(h, "Theme", Icons.LightMode, ref x);
            TalkButton = HeaderIcon(h, "TalkFollowUp", Icons.Mic, ref x);
            SpeakButton = HeaderIcon(h, "ReadAloud", Icons.Speaker, ref x);
            CloseButton.Clicked += () => CloseClicked?.Invoke();
            PinButton.Clicked += () => PinClicked?.Invoke();
            ExpandButton.Clicked += () => ExpandClicked?.Invoke();
            ThemeButton.Clicked += () => ThemeClicked?.Invoke();
            SpeakButton.Clicked += () => SpeakClicked?.Invoke();
            TalkButton.Pressed += () => TalkPressed?.Invoke();
            TalkButton.Released += () => TalkReleased?.Invoke();
            TalkButton.Clicked += () => TalkClicked?.Invoke();

            // meta line ("Output / action" in the original)
            float metaY = Height * 0.5f - 9.8f - 46f - 14f;
            _meta = UiKit.Text(shell, "Meta", "Output", CursivisTheme.TextMeta, Color.white, UiKit.SemiBold, TextAlignmentOptions.Left, new Vector2(300, 18), new Vector2(-Width * 0.5f + 20f + 150f, metaY));
            _timing = UiKit.Text(shell, "Timing", "", CursivisTheme.TextMeta, Color.white, UiKit.Regular, TextAlignmentOptions.Right, new Vector2(200, 18), new Vector2(Width * 0.5f - 20f - 100f, metaY));

            // body with scroll
            float bodyTop = metaY - 12f;
            float bodyH = 232f;
            _body = UiKit.Panel(shell, "Body", new Vector2(Width - 19.6f, bodyH), new Vector2(0, bodyTop - bodyH * 0.5f), Color.white, Color.white, Color.white, CursivisTheme.RadiusBody);
            _body.raycastTarget = true;
            var viewport = UiKit.Rect(_body.transform, "Viewport", Vector2.zero);
            UiKit.Stretch(viewport, 14f, 16f, 10f, 10f);
            viewport.gameObject.AddComponent<RectMask2D>().padding = new Vector4(0, 0, 0, 0);
            var vpGraphic = viewport.gameObject.AddComponent<RoundedRect>();
            vpGraphic.Set(Color.clear, Color.clear, Color.clear, 0f, 0f);
            vpGraphic.raycastTarget = true; // drag target for scrolling
            _content = UiKit.Rect(viewport, "Content", new Vector2(Width - 19.6f - 30f, 100f));
            _content.anchorMin = new Vector2(0, 1);
            _content.anchorMax = new Vector2(1, 1);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0, 100f);
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 10f;
            layout.padding = new RectOffset(0, 0, 2, 8);
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _text = UiKit.Text(_content, "Answer", "", CursivisTheme.TextBody, Color.white, UiKit.Regular, TextAlignmentOptions.TopLeft, new Vector2(460, 40));
            _text.lineSpacing = 6f;
            _text.paragraphSpacing = 4.9f;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.fontFeatures.Clear();
            _followRow = UiKit.Rect(_content, "FollowUps", new Vector2(460, 38));
            var hl = _followRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 8f;
            hl.childControlWidth = false;
            hl.childControlHeight = false;
            hl.childForceExpandWidth = false;
            var le = _followRow.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 38f;
            for (int i = 0; i < 3; i++)
            {
                var chip = CursivisButton.Create(_followRow, "FollowUp" + i, "", Icons.Sparkle, new Vector2(150, 34), Vector2.zero, ButtonStyle.Chip, CursivisTheme.TextMeta);
                int idx = i;
                chip.Clicked += () => { var t = chip.Tag as string; if (!string.IsNullOrEmpty(t)) FollowUpClicked?.Invoke(t); };
                _followChips.Add(chip);
            }
            Scroll = _body.gameObject.AddComponent<ScrollRect>();
            Scroll.viewport = viewport;
            Scroll.content = _content;
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Elastic;
            Scroll.elasticity = 0.12f;
            Scroll.inertia = true;
            Scroll.decelerationRate = 0.12f;
            Scroll.scrollSensitivity = 18f;
            _scrollThumb = UiKit.Panel(_body.transform, "ScrollThumb", new Vector2(4f, 40f), new Vector2((Width - 19.6f) * 0.5f - 7f, 0), Color.white, Color.white, Color.clear, 2f, 0f);

            // loading skeleton + status
            _skeleton = UiKit.Rect(_body.transform, "Skeleton", new Vector2(Width - 50f, bodyH - 30f));
            for (int i = 0; i < 5; i++)
            {
                float w = i == 4 ? 0.55f : (i % 2 == 0 ? 0.95f : 0.82f);
                var bar = UiKit.Panel(_skeleton, "Bar" + i, new Vector2((Width - 50f) * w, 12f), new Vector2(-(Width - 50f) * (1f - w) * 0.5f, 70f - i * 26f), Color.white, Color.white, Color.clear, 6f, 0f);
                _skeletonBars.Add(bar);
            }
            _status = UiKit.Text(_skeleton, "Status", "", CursivisTheme.TextMeta, Color.white, UiKit.SemiBold, TextAlignmentOptions.Left, new Vector2(Width - 50f, 20f), new Vector2(0, -78f));

            // error state
            _errorRoot = UiKit.Rect(_body.transform, "Error", new Vector2(Width - 60f, bodyH - 30f));
            _errorIcon = UiKit.Icon(_errorRoot, Icons.Warning, 30f, Color.white, new Vector2(0, 52f));
            var errText = UiKit.Text(_errorRoot, "ErrorText", "", CursivisTheme.TextBody, Color.white, UiKit.Regular, TextAlignmentOptions.Center, new Vector2(Width - 80f, 70f), new Vector2(0, 2f));
            errText.name = "ErrorText";
            RetryButton = CursivisButton.Create(_errorRoot, "Retry", "Retry", Icons.Refresh, new Vector2(130, 40), new Vector2(0, -62f), ButtonStyle.Accent);
            RetryButton.Clicked += () => RetryClicked?.Invoke();

            // chips row (uncertainty / evidence)
            float chipsY = bodyTop - bodyH - 8f - 14f;
            _uncertaintyChip = UiKit.Panel(shell, "Uncertainty", new Vector2(240, 28), new Vector2(-Width * 0.5f + 9.8f + 120f, chipsY), Color.white, Color.white, Color.white, 14f, 1f);
            _uncertaintyText = UiKit.Text(_uncertaintyChip.transform, "Text", "", CursivisTheme.TextMeta, Color.white, UiKit.SemiBold, TextAlignmentOptions.Left);
            UiKit.Stretch(_uncertaintyText.rectTransform, 12, 10, 2, 2);
            _uncertaintyText.textWrappingMode = TextWrappingModes.NoWrap;
            _evidenceChip = UiKit.Panel(shell, "Evidence", new Vector2(240, 28), new Vector2(Width * 0.5f - 9.8f - 120f, chipsY), Color.white, Color.white, Color.white, 14f, 1f);
            _evidenceText = UiKit.Text(_evidenceChip.transform, "Text", "", CursivisTheme.TextMeta, Color.white, UiKit.Regular, TextAlignmentOptions.Left);
            UiKit.Stretch(_evidenceText.rectTransform, 12, 10, 2, 2);
            _evidenceText.textWrappingMode = TextWrappingModes.NoWrap;

            // footer actions
            float footY = -Height * 0.5f + 9.8f + 21f;
            FollowUpButton = CursivisButton.Create(shell, "AskFollowUp", "Ask follow-up", Icons.Mic, new Vector2(150, 42), new Vector2(-174, footY), ButtonStyle.Accent);
            MoreButton = CursivisButton.Create(shell, "MoreOptions", "More options", Icons.More, new Vector2(140, 42), new Vector2(-24, footY), ButtonStyle.Primary);
            RegenButton = CursivisButton.Create(shell, "Regenerate", "Redo", Icons.Refresh, new Vector2(96, 42), new Vector2(104, footY), ButtonStyle.Primary);
            CopyButton = CursivisButton.Create(shell, "Copy", "Copy", Icons.Copy, new Vector2(92, 42), new Vector2(204, footY), ButtonStyle.Primary);
            FollowUpButton.Pressed += () => TalkPressed?.Invoke();
            FollowUpButton.Released += () => TalkReleased?.Invoke();
            FollowUpButton.Clicked += () => TalkClicked?.Invoke();
            MoreButton.Clicked += () => MoreOptionsClicked?.Invoke();
            RegenButton.Clicked += () => RegenerateClicked?.Invoke();
            CopyButton.Clicked += () => CopyClicked?.Invoke();

            // grab handle (visual; the Interaction SDK grab collider is placed here by the scene builder)
            _handle = UiKit.Panel(_root, "GrabHandleVisual", new Vector2(150f, 14f), new Vector2(0, -Height * 0.5f - 8f), Color.white, Color.white, Color.clear, 7f, 0f);

            _anim = gameObject.AddComponent<PanelAnimator>();
            _anim.Init(_shellRt);
            ApplyTheme();
            CursivisTheme.Changed += ApplyTheme;
        }

        void OnDestroy() => CursivisTheme.Changed -= ApplyTheme;

        CursivisButton HeaderIcon(Transform parent, string name, string glyph, ref float x)
        {
            var b = CursivisButton.Create(parent, name, null, glyph, new Vector2(34f, 34f), new Vector2(x, 0), ButtonStyle.Icon);
            x -= 40f;
            return b;
        }

        public void ApplyTheme()
        {
            var p = CursivisTheme.P;
            _shell.Set(p.ShellA, p.ShellB, p.ShellBorder, CursivisTheme.RadiusCard, 1f);
            _shell.SetShadow(18f, p.ShadowAlpha);
            _header.Set(p.Header, p.Header, p.SurfaceBorder, CursivisTheme.RadiusHeader, 1f);
            _body.Set(p.Surface, p.Surface, p.SurfaceBorder, CursivisTheme.RadiusBody, 1f);
            _title.color = p.TextMuted;
            _meta.color = p.TextMuted;
            _timing.color = p.TextSoft;
            _text.color = p.TextMain;
            _status.color = p.TextMuted;
            _errorIcon.color = p.Warning;
            _errorRoot.Find("ErrorText").GetComponent<TextMeshProUGUI>().color = p.TextMain;
            foreach (var bar in _skeletonBars) bar.Set(CursivisTheme.WithAlpha(p.TextMain, 0.08f), CursivisTheme.WithAlpha(p.TextMain, 0.05f), Color.clear, 6f, 0f);
            _scrollThumb.Set(CursivisTheme.WithAlpha(p.TextMain, 0.28f), CursivisTheme.WithAlpha(p.TextMain, 0.28f), Color.clear, 2f, 0f);
            _handle.Set(CursivisTheme.WithAlpha(p.TextMain, 0.55f), CursivisTheme.WithAlpha(p.TextMain, 0.45f), Color.clear, 7f, 0f);
            _evidenceChip.Set(p.ChipA, p.ChipB, p.ChipBorder, 14f, 1f);
            _evidenceText.color = p.TextMuted;
            ThemeButton.SetGlyph(p.IsDark ? Icons.LightMode : Icons.DarkMode);
            if (IsShowingResult && _lastMarkdown != null) _text.text = MarkdownTmp.Convert(_lastMarkdown, StyleFor(p));
            if (IsShowingResult) SetChips(_lastUncertainty, _lastNote, _lastEvidence);
        }

        static MarkdownTmp.Style StyleFor(Palette p) => new MarkdownTmp.Style
        {
            Heading = "#" + ColorUtility.ToHtmlStringRGB(p.Heading),
            Bullet = "#" + ColorUtility.ToHtmlStringRGB(p.Bullet),
            CodeFg = "#" + ColorUtility.ToHtmlStringRGB(p.CodeFg),
            CodeBg = "#" + ColorUtility.ToHtmlStringRGBA(p.CodeBg),
            Muted = "#" + ColorUtility.ToHtmlStringRGB(p.TextMuted)
        };

        string _lastMarkdown;
        string _lastUncertainty, _lastNote, _lastEvidence;

        public bool Visible => _anim != null && _anim.Visible;
        public void Show() => _anim.Show();
        public void Hide(Action done = null) { StopSpeakingVisual(); _anim.Hide(done); }

        public void ShowLoading(string actionLabel, string status)
        {
            IsShowingResult = false;
            _title.text = actionLabel ?? "";
            _meta.text = "Output  \u00B7  " + (actionLabel ?? "Working");
            _timing.text = "";
            _status.text = status ?? $"Cursivis is working on \u201C{actionLabel}\u201D\u2026";
            _skeleton.gameObject.SetActive(true);
            _errorRoot.gameObject.SetActive(false);
            _content.gameObject.SetActive(false);
            _scrollThumb.gameObject.SetActive(false);
            SetChips(null, null, null);
            SetFooterInteractable(false);
            if (!Visible) Show();
        }

        public void ShowError(string actionLabel, string message, bool canRetry)
        {
            IsShowingResult = false;
            _title.text = actionLabel ?? "";
            _meta.text = "Output  \u00B7  couldn\u2019t finish";
            _skeleton.gameObject.SetActive(false);
            _content.gameObject.SetActive(false);
            _scrollThumb.gameObject.SetActive(false);
            _errorRoot.gameObject.SetActive(true);
            _errorRoot.Find("ErrorText").GetComponent<TextMeshProUGUI>().text = message;
            RetryButton.gameObject.SetActive(canRetry);
            SetChips(null, null, null);
            SetFooterInteractable(false);
            MoreButton.SetInteractable(true);
            if (!Visible) Show();
        }

        public void ShowResult(ResultResponse r, string actionLabel, bool fromVoice)
        {
            IsShowingResult = true;
            _lastMarkdown = r.answer ?? "";
            CurrentPlainAnswer = MarkdownTmp.ToSpeech(_lastMarkdown);
            _title.text = string.IsNullOrEmpty(r.title) ? actionLabel : r.title;
            var heard = fromVoice && !string.IsNullOrEmpty(r.transcript) ? $"You said \u201C{Trim(r.transcript, 60)}\u201D" : null;
            _meta.text = heard ?? "Output  \u00B7  " + (actionLabel ?? r.actionLabel ?? "Result");
            _timing.text = r.latencyMs > 0 ? $"{r.latencyMs / 1000f:0.0} s" : "";
            _skeleton.gameObject.SetActive(false);
            _errorRoot.gameObject.SetActive(false);
            _content.gameObject.SetActive(true);
            _text.text = MarkdownTmp.Convert(_lastMarkdown, StyleFor(CursivisTheme.P));
            _text.ForceMeshUpdate();
            _totalWords = Mathf.Max(1, _text.textInfo.wordCount);
            _revealWords = 0;
            _text.maxVisibleWords = 0;
            // original reveal cadence: 1-4 words per step every 12-20 ms depending on length
            _revealStep = _totalWords > 320 ? 4 : _totalWords > 200 ? 3 : _totalWords > 110 ? 2 : 1;
            _revealInterval = _totalWords > 320 ? 0.012f : _totalWords > 200 ? 0.014f : _totalWords > 110 ? 0.017f : 0.020f;
            _lastUncertainty = r.uncertainty;
            _lastNote = r.uncertaintyNote;
            _lastEvidence = r.evidence;
            SetChips(r.uncertainty, r.uncertaintyNote, r.evidence);
            for (int i = 0; i < _followChips.Count; i++)
            {
                bool on = r.followUps != null && i < r.followUps.Count;
                _followChips[i].gameObject.SetActive(on);
                if (on)
                {
                    _followChips[i].Tag = r.followUps[i];
                    _followChips[i].SetLabel(r.followUps[i]);
                    var w = Mathf.Clamp(36f + r.followUps[i].Length * 7.4f, 110f, 190f);
                    _followChips[i].Background.rectTransform.sizeDelta = new Vector2(w, 34f);
                }
            }
            _followRow.gameObject.SetActive(r.followUps != null && r.followUps.Count > 0);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Scroll.verticalNormalizedPosition = 1f;
            SetFooterInteractable(true);
            if (!Visible) Show();
        }

        static string Trim(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "\u2026";

        void SetChips(string uncertainty, string note, string evidence)
        {
            var p = CursivisTheme.P;
            bool showU = !string.IsNullOrEmpty(uncertainty) && uncertainty != "none" && uncertainty != "low";
            _uncertaintyChip.gameObject.SetActive(showU || uncertainty == "low");
            if (showU)
            {
                var c = uncertainty == "high" ? p.Error : p.Warning;
                _uncertaintyChip.Set(CursivisTheme.WithAlpha(c, 0.16f), CursivisTheme.WithAlpha(c, 0.10f), CursivisTheme.WithAlpha(c, 0.8f), 14f, 1f);
                _uncertaintyText.color = c;
                _uncertaintyText.text = $"<font=\"MaterialIcons-Regular SDF\">{Icons.Warning}</font> {(uncertainty == "high" ? "Not enough evidence" : "Partly uncertain")}{(string.IsNullOrEmpty(note) ? "" : ": " + note)}";
            }
            else if (uncertainty == "low")
            {
                _uncertaintyChip.Set(CursivisTheme.WithAlpha(p.Success, 0.14f), CursivisTheme.WithAlpha(p.Success, 0.08f), CursivisTheme.WithAlpha(p.Success, 0.6f), 14f, 1f);
                _uncertaintyText.color = p.Success;
                _uncertaintyText.text = $"<font=\"MaterialIcons-Regular SDF\">{Icons.Check}</font> Grounded in your selection";
            }
            bool showE = !string.IsNullOrEmpty(evidence);
            _evidenceChip.gameObject.SetActive(showE);
            if (showE) _evidenceText.text = $"<font=\"MaterialIcons-Regular SDF\">{Icons.Eye}</font> {evidence}";
        }

        void SetFooterInteractable(bool on)
        {
            FollowUpButton.SetInteractable(on);
            MoreButton.SetInteractable(on);
            RegenButton.SetInteractable(on);
            CopyButton.SetInteractable(on);
            SpeakButton.SetInteractable(on);
            TalkButton.SetInteractable(on);
        }

        public void SetSpeaking(bool speaking)
        {
            _speaking = speaking;
            SpeakButton.SetGlyph(speaking ? Icons.Stop : Icons.Speaker);
            SpeakButton.SetSelected(speaking);
        }

        void StopSpeakingVisual() => SetSpeaking(false);

        public void SetPinned(bool pinned) => PinButton.SetSelected(pinned);
        public void SetExpanded(bool expanded) => ExpandButton.SetGlyph(expanded ? Icons.Collapse : Icons.Expand);
        public void SetListening(bool listening)
        {
            FollowUpButton.SetSelected(listening);
            FollowUpButton.SetLabel(listening ? "Listening\u2026" : "Ask follow-up");
            TalkButton.SetSelected(listening);
        }

        public void SetStatus(string text) { if (_status != null) _status.text = text; }

        void Update()
        {
            if (IsShowingResult && _revealWords < _totalWords)
            {
                _revealTimer += Time.deltaTime;
                while (_revealTimer >= _revealInterval && _revealWords < _totalWords)
                {
                    _revealTimer -= _revealInterval;
                    _revealWords = Mathf.Min(_totalWords, _revealWords + _revealStep);
                }
                _text.maxVisibleWords = _revealWords >= _totalWords ? 99999 : _revealWords;
            }
            if (_skeleton.gameObject.activeSelf)
            {
                float t = Time.time * 1.6f;
                var p = CursivisTheme.P;
                for (int i = 0; i < _skeletonBars.Count; i++)
                {
                    float a = 0.05f + 0.05f * (0.5f + 0.5f * Mathf.Sin(t - i * 0.55f));
                    _skeletonBars[i].Set(CursivisTheme.WithAlpha(p.TextMain, a + 0.03f), CursivisTheme.WithAlpha(p.TextMain, a), Color.clear, 6f, 0f);
                }
            }
            UpdateWordmarkShine();
            UpdateScrollThumb();
        }

        void UpdateScrollThumb()
        {
            if (!_content.gameObject.activeSelf) return;
            var vp = Scroll.viewport.rect.height;
            var ch = _content.rect.height;
            bool scrollable = ch > vp + 2f;
            _scrollThumb.gameObject.SetActive(scrollable);
            if (!scrollable) return;
            float h = Mathf.Max(19.6f, vp * vp / ch);
            float travel = vp - h;
            float y = (Scroll.verticalNormalizedPosition - 0.5f) * travel;
            _scrollThumb.rectTransform.sizeDelta = new Vector2(4f, h);
            _scrollThumb.rectTransform.anchoredPosition = new Vector2(_scrollThumb.rectTransform.anchoredPosition.x, y);
        }

        void UpdateWordmarkShine()
        {
            var p = CursivisTheme.P;
            float period = p.IsDark ? 2.65f : 3.35f;
            _shineT = (Time.time % period) / period;
            _wordmark.ForceMeshUpdate();
            var info = _wordmark.textInfo;
            if (info.characterCount == 0) return;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                minX = Mathf.Min(minX, ch.bottomLeft.x);
                maxX = Mathf.Max(maxX, ch.topRight.x);
            }
            if (maxX <= minX) return;
            float sweep = Mathf.Lerp(-0.08f, 1.08f, _shineT);
            Color32 b = p.TitleShineBase, s = p.TitleShine;
            float strength = p.IsDark ? 0.94f : 0.32f;
            for (int i = 0; i < info.characterCount; i++)
            {
                var ch = info.characterInfo[i];
                if (!ch.isVisible) continue;
                var mesh = info.meshInfo[ch.materialReferenceIndex];
                for (int v = 0; v < 4; v++)
                {
                    float u = (mesh.vertices[ch.vertexIndex + v].x - minX) / (maxX - minX);
                    float w = Mathf.Clamp01(1f - Mathf.Abs(u - sweep) / 0.18f) * strength;
                    mesh.colors32[ch.vertexIndex + v] = Color32.Lerp(b, s, w);
                }
            }
            _wordmark.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary>Scroll by a fraction of a page (used by voice "scroll down" and automation).</summary>
        public void ScrollBy(float pages)
        {
            var vp = Scroll.viewport.rect.height;
            var ch = Mathf.Max(1f, _content.rect.height - vp);
            Scroll.verticalNormalizedPosition = Mathf.Clamp01(Scroll.verticalNormalizedPosition - pages * vp / ch);
        }
    }
}
