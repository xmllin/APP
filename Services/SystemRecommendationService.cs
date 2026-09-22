using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Management;
using Hardware.Info;
using HardwareInfoApi = Hardware.Info.HardwareInfo;

namespace Nexora.Services
{
    public sealed class SystemHardwareInfo
    {
        public string CpuName { get; set; }
        public string CpuManufacturer { get; set; }
        public string GpuName { get; set; }
        public string GpuManufacturer { get; set; }
        public List<string> GpuNames { get; set; } = new List<string>();
        public string MotherboardManufacturer { get; set; }
        public string MotherboardModel { get; set; }
        public string OperatingSystem { get; set; }
        public string WindowsVersion { get; set; }
        public string Architecture { get; set; }
        public ulong MemoryBytes { get; set; }
        public int CpuCoreCount { get; set; }
        public int CpuLogicalProcessorCount { get; set; }

        public string Summary
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(CpuName)) parts.Add("CPU: " + CpuName);
                if (GpuNames.Count > 0)
                    parts.Add("GPU: " + string.Join(", ", GpuNames));
                else if (!string.IsNullOrWhiteSpace(GpuName))
                    parts.Add("GPU: " + GpuName);
                if (!string.IsNullOrWhiteSpace(MotherboardManufacturer) || !string.IsNullOrWhiteSpace(MotherboardModel))
                    parts.Add("MB: " + (MotherboardManufacturer + " " + MotherboardModel).Trim());
                return string.Join("\n", parts);
            }
        }
    }

    public static class SystemRecommendationService
    {
        private static readonly string HardwareCacheFile = UserDataPath.File("system_hardware_cache.json");

        public static SystemHardwareInfo LoadCachedHardwareInfo()
        {
            try
            {
                if (!File.Exists(HardwareCacheFile)) return null;
                var info = JsonSerializer.Deserialize<SystemHardwareInfo>(File.ReadAllText(HardwareCacheFile));
                if (info != null && info.GpuNames == null)
                    info.GpuNames = new List<string>();
                return info;
            }
            catch
            {
                return null;
            }
        }

        public static void SaveCachedHardwareInfo(SystemHardwareInfo info)
        {
            if (info == null) return;

            try
            {
                var temporaryFile = HardwareCacheFile + ".tmp";
                File.WriteAllText(temporaryFile, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
                File.Copy(temporaryFile, HardwareCacheFile, true);
                File.Delete(temporaryFile);
            }
            catch
            {
                // Hardware detection should not fail because the cache cannot be written.
            }
        }

        public static bool AreHardwareInfoEqual(SystemHardwareInfo first, SystemHardwareInfo second)
        {
            if (ReferenceEquals(first, second)) return true;
            if (first == null || second == null) return false;

            return string.Equals(first.CpuName, second.CpuName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(first.CpuManufacturer, second.CpuManufacturer, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(first.GpuName, second.GpuName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(first.GpuManufacturer, second.GpuManufacturer, StringComparison.OrdinalIgnoreCase) &&
                   first.GpuNames.SequenceEqual(second.GpuNames, StringComparer.OrdinalIgnoreCase) &&
                   string.Equals(first.MotherboardManufacturer, second.MotherboardManufacturer, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(first.MotherboardModel, second.MotherboardModel, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(first.OperatingSystem, second.OperatingSystem, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(first.WindowsVersion, second.WindowsVersion, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(first.Architecture, second.Architecture, StringComparison.OrdinalIgnoreCase) &&
                   first.MemoryBytes == second.MemoryBytes &&
                   first.CpuCoreCount == second.CpuCoreCount &&
                   first.CpuLogicalProcessorCount == second.CpuLogicalProcessorCount;
        }

        public static SystemHardwareInfo GetHardwareInfo()
        {
            var info = new SystemHardwareInfo();

            var hardwareInfo = new HardwareInfoApi();
            RefreshHardware(() => hardwareInfo.RefreshCPUList());
            RefreshHardware(() => hardwareInfo.RefreshVideoControllerList());
            RefreshHardware(() => hardwareInfo.RefreshMotherboardList());
            RefreshHardware(() => hardwareInfo.RefreshMemoryStatus());

            var cpu = hardwareInfo.CpuList.FirstOrDefault();
            if (cpu != null)
            {
                info.CpuManufacturer = cpu.Manufacturer ?? string.Empty;
                info.CpuName = CleanCpuName(cpu.Name ?? string.Empty);
                info.CpuCoreCount = (int)cpu.NumberOfCores;
                info.CpuLogicalProcessorCount = (int)cpu.NumberOfLogicalProcessors;
            }

            foreach (var gpu in hardwareInfo.VideoControllerList)
            {
                var manufacturer = gpu.Manufacturer ?? string.Empty;
                var name = gpu.Name ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var combined = (manufacturer + " " + name).Trim();
                if (!info.GpuNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                    info.GpuNames.Add(name);

                if (string.IsNullOrWhiteSpace(info.GpuName) || IsDiscreteGpu(combined))
                {
                    info.GpuManufacturer = manufacturer;
                    info.GpuName = name;
                }
            }

            var board = hardwareInfo.MotherboardList.FirstOrDefault();
            if (board != null)
            {
                info.MotherboardManufacturer = board.Manufacturer ?? string.Empty;
                info.MotherboardModel = board.Product ?? string.Empty;
            }

            if (hardwareInfo.MemoryStatus != null)
                info.MemoryBytes = hardwareInfo.MemoryStatus.TotalPhysical;

            ReadOperatingSystemInfo(info);
            return info;
        }

        private static void RefreshHardware(Action refresh)
        {
            try { refresh(); }
            catch { }
        }

        public static IReadOnlyList<string> GetTags()
        {
            return GetTags(GetHardwareInfo());
        }

        public static IReadOnlyList<string> GetTags(SystemHardwareInfo info)
        {
            info = info ?? new SystemHardwareInfo();
            var architecture = Nexora.Services.Downloads.PlatformDetectionService.Current.Architecture;
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "recommendation:official",
                "arch:" + architecture
            };

            AddCpuTags(tags, info.CpuManufacturer, info.CpuName);
            AddGpuTags(tags, info.GpuManufacturer, info.GpuName, info.GpuNames);
            AddMotherboardTags(tags, info.MotherboardManufacturer, info.MotherboardModel);

            return tags.ToList();
        }

        private static void AddCpuTags(HashSet<string> tags, string manufacturer, string name)
        {
            var text = ((manufacturer ?? "") + " " + (name ?? "")).ToLowerInvariant();
            if (text.Contains("amd") || text.Contains("advanced micro devices") || text.Contains("ryzen"))
            {
                tags.Add("cpu:amd");
                tags.Add("cpu:amd:ryzen");
            }
            if (text.Contains("intel") || text.Contains("genuineintel"))
            {
                tags.Add("cpu:intel");
            }
        }

        private static void AddGpuTags(HashSet<string> tags, string manufacturer, string selectedName, IEnumerable<string> allNames)
        {
            var text = string.Join(" ", new[] { manufacturer, selectedName }.Concat(allNames ?? Enumerable.Empty<string>())).ToLowerInvariant();
            if (text.Contains("nvidia") || text.Contains("geforce") || text.Contains("quadro") || text.Contains("rtx") || text.Contains("gtx"))
            {
                tags.Add("gpu:nvidia");
                tags.Add("gpu:nvidia:geforce");
            }
            if (text.Contains("amd") || text.Contains("ati") || text.Contains("radeon"))
            {
                tags.Add("gpu:amd");
                tags.Add("gpu:amd:radeon");
            }
            if (text.Contains("intel") || text.Contains("arc"))
            {
                tags.Add("gpu:intel");
            }
        }

        private static void AddMotherboardTags(HashSet<string> tags, string manufacturer, string model)
        {
            var text = ((manufacturer ?? "") + " " + (model ?? "")).ToLowerInvariant();
            if (text.Contains("asrock"))
            {
                tags.Add("motherboard:asrock");
                var modelText = (model ?? "").ToLowerInvariant();
                if (NormalizeModel(modelText) == "b550mpro4") tags.Add("motherboard:asrock:b550m-pro4");
            }
            if (text.Contains("asus") || text.Contains("asustek")) tags.Add("motherboard:asus");
            if (text.Contains("micro-star") || text.Contains("msi")) tags.Add("motherboard:msi");
            if (text.Contains("gigabyte") || text.Contains("giga-byte")) tags.Add("motherboard:gigabyte");
            if (text.Contains("biostar")) tags.Add("motherboard:biostar");
            if (text.Contains("evga")) tags.Add("motherboard:evga");
            if (text.Contains("colorful")) tags.Add("motherboard:colorful");
        }


        private static string NormalizeModel(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private static bool IsDiscreteGpu(string value)
        {
            var text = (value ?? "").ToLowerInvariant();
            return text.Contains("nvidia") || text.Contains("geforce") || text.Contains("rtx") || text.Contains("gtx") ||
                   text.Contains("radeon rx") || text.Contains("radeon pro") || text.Contains("arc a") || text.Contains("arc b");
        }

        public static string CleanCpuName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var result = value.Trim();

            // Hardware.Info/WMI may return names such as:
            // "AMD Ryzen 5 5600 6-Core Processor"
            // "Intel(R) Core(TM) i5-12400F 6-Core Processor"
            result = Regex.Replace(result, @"\s+\d+[- ]Core\s+Processor\b", string.Empty, RegexOptions.IgnoreCase);
            result = Regex.Replace(result, @"\s+\d+[- ]Core\b", string.Empty, RegexOptions.IgnoreCase);
            return result.Trim();
        }

        private static void ReadOperatingSystemInfo(SystemHardwareInfo info)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem"))
                using (var results = searcher.Get())
                {
                    var operatingSystem = results.Cast<ManagementObject>().FirstOrDefault();
                    if (operatingSystem == null) return;

                    var caption = Convert.ToString(operatingSystem["Caption"])?.Trim() ?? string.Empty;
                    var windowsIndex = caption.IndexOf("Windows", StringComparison.OrdinalIgnoreCase);
                    info.OperatingSystem = windowsIndex >= 0 ? caption.Substring(windowsIndex) : caption;

                    var version = Convert.ToString(operatingSystem["Version"])?.Trim() ?? string.Empty;
                    var build = Convert.ToString(operatingSystem["BuildNumber"])?.Trim() ?? string.Empty;
                    info.WindowsVersion = string.IsNullOrWhiteSpace(build) || version.EndsWith(build, StringComparison.OrdinalIgnoreCase)
                        ? version
                        : version + " (сборка " + build + ")";
                    // WMI's OSArchitecture is presentation data ("64-bit", "32-bit").
                    // Use the canonical architecture detector for download selection so ARM64
                    // can never collapse into x64.
                    info.Architecture = Nexora.Services.Downloads.PlatformDetectionService.Current.DisplayArchitecture;
                }
            }
            catch
            {
                info.OperatingSystem = string.Empty;
                info.WindowsVersion = string.Empty;
                info.Architecture = Nexora.Services.Downloads.PlatformDetectionService.Current.DisplayArchitecture;
            }
        }
    }
}
