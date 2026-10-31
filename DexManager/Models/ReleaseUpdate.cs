using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DexManager.Models
{
    public sealed class ReleaseUpdate
    {
        public const string ProjectUrl = "https://github.com/maze-mei/DX-Manager";
        public string VersionText { get; private set; }
        public Version Version { get; private set; }
        public string ReleaseUrl { get; private set; }

        private ReleaseUpdate() { }

        public static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(text) || text.Length > 96) return false;
            var match = Regex.Match(text.Trim(),
                @"\A[vV]?(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?(?:\+[A-Za-z0-9.-]+)?\z");
            if (!match.Success) return false;
            var parts = new int[4];
            for (var i = 0; i < 4; i++)
            {
                if (!match.Groups[i + 1].Success) continue;
                if (!int.TryParse(match.Groups[i + 1].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out parts[i])) return false;
            }
            version = new Version(parts[0], parts[1], parts[2], parts[3]);
            return true;
        }

        public static bool TryCreate(string tag, string url, out ReleaseUpdate update)
        {
            update = null;
            Version version;
            if (!TryParseVersion(tag, out version)) return false;
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 ||
                !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment)) return false;
            var prefix = "/maze-mei/DX-Manager/releases/tag/";
            if (!uri.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            var suffix = Uri.UnescapeDataString(uri.AbsolutePath.Substring(prefix.Length));
            if (!string.Equals(suffix, tag.Trim(), StringComparison.Ordinal)) return false;
            update = new ReleaseUpdate
            {
                Version = version,
                VersionText = version.Revision == 0 ? version.ToString(3) : version.ToString(4),
                ReleaseUrl = ProjectUrl + "/releases/tag/" + Uri.EscapeDataString(tag.Trim())
            };
            return true;
        }
    }
}
