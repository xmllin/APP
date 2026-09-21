using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using WpfApp1.Models;
using WpfApp1.Services;

namespace WpfApp1.Services.Downloads
{
    public static class DownloadHistoryService
    {
        private static readonly string HistoryFile = UserDataPath.File("downloads_history.json");

        public static List<DownloadRecord> Load()
        {
            try
            {
                if (!File.Exists(HistoryFile))
                    return new List<DownloadRecord>();

                var json = File.ReadAllText(HistoryFile);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<DownloadRecord>();

                return JsonSerializer.Deserialize<List<DownloadRecord>>(json) ?? new List<DownloadRecord>();
            }
            catch (Exception ex)
            {
                DownloadLog.Error("Не удалось прочитать историю загрузок.", ex);
                return new List<DownloadRecord>();
            }
        }

        public static void Save(IEnumerable<DownloadRecord> items)
        {
            try
            {
                var directory = Path.GetDirectoryName(HistoryFile);
                if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(items.ToList());
                File.WriteAllText(HistoryFile, json);
            }
            catch (Exception ex)
            {
                DownloadLog.Error("Не удалось сохранить историю загрузок.", ex);
            }
        }

        public static void Add(DownloadRecord record)
        {
            if (record == null) return;
            var items = Load();
            items.Insert(0, record);
            Save(items.Take(50));
        }

        public static void Delete(DownloadRecord record)
        {
            if (record == null) return;

            var items = Load();
            var target = items.FirstOrDefault(x => x.Id == record.Id || string.Equals(x.FullPath, record.FullPath, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                items.Remove(target);
                TryDeleteFile(target.FullPath);
                Save(items);
            }
            else
            {
                TryDeleteFile(record.FullPath);
            }
        }

        public static void Clear()
        {
            var items = Load();
            Save(new List<DownloadRecord>());
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                DownloadLog.Error("Не удалось удалить файл загрузки.", ex);
            }
        }
    }
}
