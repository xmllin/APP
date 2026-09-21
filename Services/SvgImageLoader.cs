using System;
using System.Globalization;
using System.IO;
using System.Collections.Concurrent;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace WpfApp1.Services
{
    public sealed class CategoryColorConverter : IValueConverter
    {
        private static readonly ConcurrentDictionary<string, Brush> Brushes = new ConcurrentDictionary<string, Brush>(StringComparer.OrdinalIgnoreCase);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string category = (value as string ?? string.Empty).Trim().ToLowerInvariant();
            return Brushes.GetOrAdd(category, CreateBrush);
        }

        private static Brush CreateBrush(string category)
        {
            Color color;
            switch (category)
            {
                case "браузеры": color = Color.FromRgb(22, 119, 200); break;
                case "игры": color = Color.FromRgb(102, 82, 215); break;
                case "безопасность": color = Color.FromRgb(135, 73, 214); break;
                case "мультимедиа": color = Color.FromRgb(189, 67, 137); break;
                case "офис": color = Color.FromRgb(198, 154, 43); break;
                case "архиваторы": color = Color.FromRgb(214, 101, 50); break;
                case "общение": color = Color.FromRgb(22, 167, 127); break;
                case "настройка windows": color = Color.FromRgb(0, 157, 197); break;
                case "загрузки": color = Color.FromRgb(8, 126, 232); break;
                case "сеть": color = Color.FromRgb(0, 169, 184); break;
                case "система": color = Color.FromRgb(84, 122, 164); break;
                case "драйверы": color = Color.FromRgb(0, 165, 165); break;
                case "разработка": color = Color.FromRgb(115, 80, 215); break;
                case "устройства": color = Color.FromRgb(71, 91, 176); break;
                case "утилиты": color = Color.FromRgb(42, 113, 201); break;
                default: color = Color.FromRgb(22, 135, 248); break;
            }
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class SvgImage : MarkupExtension
    {
        public string Source { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            string path = Source;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.Replace('/', Path.DirectorySeparatorChar));
            return SvgImageLoader.Load(path);
        }
    }

    public class SvgImageExtension : SvgImage
    {
    }

    public static class SvgImageLoader
    {
        private static readonly ConcurrentDictionary<string, ImageSource> Cache = new ConcurrentDictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly WpfDrawingSettings Settings = new WpfDrawingSettings
        {
            IncludeRuntime = true,
            TextAsGeometry = false
        };

        public static ImageSource Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = Path.GetFullPath(path);
            if (!File.Exists(path)) return null;

            ImageSource cached;
            if (Cache.TryGetValue(path, out cached))
                return cached;

            var loaded = LoadCore(path);
            if (loaded != null)
                Cache.TryAdd(path, loaded);
            return loaded;
        }

        private static ImageSource LoadCore(string path)
        {
            if (string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase))
            {
                var reader = new FileSvgReader(Settings);
                var drawing = reader.Read(path);
                if (drawing == null) return null;
                var drawingImage = new DrawingImage(drawing);
                if (drawingImage.CanFreeze) drawingImage.Freeze();
                return drawingImage;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();
            return image;
        }
    }

    public sealed class AppIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (!(value is string path) || string.IsNullOrWhiteSpace(path)) return null;
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.IsFile)
                path = uri.LocalPath;
            else if (!Path.IsPathRooted(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.Replace('/', Path.DirectorySeparatorChar));

            return SvgImageLoader.Load(path);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
