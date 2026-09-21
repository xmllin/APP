using System;
using System.IO;
using WpfApp1.Services;

namespace WpfApp1.Services.Downloads
{
    public static class DownloadSettings
    {
        private static readonly string SettingsFile = UserDataPath.File("download_settings.txt");

        public static string GetDefaultFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }

        public static string GetFolder()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                    return GetDefaultFolder();

                var value = File.ReadAllText(SettingsFile).Trim();
                if (string.IsNullOrWhiteSpace(value))
                    return GetDefaultFolder();

                return value;
            }
            catch
            {
                return GetDefaultFolder();
            }
        }

        public static void SaveFolder(string folder)
        {
            var target = string.IsNullOrWhiteSpace(folder) ? GetDefaultFolder() : folder.Trim();
            var directory = Path.GetDirectoryName(SettingsFile);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(SettingsFile, target);
        }
    }
}
