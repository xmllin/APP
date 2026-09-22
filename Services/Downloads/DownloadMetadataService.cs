using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Nexora.Services.Downloads
{
    public sealed class DownloadProbeResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string FinalUrl { get; set; }
        public string MediaType { get; set; }
        public string FileName { get; set; }
        public long? SizeBytes { get; set; }
        public string Error { get; set; }
    }

    public sealed class DownloadMetadataService
    {
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36 Nexora/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream,*/*;q=0.8");
            return client;
        }

        public async Task<DownloadProbeResult> ProbeAsync(string url, CancellationToken token)
        {
            if (!IsHttpUrl(url))
                return new DownloadProbeResult { Success = false, Error = "Некорректный HTTP/HTTPS URL." };

            DownloadProbeResult ranged = null;
            try
            {
                ranged = await ProbeRequestAsync(url, token, true);
                if (ranged.Success || ranged.StatusCode == 404)
                    return ranged;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ranged = new DownloadProbeResult { Success = false, Error = ex.Message };
            }

            try
            {
                var head = await ProbeHeadAsync(url, token);
                if (head.Success)
                    return head;

                var fallback = await ProbeRequestAsync(url, token, false);
                if (fallback.Success)
                    return fallback;
                return MergeProbeResults(ranged, MergeProbeResults(head, fallback));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ranged = ranged ?? new DownloadProbeResult();
                if (string.IsNullOrWhiteSpace(ranged.Error)) ranged.Error = ex.Message;
                return ranged;
            }
        }

        private static async Task<DownloadProbeResult> ProbeHeadAsync(string url, CancellationToken token)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Head, url))
            using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
            {
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                var finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
                var fileName = SanitizeFileName(GetResponseFileName(response));
                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = FileNameFromUrl(finalUrl);
                var size = GetResponseSize(response);
                if (!response.IsSuccessStatusCode)
                {
                    return new DownloadProbeResult
                    {
                        Success = false,
                        StatusCode = (int)response.StatusCode,
                        FinalUrl = finalUrl,
                        MediaType = mediaType,
                        FileName = fileName,
                        SizeBytes = size,
                        Error = "Сервер вернул HTTP " + (int)response.StatusCode + "."
                    };
                }

                if (IsHtmlMediaType(mediaType))
                {
                    return new DownloadProbeResult
                    {
                        Success = false,
                        StatusCode = (int)response.StatusCode,
                        FinalUrl = finalUrl,
                        MediaType = mediaType,
                        FileName = fileName,
                        SizeBytes = size,
                        Error = "Источник вернул веб-страницу вместо файла."
                    };
                }

                var hasFileName = !string.IsNullOrWhiteSpace(fileName) && Path.HasExtension(fileName);
                var looksBinary = !string.IsNullOrWhiteSpace(mediaType) && !IsHtmlMediaType(mediaType);
                var success = hasFileName || size.HasValue || looksBinary;
                return new DownloadProbeResult
                {
                    Success = success,
                    StatusCode = (int)response.StatusCode,
                    FinalUrl = finalUrl,
                    MediaType = mediaType,
                    FileName = fileName,
                    SizeBytes = size,
                    Error = success ? null : "Сервер не сообщил признаки файла загрузки."
                };
            }
        }

        private static async Task<DownloadProbeResult> ProbeRequestAsync(string url, CancellationToken token, bool useRange)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                if (useRange)
                    request.Headers.Range = new RangeHeaderValue(0, 511);

                using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                    var finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
                    var size = GetResponseSize(response);
                    var fileName = GetResponseFileName(response);
                    if (string.IsNullOrWhiteSpace(fileName) && Uri.TryCreate(finalUrl, UriKind.Absolute, out var finalUri))
                        fileName = Path.GetFileName(finalUri.AbsolutePath);
                    fileName = SanitizeFileName(fileName);

                    if (!response.IsSuccessStatusCode)
                    {
                        return new DownloadProbeResult
                        {
                            Success = false,
                            StatusCode = (int)response.StatusCode,
                            FinalUrl = finalUrl,
                            MediaType = mediaType,
                            FileName = fileName,
                            SizeBytes = size,
                            Error = "Сервер вернул HTTP " + (int)response.StatusCode + "."
                        };
                    }

                    if (IsHtmlMediaType(mediaType))
                    {
                        return new DownloadProbeResult
                        {
                            Success = false,
                            StatusCode = (int)response.StatusCode,
                            FinalUrl = finalUrl,
                            MediaType = mediaType,
                            FileName = fileName,
                            SizeBytes = size,
                            Error = "Источник вернул веб-страницу вместо файла."
                        };
                    }

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        var prefix = new byte[512];
                        var read = 0;
                        while (read < prefix.Length)
                        {
                            var chunk = await stream.ReadAsync(prefix, read, prefix.Length - read, token);
                            if (chunk <= 0) break;
                            read += chunk;
                            if (read >= 64) break;
                        }

                        if (read <= 0)
                        {
                            return new DownloadProbeResult
                            {
                                Success = false,
                                StatusCode = (int)response.StatusCode,
                                FinalUrl = finalUrl,
                                MediaType = mediaType,
                                FileName = fileName,
                                SizeBytes = size,
                                Error = "Сервер не передал содержимое файла."
                            };
                        }

                        if (FileValidator.IsHtmlPrefix(prefix, read))
                        {
                            return new DownloadProbeResult
                            {
                                Success = false,
                                StatusCode = (int)response.StatusCode,
                                FinalUrl = finalUrl,
                                MediaType = mediaType,
                                FileName = fileName,
                                SizeBytes = size,
                                Error = "Источник вернул веб-страницу вместо файла."
                            };
                        }

                        if (!string.IsNullOrWhiteSpace(fileName))
                        {
                            try
                            {
                                FileValidator.ValidatePrefix(fileName, prefix, read);
                            }
                            catch (InvalidDataException ex)
                            {
                                return new DownloadProbeResult
                                {
                                    Success = false,
                                    StatusCode = (int)response.StatusCode,
                                    FinalUrl = finalUrl,
                                    MediaType = mediaType,
                                    FileName = fileName,
                                    SizeBytes = size,
                                    Error = ex.Message
                                };
                            }
                        }
                    }

                    return new DownloadProbeResult
                    {
                        Success = true,
                        StatusCode = (int)response.StatusCode,
                        FinalUrl = finalUrl,
                        MediaType = mediaType,
                        FileName = fileName,
                        SizeBytes = size
                    };
                }
            }
        }

        private static DownloadProbeResult MergeProbeResults(DownloadProbeResult first, DownloadProbeResult second)
        {
            if (first == null) return second;
            if (second == null) return first;
            if (string.IsNullOrWhiteSpace(second.FinalUrl)) second.FinalUrl = first.FinalUrl;
            if (string.IsNullOrWhiteSpace(second.FileName)) second.FileName = first.FileName;
            if (!second.SizeBytes.HasValue) second.SizeBytes = first.SizeBytes;
            if (string.IsNullOrWhiteSpace(second.MediaType)) second.MediaType = first.MediaType;
            if (second.StatusCode == 0) second.StatusCode = first.StatusCode;
            if (string.IsNullOrWhiteSpace(second.Error)) second.Error = first.Error;
            return second;
        }

        public async Task<long?> GetSizeAsync(string url, CancellationToken token)
        {
            if (!IsHttpUrl(url)) return null;

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    var length = response.Content.Headers.ContentLength;
                    if (response.IsSuccessStatusCode && length.HasValue && length.Value > 0)
                        return length.Value;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Range = new RangeHeaderValue(0, 0);
                    using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        var range = response.Content.Headers.ContentRange;
                        if (response.StatusCode == HttpStatusCode.PartialContent && range?.Length.HasValue == true && range.Length.Value > 0)
                            return range.Length.Value;

                        var length = response.Content.Headers.ContentLength;
                        if (response.IsSuccessStatusCode && length.HasValue && length.Value > 1)
                            return length.Value;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            return null;
        }

        public async Task<string> GetFileNameAsync(string url, CancellationToken token)
        {
            if (!IsHttpUrl(url)) return null;

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    var name = GetResponseFileName(response);
                    if (!string.IsNullOrWhiteSpace(name)) return SanitizeFileName(name);
                    var finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
                    var fromUrl = FileNameFromUrl(finalUrl);
                    if (!string.IsNullOrWhiteSpace(fromUrl)) return fromUrl;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                {
                    request.Headers.Range = new RangeHeaderValue(0, 0);
                    using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                    {
                        var name = GetResponseFileName(response);
                        if (!string.IsNullOrWhiteSpace(name)) return SanitizeFileName(name);
                        var finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
                        var fromUrl = FileNameFromUrl(finalUrl);
                        if (!string.IsNullOrWhiteSpace(fromUrl)) return fromUrl;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, url))
                using (var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token))
                {
                    var name = GetResponseFileName(response);
                    if (!string.IsNullOrWhiteSpace(name)) return SanitizeFileName(name);
                    var finalUrl = response.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
                    return FileNameFromUrl(finalUrl);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }

            return null;
        }

        private static string GetResponseFileName(HttpResponseMessage response)
        {
            try
            {
                var header = response?.Content?.Headers?.ContentDisposition;
                if (header == null) return null;
                var value = !string.IsNullOrWhiteSpace(header.FileNameStar) ? header.FileNameStar : header.FileName;
                if (string.IsNullOrWhiteSpace(value)) return null;
                return Uri.UnescapeDataString(value.Trim('"'));
            }
            catch { return null; }
        }

        private static string FileNameFromUrl(string url)
        {
            try
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
                var name = Path.GetFileName(uri.AbsolutePath);
                if (string.IsNullOrWhiteSpace(name) || !name.Contains(".")) return null;
                return SanitizeFileName(Uri.UnescapeDataString(name));
            }
            catch { return null; }
        }

        private static long? GetResponseSize(HttpResponseMessage response)
        {
            var range = response.Content.Headers.ContentRange;
            if (range?.Length.HasValue == true && range.Length.Value > 0) return range.Length.Value;
            var length = response.Content.Headers.ContentLength;
            return length.HasValue && length.Value > 0 ? length.Value : (long?)null;
        }

        private static bool IsHtmlMediaType(string mediaType)
        {
            return mediaType.IndexOf("html", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   mediaType.IndexOf("xhtml", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsHttpUrl(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value.Trim();
        }
    }
}
