using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfApp1.Services;

namespace WpfApp1.Pages
{
    public partial class ProfilePage : UserControl
    {
        public ProfilePage(MainWindow main)
        {
            InitializeComponent();
            Loaded += ProfilePage_Loaded;
        }

        private async void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= ProfilePage_Loaded;
            var userName = Environment.UserName;
            UserNameText.Text = userName;
            UserInitialText.Text = string.IsNullOrWhiteSpace(userName) ? "?" : userName.Substring(0, 1).ToUpperInvariant();
            UserAccountText.Text = "Учетная запись: " + userName;
            SetIcons();
            var info = SystemRecommendationService.LoadCachedHardwareInfo();
            if (info == null)
            {
                SystemText.Text = "Определение системы…";
                CpuText.Text = "Определение процессора…";
                MemoryText.Text = "Определение памяти…";
                GpuText.Text = "Определение видеокарты…";
                MotherboardText.Text = "Определение материнской платы…";
            }
            else
            {
                ApplyHardwareInfo(info);
            }

            await RefreshHardwareInfoAsync(info);
        }

        private async Task RefreshHardwareInfoAsync(SystemHardwareInfo cachedInfo)
        {
            var info = await Task.Run(SystemRecommendationService.GetHardwareInfo);
            await Task.Run(() => SystemRecommendationService.SaveCachedHardwareInfo(info));
            if (SystemRecommendationService.AreHardwareInfoEqual(cachedInfo, info)) return;

            ApplyHardwareInfo(info);
        }

        private void ApplyHardwareInfo(SystemHardwareInfo info)
        {
            SystemText.Text = FormatSystem(info);
            CpuText.Text = FormatCpu(info);
            MemoryText.Text = info.MemoryBytes > 0
                ? string.Format("{0:0.0} ГБ RAM", info.MemoryBytes / 1024d / 1024d / 1024d)
                : "Не удалось определить объём памяти.";
            GpuText.Text = info.GpuNames.Count == 0 ? "Не удалось определить видеокарту." : string.Join("\n", info.GpuNames);
            MotherboardText.Text = FormatMotherboard(info);
        }

        private void PcDetails_Click(object sender, RoutedEventArgs e)
        {
            bool show = PcInfoPanel.Visibility != Visibility.Visible;
            PcInfoPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            PcDetailsButton.Content = show ? "Скрыть сведения о ПК" : "Показать сведения о ПК";
        }

        private void SetIcons()
        {
            SystemIcon.Source = LoadSvg("interface/white/brand-windows.svg");
            MemoryIcon.Source = LoadSvg("interface/white/icons8-memory-50.svg");
            CpuIcon.Source = LoadSvg("interface/white/cpu.svg");
            GpuIcon.Source = LoadSvg("interface/white/icons8-videocard-50.svg");
            MotherboardIcon.Source = LoadSvg("interface/white/icons8-motherboard-50.svg");
        }

        private static ImageSource LoadSvg(string relativePath)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return SvgImageLoader.Load(path);
        }

        private static string FormatSystem(SystemHardwareInfo info)
        {
            var system = string.IsNullOrWhiteSpace(info.OperatingSystem) ? "Windows" : info.OperatingSystem.Trim();
            var version = string.IsNullOrWhiteSpace(info.WindowsVersion) ? "Не определена" : info.WindowsVersion.Trim();
            var architecture = string.IsNullOrWhiteSpace(info.Architecture) ? "Не определена" : info.Architecture.Trim();
            return system + "\nВерсия Windows " + version + "\n" + architecture;
        }

        private static string FormatCpu(SystemHardwareInfo info)
        {
            if (string.IsNullOrWhiteSpace(info.CpuName)) return "Не удалось определить процессор.";
            var result = info.CpuName;
            if (info.CpuCoreCount > 0 || info.CpuLogicalProcessorCount > 0)
                result += string.Format("\nЯдра: {0}, потоков: {1}", info.CpuCoreCount, info.CpuLogicalProcessorCount);
            return result;
        }

        private static string FormatMotherboard(SystemHardwareInfo info)
        {
            var motherboard = (info.MotherboardManufacturer + " " + info.MotherboardModel).Trim();
            return string.IsNullOrWhiteSpace(motherboard) ? "Не удалось определить материнскую плату." : motherboard;
        }
    }
}
