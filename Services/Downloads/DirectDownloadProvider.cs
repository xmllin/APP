using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nexora.Models;

namespace Nexora.Services.Downloads
{
    public class DirectDownloadProvider : IDownloadProvider
    {
        public Task<DownloadInfo> ResolveAsync(AppDefinition app, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(app.Download.Url))
                throw new InvalidOperationException("Для приложения не задан URL загрузки.");

            if (app.Download.Url.IndexOf("{discordArch}", StringComparison.OrdinalIgnoreCase) >= 0 &&
                string.Equals(PlatformDetectionService.Current.Architecture, "x86", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Discord больше не поддерживает 32-разрядную Windows.");

            var url = ResolveUrl(app.Download.Url);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("URL загрузки должен использовать HTTP или HTTPS.");

            var name = ResolveUrl(app.Download.FileName);
            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileName(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(name) || name == "/")
                name = app.Id + ".download";

            var explicitArchitecture = app.Download.Architecture;
            var architectureCompatible = AssetSelector.IsExplicitArchitectureCompatible(explicitArchitecture, app.Download.AllowArchitectureFallback) &&
                AssetSelector.IsArchitectureCompatible(url + " " + name, app.Download.AllowArchitectureFallback);
            if (!architectureCompatible)
                throw new InvalidOperationException("Для текущей архитектуры Windows не найден совместимый установщик.");

            return ResolveProbedAsync(url, name, app, cancellationToken);
        }

        private static string ResolveUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            var resolved = url.Replace("{arch}", PlatformDetectionService.Current.Architecture, StringComparison.OrdinalIgnoreCase);
            var architecture = PlatformDetectionService.Current.Architecture;
            var vlcArchitecture = architecture == "arm64"
                ? "winarm64"
                : architecture == "x86" ? "win32" : "win64";
            var discordArchitecture = architecture == "arm64" ? "arm64" : architecture == "x86" ? "x86" : "x64";
            resolved = resolved.Replace("{vlcArch}", vlcArchitecture, StringComparison.OrdinalIgnoreCase);
            return resolved.Replace("{discordArch}", discordArchitecture, StringComparison.OrdinalIgnoreCase);
        }
        private static async Task<DownloadInfo> ResolveProbedAsync(string url, string name, AppDefinition app, CancellationToken token)
        {
            var info = new DownloadInfo
            {
                Url = url,
                FileName = name,
                Source = "Direct",
                Version = VersionNormalizer.ExtractMostSpecific(name, url)
            };

            var probe = await new DownloadMetadataService().ProbeAsync(url, token);
            if (!probe.Success)
                throw new InvalidOperationException(probe.Error ?? "Источник не вернул файл загрузки.");

            if (!string.IsNullOrWhiteSpace(probe.FinalUrl)) info.Url = probe.FinalUrl;
            if (!string.IsNullOrWhiteSpace(probe.FileName) && HasUsableFileName(probe.FileName)) info.FileName = probe.FileName;
            if (probe.SizeBytes.HasValue) info.SizeBytes = probe.SizeBytes;
            info.Version = VersionNormalizer.ExtractMostSpecific(info.Version, info.FileName, info.Url);
            return info;
        }

        private static bool HasUsableFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            var ext = Path.GetExtension(value);
            return !string.IsNullOrWhiteSpace(ext) && ext.Length > 1;
        }

    }
}
