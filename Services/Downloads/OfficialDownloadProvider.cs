using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.Models;

namespace WpfApp1.Services.Downloads
{
    public sealed class OfficialDownloadProvider : IDownloadProvider
    {
        public Task<DownloadInfo> ResolveAsync(AppDefinition app, CancellationToken cancellationToken)
        {
            if (app?.Download == null || string.IsNullOrWhiteSpace(app.Download.Url))
                throw new InvalidOperationException("Для официальной загрузки не задан URL.");

            if (!Uri.TryCreate(app.Download.Url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("URL официальной загрузки должен использовать HTTP или HTTPS.");

            var url = ResolveUrl(app.Download.Url);
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("URL официальной загрузки должен использовать HTTP или HTTPS.");

            var explicitArchitecture = app.Download.Architecture;
            if (!AssetSelector.IsExplicitArchitectureCompatible(explicitArchitecture, app.Download.AllowArchitectureFallback) ||
                !AssetSelector.IsArchitectureCompatible(url + " " + (app.Download.FileName ?? string.Empty), app.Download.AllowArchitectureFallback))
                throw new InvalidOperationException("Для текущей архитектуры Windows не найден совместимый установщик.");

            var fileName = ResolveUrl(app.Download.FileName);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = Path.GetFileName(uri.AbsolutePath);

            if (string.IsNullOrWhiteSpace(fileName) || fileName == "/")
                fileName = (app.Name ?? app.Id ?? "installer") + ".exe";

            return Task.FromResult(new DownloadInfo
            {
                Url = url,
                FileName = fileName,
                Source = "Official",
                Version = VersionNormalizer.ExtractMostSpecific(fileName, url)
            });
        }
        private static string ResolveUrl(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return value;
            var architecture = PlatformDetectionService.Current.Architecture;
            var discordArchitecture = architecture == "arm64" ? "arm64" : architecture == "x86" ? "x86" : "x64";
            var vlcArchitecture = architecture == "arm64" ? "winarm64" : architecture == "x86" ? "win32" : "win64";
            return value
                .Replace("{arch}", architecture, StringComparison.OrdinalIgnoreCase)
                .Replace("{osArch}", architecture, StringComparison.OrdinalIgnoreCase)
                .Replace("{discordArch}", discordArchitecture, StringComparison.OrdinalIgnoreCase)
                .Replace("{vlcArch}", vlcArchitecture, StringComparison.OrdinalIgnoreCase);
        }

    }
}
