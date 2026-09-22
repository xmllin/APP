using System;
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
