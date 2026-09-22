using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nexora.Models;

namespace Nexora.Services.Downloads
{
    public sealed class CatalogDiagnosticResult
    {
        public string AppId { get; set; }
        public string AppName { get; set; }
        public string ProviderType { get; set; }
        public string OsArchitecture { get; set; }
        public string ProcessArchitecture { get; set; }
        public bool Success { get; set; }
        public string Version { get; set; }
        public string FileName { get; set; }
        public string Format { get; set; }
        public long? SizeBytes { get; set; }
        public int? HttpStatus { get; set; }
        public string MediaType { get; set; }
        public string Url { get; set; }
        public string Error { get; set; }
    }

    /// <summary>
    /// Non-destructive catalog smoke test. It resolves every app and checks
    /// the selected URL/metadata without downloading the actual installer.
    /// </summary>
    public sealed class CatalogDiagnosticService
    {
        public async Task<IReadOnlyList<CatalogDiagnosticResult>> ProbeAllAsync(
            IEnumerable<AppDefinition> apps,
            CancellationToken token,
            int maxConcurrency = 4)
        {
            var list = (apps ?? Enumerable.Empty<AppDefinition>()).Where(item => item != null).ToList();
            var results = new CatalogDiagnosticResult[list.Count];
            var semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency));
            var tasks = new List<Task>();

            for (var index = 0; index < list.Count; index++)
            {
                var capturedIndex = index;
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(token);
                    try
                    {
                        results[capturedIndex] = await ProbeAsync(list[capturedIndex], token);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, token));
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            finally
            {
                semaphore.Dispose();
            }

            return results;
        }

        public async Task<CatalogDiagnosticResult> ProbeAsync(AppDefinition app, CancellationToken token)
        {
            var result = new CatalogDiagnosticResult
            {
                AppId = app?.Id,
                AppName = app?.Name,
                ProviderType = app?.Download?.Type,
                OsArchitecture = PlatformDetectionService.Current.Architecture,
                ProcessArchitecture = PlatformDetectionService.Current.ProcessArchitecture
            };

            if (app?.Download == null)
            {
                result.Error = "Не настроен источник загрузки.";
                return result;
            }

            try
            {
                var service = new DownloadService();
                var info = await service.ResolveAsync(app, token);
                result.Success = info != null && Uri.TryCreate(info.Url, UriKind.Absolute, out var uri) &&
                                 (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
                result.Version = info?.Version;
                result.FileName = info?.FileName;
                result.Format = info?.Format;
                result.Url = info?.Url;
                if (result.Success && !string.IsNullOrWhiteSpace(info.Url))
                {
                    var metadata = new DownloadMetadataService();
                    var probe = await metadata.ProbeAsync(info.Url, token);
                    result.HttpStatus = probe.StatusCode;
                    result.MediaType = probe.MediaType;
                    result.FileName = string.IsNullOrWhiteSpace(result.FileName) ? probe.FileName : result.FileName;
                    result.Format = string.IsNullOrWhiteSpace(result.Format) ? GetFormat(result.FileName) : result.Format;
                    result.SizeBytes = info.SizeBytes ?? probe.SizeBytes;
                    result.Success = probe.Success;
                    if (!probe.Success) result.Error = probe.Error;
                }
                if (!result.Success && string.IsNullOrWhiteSpace(result.Error))
                    result.Error = "Резолвер не вернул корректный файл загрузки.";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            return result;
        }
        private static string GetFormat(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "Не указан";
            var ext = System.IO.Path.GetExtension(fileName);
            return string.IsNullOrWhiteSpace(ext) ? "Файл" : "." + ext.TrimStart('.').ToUpperInvariant();
        }

    }
}
