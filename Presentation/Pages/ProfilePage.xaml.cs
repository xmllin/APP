using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Nexora.Services;

namespace Nexora.Pages
{
    public partial class ProfilePage : UserControl
    {
        private readonly MainWindow _main;
        private bool _refreshing;

        public ProfilePage(MainWindow main)
        {
            _main = main;
            InitializeComponent();
            Loaded += ProfilePage_Loaded;
        }

        private async void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ProfilePage_Loaded;
            var userName = Environment.UserName;
            UserNameText.Text = string.IsNullOrWhiteSpace(userName) ? "Пользователь" : userName;
            UserInitialText.Text = string.IsNullOrWhiteSpace(userName) ? "?" : userName.Substring(0, 1).ToUpperInvariant();
            UserAccountText.Text = "Учётная запись Windows: " + userName;
            await RefreshHardwareInfoAsync(SystemRecommendationService.LoadCachedHardwareInfo());
        }

        private async void RefreshHardware_Click(object sender, RoutedEventArgs e)
        {
            await RefreshHardwareInfoAsync(null);
        }

        private async Task RefreshHardwareInfoAsync(SystemHardwareInfo cachedInfo)
        {
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                if (cachedInfo != null) ApplyHardwareInfo(cachedInfo);
                else SetLoadingState();

                var info = await Task.Run(SystemRecommendationService.GetHardwareInfo);
                await Task.Run(() => SystemRecommendationService.SaveCachedHardwareInfo(info));
                ApplyHardwareInfo(info);
            }
            catch (Exception ex)
            {
                SystemText.Text = "Не удалось получить сведения о системе.";
                CpuText.Text = ex.Message;
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void SetLoadingState()
        {
            SystemText.Text = "Определение системы…";
            WindowsVersionText.Text = "Определение версии";
            ArchitectureText.Text = "Определение архитектуры";
            MemoryText.Text = "Определение памяти…";
            CpuText.Text = "Определение процессора…";
            GpuText.Text = "Определение видеокарты…";
            MotherboardText.Text = "Определение материнской платы…";
        }

        private void ApplyHardwareInfo(SystemHardwareInfo info)
        {
            SystemText.Text = string.IsNullOrWhiteSpace(info.OperatingSystem) ? "Windows" : info.OperatingSystem.Trim();
            WindowsVersionText.Text = string.IsNullOrWhiteSpace(info.WindowsVersion) ? "Версия не определена" : info.WindowsVersion.Trim();
            ArchitectureText.Text = string.IsNullOrWhiteSpace(info.Architecture) ? "Архитектура не определена" : info.Architecture.Trim();
            SystemSummaryText.Text = string.IsNullOrWhiteSpace(info.OperatingSystem) ? "Windows" : info.OperatingSystem.Trim();

            MemoryText.Text = info.MemoryBytes > 0
                ? $"{info.MemoryBytes / 1024d / 1024d / 1024d:0.0} ГБ"
                : "Не удалось определить объём памяти.";

            CpuText.Text = string.IsNullOrWhiteSpace(info.CpuName) ? "Не удалось определить процессор." : info.CpuName.Trim();
            CpuCoresText.Text = info.CpuCoreCount > 0 ? "Ядра: " + info.CpuCoreCount : "Ядра: —";
            CpuThreadsText.Text = info.CpuLogicalProcessorCount > 0 ? "Потоки: " + info.CpuLogicalProcessorCount : "Потоки: —";

            GpuText.Text = info.GpuNames.Count == 0 ? "Не удалось определить видеокарту." : string.Join("\n", info.GpuNames);
            MotherboardText.Text = FormatMotherboard(info);
            DrivesControl.ItemsSource = GetDriveTiles();
        }

        private static DriveTileInfo[] GetDriveTiles()
        {
            try
            {
                return DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.TotalSize > 0)
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(d =>
                    {
                        var used = d.TotalSize - d.AvailableFreeSpace;
                        var percent = d.TotalSize == 0 ? 0 : (int)Math.Round(used * 100d / d.TotalSize);
                        var label = string.IsNullOrWhiteSpace(d.VolumeLabel)
                            ? d.Name.TrimEnd('\\')
                            : d.VolumeLabel.Trim();

                        return new DriveTileInfo
                        {
                            Path = d.RootDirectory.FullName,
                            DisplayName = label,
                            UsageText = $"{FormatBytes(used)} занято из {FormatBytes(d.TotalSize)} · {percent}%"
                        };
                    })
                    .ToArray();
            }
            catch
            {
                return Array.Empty<DriveTileInfo>();
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L)
                return $"{bytes / 1024d / 1024d / 1024d:0.0} ГБ";
            if (bytes >= 1024L * 1024L)
                return $"{bytes / 1024d / 1024d:0.0} МБ";
            return $"{Math.Max(0, bytes) / 1024d:0.0} КБ";
        }


        private sealed class DriveTileInfo
        {
            public string Path { get; set; } = string.Empty;
            public string DisplayName { get; set; } = string.Empty;
            public string UsageText { get; set; } = string.Empty;
        }

        private void DriveTile_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is System.Windows.Controls.Button button) || !(button.Tag is DriveTileInfo drive))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = drive.Path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось открыть диск: " + ex.Message, "Профиль", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static string FormatMotherboard(SystemHardwareInfo info)
        {
            var motherboard = (info.MotherboardManufacturer + " " + info.MotherboardModel).Trim();
            return string.IsNullOrWhiteSpace(motherboard) ? "Не удалось определить материнскую плату." : motherboard;
        }

        private void OpenApps_Click(object sender, RoutedEventArgs e) => _main.Navigate("apps");
        private void OpenWindows_Click(object sender, RoutedEventArgs e) => _main.Navigate("windows");
        private void OpenCleanup_Click(object sender, RoutedEventArgs e) => _main.Navigate("diskcleanup");
    }
}
