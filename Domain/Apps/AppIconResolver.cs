using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nexora.Models;

namespace Nexora.Domain.Apps
{
    public sealed class AppIconResolver
    {
        private readonly string _baseDirectory;
        private readonly string _fallback;

        private static readonly IReadOnlyDictionary<string, string> LogoById =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["firefox"] = "Firefox_logo,_2019.svg",
                ["chrome"] = "chrome-logo.svg",
                ["chromium"] = "chromium.svg",
                ["librewolf"] = "LibreWolf.svg",
                ["opera"] = "Opera_2015_icon.svg",
                ["opera-gx"] = "Opera_GX_Icon.svg",
                ["telegram"] = "Telegram_2019_Logo.svg",
                ["discord"] = "discord-icon-svgrepo-com.svg",
                ["steam"] = "Steam_icon_logo.svg",
                ["7zip"] = "7ziplogo.svg",
                ["winrar"] = "WinRAR_icon.svg",
                ["nvidia-app"] = "nvidia-logo-svgrepo-com.svg",
                ["lightshot"] = "lightshot.ico",
                ["happ"] = "happ.ico",
                ["tg-ws-proxy"] = "tg-ws-proxy.ico",
                ["omniget"] = "omniget.ico",
                ["everything"] = "everything.ico",
                ["vlc"] = "VLC_Icon.svg",
                ["mpc-hc"] = "mpc_hc_18911.ico",
                ["tor-browser"] = "Tor_Browser_icon.svg"
            };

        public AppIconResolver(string baseDirectory = null)
        {
            _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            _fallback = Path.Combine(_baseDirectory, "interface", "white", "fluent-app-folder.svg");
        }

        public string Resolve(AppDefinition app)
        {
            if (app == null) return ExistingFallback();

            var configured = ResolveConfiguredPath(app.Icon);
            if (!string.IsNullOrWhiteSpace(configured)) return configured;

            var logoDirectory = Path.Combine(_baseDirectory, "logos");
            var mapped = ResolveMappedLogo(app, logoDirectory);
            if (!string.IsNullOrWhiteSpace(mapped)) return ToUri(mapped);

            var fuzzy = ResolveFuzzyLogo(app, logoDirectory);
            if (!string.IsNullOrWhiteSpace(fuzzy)) return ToUri(fuzzy);

            return ExistingFallback();
        }

        private string ResolveConfiguredPath(string icon)
        {
            if (string.IsNullOrWhiteSpace(icon) || icon.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                icon.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return null;

            if (Uri.TryCreate(icon, UriKind.Absolute, out var uri) && uri.IsFile)
                return icon;

            var path = icon;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(_baseDirectory, path.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? ToUri(path) : null;
        }

        private static string ResolveMappedLogo(AppDefinition app, string logoDirectory)
        {
            if (!Directory.Exists(logoDirectory)) return null;
            if (!string.IsNullOrWhiteSpace(app.Id) && LogoById.TryGetValue(app.Id, out var mapped))
            {
                var path = Path.Combine(logoDirectory, mapped);
                if (File.Exists(path)) return path;
            }
            if (!string.IsNullOrWhiteSpace(app.Name))
            {
                var nameKey = NormalizeLogoKey(app.Name);
                foreach (var file in Directory.EnumerateFiles(logoDirectory))
                {
                    if (!IsSupportedLogo(file)) continue;
                    if (string.Equals(NormalizeLogoKey(Path.GetFileNameWithoutExtension(file)), nameKey, StringComparison.OrdinalIgnoreCase))
                        return file;
                }
            }
            return null;
        }

        private static string ResolveFuzzyLogo(AppDefinition app, string logoDirectory)
        {
            if (!Directory.Exists(logoDirectory)) return null;
            var idKey = NormalizeLogoKey(app.Id);
            var nameKey = NormalizeLogoKey(app.Name);
            if (idKey.Length < 3 && nameKey.Length < 3) return null;

            string best = null;
            var bestScore = 0;
            foreach (var file in Directory.EnumerateFiles(logoDirectory))
            {
                if (!IsSupportedLogo(file)) continue;
                var key = NormalizeLogoKey(Path.GetFileNameWithoutExtension(file));
                var score = 0;
                if (idKey.Length >= 3 && (key.Contains(idKey) || idKey.Contains(key))) score += 2;
                if (nameKey.Length >= 3 && (key.Contains(nameKey) || nameKey.Contains(key))) score += 2;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = file;
                }
            }
            return bestScore > 0 ? best : null;
        }

        private static bool IsSupportedLogo(string file)
        {
            var extension = Path.GetExtension(file);
            return string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeLogoKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private string ExistingFallback()
        {
            return File.Exists(_fallback) ? ToUri(_fallback) : null;
        }

        private static string ToUri(string path) => new Uri(path, UriKind.Absolute).AbsoluteUri;
    }
}
