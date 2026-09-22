using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using Nexora.Models;
using Nexora.Services;

namespace Nexora.Pages
{
    public partial class UwpAppsPage : UserControl
    {
        private readonly UwpPackageService _packageService = new UwpPackageService();
        private readonly ObservableCollection<UwpPackageInfo> _allPackages = new ObservableCollection<UwpPackageInfo>();
        private CancellationTokenSource _loadCancellation;
        private bool _hasLoaded;
        private int _displayedProgress;
        private static readonly Tuple<string, string>[] SupportedApplications =
        {
            Tuple.Create("Windows Mixed Reality", "Microsoft.MixedReality.Portal"),
            Tuple.Create("Cortana", "Microsoft.549981C3F5F10"),
            Tuple.Create("Skype", "Microsoft.SkypeApp"),
            Tuple.Create("Dev Home", "Microsoft.Windows.devhome"),
            Tuple.Create("3D Builder, Paint 3D", "Microsoft.MSPaint"),
            Tuple.Create("Техническая поддержка", "Microsoft.GetHelp"),
            Tuple.Create("Центр отзывов", "Microsoft.WindowsFeedbackHub"),
            Tuple.Create("Карты", "Microsoft.WindowsMaps"),
            Tuple.Create("OneNote", "Microsoft.Office.OneNote"),
            Tuple.Create("Сообщения", "Microsoft.Messaging"),
            Tuple.Create("Power Automate", "Microsoft.PowerAutomateDesktop"),
            Tuple.Create("People", "Microsoft.People"),
            Tuple.Create("Get Started / Tips", "Microsoft.Getstarted"),
            Tuple.Create("Люди, Почта, Календарь", "microsoft.windowscommunicationsapps"),
            Tuple.Create("Solitaire Collection", "Microsoft.MicrosoftSolitaireCollection"),
            Tuple.Create("Clipchamp", "Clipchamp.Clipchamp"),
            Tuple.Create("Записки", "Microsoft.MicrosoftStickyNotes"),
            Tuple.Create("Новости", "Microsoft.BingNews"),
            Tuple.Create("Погода", "Microsoft.BingWeather"),
            Tuple.Create("Звукозапись", "Microsoft.WindowsSoundRecorder"),
            Tuple.Create("Будильники и часы", "Microsoft.WindowsAlarms"),
            Tuple.Create("Outlook", "Microsoft.OutlookForWindows"),
            Tuple.Create("Ваш телефон", "Microsoft.YourPhone"),
            Tuple.Create("Камера", "Microsoft.WindowsCamera"),
            Tuple.Create("Медиаплеер", "Microsoft.ZuneMusic"),
            Tuple.Create("Фильмы и ТВ", "Microsoft.ZuneVideo"),
            Tuple.Create("Microsoft Store", "Microsoft.WindowsStore"),
            Tuple.Create("Xbox", "Microsoft.GamingApp")
        };

        public ObservableCollection<UwpPackageInfo> VisiblePackages { get; } = new ObservableCollection<UwpPackageInfo>();
        public ObservableCollection<UwpPackageInfo> FirstColumn { get; } = new ObservableCollection<UwpPackageInfo>();
        public ObservableCollection<UwpPackageInfo> SecondColumn { get; } = new ObservableCollection<UwpPackageInfo>();
        public ObservableCollection<UwpPackageInfo> ThirdColumn { get; } = new ObservableCollection<UwpPackageInfo>();

        public UwpAppsPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += UwpAppsPage_Loaded;
            Unloaded += UwpAppsPage_Unloaded;
        }

        private async void UwpAppsPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_hasLoaded)
                await LoadPackagesAsync();
        }

        private void UwpAppsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _loadCancellation?.Cancel();
        }

        private async void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = _allPackages.Where(item => item.IsSelected && item.IsInstalled).ToList();
            if (selected.Count == 0)
            {
                AppDialog.ShowInfo(Window.GetWindow(this), "Ничего не выбрано", "Выберите установленные приложения переключателями.");
                return;
            }

            if (!AppDialog.ShowConfirm(Window.GetWindow(this), "Удалить приложения?",
                "Будут удалены: " + string.Join(", ", selected.Select(item => item.DisplayName)) + "."))
                return;

            SetBusy(true, "Удаление выбранных приложений…");
            try
            {
                await _packageService.RemovePackagesAsync(selected, CancellationToken.None);
                await LoadPackagesAsync(true);
                AppDialog.ShowInfo(Window.GetWindow(this), "Удаление завершено", "Список приложений повторно проверен после удаления.");
            }
            catch (Exception exception)
            {
                AppDialog.ShowError(Window.GetWindow(this), "Не удалось удалить приложения",
                    "Возможно, Windows запросила права администратора или пакет используется системой.\n\n" + exception.Message);
            }
            finally
            {
                SetBusy(false, string.Empty);
            }
        }

        private async Task LoadPackagesAsync(bool forceRefresh = false)
        {
            if (_hasLoaded && !forceRefresh)
                return;

            _loadCancellation?.Cancel();
            _loadCancellation = new CancellationTokenSource();
            SetBusy(true, "Загрузка списка приложений…");
            var loadCancellation = _loadCancellation;
            var progress = new Progress<int>(value => SetProgress(value));
            try
            {
                var packages = await _packageService.GetInstalledPackagesAsync(loadCancellation.Token, progress);
                _allPackages.Clear();
                foreach (var application in SupportedApplications.OrderBy(item => item.Item1, StringComparer.CurrentCultureIgnoreCase))
                {
                    var package = packages.FirstOrDefault(item =>
                        item.Name.Equals(application.Item2, StringComparison.OrdinalIgnoreCase) ||
                        item.Name.StartsWith(application.Item2 + ".", StringComparison.OrdinalIgnoreCase));
                    _allPackages.Add(new UwpPackageInfo
                    {
                        Name = package?.Name ?? application.Item2,
                        DisplayName = application.Item1,
                        PackageFullName = package?.PackageFullName ?? string.Empty,
                        Version = package?.Version ?? string.Empty,
                        Publisher = package?.Publisher ?? string.Empty,
                        InstallLocation = package?.InstallLocation ?? string.Empty,
                        InstalledForCurrentUser = package?.InstalledForCurrentUser == true,
                        InstalledForAllUsers = package?.InstalledForAllUsers == true,
                        IsProvisioned = package?.IsProvisioned == true,
                        IsInstalled = package?.IsInstalled == true
                    });
                }
                RefreshColumns();
                _hasLoaded = true;
                SetProgress(100);
                StatusText.Text = string.Empty;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                StatusText.Text = "Не удалось получить список: " + exception.Message;
                PackagesList.Visibility = Visibility.Collapsed;
            }
            finally
            {
                if (ReferenceEquals(_loadCancellation, loadCancellation))
                    SetBusy(false, string.Empty);
            }
        }

        private void RefreshColumns()
        {
            VisiblePackages.Clear();
            FirstColumn.Clear();
            SecondColumn.Clear();
            ThirdColumn.Clear();
            foreach (var package in _allPackages) VisiblePackages.Add(package);
            var firstCount = 13;
            var secondCount = 13;
            foreach (var package in _allPackages.Take(firstCount)) FirstColumn.Add(package);
            foreach (var package in _allPackages.Skip(firstCount).Take(secondCount)) SecondColumn.Add(package);
            foreach (var package in _allPackages.Skip(firstCount + secondCount)) ThirdColumn.Add(package);

            PackagesList.Visibility = VisiblePackages.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            if (VisiblePackages.Count > 0) StatusText.Text = string.Empty;
        }

        private void SetBusy(bool busy, string status)
        {
            RemoveButton.IsEnabled = !busy;
            ProgressPanel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            LoadingProgressBar.IsIndeterminate = busy;
            if (busy)
            {
                LoadingProgressBar.Value = 0;
                _displayedProgress = 0;
            }
            if (!string.IsNullOrWhiteSpace(status)) StatusText.Text = status;
        }

        private void SetProgress(int value)
        {
            var target = Math.Max(0, Math.Min(100, value));
            LoadingProgressBar.IsIndeterminate = false;
            target = Math.Max(target, _displayedProgress);
            _displayedProgress = target;
            var current = LoadingProgressBar.Value;
            LoadingProgressBar.Value = target;
            LoadingProgressBar.BeginAnimation(ProgressBar.ValueProperty, new DoubleAnimation
            {
                From = current,
                To = target,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            }, HandoffBehavior.SnapshotAndReplace);
            StatusText.Text = "Загрузка списка приложений… " + target + "%";
        }
    }
}
