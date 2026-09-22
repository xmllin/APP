using System;
using System.IO;

namespace Nexora.Services
{
    public static class UserDataPath
    {
        private static readonly string LegacyRoot = Path.Combine(AppContext.BaseDirectory, "UserData");

        public static string Root { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexora");

        public static string FileInFolder(string folderName, string fileName)
        {
            var folder = Subfolder(folderName);
            return Path.Combine(folder, fileName);
        }

        static UserDataPath()
        {
            Directory.CreateDirectory(Root);
            MigrateLegacyData();
        }

        public static string File(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Root;

            var lower = name.ToLowerInvariant();
            var folder = lower.Contains("cache") ? "Cache"
                : lower.Contains("log") ? "Logs"
                : lower.Contains("history") ? "Downloads"
                : lower.Contains("setting") ? "Settings"
                : lower.Contains("wallpaper") ? "Wallpapers"
                : lower.Contains("diagnostic") ? "Diagnostics"
                : lower.Contains("recent") || lower.Contains("favorite") ? "Data"
                : "Data";

            return Path.Combine(Subfolder(folder), name);
        }

        public static string Subfolder(params string[] parts)
        {
            var path = Root;
            foreach (var part in parts ?? new string[0])
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                path = Path.Combine(path, part);
            }
            Directory.CreateDirectory(path);
            return path;
        }

        private static void MigrateLegacyData()
        {
            try
            {
                if (!Directory.Exists(LegacyRoot) ||
                    string.Equals(Path.GetFullPath(LegacyRoot).TrimEnd(Path.DirectorySeparatorChar),
                        Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    return;

                foreach (var source in Directory.EnumerateFiles(LegacyRoot, "*", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(LegacyRoot, source);
                    var destination = relative.IndexOf(Path.DirectorySeparatorChar) >= 0 || relative.IndexOf(Path.AltDirectorySeparatorChar) >= 0
                        ? Path.Combine(Root, relative)
                        : File(Path.GetFileName(source));
                    if (System.IO.File.Exists(destination))
                        continue;
                    var destinationDir = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(destinationDir)) Directory.CreateDirectory(destinationDir);
                    System.IO.File.Copy(source, destination, false);
                }
            }
            catch
            {
                // Migration is best-effort; failure must not prevent the application from starting.
            }
        }
    }
}
