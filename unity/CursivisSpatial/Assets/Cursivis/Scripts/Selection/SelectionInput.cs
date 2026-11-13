using System;
using System.Text;
using Cursivis.Spatial.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cursivis.Spatial.Selection
{
    public static class PointerWorld
    {
        /// <summary>World hit point of an Interaction SDK / uGUI pointer event on a world-space canvas.</summary>
        public static bool TryGet(PointerEventData e, RectTransform plane, out Vector3 world)
        {
            if (e.pointerCurrentRaycast.gameObject != null && e.pointerCurrentRaycast.worldPosition != Vector3.zero)
            {
                world = e.pointerCurrentRaycast.worldPosition;
                return true;
            }
            var cam = e.pressEventCamera != null ? e.pressEventCamera : e.enterEventCamera;
            return RectTransformUtility.ScreenPointToWorldPointInRectangle(plane, e.position, cam, out world);
        }
    }

    /// <summary>
    /// Large transparent world-space surface shown only while "Snip" is armed. The user pinches and drags with the hand
    /// ray (Interaction SDK ray interaction -> uGUI pointer events) to draw a rectangle over something in the room.
    /// The surface is placed at the estimated depth of what the user is looking at, so the drawn rectangle lies on the
    /// physical surface and its corners project correctly into the passthrough camera image.
    /// </summary>
    public sealed class SnipSurface : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler, IInitializePotentialDragHandler
    {
        public event Action<Vector3[]> Completed;   // BL, TL, TR, BR world corners
        public event Action Started;
        public event Action<string> Rejected;

        RectTransform _rt;
        RoundedRect _rect, _cameraFrame, _hitArea;
        RoundedRect[] _handles = new RoundedRect[4];
        TextMeshProUGUI _hint;
        bool _dragging;
        Vector2 _startLocal, _currentLocal;
        int _pointerId = int.MinValue;
        public bool Dragging => _dragging;
        public const float MinSizeMm = 30f;

        public void Build()
        {
            _rt = (RectTransform)transform;
            var size = _rt.sizeDelta;
            _hitArea = UiKit.Panel(_rt, "HitArea", size, Vector2.zero, new Color(0, 0, 0, 0.001f), new Color(0, 0, 0, 0.001f), Color.clear, 0f, 0f);
            _hitArea.raycastTarget = true;
            _cameraFrame = UiKit.Panel(_rt, "CameraFrame", new Vector2(400, 300), Vector2.zero, Color.clear, Color.clear, Color.white, 18f, 2.2f);
            _cameraFrame.RimHighlight = 0f;
            _rect = UiKit.Panel(_rt, "SnipRect", new Vector2(10, 10), Vector2.zero, Color.clear, Color.clear, Color.white, 10f, 3f);
            _rect.RimHighlight = 0f;
            for (int i = 0; i < 4; i++)
            {
                _handles[i] = UiKit.Panel(_rt, "Handle" + i, new Vector2(16, 16), Vector2.zero, Color.white, Color.white, Color.white, 8f, 0f);
            }
            _hint = UiKit.Text(_rt, "Hint", "Pinch and drag to frame what you want Cursivis to see", 22f, Color.white, UiKit.SemiBold, TextAlignmentOptions.Center, new Vector2(900, 40), new Vector2(0, 220));
            ApplyTheme();
            CursivisTheme.Changed += ApplyTheme;
            SetRectVisible(false);
        }

        void OnDestroy() => CursivisTheme.Changed -= ApplyTheme;

        void ApplyTheme()
        {
            var p = CursivisTheme.P;
            _rect.Set(p.SnipFill, p.SnipFill, p.Snip, 10f, 3f);
            foreach (var h in _handles) h.Set(p.Snip, p.Snip, Color.clear, 8f, 0f);
            _cameraFrame.Set(Color.clear, Color.clear, CursivisTheme.WithAlpha(p.TextMain, 0.35f), 18f, 2.2f);
            _hint.color = Color.white;
            _hint.outlineWidth = 0.18f;
            _hint.outlineColor = new Color32(0, 0, 0, 160);
        }

        public void SetHint(string text) => _hint.text = text;

        /// <summary>Shows where the passthrough camera can actually see (its FOV is smaller than the headset view).</summary>
        public void SetCameraFrame(Rect localRect, bool visible)
        {
            _cameraFrame.gameObject.SetActive(visible);
            if (!visible) return;
            _cameraFrame.rectTransform.anchoredPosition = localRect.center;
            _cameraFrame.rectTransform.sizeDelta = localRect.size;
        }

        public void ResetDrawing()
        {
            _dragging = false;
            _pointerId = int.MinValue;
            SetRectVisible(false);
        }

        void SetRectVisible(bool on)
        {
            _rect.gameObject.SetActive(on);
            foreach (var h in _handles) h.gameObject.SetActive(on);
        }

        Vector2 ToLocal(Vector3 world) => _rt.InverseTransformPoint(world);

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            if (_dragging) return;
            if (!PointerWorld.TryGet(e, _rt, out var w)) return;
            _pointerId = e.pointerId;
            _dragging = true;
            _startLocal = _currentLocal = ToLocal(w);
            SetRectVisible(true);
            UpdateRect();
            UiKit.PlayClick(false, w);
            CursivisMetrics.Mark("snip_down");
            Started?.Invoke();
        }

        public void OnBeginDrag(PointerEventData e) { }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging || e.pointerId != _pointerId) return;
            if (!PointerWorld.TryGet(e, _rt, out var w)) return;
            _currentLocal = ToLocal(w);
            var half = _rt.sizeDelta * 0.5f;
            _currentLocal = new Vector2(Mathf.Clamp(_currentLocal.x, -half.x, half.x), Mathf.Clamp(_currentLocal.y, -half.y, half.y));
            UpdateRect();
        }

        public void OnEndDrag(PointerEventData e) { }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging || e.pointerId != _pointerId) return;
            if (PointerWorld.TryGet(e, _rt, out var w)) _currentLocal = ToLocal(w);
            _dragging = false;
            UpdateRect();
            var min = Vector2.Min(_startLocal, _currentLocal);
            var max = Vector2.Max(_startLocal, _currentLocal);
            var size = max - min;
            float mm = _rt.lossyScale.x * 1000f; // canvas units -> mm in the world
            if (size.x * mm < MinSizeMm || size.y * mm < MinSizeMm)
            {
                SetRectVisible(false);
                Rejected?.Invoke("That selection is too small. Pinch and drag a larger box.");
                return;
            }
            Completed?.Invoke(CornersOf(min, max));
        }

        /// <summary>Programmatic snip used by the automation harness (same code path after input).</summary>
        public void SimulateDrag(Vector2 localA, Vector2 localB)
        {
            _startLocal = localA;
            _currentLocal = localB;
            SetRectVisible(true);
            UpdateRect();
            var min = Vector2.Min(localA, localB);
            var max = Vector2.Max(localA, localB);
            Completed?.Invoke(CornersOf(min, max));
        }

        Vector3[] CornersOf(Vector2 min, Vector2 max) => new[]
        {
            _rt.TransformPoint(new Vector3(min.x, min.y, 0)),
            _rt.TransformPoint(new Vector3(min.x, max.y, 0)),
            _rt.TransformPoint(new Vector3(max.x, max.y, 0)),
            _rt.TransformPoint(new Vector3(max.x, min.y, 0))
        };

        void UpdateRect()
        {
            var min = Vector2.Min(_startLocal, _currentLocal);
            var max = Vector2.Max(_startLocal, _currentLocal);
            var rt = _rect.rectTransform;
            rt.anchoredPosition = (min + max) * 0.5f;
            rt.sizeDelta = Vector2.Max(max - min, new Vector2(6, 6));
            _handles[0].rectTransform.anchoredPosition = new Vector2(min.x, min.y);
            _handles[1].rectTransform.anchoredPosition = new Vector2(min.x, max.y);
            _handles[2].rectTransform.anchoredPosition = new Vector2(max.x, max.y);
            _handles[3].rectTransform.anchoredPosition = new Vector2(max.x, min.y);
        }
    }

    /// <summary>
    /// Selectable text owned by Cursivis Spatial. Pinch-tap selects the whole block; pinch-drag selects a word range.
    /// The original text is sent (no OCR needed). Highlight uses TMP mark tags.
    /// </summary>
    public sealed class WorkspaceText : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        public string Title;
        public string DocType;
        public TextMeshProUGUI Text;
        public string SourceText;
        public event Action<WorkspaceText, string, Vector3[]> Selected;

        int _anchorWord = -1, _focusWord = -1;
        bool _down;
        Vector2 _downLocal;
        int _pointerId;

        public void Init(TextMeshProUGUI text, string source, string title, string docType)
        {
            Text = text;
            SourceText = source;
            Title = title;
            DocType = docType;
            Text.text = Escape(source);
            Text.raycastTarget = true;
        }

        static string Escape(string s) => s.Replace("<", "<\u200B");

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            if (!PointerWorld.TryGet(e, Text.rectTransform, out var w)) return;
            _down = true;
            _pointerId = e.pointerId;
            _downLocal = Text.rectTransform.InverseTransformPoint(w);
            _anchorWord = _focusWord = WordAt(_downLocal);
            CursivisMetrics.Mark("select_down");
            Highlight();
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_down || e.pointerId != _pointerId) return;
            if (!PointerWorld.TryGet(e, Text.rectTransform, out var w)) return;
            var local = (Vector2)Text.rectTransform.InverseTransformPoint(w);
            var word = WordAt(local);
            if (word >= 0 && word != _focusWord)
            {
                _focusWord = word;
                Highlight();
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_down || e.pointerId != _pointerId) return;
            _down = false;
            bool range = _anchorWord >= 0 && _focusWord >= 0 && _anchorWord != _focusWord;
            string selected = range ? RangeText(Mathf.Min(_anchorWord, _focusWord), Mathf.Max(_anchorWord, _focusWord)) : SourceText;
            if (!range) { _anchorWord = 0; _focusWord = Text.textInfo.wordCount - 1; }
            Highlight();
            Selected?.Invoke(this, selected, WorldCornersOfSelection());
        }

        public void SelectAll()
        {
            Text.ForceMeshUpdate();
            _anchorWord = 0;
            _focusWord = Mathf.Max(0, Text.textInfo.wordCount - 1);
            Highlight();
            Selected?.Invoke(this, SourceText, WorldCornersOfSelection());
        }

        public void ClearHighlight()
        {
            _anchorWord = _focusWord = -1;
            Text.text = Escape(SourceText);
        }

        int WordAt(Vector2 local)
        {
            Text.ForceMeshUpdate();
            var info = Text.textInfo;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < info.wordCount; i++)
            {
                var wi = info.wordInfo[i];
                if (wi.firstCharacterIndex < 0 || wi.lastCharacterIndex >= info.characterCount) continue;
                var a = info.characterInfo[wi.firstCharacterIndex];
                var b = info.characterInfo[wi.lastCharacterIndex];
                var line = info.lineInfo[a.lineNumber];
                float x0 = a.bottomLeft.x, x1 = b.topRight.x, y0 = line.descender, y1 = line.ascender;
                if (local.x >= x0 && local.x <= x1 && local.y >= y0 && local.y <= y1) return i;
                float dx = Mathf.Max(0, Mathf.Max(x0 - local.x, local.x - x1));
                float dy = Mathf.Max(0, Mathf.Max(y0 - local.y, local.y - y1));
                float d = dx * dx + dy * dy * 4f;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        string RangeText(int a, int b)
        {
            var info = Text.textInfo;
            int c0 = info.wordInfo[a].firstCharacterIndex;
            int c1 = info.wordInfo[b].lastCharacterIndex;
            int s = info.characterInfo[c0].index;
            int e = info.characterInfo[c1].index + info.characterInfo[c1].stringLength;
            var shown = Escape(SourceText);
            var raw = shown.Substring(Mathf.Clamp(s, 0, shown.Length), Mathf.Clamp(e - s, 0, shown.Length - s));
            return raw.Replace("<\u200B", "<");
        }

        void Highlight()
        {
            if (_anchorWord < 0 || _focusWord < 0) return;
            Text.ForceMeshUpdate();
            var info = Text.textInfo;
            int a = Mathf.Min(_anchorWord, _focusWord), b = Mathf.Max(_anchorWord, _focusWord);
            if (b >= info.wordCount) return;
            int s = info.characterInfo[info.wordInfo[a].firstCharacterIndex].index;
            int lc = info.wordInfo[b].lastCharacterIndex;
            int e = info.characterInfo[lc].index + info.characterInfo[lc].stringLength;
            var shown = Escape(SourceText);
            s = Mathf.Clamp(s, 0, shown.Length);
            e = Mathf.Clamp(e, s, shown.Length);
            var hl = "#" + ColorUtility.ToHtmlStringRGBA(CursivisTheme.P.Highlight);
            Text.text = shown.Substring(0, s) + $"<mark={hl} padding=\"2,2,1,1\">" + shown.Substring(s, e - s) + "</mark>" + shown.Substring(e);
        }

        Vector3[] WorldCornersOfSelection()
        {
            var corners = new Vector3[4];
            Text.rectTransform.GetWorldCorners(corners); // BL, TL, TR, BR
            return corners;
        }
    }

    /// <summary>
    /// Selectable image owned by Cursivis Spatial: pinch-tap selects the whole image, pinch-drag snips a region.
    /// Pixels come straight from the source texture (exact crop, no camera involved).
    /// </summary>
    public sealed class WorkspaceImage : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        public string Title;
        public RawImage Image;
        public event Action<WorkspaceImage, Rect, Vector3[]> Selected; // uv rect (0..1, bottom-left origin)
        RoundedRect _rect;
        bool _down;
        int _pointerId;
        Vector2 _a, _b;

        public void Init(RawImage image, string title)
        {
            Image = image;
            Title = title;
            Image.raycastTarget = true;
            _rect = UiKit.Panel(Image.transform, "Region", new Vector2(10, 10), Vector2.zero, Color.clear, Color.clear, Color.white, 6f, 2.5f);
            _rect.RimHighlight = 0f;
            _rect.gameObject.SetActive(false);
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        Vector2 Local(Vector3 w)
        {
            var l = (Vector2)Image.rectTransform.InverseTransformPoint(w);
            var r = Image.rectTransform.rect;
            return new Vector2(Mathf.Clamp(l.x, r.xMin, r.xMax), Mathf.Clamp(l.y, r.yMin, r.yMax));
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!PointerWorld.TryGet(e, Image.rectTransform, out var w)) return;
            _down = true;
            _pointerId = e.pointerId;
            _a = _b = Local(w);
            CursivisMetrics.Mark("select_down");
            Draw();
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_down || e.pointerId != _pointerId) return;
            if (!PointerWorld.TryGet(e, Image.rectTransform, out var w)) return;
            _b = Local(w);
            Draw();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_down || e.pointerId != _pointerId) return;
            _down = false;
            var r = Image.rectTransform.rect;
            var min = Vector2.Min(_a, _b);
            var max = Vector2.Max(_a, _b);
            bool region = (max.x - min.x) > r.width * 0.08f && (max.y - min.y) > r.height * 0.08f;
            if (!region) { min = r.min; max = r.max; }
            Draw(min, max);
            var uv = new Rect((min.x - r.xMin) / r.width, (min.y - r.yMin) / r.height, (max.x - min.x) / r.width, (max.y - min.y) / r.height);
            var corners = new[]
            {
                Image.rectTransform.TransformPoint(new Vector3(min.x, min.y)), Image.rectTransform.TransformPoint(new Vector3(min.x, max.y)),
                Image.rectTransform.TransformPoint(new Vector3(max.x, max.y)), Image.rectTransform.TransformPoint(new Vector3(max.x, min.y))
            };
            Selected?.Invoke(this, uv, corners);
        }

        public void SelectAll()
        {
            var r = Image.rectTransform.rect;
            Draw(r.min, r.max);
            var c = new Vector3[4];
            Image.rectTransform.GetWorldCorners(c);
            Selected?.Invoke(this, new Rect(0, 0, 1, 1), c);
        }

        public void SelectUv(Rect uv)
        {
            var r = Image.rectTransform.rect;
            var min = new Vector2(r.xMin + uv.xMin * r.width, r.yMin + uv.yMin * r.height);
            var max = new Vector2(r.xMin + uv.xMax * r.width, r.yMin + uv.yMax * r.height);
            Draw(min, max);
            var corners = new[]
            {
                Image.rectTransform.TransformPoint(new Vector3(min.x, min.y)), Image.rectTransform.TransformPoint(new Vector3(min.x, max.y)),
                Image.rectTransform.TransformPoint(new Vector3(max.x, max.y)), Image.rectTransform.TransformPoint(new Vector3(max.x, min.y))
            };
            Selected?.Invoke(this, uv, corners);
        }

        void Draw() => Draw(Vector2.Min(_a, _b), Vector2.Max(_a, _b));

        void Draw(Vector2 min, Vector2 max)
        {
            var p = CursivisTheme.P;
            _rect.Set(p.Highlight, p.Highlight, p.HighlightBorder, 6f, 2.5f);
            _rect.gameObject.SetActive(true);
            _rect.rectTransform.anchoredPosition = (min + max) * 0.5f - Image.rectTransform.rect.center + Image.rectTransform.rect.center;
            _rect.rectTransform.sizeDelta = Vector2.Max(max - min, new Vector2(4, 4));
        }

        public void ClearHighlight() => _rect.gameObject.SetActive(false);
    }

    /// <summary>"Ask Cursivis" trigger chip attached to the active selection (+ Retake/Clear and Cancel).</summary>
    public sealed class SelectionChip : MonoBehaviour
    {
        public CursivisButton AskButton, RetakeButton, CancelButton;
        public TextMeshProUGUI Caption;
        PanelAnimator _anim;

        public void Build()
        {
            var root = (RectTransform)transform;
            var bg = UiKit.Panel(root, "ChipBg", new Vector2(360, 58), Vector2.zero, Color.white, Color.white, Color.white, 29f, 1f);
            AskButton = CursivisButton.Create(bg.transform, "Ask", "Ask Cursivis", Icons.Sparkle, new Vector2(176, 44), new Vector2(-82, 0), ButtonStyle.Talk, CursivisTheme.TextChip);
            RetakeButton = CursivisButton.Create(bg.transform, "Retake", "Retake", Icons.Replay, new Vector2(108, 44), new Vector2(66, 0), ButtonStyle.Secondary);
            CancelButton = CursivisButton.Create(bg.transform, "Cancel", null, Icons.Close, new Vector2(44, 44), new Vector2(150, 0), ButtonStyle.Icon);
            Caption = UiKit.Text(root, "Caption", "", CursivisTheme.TextMeta, Color.white, UiKit.SemiBold, TextAlignmentOptions.Center, new Vector2(360, 18), new Vector2(0, -40));
            _anim = gameObject.AddComponent<PanelAnimator>();
            _anim.Init(bg.rectTransform);
            void Theme()
            {
                var p = CursivisTheme.P;
                bg.Set(p.ShellA, p.ShellB, p.ShellBorder, 29f, 1f);
                bg.SetShadow(10f, p.ShadowAlpha);
                Caption.color = p.TextMuted;
            }
            Theme();
            CursivisTheme.Changed += Theme;
        }

        public void Show(string caption, string retakeLabel)
        {
            Caption.text = caption ?? "";
            RetakeButton.SetLabel(retakeLabel ?? "Retake");
            _anim.Show();
        }

        public void Hide() => _anim.Hide();
        public bool Visible => _anim != null && _anim.Visible;
    }
}
