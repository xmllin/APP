using System;
using System.IO;
using Nexora.Services;

namespace Nexora.Services.Downloads
{
    public static class DownloadLog
    {
        private static readonly string LogFile = UserDataPath.File("downloads.log");

        public static void Error(string message, Exception exception = null)
        {
            try
            {
                var directory = Path.GetDirectoryName(LogFile);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.AppendAllText(LogFile, $"[{DateTime.Now:O}] {message} {exception}\r\n");
            }
            catch { }
        }
    }
}
