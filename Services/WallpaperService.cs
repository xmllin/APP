using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nexora.Models;

namespace Nexora.Services
{
    public sealed class WallpaperService
    {
        private sealed class WallpaperSource
        {
            public string Name { get; private set; }
            public string BaseUrl { get; private set; }
            public string CatalogUrl { get { return BaseUrl + "wallpapers.json"; } }

            public WallpaperSource(string name, string baseUrl)
            {
                Name = name;
                BaseUrl = baseUrl;
            }
        }

        private static readonly IReadOnlyList<WallpaperSource> Sources = new List<WallpaperSource>
        {
            new WallpaperSource("anime_4k", "https://raw.githubusercontent.com/xmlin717-hub/anime/main/"),
            new WallpaperSource("nature", "https://raw.githubusercontent.com/xmlin717-hub/nature/main/"),
            new WallpaperSource("cars", "https://raw.githubusercontent.com/xmlin717-hub/cars/main/"),
            new WallpaperSource("space", "https://raw.githubusercontent.com/xmlin717-hub/space/main/"),
            new WallpaperSource("abstract", "https://raw.githubusercontent.com/xmlin717-hub/abstract/main/")
        };

        private readonly WallpaperCacheService _cache;
        private static readonly HttpClient Client = CreateClient();
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public string LastError { get; private set; }
        public bool UsedOfflineCatalog { get; private set; }

        public WallpaperService(WallpaperCacheService cache = null)
        {
            _cache = cache ?? new WallpaperCacheService();
        }

        public async Task<IReadOnlyList<Wallpaper>> LoadAsync(CancellationToken token, bool forceRefresh = false)
        {
            LastError = null;
            UsedOfflineCatalog = false;
            var tasks = Sources.Select(source => LoadSourceAsync(source, token)).ToArray();
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            var remote = results.SelectMany(result => result.Items ?? new List<Wallpaper>()).ToList();
            var errors = results.Where(result => !string.IsNullOrWhiteSpace(result.Error)).Select(result => result.Error).ToList();

            if (remote.Count > 0)
            {
                var unique = remote.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
                await _cache.SaveCatalogAsync(JsonSerializer.Serialize(unique, _jsonOptions), token).ConfigureAwait(false);
                await _cache.CleanupAsync(unique.SelectMany(item => new[] { item.Image, item.Preview }), token).ConfigureAwait(false);
                if (errors.Count > 0)
                    LastError = "Не удалось обновить часть категорий: " + string.Join("; ", errors);
                return unique;
            }

            var cachedJson = await TryLoadCachedCatalogAsync(token).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(cachedJson))
            {
                try
                {
                    UsedOfflineCatalog = true;
                    LastError = errors.Count > 0 ? string.Join("; ", errors) : "Не удалось обновить библиотеку.";
                    return Deserialize(cachedJson, null);
                }
                catch (Exception cachedException)
                {
                    LastError = FormatError(cachedException);
                }
            }
            if (errors.Count > 0) LastError = string.Join("; ", errors);
            return new List<Wallpaper>();
        }

        private async Task<SourceResult> LoadSourceAsync(WallpaperSource source, CancellationToken token)
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, source.CatalogUrl))
                {
                    request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
                    using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                            throw new WallpaperHttpException(response.StatusCode);
                        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        return new SourceResult(source.Name, Deserialize(json, source.BaseUrl), null);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                return new SourceResult(source.Name, new List<Wallpaper>(), source.Name + ": " + FormatError(ex));
            }
        }

        private sealed class SourceResult
        {
            public string Name { get; private set; }
            public IReadOnlyList<Wallpaper> Items { get; private set; }
            public string Error { get; private set; }

            public SourceResult(string name, IReadOnlyList<Wallpaper> items, string error)
            {
                Name = name;
                Items = items;
                Error = error;
            }
        }

        private async Task<string> TryLoadCachedCatalogAsync(CancellationToken token)
        {
            try { return await _cache.LoadCatalogAsync(token).ConfigureAwait(false); }
            catch { return null; }
        }

        private IReadOnlyList<Wallpaper> Deserialize(string json, string baseUrl)
        {
            var items = JsonSerializer.Deserialize<List<Wallpaper>>(json, _jsonOptions);
            if (items == null) throw new JsonException("Каталог обоев пуст.");

            return items.Where(item => item != null && !string.IsNullOrWhiteSpace(item.Id))
                .Select(item => Normalize(item, baseUrl))
                .Where(item => !string.IsNullOrWhiteSpace(item.Image))
                .ToList();
        }

            private static Wallpaper Normalize(Wallpaper item, string baseUrl)
        {
            item.Name = string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name;
            item.Category = string.IsNullOrWhiteSpace(item.Category) ? "Другое" : item.Category;
            item.Image = ToAbsoluteUrl(item.Image, baseUrl);
            item.Preview = string.IsNullOrWhiteSpace(item.Preview) ? null : ToAbsoluteUrl(item.Preview, baseUrl);
            return item;
        }

        private static string ToAbsoluteUrl(string path, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) && absolute.Scheme == Uri.UriSchemeHttps)
                return absolute.ToString();
            return new Uri(new Uri(baseUrl ?? Sources[0].BaseUrl), path.TrimStart('/')).ToString();
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Nexora/1.0");
            return client;
        }

        private static string FormatError(Exception exception)
        {
            var http = exception as WallpaperHttpException;
            if (http != null)
            {
                if ((int)http.StatusCode == 404) return "Не удалось обновить библиотеку: каталог GitHub не найден (404).";
                if ((int)http.StatusCode == 403) return "Не удалось обновить библиотеку: GitHub запретил доступ (403).";
                if ((int)http.StatusCode == 429) return "Не удалось обновить библиотеку: GitHub временно ограничил запросы (429).";
                if ((int)http.StatusCode >= 500) return "Не удалось обновить библиотеку: GitHub временно недоступен.";
                return "Не удалось обновить библиотеку: HTTP " + (int)http.StatusCode + ".";
            }
            if (exception is HttpRequestException) return "Не удалось обновить библиотеку: GitHub недоступен.";
            if (exception is TaskCanceledException) return "Не удалось обновить библиотеку: превышено время ожидания.";
            if (exception is JsonException) return "Не удалось обновить библиотеку: некорректный JSON.";
            return "Не удалось обновить библиотеку.";
        }

    }
}