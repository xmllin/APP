namespace Nexora.Models
{
    public class DownloadProgress
    {
        public long BytesReceived { get; set; }
        public long? TotalBytes { get; set; }
        public double Progress { get; set; }
        public double BytesPerSecond { get; set; }
    }
}
