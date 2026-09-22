using System;

namespace Nexora.Services.Downloads
{
    public sealed class DetectedPlatform
    {
        public bool IsWindows { get; set; }
        public string Architecture { get; set; }
        public string DisplayArchitecture { get; set; }
        public string ProcessArchitecture { get; set; }
        public string DisplayProcessArchitecture { get; set; }
        public int WindowsBuild { get; set; }
        public bool IsWindows10OrNewer => WindowsBuild >= 10240;
    }

    public static class PlatformDetectionService
    {
        private static readonly Lazy<DetectedPlatform> CurrentPlatform = new Lazy<DetectedPlatform>(Detect);

        public static DetectedPlatform Current => CurrentPlatform.Value;

        public static DetectedPlatform Detect()
        {
            return new DetectedPlatform
            {
                IsWindows = PlatformInfo.IsWindows,
                Architecture = PlatformInfo.ArchitectureName,
                DisplayArchitecture = PlatformInfo.DisplayArchitecture,
                ProcessArchitecture = PlatformInfo.ProcessArchitectureName,
                DisplayProcessArchitecture = PlatformInfo.DisplayProcessArchitecture,
                WindowsBuild = PlatformInfo.WindowsBuildNumber
            };
        }
    }
}
