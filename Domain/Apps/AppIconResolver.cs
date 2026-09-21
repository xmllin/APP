using System;
using System.IO;
using WpfApp1.Models;

namespace WpfApp1.Domain.Apps
{
    public sealed class AppIconResolver
    {
        private readonly string _baseDirectory;
        private readonly string _fallback;

        public AppIconResolver(string baseDirectory = null)
        {
            _baseDirectory = baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory;
            _fallback = Path.Combine(_baseDirectory, "interface", "white", "fluent-app-folder.svg");
        }

        public string Resolve(AppDefinition app)
        {
            if (app == null) return ToUri(_fallback);
            if (!string.IsNullOrWhiteSpace(app.Icon))
            {
                var path = app.Icon;
                if (!Path.IsPathRooted(path)) path = Path.Combine(_baseDirectory, path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) return ToUri(path);
            }
            return File.Exists(_fallback) ? ToUri(_fallback) : app.Icon;
        }

        private static string ToUri(string path) => new Uri(path, UriKind.Absolute).AbsoluteUri;
    }
}
