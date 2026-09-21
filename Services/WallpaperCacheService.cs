using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1.Services
{
    public sealed class WallpaperHttpException : Exception
    {
        public HttpStatusCode StatusCode { get; private set; }

        public WallpaperHttpException(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }
    }

    public sealed class WallpaperCacheService
    {
        private readonly string _root = UserDataPath.Subfolder("WallpapersCache");
        private static readonly HttpClient Client = CreateClient();

        public string CatalogPath { get { return Path.Combine(_root, "wallpapers.json"); } }

        public WallpaperCacheService()
        {
            Directory.CreateDirectory(_root);
        }

        public string GetPath(string url, string extensionHint)
        {
            var extension = Path.GetExtension(extensionHint ?? string.Empty);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
                extension = ".img";
            return Path.Combine(_root, Hash(url) + extension.ToLowerInvariant());
        }

        public async Task<string> GetOrDownloadAsync(string url, string extensionHint, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var path = GetPath(url, extensionHint);
            if (File.Exists(path) && new FileInfo(path).Length > 0)
                return path;

            var temporaryPath = path + ".tmp";
            try
            {
                using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new WallpaperHttpException(response.StatusCode);
                    using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.SequentialScan))
                        await input.CopyToAsync(output, 81920, token).ConfigureAwait(false);
                }

                if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length == 0)
                    return null;
                File.Move(temporaryPath, path, true);
                return path;
            }
            catch
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                throw;
            }
        }

        public async Task SaveCatalogAsync(string json, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            var temporaryPath = CatalogPath + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, json, token).ConfigureAwait(false);
            File.Move(temporaryPath, CatalogPath, true);
        }

        public async Task<string> LoadCatalogAsync(CancellationToken token)
        {
            if (!File.Exists(CatalogPath)) return null;
            return await File.ReadAllTextAsync(CatalogPath, token).ConfigureAwait(false);
        }

        public Task CleanupAsync(IEnumerable<string> urls, CancellationToken token)
        {
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var url in urls ?? Enumerable.Empty<string>())
                if (!string.IsNullOrWhiteSpace(url)) keep.Add(Hash(url));

            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                CleanupImageFiles(keep, token);
            }, token);
        }

        private void CleanupImageFiles(HashSet<string> keepHashes, CancellationToken token)
        {
            if (!Directory.Exists(_root)) return;
            foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.TopDirectoryOnly))
            {
                token.ThrowIfCancellationRequested();
                if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(CatalogPath), StringComparison.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileName(file);
                var hash = Path.GetFileNameWithoutExtension(name);
                if (!keepHashes.Contains(hash)) TryDelete(file);
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        public static string DescribeHttpError(Exception exception)
        {
            var http = exception as WallpaperHttpException;
            if (http != null)
            {
                if (http.StatusCode == HttpStatusCode.NotFound) return "Изображение не найдено (404).";
                if (http.StatusCode == HttpStatusCode.Forbidden) return "GitHub запретил доступ к изображению (403).";
                if ((int)http.StatusCode == 429) return "GitHub временно ограничил загрузки (429).";
                if ((int)http.StatusCode >= 500) return "GitHub временно недоступен.";
                return "GitHub вернул ошибку HTTP " + (int)http.StatusCode + ".";
            }
            if (exception is TaskCanceledException) return "Превышено время ожидания загрузки.";
            if (exception is HttpRequestException) return "Нет соединения с GitHub.";
            return "Не удалось загрузить изображение.";
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("WpfApp1/1.0");
            return client;
        }

        private static string Hash(string value)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (var item in bytes) builder.Append(item.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}