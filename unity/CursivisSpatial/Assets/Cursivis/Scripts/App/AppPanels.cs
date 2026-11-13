using System;
using System.Collections.Generic;
using Cursivis.Spatial.Selection;
using Cursivis.Spatial.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cursivis.Spatial.App
{
    /// <summary>
    /// The Cursivis workspace: original, unbranded documents owned by the app (research abstract, Spanish notice,
    /// public-domain photo, buggy code). Gives judges useful content within seconds even with an empty desk.
    /// </summary>
    public sealed class Workspace : MonoBehaviour
    {
        public readonly List<WorkspaceText> Texts = new List<WorkspaceText>();
        public readonly List<WorkspaceImage> Images = new List<WorkspaceImage>();
        public readonly List<Canvas> Canvases = new List<Canvas>();
        public Texture2D Photo { get; private set; }
        public event Action<WorkspaceText, string, Vector3[]> TextSelected;
        public event Action<WorkspaceImage, Rect, Vector3[]> ImageSelected;
        readonly List<Action> _theme = new List<Action>();
        public bool Shown { get; private set; } = true;

        public const string Research =
            "Urban heat islands raise night-time temperatures in dense neighbourhoods by up to 4 \u00B0C compared with surrounding rural areas. " +
            "In a two-summer field study across 38 rooftops, we compared extensive green roofs with white reflective membranes. " +
            "Green roofs reduced peak surface temperature by 21 \u00B0C and indoor top-floor temperature by 1.8 \u00B0C, while reflective roofs " +
            "achieved a larger surface reduction (27 \u00B0C) but a smaller indoor effect (1.1 \u00B0C) because they provide no evapotranspiration. " +
            "Benefits were strongest during heatwaves and declined when substrate moisture fell below 12%. We argue that irrigation-aware " +
            "green roofs should be prioritised for buildings with vulnerable occupants.";

        public const string Spanish =
            "Aviso a los vecinos: el pr\u00F3ximo martes 3 de diciembre se cortar\u00E1 el suministro de agua entre las 9:00 y las 14:00 " +
            "por obras de mantenimiento en la tuber\u00EDa principal. Les rogamos que llenen recipientes con antelaci\u00F3n y que no utilicen " +
            "el ascensor durante ese horario, ya que los operarios necesitar\u00E1n acceso al cuarto de m\u00E1quinas. " +
            "Disculpen las molestias. \u2014 La administraci\u00F3n";

        public const string Code =
            "// Returns the average rating of reviews with at least `minVotes` votes.\n" +
            "function averageRating(reviews, minVotes) {\n" +
            "  let total = 0;\n" +
            "  let count = 0;\n" +
            "  for (let i = 0; i <= reviews.length; i++) {\n" +
            "    if (reviews[i].votes > minVotes) {\n" +
            "      total += reviews[i].rating;\n" +
            "      count++;\n" +
            "    }\n" +
            "  }\n" +
            "  return total / count;\n" +
            "}";

        public void Build(IList<Canvas> canvases)
        {
            Canvases.AddRange(canvases);
            Photo = Resources.Load<Texture2D>("Workspace/photo-footbridge");
            BuildTextPanel(canvases[0], "Research abstract", "research_paper", Icons.Book, Research, false);
            BuildTextPanel(canvases[1], "Aviso a los vecinos", "notice", Icons.Translate, Spanish, false);
            BuildImagePanel(canvases[2], "Photo \u00B7 coastal footbridge", Photo);
            BuildTextPanel(canvases[3], "reviews.js", "code", Icons.Code, Code, true);
            CursivisTheme.Changed += ApplyTheme;
            ApplyTheme();
        }

        void OnDestroy() => CursivisTheme.Changed -= ApplyTheme;

        void ApplyTheme() { foreach (var a in _theme) a(); }

        RoundedRect Shell(Canvas c, string title, string kind, string glyph, out RectTransform body)
        {
            var root = (RectTransform)c.transform;
            var size = root.sizeDelta;
            var shell = UiKit.Panel(root, "Shell", size - new Vector2(6, 6), Vector2.zero, Color.white, Color.white, Color.white, CursivisTheme.RadiusCard);
            var icon = UiKit.Icon(shell.transform, glyph, 20f, Color.white, new Vector2(-size.x * 0.5f + 30f, size.y * 0.5f - 30f));
            var t = UiKit.Text(shell.transform, "Title", title, CursivisTheme.TextSubtitle, Color.white, UiKit.SemiBold, TextAlignmentOptions.Left, new Vector2(size.x - 150f, 24f), new Vector2(-size.x * 0.5f + 48f + (size.x - 150f) * 0.5f, size.y * 0.5f - 30f));
            var k = UiKit.Text(shell.transform, "Kind", kind, CursivisTheme.TextMeta, Color.white, UiKit.Regular, TextAlignmentOptions.Right, new Vector2(120f, 20f), new Vector2(size.x * 0.5f - 76f, size.y * 0.5f - 30f));
            var bodyBg = UiKit.Panel(shell.transform, "Body", new Vector2(size.x - 26f, size.y - 66f), new Vector2(0, -20f), Color.white, Color.white, Color.white, CursivisTheme.RadiusBody);
            body = bodyBg.rectTransform;
            _theme.Add(() =>
            {
                var p = CursivisTheme.P;
                shell.Set(p.ShellA, p.ShellB, p.ShellBorder, CursivisTheme.RadiusCard);
                shell.SetShadow(14f, p.ShadowAlpha);
                bodyBg.Set(p.Surface, p.Surface, p.SurfaceBorder, CursivisTheme.RadiusBody);
                icon.color = p.TextMuted;
                t.color = p.TextMain;
                k.color = p.TextSoft;
            });
            return shell;
        }

        void BuildTextPanel(Canvas c, string title, string docType, string glyph, string content, bool code)
        {
            Shell(c, title, code ? "code \u00B7 pinch or drag to select" : "pinch or drag to select", glyph, out var body);
            var text = UiKit.Text(body, "Text", content, code ? CursivisTheme.TextMeta + 0.6f : CursivisTheme.TextBody - 0.4f, Color.white, code ? UiKit.Mono : UiKit.Regular, TextAlignmentOptions.TopLeft);
            UiKit.Stretch(text.rectTransform, 14, 14, 12, 12);
            text.overflowMode = TextOverflowModes.Truncate;
            text.lineSpacing = code ? 2f : 4f;
            var item = text.gameObject.AddComponent<WorkspaceText>();
            item.Init(text, content, title, docType);
            item.Selected += (w, s, corners) => TextSelected?.Invoke(w, s, corners);
            Texts.Add(item);
            _theme.Add(() => text.color = CursivisTheme.P.TextMain);
        }

        void BuildImagePanel(Canvas c, string title, Texture2D tex)
        {
            Shell(c, title, "pinch, or drag a region", Icons.Image, out var body);
            var frame = UiKit.Rect(body, "Frame", Vector2.zero);
            UiKit.Stretch(frame, 10, 10, 10, 10);
            var img = UiKit.Rect(frame, "Photo", Vector2.zero);
            var raw = img.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            // fit preserving aspect inside the frame
            float fw = ((RectTransform)c.transform).sizeDelta.x - 46f, fh = ((RectTransform)c.transform).sizeDelta.y - 106f;
            float aspect = tex != null ? (float)tex.width / tex.height : 1.5f;
            var size = fw / fh > aspect ? new Vector2(fh * aspect, fh) : new Vector2(fw, fw / aspect);
            img.anchorMin = img.anchorMax = new Vector2(0.5f, 0.5f);
            img.sizeDelta = size;
            var item = img.gameObject.AddComponent<WorkspaceImage>();
            item.Init(raw, "Photo: coastal footbridge (public domain, BLM Oregon & Washington)");
            item.Selected += (w, uv, corners) => ImageSelected?.Invoke(w, uv, corners);
            Images.Add(item);
        }

        public void SetShown(bool shown)
        {
            Shown = shown;
            foreach (var c in Canvases) c.gameObject.SetActive(shown);
        }

        public void ClearHighlights()
        {
            foreach (var t in Texts) t.ClearHighlight();
            foreach (var i in Images) i.ClearHighlight();
        }
    }

    /// <summary>First-run guide (also opened from Help). Explains the loop and what is sent to the cloud.</summary>
    public sealed class Onboarding : MonoBehaviour
    {
        public CursivisButton GotIt, TryWorkspace, TrySnip;
        PanelAnimator _anim;

        public void Build()
        {
            var root = (RectTransform)transform;
            var size = root.sizeDelta;
            var shell = UiKit.Panel(root, "Shell", size - new Vector2(8, 8), Vector2.zero, Color.white, Color.white, Color.white, CursivisTheme.RadiusCard);
            var title = UiKit.Text(shell.transform, "Title", "Cursivis Spatial", CursivisTheme.TextH1, Color.white, UiKit.SemiBold, TextAlignmentOptions.Left, new Vector2(size.x - 60, 30), new Vector2(0, size.y * 0.5f - 40f));
            var tag = UiKit.Text(shell.transform, "Tagline", CursivisVersion.Tagline, CursivisTheme.TextMeta, Color.white, UiKit.Regular, TextAlignmentOptions.Left, new Vector2(size.x - 60, 20), new Vector2(0, size.y * 0.5f - 66f));
            string[] steps =
            {
                $"<font=\"MaterialIcons-Regular SDF\">{Icons.Snip}</font>  <b>Select.</b> Pinch a paragraph or photo in the workspace, or tap <b>Snip</b> and pinch-drag a box around anything in your room.",
                $"<font=\"MaterialIcons-Regular SDF\">{Icons.Sparkle}</font>  <b>Ask Cursivis.</b> Pinch the chip next to your selection. The orb reads it and suggests four next steps.",
                $"<font=\"MaterialIcons-Regular SDF\">{Icons.Mic}</font>  <b>Act or Talk.</b> Pinch a suggestion, or hold <b>Talk</b> and ask anything about it. Pinch the orb to talk without a selection."
            };
            var stepTexts = new List<TextMeshProUGUI>();
            for (int i = 0; i < steps.Length; i++)
            {
                var st = UiKit.Text(shell.transform, "Step" + i, steps[i], CursivisTheme.TextBody - 1f, Color.white, UiKit.Regular, TextAlignmentOptions.TopLeft, new Vector2(size.x - 60, 52), new Vector2(0, size.y * 0.5f - 112f - i * 58f));
                stepTexts.Add(st);
            }
            var privacy = UiKit.Text(shell.transform, "Privacy", "Only the selection you ask about (and your voice while you hold Talk) is sent to Google Gemini through the Cursivis server. The camera runs only while Snip is active.", CursivisTheme.TextMeta, Color.white, UiKit.Regular, TextAlignmentOptions.TopLeft, new Vector2(size.x - 60, 40), new Vector2(0, -size.y * 0.5f + 92f));
            TrySnip = CursivisButton.Create(shell.transform, "TrySnip", "Snip my room", Icons.Snip, new Vector2(170, 44), new Vector2(-size.x * 0.5f + 30f + 85f, -size.y * 0.5f + 40f), ButtonStyle.Primary);
            TryWorkspace = CursivisButton.Create(shell.transform, "TryWorkspace", "Use workspace", Icons.Workspace, new Vector2(180, 44), new Vector2(-size.x * 0.5f + 30f + 170f + 12f + 90f, -size.y * 0.5f + 40f), ButtonStyle.Primary);
            GotIt = CursivisButton.Create(shell.transform, "GotIt", "Got it", Icons.Check, new Vector2(120, 44), new Vector2(size.x * 0.5f - 30f - 60f, -size.y * 0.5f + 40f), ButtonStyle.Accent);
            _anim = gameObject.AddComponent<PanelAnimator>();
            _anim.Init(shell.rectTransform);
            void Theme()
            {
                var p = CursivisTheme.P;
                shell.Set(p.ShellA, p.ShellB, p.ShellBorder, CursivisTheme.RadiusCard);
                shell.SetShadow(18f, p.ShadowAlpha);
                title.color = p.TextMain;
                tag.color = p.TextMuted;
                privacy.color = p.TextSoft;
                foreach (var s in stepTexts) s.color = p.TextMain;
            }
            Theme();
            CursivisTheme.Changed += Theme;
        }

        public void Show() => _anim.Show();
        public void Hide() => _anim.Hide();
        public bool Visible => _anim != null && _anim.Visible;
    }

    /// <summary>Short, honest status messages (errors, permissions, tips). Non-interactive.</summary>
    public sealed class Toast : MonoBehaviour
    {
        RoundedRect _bg;
        TextMeshProUGUI _text, _icon;
        CanvasGroup _group;
        float _until, _alpha;

        public void Build()
        {
            var root = (RectTransform)transform;
            _bg = UiKit.Panel(root, "Toast", root.sizeDelta, Vector2.zero, Color.white, Color.white, Color.white, root.sizeDelta.y * 0.5f, 1f);
            _icon = UiKit.Icon(_bg.transform, Icons.Warning, 20f, Color.white, new Vector2(-root.sizeDelta.x * 0.5f + 28f, 0));
            _text = UiKit.Text(_bg.transform, "Text", "", CursivisTheme.TextMeta + 1f, Color.white, UiKit.SemiBold, TextAlignmentOptions.Left);
            UiKit.Stretch(_text.rectTransform, 48, 18, 4, 4);
            _group = UiKit.Group(gameObject);
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        public void Show(string message, bool warning = true, float seconds = 4f)
        {
            var p = CursivisTheme.P;
            _bg.Set(p.ShellA, p.ShellB, warning ? CursivisTheme.WithAlpha(p.Warning, 0.7f) : p.ShellBorder, _bg.Radius, 1.2f);
            _bg.SetShadow(10f, p.ShadowAlpha);
            _icon.text = warning ? Icons.Warning : Icons.Check;
            _icon.color = warning ? p.Warning : p.Success;
            _text.color = p.TextMain;
            _text.text = message;
            _until = Time.time + seconds;
            Debug.Log($"[CursivisToast] {message}");
        }

        void Update()
        {
            float target = Time.time < _until ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.deltaTime / 0.18f);
            _group.alpha = _alpha;
        }
    }
}
