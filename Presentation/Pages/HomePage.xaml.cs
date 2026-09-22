using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.Models;
using WpfApp1.Domain.Apps;
using WpfApp1.Services;
using WpfApp1.Services.Apps;

namespace WpfApp1.Pages
{
    public partial class HomePage : UserControl
    {
        private const int MinimumRecentApps = 2;
        private const double RecentAppCardOuterWidth = 190;
        private const int RecentAppsHistoryLimit = 50;
        private MainWindow _main;
        private AppRepository _repository = new AppRepository();
        private static readonly string RecentFile = UserDataPath.File("recent_apps.txt");
        private int _recentAppCount = MinimumRecentApps;
        private bool _recentAppsLoaded;
        private readonly SemaphoreSlim _recentLoadLock = new SemaphoreSlim(1, 1);
        private readonly AppIconResolver _iconResolver = new AppIconResolver();

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

        private async Task LoadRecentAppsAsync()
        {
            await _recentLoadLock.WaitAsync();
            try
            {
                var recentKeys = GetRecentAppNames();
                var allApps = await _repository.LoadAsync();

                var recentApps = new ObservableCollection<AppDefinition>();
                foreach (var key in recentKeys)
                {
                    var app = allApps.FirstOrDefault(a =>
                        string.Equals(a.Id, key, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(a.Name, key, StringComparison.OrdinalIgnoreCase));
                    if (app != null && !recentApps.Contains(app))
                    {
                        recentApps.Add(app);
                        if (recentApps.Count >= _recentAppCount) break;
                    }
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
            finally
            {
                _recentLoadLock.Release();
            }
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
            foreach (var app in apps ?? Enumerable.Empty<AppDefinition>())
            {
                if (app == null) continue;
                app.Icon = _iconResolver.Resolve(app);
            }
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

        public static void AddRecentApp(AppDefinition app)
        {
            if (app == null) return;
            var key = !string.IsNullOrWhiteSpace(app.Id) ? app.Id : app.Name;
            if (string.IsNullOrWhiteSpace(key)) return;

            try
            {
                var dir = Path.GetDirectoryName(RecentFile);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var recent = File.Exists(RecentFile)
                    ? File.ReadAllLines(RecentFile).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()
                    : new List<string>();

                recent.RemoveAll(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrWhiteSpace(app.Name) && string.Equals(x, app.Name, StringComparison.OrdinalIgnoreCase)));
                recent.Insert(0, key);

                File.WriteAllLines(RecentFile, recent.Take(RecentAppsHistoryLimit));
            }
            catch { }
        }

        private void RecentApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppDefinition app)
            {
                _main.SelectedApp = app;
                AddRecentApp(app);
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