using System;
using System.Collections.Generic;

namespace EntityCommandPanelMod.UI
{
    internal sealed class AbilityPresentationIconFactory
    {
        private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

        public string Build(
            string glyph,
            string accentColorHex,
            string modeBadge,
            bool blocked,
            bool active,
            bool empty)
        {
            string normalizedGlyph = string.IsNullOrWhiteSpace(glyph) ? "?" : glyph.Trim();
            string normalizedAccent = NormalizeHex(accentColorHex, empty ? "#5E6B7C" : "#58B7FF");
            string normalizedModeBadge = string.IsNullOrWhiteSpace(modeBadge) ? string.Empty : modeBadge.Trim();
            string key = string.Concat(
                normalizedGlyph, "|",
                normalizedAccent, "|",
                normalizedModeBadge, "|",
                blocked ? "1" : "0", "|",
                active ? "1" : "0", "|",
                empty ? "1" : "0");

            if (_cache.TryGetValue(key, out string? cached))
            {
                return cached;
            }

            string uri = CreateDataUri(normalizedGlyph, normalizedAccent, normalizedModeBadge, blocked, active, empty);
            _cache[key] = uri;
            return uri;
        }

        private static string CreateDataUri(
            string glyph,
            string accentColorHex,
            string modeBadge,
            bool blocked,
            bool active,
            bool empty)
        {
            string cardFill = empty ? "#14202B" : "#0C1620";
            string cardStroke = empty ? "#314253" : "#24384A";
            string badgeFill = empty ? "#223240" : accentColorHex;
            string badgeText = "#ECF4FB";
            string activeStroke = active ? "#F4D77A" : accentColorHex;
            string glyphColor = empty ? "#8AA0B4" : "#F7FBFF";
            string blockedOverlay = blocked
                ? "<path d='M16 56 L56 16' stroke='#FF7B7B' stroke-width='6' stroke-linecap='round' opacity='0.92'/>"
                : string.Empty;
            string activeOverlay = active
                ? "<rect x='5' y='5' width='62' height='62' rx='18' fill='none' stroke='#F4D77A' stroke-width='3' opacity='0.95'/>"
                : string.Empty;
            string modeOverlay = string.IsNullOrWhiteSpace(modeBadge)
                ? string.Empty
                : $"<rect x='39' y='6' width='27' height='16' rx='8' fill='#09131C' opacity='0.94'/>" +
                  $"<text x='52.5' y='17.4' text-anchor='middle' font-family='Segoe UI, Microsoft YaHei, sans-serif' font-size='8.4' font-weight='700' fill='{badgeText}'>{EscapeXml(modeBadge)}</text>";

            string svg =
                "<svg xmlns='http://www.w3.org/2000/svg' width='72' height='72' viewBox='0 0 72 72'>" +
                $"<rect x='4' y='4' width='64' height='64' rx='18' fill='{cardFill}' stroke='{cardStroke}' stroke-width='2'/>" +
                $"<rect x='10' y='10' width='52' height='52' rx='14' fill='{badgeFill}' opacity='0.94' stroke='{activeStroke}' stroke-width='1.8'/>" +
                $"<text x='36' y='42' text-anchor='middle' font-family='Segoe UI, Microsoft YaHei, sans-serif' font-size='24' font-weight='800' fill='{glyphColor}'>{EscapeXml(glyph)}</text>" +
                modeOverlay +
                activeOverlay +
                blockedOverlay +
                "</svg>";

            return "data:image/svg+xml;utf8," + Uri.EscapeDataString(svg);
        }

        private static string NormalizeHex(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            string trimmed = value.Trim();
            if (trimmed.StartsWith('#'))
            {
                trimmed = trimmed[1..];
            }

            if (trimmed.Length == 3)
            {
                trimmed = string.Concat(
                    trimmed[0], trimmed[0],
                    trimmed[1], trimmed[1],
                    trimmed[2], trimmed[2]);
            }

            if (trimmed.Length < 6)
            {
                return fallback;
            }

            string rgb = trimmed[..6];
            for (int i = 0; i < rgb.Length; i++)
            {
                char ch = rgb[i];
                bool isHex = (ch >= '0' && ch <= '9') ||
                             (ch >= 'A' && ch <= 'F') ||
                             (ch >= 'a' && ch <= 'f');
                if (!isHex)
                {
                    return fallback;
                }
            }

            return "#" + rgb.ToUpperInvariant();
        }

        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;", StringComparison.Ordinal)
                .Replace("<", "&lt;", StringComparison.Ordinal)
                .Replace(">", "&gt;", StringComparison.Ordinal)
                .Replace("\"", "&quot;", StringComparison.Ordinal)
                .Replace("'", "&apos;", StringComparison.Ordinal);
        }
    }
}
