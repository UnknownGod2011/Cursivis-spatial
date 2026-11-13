using System.Text;
using System.Text.RegularExpressions;

namespace Cursivis.Spatial.UI
{
    /// <summary>
    /// Converts the backend's panel Markdown into TextMeshPro rich text (headings, bold/italic, bullets, numbered
    /// lists, checklists, inline code, fenced code blocks, simple tables). Mirrors the original result panel's
    /// heading scale (max(14, 19 - level) px, x1.4 for headset legibility) and paragraph spacing.
    /// </summary>
    public static class MarkdownTmp
    {
        static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        static readonly Regex BoldU = new Regex(@"__(.+?)__", RegexOptions.Compiled);
        static readonly Regex Italic = new Regex(@"(?<![\*\w])\*(?!\s)(.+?)(?<!\s)\*(?![\*\w])", RegexOptions.Compiled);
        static readonly Regex InlineCode = new Regex(@"`([^`]+)`", RegexOptions.Compiled);
        static readonly Regex Link = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        static readonly Regex Numbered = new Regex(@"^(\s*)(\d+)[\.\)]\s+(.*)$", RegexOptions.Compiled);
        static readonly Regex Bullet = new Regex(@"^(\s*)[-\*\u2022]\s+(.*)$", RegexOptions.Compiled);
        static readonly Regex Check = new Regex(@"^(\s*)[-\*]\s+\[( |x|X)\]\s+(.*)$", RegexOptions.Compiled);
        static readonly Regex Heading = new Regex(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);

        public sealed class Style
        {
            public string Heading = "#F7FAFD";
            public string Bullet = "#D4DAE1";
            public string CodeFg = "#F2F5FA";
            public string CodeBg = "#FFFFFF14";
            public string Muted = "#A9B0B8";
            public float BodySize = CursivisTheme.TextBody;
            public string MonoFontName = "IBMPlexMono-Regular SDF";
        }

        public static string Escape(string s) => s.Replace("<", "<\u200B"); // neutralize stray TMP tags from model text

        public static string Inline(string line, Style st)
        {
            var s = Escape(line);
            s = InlineCode.Replace(s, m => $"<font=\"{st.MonoFontName}\"><color={st.CodeFg}><mark={st.CodeBg} padding=\"6,6,2,2\">{m.Groups[1].Value}</mark></color></font>");
            s = Bold.Replace(s, "<b>$1</b>");
            s = BoldU.Replace(s, "<b>$1</b>");
            s = Italic.Replace(s, "<i>$1</i>");
            s = Link.Replace(s, "<u>$1</u>");
            return s;
        }

        public static string Convert(string markdown, Style st = null)
        {
            st = st ?? new Style();
            if (string.IsNullOrEmpty(markdown)) return "";
            var sb = new StringBuilder(markdown.Length + 256);
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            bool inCode = false;
            bool lastBlank = false;
            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                if (raw.TrimStart().StartsWith("```"))
                {
                    inCode = !inCode;
                    sb.Append(inCode ? $"<line-height=118%><font=\"{st.MonoFontName}\"><size=88%><color={st.CodeFg}>" : "</color></size></font></line-height>\n");
                    continue;
                }
                if (inCode)
                {
                    sb.Append(Escape(raw).Replace(" ", "\u00A0")).Append('\n');
                    continue;
                }
                var line = raw.TrimEnd();
                if (line.Trim().Length == 0)
                {
                    if (!lastBlank) sb.Append("<size=45%>\n</size>");
                    lastBlank = true;
                    continue;
                }
                lastBlank = false;
                Match m;
                if ((m = Heading.Match(line)).Success)
                {
                    int level = m.Groups[1].Value.Length;
                    float px = System.Math.Max(14f, 19f - level) * 1.4f * 0.817f;
                    sb.Append($"<size={px:0.#}><b><color={st.Heading}>{Inline(m.Groups[2].Value, st)}</color></b></size>\n");
                    continue;
                }
                if ((m = Check.Match(line)).Success)
                {
                    bool done = m.Groups[2].Value.ToLowerInvariant() == "x";
                    sb.Append($"<indent={Indent(m.Groups[1].Value)}em><color={st.Bullet}>{(done ? "\u2611" : "\u2610")}</color> {Inline(m.Groups[3].Value, st)}</indent>\n");
                    continue;
                }
                if ((m = Bullet.Match(line)).Success)
                {
                    var ind = Indent(m.Groups[1].Value);
                    sb.Append($"<margin-left={ind + 0.9f:0.##}em><color={st.Bullet}><pos=-0.9em>\u2022</pos></color>{Inline(m.Groups[2].Value, st)}</margin>\n");
                    continue;
                }
                if ((m = Numbered.Match(line)).Success)
                {
                    var ind = Indent(m.Groups[1].Value);
                    sb.Append($"<margin-left={ind + 1.3f:0.##}em><color={st.Bullet}><pos=-1.3em>{m.Groups[2].Value}.</pos></color>{Inline(m.Groups[3].Value, st)}</margin>\n");
                    continue;
                }
                if (line.TrimStart().StartsWith("|"))
                {
                    if (Regex.IsMatch(line, @"^\s*\|?\s*:?-{2,}")) continue; // separator row
                    var cells = line.Trim().Trim('|').Split('|');
                    var row = new StringBuilder();
                    for (int c = 0; c < cells.Length; c++)
                    {
                        if (c > 0) row.Append($"<color={st.Muted}>  \u2502  </color>");
                        row.Append(Inline(cells[c].Trim(), st));
                    }
                    sb.Append(row).Append('\n');
                    continue;
                }
                if (line.StartsWith(">"))
                {
                    sb.Append($"<color={st.Muted}><i>{Inline(line.TrimStart('>', ' '), st)}</i></color>\n");
                    continue;
                }
                sb.Append(Inline(line, st)).Append('\n');
            }
            if (inCode) sb.Append("</color></size></font></line-height>\n");
            return sb.ToString().TrimEnd('\n');
        }

        static float Indent(string ws) => (ws.Replace("\t", "  ").Length / 2) * 1.2f;

        /// <summary>Plain text for read-aloud: strips Markdown so TTS sounds natural.</summary>
        public static string ToSpeech(string markdown)
        {
            if (string.IsNullOrEmpty(markdown)) return "";
            var s = Regex.Replace(markdown, @"```[\s\S]*?```", " (code omitted) ");
            s = Regex.Replace(s, @"`([^`]+)`", "$1");
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "$1");
            s = Regex.Replace(s, @"__(.+?)__", "$1");
            s = Regex.Replace(s, @"(?<!\w)\*(.+?)\*(?!\w)", "$1");
            s = Regex.Replace(s, @"^#{1,6}\s+", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"^\s*[-\*\u2022]\s+\[( |x|X)\]\s+", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"^\s*[-\*\u2022]\s+", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"\[([^\]]+)\]\([^)]+\)", "$1");
            s = Regex.Replace(s, @"\|", ", ");
            s = Regex.Replace(s, @"^\s*:?-{2,}.*$", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"\n{2,}", ".\n");
            s = Regex.Replace(s, @"[ \t]+", " ");
            return s.Trim();
        }
    }
}
