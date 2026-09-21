using System;

namespace WpfApp1.Models
{
    public class DownloadRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string Source { get; set; }
        public string Status { get; set; } = "Завершено";
        public string SizeText { get; set; }
        public DateTime DownloadedAt { get; set; } = DateTime.Now;
    }
}
