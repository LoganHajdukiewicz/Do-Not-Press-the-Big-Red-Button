using System.Text;
using UnityEngine;

namespace BigRedButton
{
    /// <summary>
    /// Shared on-screen text look: uppercase, letter-spaced and desaturated, so the
    /// narration, day titles and button signs all read like the same company notice.
    /// </summary>
    public static class CorporateText
    {
        public static readonly Color Ink = new Color(0.88f, 0.88f, 0.85f);
        public static readonly Color DimInk = new Color(0.62f, 0.63f, 0.6f);

        /// <summary>Spreads the letters out, which reads as printed signage rather than chat.</summary>
        public static string Tracked(string text, int spaces = 1)
        {
            if (string.IsNullOrEmpty(text) || spaces <= 0)
                return text ?? string.Empty;

            string gap = new string(' ', spaces);
            var builder = new StringBuilder(text.Length * (1 + spaces));
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                builder.Append(c);
                if (i == text.Length - 1)
                    continue;
                // Never pad around an existing space, or words stop reading as words.
                if (c != ' ' && text[i + 1] != ' ')
                    builder.Append(gap);
            }

            return builder.ToString();
        }

        public static GUIStyle CreateStyle(int fontSize, bool wordWrap = false)
        {
            return new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
                wordWrap = wordWrap,
                richText = false
            };
        }

        /// <summary>Draws the label with a hard offset shadow, like ink on a printed form.</summary>
        public static void DrawWithShadow(Rect area, string text, GUIStyle style, Color colour,
            float shadowOffset = 2f)
        {
            Color previous = GUI.color;
            Color shadow = new Color(0f, 0f, 0f, colour.a * 0.55f);

            style.normal.textColor = shadow;
            GUI.color = new Color(1f, 1f, 1f, colour.a);
            GUI.Label(new Rect(area.x + shadowOffset, area.y + shadowOffset, area.width, area.height),
                text, style);

            style.normal.textColor = colour;
            GUI.Label(area, text, style);
            GUI.color = previous;
        }
    }
}
