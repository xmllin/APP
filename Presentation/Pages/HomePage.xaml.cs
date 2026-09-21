using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfApp1.Models;
using WpfApp1.Services;
using WpfApp1.Services.Apps;

namespace WpfApp1.Pages
{
    public partial class HomePage : UserControl
    {
        private const int MinimumRecentApps = 4;
        private const double RecentAppCardOuterWidth = 190;
        private MainWindow _main;
        private AppRepository _repository = new AppRepository();
        private static readonly string RecentFile = UserDataPath.File("recent_apps.txt");
        private int _recentAppCount = MinimumRecentApps;
        private bool _recentAppsLoaded;

        public HomePage(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            Loaded += HomePage_Loaded;
            IsVisibleChanged += HomePage_VisibleChanged;
        }

        private async void HomePage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= HomePage_Loaded;
            UpdateRecentAppCount(RecentScrollViewer.ActualWidth);
            await LoadRecentAppsAsync();
            _recentAppsLoaded = true;
        }

        private async void HomePage_VisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (IsVisible)
            {
                await LoadRecentAppsAsync();
            }
        }

        private async System.Threading.Tasks.Task LoadRecentAppsAsync()
        {
            var recentNames = GetRecentAppNames();
            var allApps = await _repository.LoadAsync();

            var recentApps = new ObservableCollection<AppDefinition>();

            foreach (var name in recentNames.Take(_recentAppCount))
            {
                var app = allApps.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
                if (app != null)
                    recentApps.Add(app);
            }

            if (recentApps.Count < _recentAppCount)
            {
                var remaining = allApps.Where(a => !recentApps.Contains(a)).Take(_recentAppCount - recentApps.Count);
                foreach (var app in remaining)
                    recentApps.Add(app);
            }

            ResolveVisibleLogos(recentApps);
            RecentItems.ItemsSource = recentApps;
        }

        private async void RecentScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_recentAppsLoaded || e.NewSize.Width <= 0)
                return;

            if (!UpdateRecentAppCount(e.NewSize.Width))
                return;

            await LoadRecentAppsAsync();
        }

        private bool UpdateRecentAppCount(double availableWidth)
        {
            if (availableWidth <= 0)
                return false;

            int visibleCount = Math.Max(
                MinimumRecentApps,
                (int)Math.Floor(availableWidth / RecentAppCardOuterWidth));
            if (visibleCount == _recentAppCount)
                return false;

            _recentAppCount = visibleCount;
            return true;
        }

        private void ResolveVisibleLogos(IEnumerable<AppDefinition> apps)
        {
            string logoDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logos");
            if (!System.IO.Directory.Exists(logoDirectory)) return;

            var logosByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in System.IO.Directory.EnumerateFiles(logoDirectory))
            {
                var extension = System.IO.Path.GetExtension(file);
                if (!string.Equals(extension, ".svg", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".ico", StringComparison.OrdinalIgnoreCase)) continue;
                var key = NormalizeLogoKey(System.IO.Path.GetFileNameWithoutExtension(file));
                if (!string.IsNullOrWhiteSpace(key) && !logosByKey.ContainsKey(key)) logosByKey[key] = file;
            }

            var logoById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["firefox"] = "Firefox_logo,_2019.svg",
                ["chrome"] = "chrome-logo.svg",
                ["chromium"] = "chromium.svg",
                ["librewolf"] = "LibreWolf.svg",
                ["opera"] = "Opera_2015_icon.svg",
                ["opera-gx"] = "Opera_GX_Icon.svg",
                ["telegram"] = "Telegram_2019_Logo.svg",
                ["discord"] = "discord-icon-svgrepo-com.svg",
                ["steam"] = "Steam_icon_logo.svg",
                ["7zip"] = "7ziplogo.svg",
                ["winrar"] = "WinRAR_icon.svg",
                ["qbittorrent"] = "",
                ["nvidia-app"] = "nvidia-logo-svgrepo-com.svg",
                ["amd-software-adrenalin"] = "",
                ["intel-driver-support-assistant"] = "",
                ["amd-chipset-software"] = "",
                ["asus-armoury-crate"] = "",
                ["msi-center"] = "",
                ["gigabyte-control-center"] = "",
                ["asrock-auto-driver-installer"] = "",
                ["makutweaker"] = "",
                ["lightshot"] = "lightshot.ico",
                ["happ"] = "happ.ico",
                ["minersearch"] = "",
                ["zapret"] = "",
                ["tg-ws-proxy"] = "tg-ws-proxy.ico",
                ["nuclear"] = "",
                ["omniget"] = "omniget.ico",
                ["lamzu-aurora"] = "",
                ["lamzu-thorn-v2-firmware"] = "",
                ["lamzu-maya-x-firmware"] = "",
                ["lamzu-maya-champion-firmware"] = "",
                ["lamzu-inca-firmware"] = "",
                ["lamzu-paro-aurora-firmware"] = "",
                ["lamzu-tachi-firmware"] = "",
                ["autoruns"] = "",
                ["hwmonitor"] = "",
                ["everything"] = "everything.ico",
                ["vlc"] = "VLC_Icon.svg",
                ["mpc-hc"] = "mpc_hc_18911.ico",
                ["eartrumpet"] = "",
                ["rufus"] = "",
                ["windhawk"] = "",
                ["quicklook"] = "",
                ["notepads"] = "",
                ["notepadpp"] = "",
                ["vscode"] = "",
                ["visualstudio"] = "",
                ["aida64"] = "",
                ["driverbooster"] = "",
                ["glaryutilities5"] = "",
                ["malwarebytes"] = "",
                ["virustotal"] = "",
                ["amd-auto-detect"] = "",
                ["amd-ryzen-master"] = "",
                ["amd-cleanup-utility"] = "",
                ["intel-graphics-software"] = "",
                ["intel-xtu"] = "",
                ["msi-driver-utility-installer"] = "",
                ["snappy-driver-installer-origin"] = "",
                ["driver-store-explorer"] = "",
                ["display-driver-uninstaller"] = "",
                ["nvcleanstall"] = "",
                ["asus-driverhub"] = "",
                ["asrock-app-shop"] = "",
                ["asrock-a-tuning"] = "",
                ["tor-browser"] = "Tor_Browser_icon.svg"
            };

            foreach (var app in apps)
            {
                if (app == null) continue;
                if (!string.IsNullOrWhiteSpace(app.Icon) && !app.Icon.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    string candidate = app.Icon.Replace('/', System.IO.Path.DirectorySeparatorChar);
                    if (!System.IO.Path.IsPathRooted(candidate))
                        candidate = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, candidate);
                    if (System.IO.File.Exists(candidate))
                    {
                        app.Icon = candidate;
                        continue;
                    }
                }

                string fileName;
                if (!string.IsNullOrWhiteSpace(app.Id) && logoById.TryGetValue(app.Id, out fileName) && !string.IsNullOrWhiteSpace(fileName))
                {
                    string candidate = System.IO.Path.Combine(logoDirectory, fileName);
                    if (System.IO.File.Exists(candidate))
                        app.Icon = candidate;
                }

                if (!System.IO.File.Exists(app.Icon))
                {
                    var idKey = NormalizeLogoKey(app.Id);
                    var nameKey = NormalizeLogoKey(app.Name);
                    string candidate;
                    if (logosByKey.TryGetValue(idKey, out candidate) || logosByKey.TryGetValue(nameKey, out candidate))
                        app.Icon = candidate;
                    else if (!string.IsNullOrWhiteSpace(idKey))
                    {
                        var match = logosByKey.FirstOrDefault(x => x.Key.Contains(idKey) || (idKey.Length > 3 && idKey.Contains(x.Key))).Value;
                        if (!string.IsNullOrWhiteSpace(match)) app.Icon = match;
                    }
                }
            }
        }

        private static string NormalizeLogoKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        }

        private List<string> GetRecentAppNames()
        {
            try
            {
                if (!File.Exists(RecentFile)) return new List<string>();
                return File.ReadAllLines(RecentFile).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        public static void AddRecentApp(string appName)
        {
            try
            {
                var dir = Path.GetDirectoryName(RecentFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var recent = new List<string>();
                if (File.Exists(RecentFile))
                    recent = File.ReadAllLines(RecentFile).ToList();

                recent.Remove(appName);
                recent.Insert(0, appName);

                File.WriteAllLines(RecentFile, recent.Take(10));
            }
            catch { }
        }

        private void RecentApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppDefinition app)
            {
                _main.SelectedApp = app;
                AddRecentApp(app.Name);
                _main.NavigateToAppDetails("home");
            }
        }

        private void Apps_Click(object s, RoutedEventArgs e) => _main.Navigate("apps");
        private void UwpApps_Click(object s, RoutedEventArgs e) => _main.Navigate("uwp");
        private void Activation_Click(object s, RoutedEventArgs e) => _main.Navigate("activation");
        private void Windows_Click(object s, RoutedEventArgs e) => _main.Navigate("windows");
        private void Wallpaper_Click(object s, RoutedEventArgs e) => _main.Navigate("wallpapers");
        private void Downloads_Click(object s, RoutedEventArgs e) => _main.Navigate("downloads");
        private void Libraries_Click(object s, RoutedEventArgs e) => _main.Navigate("libraries");
        private void Profile_Click(object s, RoutedEventArgs e) => _main.Navigate("profile");
        private void Settings_Click(object s, RoutedEventArgs e) => _main.Navigate("settings");
    }
}