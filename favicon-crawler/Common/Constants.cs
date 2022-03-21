using System.Collections.Generic;

namespace FaviconFinder.Common
{
    public static class Constants
    {
        public static readonly List<string> FaviconPatterns = new List<string>() {
            "//link[@rel='shortcut icon' and @href]",
            "//link[@rel='SHORTCUT ICON' and @href]",
            "//link[@rel='icon' and @href]"
        };

        public static readonly List<string> UrlPrefixes = new List<string>() {
            "https://www.",
            "https://",
            "http://",
            "http://www."
        };

        public static readonly string HeaderAccept = "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8";
        public static readonly string HeaderLanguage = "en-CA,en-GB;q=0.9,en-US;q=0.8,en;q=0.7";
        public static readonly string HeaderUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
    }
}