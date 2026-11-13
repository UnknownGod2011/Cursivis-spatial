using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cursivis.Spatial
{
    public enum SelectionKind { Text, Image, TextImage }
    public enum SelectionSource { Camera, Workspace }

    /// <summary>
    /// Normalized selection shared by physical-world (passthrough camera) and Cursivis-workspace content.
    /// Every selection gets a unique id so stale AI responses can never overwrite a newer selection.
    /// </summary>
    public sealed class CursivisSelection
    {
        public readonly string Id;
        public readonly SelectionKind Kind;
        public readonly SelectionSource Source;
        public readonly string Text;
        public readonly byte[] Jpeg;
        public readonly int ImageWidth;
        public readonly int ImageHeight;
        public readonly string Title;
        public readonly string DocType;
        public readonly DateTime CreatedUtc = DateTime.UtcNow;

        /// <summary>World-space corners (BL, TL, TR, BR) used to anchor the orb and the frozen preview.</summary>
        public Vector3[] WorldCorners;
        /// <summary>Optional texture shown as the frozen preview (camera crop) - owned by the selection.</summary>
        public Texture2D Preview;
        public string SuggestTitle;
        public string SuggestSummary;

        public CursivisSelection(SelectionKind kind, SelectionSource source, string text, byte[] jpeg, int w, int h, string title, string docType)
        {
            Id = "sel_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            Kind = kind;
            Source = source;
            Text = text;
            Jpeg = jpeg;
            ImageWidth = w;
            ImageHeight = h;
            Title = title;
            DocType = docType;
        }

        public Vector3 Center
        {
            get
            {
                if (WorldCorners == null || WorldCorners.Length < 4) return Vector3.zero;
                return (WorldCorners[0] + WorldCorners[1] + WorldCorners[2] + WorldCorners[3]) * 0.25f;
            }
        }

        public float WorldWidth => WorldCorners == null || WorldCorners.Length < 4 ? 0.1f : Vector3.Distance(WorldCorners[1], WorldCorners[2]);
        public float WorldHeight => WorldCorners == null || WorldCorners.Length < 4 ? 0.1f : Vector3.Distance(WorldCorners[0], WorldCorners[1]);

        public string Describe()
        {
            var what = Kind == SelectionKind.Text ? $"{Text?.Length ?? 0} chars of text" : $"{ImageWidth}x{ImageHeight} image";
            return $"{Id} [{Source}/{Kind}] {what}";
        }

        public void Dispose()
        {
            if (Preview != null) UnityEngine.Object.Destroy(Preview);
            Preview = null;
        }
    }

    public sealed class Turn
    {
        public string Role; // "user" | "assistant"
        public string Text;
    }

    /// <summary>
    /// Conversation memory. Context mode keeps one history per selection id; general mode has its own.
    /// Switching selections never mixes histories.
    /// </summary>
    public sealed class ConversationSession
    {
        const int MaxTurns = 8;
        readonly Dictionary<string, List<Turn>> _bySelection = new Dictionary<string, List<Turn>>();
        readonly List<Turn> _general = new List<Turn>();
        public string LastSelectionSummary { get; private set; }

        public List<Turn> HistoryFor(string selectionId)
        {
            if (string.IsNullOrEmpty(selectionId)) return _general;
            if (!_bySelection.TryGetValue(selectionId, out var list))
            {
                list = new List<Turn>();
                _bySelection[selectionId] = list;
            }
            return list;
        }

        public void Add(string selectionId, string role, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var list = HistoryFor(selectionId);
            list.Add(new Turn { Role = role, Text = text.Length > 4000 ? text.Substring(0, 4000) : text });
            while (list.Count > MaxTurns) list.RemoveAt(0);
        }

        public void RememberSelection(CursivisSelection s)
        {
            if (s == null) return;
            var label = !string.IsNullOrEmpty(s.SuggestTitle) ? s.SuggestTitle : s.Title;
            LastSelectionSummary = $"{label} ({(s.Source == SelectionSource.Camera ? "captured from the room" : "from the workspace")}). {s.SuggestSummary}".Trim();
            if (LastSelectionSummary.Length > 380) LastSelectionSummary = LastSelectionSummary.Substring(0, 380);
        }

        public void ForgetSelection(string selectionId)
        {
            if (selectionId != null) _bySelection.Remove(selectionId);
        }

        public void ClearAll()
        {
            _bySelection.Clear();
            _general.Clear();
            LastSelectionSummary = null;
        }
    }
}
