using System;
using System.IO;
using System.Runtime.InteropServices;
using Nexora.Services;

namespace Nexora.Services.Downloads
{
    public static class DownloadSettings
    {
        private static readonly string SettingsFile = UserDataPath.File("download_settings.txt");

        private static readonly Guid DownloadsFolderId = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);

        public static string GetDefaultFolder()
        {
            IntPtr pathPtr = IntPtr.Zero;
            try
            {
                var id = DownloadsFolderId;
                if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out pathPtr) == 0 && pathPtr != IntPtr.Zero)
                {
                    var path = Marshal.PtrToStringUni(pathPtr);
                    if (!string.IsNullOrWhiteSpace(path))
                        return path;
                }
            }
            catch { }
            finally
            {
                if (pathPtr != IntPtr.Zero) Marshal.FreeCoTaskMem(pathPtr);
            }

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
