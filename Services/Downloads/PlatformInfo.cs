using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Nexora.Services.Downloads
{
    public static class PlatformInfo
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public static string ProcessArchitectureName
        {
            get
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.Arm64:
                        return "arm64";
                    case Architecture.X86:
                        return "x86";
                    case Architecture.Arm:
                        return "arm";
                    default:
                        return "x64";
                }
            }
        }

        public static string DisplayProcessArchitecture
        {
            get
            {
                if (string.Equals(ProcessArchitectureName, "arm64", StringComparison.OrdinalIgnoreCase)) return "ARM64";
                if (string.Equals(ProcessArchitectureName, "x86", StringComparison.OrdinalIgnoreCase)) return "x86";
                if (string.Equals(ProcessArchitectureName, "arm", StringComparison.OrdinalIgnoreCase)) return "ARM";
                return "x64";
            }
        }

        public static string ArchitectureName
        {
            get
            {
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.Arm64:
                        return "arm64";
                    case Architecture.X86:
                        return "x86";
                    case Architecture.Arm:
                        return "arm";
                    default:
                        return "x64";
                }
            }
        }

        public static bool IsArm64 => string.Equals(ArchitectureName, "arm64", StringComparison.OrdinalIgnoreCase);
        public static bool IsX64 => string.Equals(ArchitectureName, "x64", StringComparison.OrdinalIgnoreCase);
        public static bool IsX86 => string.Equals(ArchitectureName, "x86", StringComparison.OrdinalIgnoreCase);

        public static string DisplayArchitecture
        {
            get
            {
                if (IsArm64) return "ARM64";
                if (IsX86) return "x86";
                if (string.Equals(ArchitectureName, "arm", StringComparison.OrdinalIgnoreCase)) return "ARM";
                return "x64";
            }
        }

        public static int WindowsBuildNumber
        {
            get
            {
                if (!IsWindows) return 0;
                try
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    {
                        var raw = key?.GetValue("CurrentBuildNumber") ?? key?.GetValue("CurrentBuild");
                        int build;
                        return int.TryParse(raw?.ToString(), out build) ? build : 0;
                    }
                }
                catch
                {
                    return 0;
                }
            }
        }

        public static bool IsWindows10OrNewer => WindowsBuildNumber >= 10240;
    }
}
