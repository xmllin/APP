using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using WpfApp1.Services;

namespace WpfApp1.Models
{
    public class WallpaperItem : INotifyPropertyChanged
    {
        private BitmapImage _thumbnail;
        private bool _isSelected;

        public string FilePath { get; set; }
        public string FileName { get { return Path.GetFileName(FilePath); } }
        public string Title { get; set; }
        public string Name { get { return Title; } set { Title = value; } }
        public string Category { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public long SizeBytes { get; set; }
        public string Description { get; set; }
        public string ThumbnailUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string Id { get; set; }
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }
        public bool IsRemote => !string.IsNullOrWhiteSpace(DownloadUrl);

        public BitmapImage Thumbnail
        {
            get { return _thumbnail; }
            private set
            {
                if (ReferenceEquals(_thumbnail, value)) return;
                _thumbnail = value;
                OnPropertyChanged();
            }
        }

        public string FullPath { get { return FilePath; } }

        public string SizeText
        {
            get { return string.Format("{0} × {1} • {2:0.0} МБ", Width, Height, SizeBytes / 1024d / 1024d); }
        }

        public void SetThumbnail(BitmapImage thumbnail)
        {
            Thumbnail = thumbnail;
        }

        public static WallpaperItem FromWallpaper(Wallpaper wallpaper)
        {
            var item = new WallpaperItem
            {
                Id = wallpaper.Id,
                Title = wallpaper.Name,
                Name = wallpaper.Name,
                Category = wallpaper.Category,
                ThumbnailUrl = wallpaper.Preview,
                DownloadUrl = wallpaper.Image,
                FilePath = RuntimeImageCache.FilePath("originals", SafeFileName(wallpaper.Id) + Path.GetExtension(new Uri(wallpaper.Image).AbsolutePath)),
                    SizeBytes = wallpaper.SizeBytes,
                    Width = wallpaper.Width,
                    Height = wallpaper.Height,
                Description = "Изображение из удалённой библиотеки GitHub."
            };
            item.RefreshLocalInfo();
            return item;
        }

            private static string SafeFileName(string value)
            {
                foreach (var invalid in Path.GetInvalidFileNameChars()) value = (value ?? "wallpaper").Replace(invalid, '_');
                return string.IsNullOrWhiteSpace(value) ? "wallpaper" : value;
            }

        public void LoadThumbnail(int width, int height)
        {
            if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath)) return;
            Thumbnail = CreateThumbnail(FilePath, width, height);
        }

        public void RefreshLocalInfo()
        {
            if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath)) return;

            try
            {
                var info = new FileInfo(FilePath);
                SizeBytes = info.Length;
                using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (decoder.Frames.Count > 0)
                    {
                        Width = decoder.Frames[0].PixelWidth;
                        Height = decoder.Frames[0].PixelHeight;
                    }
                }
                OnPropertyChanged(nameof(SizeText));
            }
            catch { }
        }

        public async System.Threading.Tasks.Task EnsureLocalAsync(System.Threading.CancellationToken token)
        {
            if (!IsRemote) return;
            await RuntimeImageCache.DownloadAsync(DownloadUrl, FilePath, token);
            RefreshLocalInfo();
        }

        public static async System.Threading.Tasks.Task<BitmapImage> CreateRemoteThumbnailAsync(string url, int width, CancellationToken token)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) })
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
            {
                if (!response.IsSuccessStatusCode)
                    throw new WallpaperHttpException(response.StatusCode);
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var memory = new MemoryStream())
                {
                    await stream.CopyToAsync(memory, 81920, token);
                    memory.Position = 0;
                    return CreateThumbnail(memory, width);
                }
            }
        }

        private static BitmapImage CreateThumbnail(MemoryStream memory, int width)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = memory;
            image.DecodePixelWidth = width;
            image.EndInit();
            image.Freeze();
            return image;
        }

        public static async System.Threading.Tasks.Task<BitmapImage> CreateRemoteImageAsync(string url, int width, CancellationToken token)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var memory = new MemoryStream())
                {
                    await stream.CopyToAsync(memory, 81920, token);
                    memory.Position = 0;
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = memory;
                    image.DecodePixelWidth = width;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }
            }
        }

        public static BitmapImage CreateThumbnail(string filePath, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.UriSource = new Uri(filePath, UriKind.Absolute);
                image.DecodePixelWidth = width;
                image.DecodePixelHeight = height;
                image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
