using System;
using System.IO;

namespace WpfApp1.Services
{
    public static class AssetPath
    {
        public static string BaseDirectory
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string WallpapersDirectory
        {
            get { return Path.Combine(BaseDirectory, "Wallpaper"); }
        }

        public static string LogosDirectory
        {
            get { return Path.Combine(BaseDirectory, "logos"); }
        }

        public static string IconsDirectory
        {
            get { return Path.Combine(BaseDirectory, "interface"); }
        }

        public static string Wallpaper(string fileName)
        {
            return Path.Combine(WallpapersDirectory, fileName);
        }

        public static string Logo(string fileName)
        {
            return Path.Combine(LogosDirectory, fileName);
        }

        public static string Icon(string theme, string fileName)
        {
            return Path.Combine(IconsDirectory, theme, fileName);
        }
    }
}
