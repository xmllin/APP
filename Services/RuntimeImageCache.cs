using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nexora.Services
{
    public static class RuntimeImageCache
    {
        public static string Root { get; } = UserDataPath.Subfolder("RuntimeImageCache");
        private static readonly HttpClient Client = CreateClient();

        public static string FilePath(string folder, string fileName)
        {
            var directory = Path.Combine(Root, folder ?? string.Empty);
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, fileName);
        }

        public static async Task DownloadAsync(string url, string path, CancellationToken token)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0) return;

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
                    throw new IOException("Downloaded image is empty.");
                File.Move(temporaryPath, path, true);
            }
            catch
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); } catch { }
                throw;
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Nexora/1.0");
            return client;
        }

        public static void Clear()
        {
            try
            {
                if (Directory.Exists(Root))
                    Directory.Delete(Root, true);
            }
            catch
            {
                // A locked image can be removed on the next application start.
            }
        }

        public static void Initialize()
        {
            Clear();
            Directory.CreateDirectory(Root);
        }
    }
}