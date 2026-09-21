using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WpfApp1.Services.Downloads
{
    public sealed class AssetCandidate
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public long? Size { get; set; }
        public int Score { get; set; }
    }

    public static class AssetSelector
    {
        public static string ResolveArchitecturePattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return pattern;
            var architecture = PlatformDetectionService.Current.Architecture;
            return pattern
                .Replace("{arch}", architecture, StringComparison.OrdinalIgnoreCase)
                .Replace("{osArch}", architecture, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsArchitectureCompatible(string value, bool allowArchitectureFallback)
        {
            var candidate = InferArchitecture(value);
            if (string.IsNullOrWhiteSpace(candidate))
                return true;

            var current = PlatformDetectionService.Current.Architecture;
            if (string.Equals(candidate, current, StringComparison.OrdinalIgnoreCase))
                return true;

            // Windows on ARM64 can emulate x64 applications, but never silently
            // treat ARM64 as x64. The catalog must explicitly allow that fallback.
            return string.Equals(current, "arm64", StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(candidate, "x64", StringComparison.OrdinalIgnoreCase) &&
                   allowArchitectureFallback;
        }

        public static bool IsExplicitArchitectureCompatible(string expectedArchitecture, bool allowArchitectureFallback)
        {
            if (string.IsNullOrWhiteSpace(expectedArchitecture)) return true;
            return IsArchitectureCompatible(expectedArchitecture, allowArchitectureFallback);
        }

        public static string InferArchitecture(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var lower = value.ToLowerInvariant();

            if (lower.Contains("arm64") || lower.Contains("aarch64")) return "arm64";
            if (Regex.IsMatch(lower, @"(^|[^a-z0-9])(?:x86_64|amd64|x64|win64|64bit)([^a-z0-9]|$)")) return "x64";
            if (Regex.IsMatch(lower, @"(^|[^a-z0-9])(?:x86|win32|32bit|i386|i686)([^a-z0-9]|$)")) return "x86";
            return string.Empty;
        }

        public static bool IsWindowsAsset(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return true;
            var lower = value.ToLowerInvariant();
            if (lower.Contains("android") || lower.Contains("darwin") || lower.Contains("macos") || lower.Contains("osx") ||
                lower.Contains("linux") || lower.Contains("freebsd") || lower.Contains("appimage") || lower.Contains("deb"))
                return false;
            return !Regex.IsMatch(lower, @"(^|[^a-z0-9])(?:mac|osx|dmg)([^a-z0-9]|$)");
        }

        public static string SelectPreferredKey(IDictionary<string, string> assets, bool allowFallback)
        {
            if (assets == null || assets.Count == 0) return null;
            var architecture = PlatformDetectionService.Current.Architecture;
            var keys = assets.Keys.ToList();
            var preferred = GetPreferredKeys(architecture, PlatformDetectionService.Current.WindowsBuild).ToList();

            foreach (var candidate in preferred)
            {
                var match = keys.FirstOrDefault(key => string.Equals(key, candidate, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(match)) return match;
            }

            if (allowFallback && string.Equals(architecture, "arm64", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var candidate in new[] { "x64", "windows-x64", "win-x64", "amd64", "win64" })
                {
                    var match = keys.FirstOrDefault(key => string.Equals(key, candidate, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrWhiteSpace(match)) return match;
                }
            }

            return null;
        }

        public static int Score(string name, bool preferNative)
        {
            var lower = (name ?? string.Empty).ToLowerInvariant();
            var architecture = PlatformDetectionService.Current.Architecture;
            var score = 0;

            if (lower.EndsWith(".exe")) score += 100;
            else if (lower.EndsWith(".msi")) score += 95;
            else if (lower.EndsWith(".zip")) score += 70;
            else if (lower.EndsWith(".7z")) score += 65;
            else if (lower.EndsWith(".rar")) score += 60;
            else score -= 50;

            var candidateArchitecture = InferArchitecture(name);
            if (preferNative && !string.IsNullOrWhiteSpace(candidateArchitecture))
            {
                if (string.Equals(candidateArchitecture, architecture, StringComparison.OrdinalIgnoreCase))
                    score += 250;
                else if (string.Equals(architecture, "arm64", StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(candidateArchitecture, "x64", StringComparison.OrdinalIgnoreCase))
                    score += 40;
                else
                    score -= 300;
            }

            if (lower.Contains("windows") || lower.Contains("win")) score += 25;
            if (lower.Contains("portable")) score += 5;
            if (lower.Contains("source") || lower.Contains("checksums") || lower.EndsWith(".sha256") || lower.EndsWith(".txt")) score -= 500;
            return score;
        }

        public static bool WildcardMatch(string text, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return true;
            var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(text ?? string.Empty, regex, RegexOptions.IgnoreCase);
        }

        private static IEnumerable<string> GetPreferredKeys(string architecture, int windowsBuild)
        {
            if (string.Equals(architecture, "arm64", StringComparison.OrdinalIgnoreCase))
                return new[] { "arm64", "windows-arm64", "win-arm64", "aarch64", "arm" };

            if (string.Equals(architecture, "x86", StringComparison.OrdinalIgnoreCase))
                return windowsBuild > 0 && windowsBuild < 10240
                    ? new[] { "win7-x86", "x86", "win-x86", "windows-x86", "win32", "32bit" }
                    : new[] { "x86", "win-x86", "windows-x86", "win32", "32bit" };

            return windowsBuild > 0 && windowsBuild < 10240
                ? new[] { "win7-x64", "x64", "win-x64", "windows-x64", "amd64", "win64" }
                : new[] { "x64", "win-x64", "windows-x64", "amd64", "win64" };
        }
    }
}
