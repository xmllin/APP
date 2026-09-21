using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp1.Models;
using WpfApp1.Services;
using WpfApp1.Services.Downloads;
using WpfApp1.Services.Libraries;

namespace WpfApp1.Pages
{
    public partial class LibrariesPage : UserControl
    {
        private readonly LibraryCatalogService _catalog = new LibraryCatalogService();
        private readonly LibraryDetectionService _detection = new LibraryDetectionService();
        private readonly LibraryDownloadService _downloads = new LibraryDownloadService();
        private readonly LibraryInstallationService _installation = new LibraryInstallationService();
        private readonly ObservableCollection<LibraryItem> _items = new ObservableCollection<LibraryItem>();
        private ICollectionView _view;
        private CancellationTokenSource _installCts;
        private bool _loading;
        private Window _hostWindow;

        public LibrariesPage()
        {
            InitializeComponent();
            Loaded += LibrariesPage_Loaded;
            Unloaded += LibrariesPage_Unloaded;
        }

        private async void LibrariesPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _loading = true;
            try
            {
                var definitions = await _catalog.LoadAsync();
                foreach (var definition in definitions)
                {
                    var item = _detection.Detect(definition);
                    if (item == null) continue;

                    item.IsRecommended = IsRecommendedForSystem(definition);
                    _items.Add(item);
                }
                BuildFilters();
                _view = CollectionViewSource.GetDefaultView(_items);
                _view.Filter = FilterItem;
                LibrariesItems.ItemsSource = _view;
                UpdateSummary();
                _hostWindow = Window.GetWindow(this);
                if (_hostWindow != null) _hostWindow.Activated += HostWindow_Activated;
            }
            catch (Exception ex)
            {
                InstallStatusText.Text = "Не удалось загрузить каталог: " + ex.Message;
            }
        }

        private void LibrariesPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_hostWindow != null) _hostWindow.Activated -= HostWindow_Activated;
            _hostWindow = null;
        }

        private void HostWindow_Activated(object sender, EventArgs e)
        {
            if (_loading && _items.Count > 0 && _installCts == null)
                RefreshDetectedStatuses();
        }

        private void RefreshDetectedStatuses()
        {
            foreach (var item in _items)
            {
                if (item.IsWindowsFeature)
                    continue;

                var detected = _detection.Detect(item.Definition);
                var wasInstalled = item.Status == LibraryInstallStatus.Installed;
                item.Status = detected.Status;
                item.InstalledVersion = detected.InstalledVersion;
                if (!wasInstalled && item.Status == LibraryInstallStatus.Installed)
                    DeleteDownloadedInstaller(item.Definition);
                item.Refresh();
            }
            if (_view != null) _view.Refresh();
            UpdateSummary();
        }

        private void BuildFilters()
        {
            CategoryComboBox.ItemsSource = new[] { "Все категории" }.Concat(_items.Select(x => x.Definition.Category).Distinct(StringComparer.OrdinalIgnoreCase)).ToList();
            CategoryComboBox.SelectedIndex = 0;
            StatusComboBox.ItemsSource = new[] { "Все статусы", "Установлено", "Не установлено", "Рекомендуемые" };
            StatusComboBox.SelectedIndex = 0;
        }

        private bool FilterItem(object value)
        {
            var item = value as LibraryItem;
            if (item == null) return false;
            var category = CategoryComboBox?.SelectedItem as string;
            var status = StatusComboBox?.SelectedItem as string;
            var matchesCategory = string.IsNullOrWhiteSpace(category) || category == "Все категории" || string.Equals(item.Definition.Category, category, StringComparison.OrdinalIgnoreCase);
            var matchesRecommended = status != "Рекомендуемые" || item.IsRecommended;
            var matchesStatusValue = status == "Рекомендуемые" || string.IsNullOrWhiteSpace(status) || status == "Все статусы" || string.Equals(item.StatusText, status, StringComparison.OrdinalIgnoreCase);
            return matchesCategory && matchesStatusValue && matchesRecommended;
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            if (_view != null) _view.Refresh();
            EmptyText.Visibility = _view != null && _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateSummary()
        {
            QueueText.Text = "Выбрано: " + _items.Count(x => x.IsSelected);
        }

        private void SelectMissing_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items.Where(x => x.IsMissing)) item.IsSelected = true;
            UpdateSummary();
        }

        private void ClearQueue_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _items) item.IsSelected = false;
            UpdateSummary();
        }

        private void LibraryCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                if (source is Button) return;
                source = source is Visual
                    ? VisualTreeHelper.GetParent(source)
                    : (source as FrameworkContentElement)?.Parent;
            }

            var item = (sender as Border)?.DataContext as LibraryItem;
            if (item == null) return;

            item.IsSelected = !item.IsSelected;
            item.Refresh();
            UpdateSummary();
        }

        private void OpenInstallerFolder_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuItem)?.Tag as LibraryItem;
            if (item == null) return;
            var folder = item.HasDownloadedFile
                ? Path.GetDirectoryName(item.DownloadedFilePath)
                : Path.Combine(Path.GetTempPath(), "WpfApp1", "Libraries");
            Directory.CreateDirectory(folder);
            OpenFolder(folder);
        }

        private void OpenLibraryFolder_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as MenuItem)?.Tag as LibraryItem;
            if (item == null) return;

            var folder = _installation.FindInstallLocation(item.Definition);
            if (string.IsNullOrWhiteSpace(folder) && item.HasDownloadedFile)
                folder = Path.GetDirectoryName(item.DownloadedFilePath);
            OpenFolder(folder);
        }

        private void MoreActions_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.ContextMenu == null) return;
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.Tag as LibraryItem;
            if (item == null) return;
            var details = item.Definition.Purpose + "\n\nВерсия: " + (item.Definition.Version ?? item.Definition.VersionRule ?? "Актуальная") +
                "\nАрхитектуры: " + item.Definition.ArchitectureText + "\nИсточник: " + item.Definition.SourceUrl +
                "\nТребования: " + string.Join("; ", item.Definition.Requirements ?? new List<string>()) +
                "\nНужно для: " + string.Join("; ", item.Definition.UsedBy ?? new List<string>());
            if (item.Definition.Warnings != null && item.Definition.Warnings.Count > 0) details += "\n\nПредупреждение: " + string.Join("; ", item.Definition.Warnings);
            AppDialog.ShowInfo(Window.GetWindow(this), item.Definition.Name, details);
        }

        private async void Repair_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.Tag as LibraryItem;
            if (item == null) return;
            try
            {
                item.IsBusy = true;
                item.Notify(nameof(item.IsBusy));
                item.Notify(nameof(item.ShowProgress));
                InstallStatusText.Text = "Восстановление: " + item.Definition.Name;
                if (item.IsWindowsFeature)
                    await _installation.InstallWindowsFeatureAsync(item.Definition, CancellationToken.None);
                else
                    await _installation.RepairAsync(item.Definition, CancellationToken.None);
                item.IsBusy = false;
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = "Восстановление завершено: " + item.Definition.Name;
            }
            catch (OperationCanceledException)
            {
                item.IsBusy = false;
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = "Восстановление отменено: " + item.Definition.Name;
            }
            catch (Exception ex)
            {
                item.IsBusy = false;
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = "Ошибка восстановления: " + ex.Message;
                AppDialog.ShowInfo(Window.GetWindow(this), item.Definition.Name, ex.Message);
            }
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.Tag as LibraryItem;
            if (item == null) return;
            await DownloadLibraryAsync(item, installAfterDownload: false);
        }

        private async void DownloadAndInstall_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button)?.Tag as LibraryItem;
            if (item == null) return;
            await DownloadLibraryAsync(item, installAfterDownload: true);
        }

        private async void WindowsFeatureToggle_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var toggle = sender as ToggleButton;
            var item = toggle?.Tag as LibraryItem;
            if (item == null || !item.IsWindowsFeature || item.IsBusy) return;

            var enabled = toggle.IsChecked == true;
            var action = enabled ? "Включить" : "Отключить";
            if (!AppDialog.ShowConfirm(Window.GetWindow(this), action + " компонент", action + " компонент \"" + item.Definition.Name + "\" в Windows?"))
            {
                toggle.IsChecked = !enabled;
                return;
            }

            try
            {
                item.IsBusy = true;
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = action + ": " + item.Definition.Name;
                if (enabled)
                    await _installation.InstallWindowsFeatureAsync(item.Definition, CancellationToken.None);
                else
                    await _installation.UninstallWindowsFeatureAsync(item.Definition, CancellationToken.None);

                item.Status = enabled ? LibraryInstallStatus.Installed : LibraryInstallStatus.Missing;
                item.IsBusy = false;
                item.Refresh();
                InstallStatusText.Text = action + " завершено: " + item.Definition.Name;
            }
            catch (Exception ex)
            {
                item.IsBusy = false;
                item.Notify(nameof(item.IsBusy));
                toggle.IsChecked = !enabled;
                InstallStatusText.Text = "Ошибка: " + ex.Message;
                AppDialog.ShowInfo(Window.GetWindow(this), item.Definition.Name, ex.Message);
            }
        }

        private async void Delete_Click (object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            var item = (sender as Button)?.Tag as LibraryItem;
            if (item == null || item.Definition == null || item.IsDeleting) return;

            var actionText = item.IsWindowsFeature
                ? $"Отключить компонент \"{item.Definition.Name}\" в Windows?"
                : $"Удалить компонент \"{item.Definition.Name}\" из системы?\nЭто удалит установленные файлы и запись из реестра Windows.";
            if (!AppDialog.ShowConfirm(Window.GetWindow(this), item.IsWindowsFeature ? "Отключение компонента" : "Удаление компонента", actionText)) return;

            try
            {
                item.IsDeleting = true;
                item.IsBusy = true;
                item.Notify(nameof(item.IsDeleting));
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = "Удаление: " + item.Definition.Name;

                if (item.IsWindowsFeature)
                    await _installation.UninstallWindowsFeatureAsync(item.Definition, CancellationToken.None);
                else
                    await _installation.UninstallAsync(item.Definition, CancellationToken.None);

                var detected = _detection.Detect(item.Definition);
                item.Status = detected.Status;
                item.InstalledVersion = detected.InstalledVersion;
                item.IsSelected = false;
                item.IsBusy = false;
                item.IsDeleting = false;
                item.Notify(nameof(item.Status));
                item.Notify(nameof(item.InstalledVersion));
                item.Notify(nameof(item.IsSelected));
                item.Notify(nameof(item.IsDeleting));
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = "Удаление завершено: " + item.Definition.Name;
            }
            catch (OperationCanceledException)
            {
                item.IsBusy = false;
                item.IsDeleting = false;
                item.Notify(nameof(item.IsDeleting));
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = "Удаление отменено: " + item.Definition.Name;
            }
            catch (Exception ex)
            {
                item.IsBusy = false;
                item.IsDeleting = false;
                item.Notify(nameof(item.IsDeleting));
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = "Ошибка удаления: " + ex.Message;
                AppDialog.ShowInfo(Window.GetWindow(this), item.Definition.Name, ex.Message);
            }
        }

        private async void InstallSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _items.Where(x => x.IsSelected && x.Status != LibraryInstallStatus.Manual).ToList();
            if (selected.Count == 0)
            {
                AppDialog.ShowInfo(Window.GetWindow(this), "Библиотеки", "Выберите устанавливаемые компоненты. Компоненты с ручной установкой можно открыть через кнопку «Описание».");
                return;
            }
            if (!AppDialog.ShowConfirm(Window.GetWindow(this), "Подтверждение", "Установить выбранные компоненты? Установщики будут запущены с правами администратора.")) return;
            await InstallItemsAsync(selected);
        }

        private bool IsRecommendedForSystem(LibraryDefinition definition)
        {
            if (definition == null) return false;

            var architecture = PlatformDetectionService.Current.Architecture ?? "x64";
            var matchesArchitecture = definition.Architectures.Any(a => string.Equals(a, architecture, StringComparison.OrdinalIgnoreCase));

            if (definition.Category == "Visual C++ Redistributable") return matchesArchitecture;
            return false;
        }

        private async Task InstallItemsAsync(IEnumerable<LibraryItem> source)
        {
            if (_installCts != null) return;
            _installCts = new CancellationTokenSource();
            var items = source.ToList();
            var completed = 0;
            try
            {
                foreach (var item in items)
                {
                    _installCts.Token.ThrowIfCancellationRequested();
                    if (item.IsWindowsFeature)
                    {
                        InstallStatusText.Text = "Включение компонента Windows: " + item.Definition.Name;
                        await _installation.InstallWindowsFeatureAsync(item.Definition, _installCts.Token);
                        var featureDetected = _detection.Detect(item.Definition);
                        item.Status = featureDetected.Status;
                        item.InstalledVersion = featureDetected.InstalledVersion;
                        item.IsSelected = false;
                        item.Refresh();
                        completed++;
                        continue;
                    }
                    item.IsBusy = true; item.ShowProgress = true; item.ProgressOpacity = 1; item.Progress = 0; item.ProgressText = "Подготовка…";
                    item.Notify(nameof(item.IsBusy)); item.Notify(nameof(item.ShowProgress)); item.Notify(nameof(item.ProgressOpacity)); item.Notify(nameof(item.Progress)); item.Notify(nameof(item.ProgressText)); item.Refresh();
                    InstallStatusText.Text = "Подготовка: " + item.Definition.Name + " (" + completed + "/" + items.Count + ")";
                    var progress = new Progress<DownloadProgress>(p =>
                    {
                        item.Progress = p.Progress < 0 ? 0 : p.Progress;
                        item.ProgressText = FormatProgress(p);
                        item.Notify(nameof(item.Progress)); item.Notify(nameof(item.ProgressText));
                    });
                    var path = await _downloads.DownloadAsync(item.Definition, progress, _installCts.Token);
                    InstallStatusText.Text = "Установка: " + item.Definition.Name;
                    await _installation.InstallAsync(item.Definition, path, _installCts.Token);
                    var detected = _detection.Detect(item.Definition);
                    item.Status = detected.Status;
                    item.InstalledVersion = detected.InstalledVersion;
                    item.IsSelected = false; item.IsBusy = false; item.Progress = item.Status == LibraryInstallStatus.Installed ? 100 : 0;
                    item.ProgressText = item.Status == LibraryInstallStatus.Installed ? "Загружено и установлено" : "Установщик завершён";
                    if (item.Status == LibraryInstallStatus.Installed)
                    {
                        DeleteDownloadedInstaller(item.Definition);
                        item.DownloadedFilePath = null;
                        item.Notify(nameof(item.DownloadedFilePath));
                        item.Notify(nameof(item.HasDownloadedFile));
                    }
                    item.Notify(nameof(item.IsBusy)); item.Notify(nameof(item.ProgressText)); item.Refresh();
                    _ = HideProgressAfterDelayAsync(item);
                    completed++;
                }
                InstallStatusText.Text = "Готово: установлено " + completed + " из " + items.Count + ".";
            }
            catch (OperationCanceledException)
            {
                InstallStatusText.Text = "Установка отменена.";
            }
            catch (Exception ex)
            {
                InstallStatusText.Text = "Ошибка установки: " + ex.Message;
            }
            finally
            {
                foreach (var item in items) { item.IsBusy = false; item.Refresh(); }
                _installCts.Dispose(); _installCts = null; UpdateSummary();
            }
        }

        private void CancelInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_installCts != null) _installCts.Cancel();
        }

        private async Task DownloadLibraryAsync(LibraryItem item, bool installAfterDownload)
        {
            if (item == null || item.Definition == null) return;

            if (string.IsNullOrWhiteSpace(item.Definition.DownloadUrl))
            {
                if (item.IsWindowsFeature)
                {
                    await InstallWindowsFeatureAsync(item);
                    return;
                }
                OpenSource(item);
                return;
            }

            try
            {
                var progress = new Progress<DownloadProgress>(p =>
                {
                    item.Progress = p.Progress < 0 ? 0 : p.Progress;
                    item.ProgressText = FormatProgress(p);
                    item.Notify(nameof(item.Progress));
                    item.Notify(nameof(item.ProgressText));
                });

                item.IsBusy = true;
                item.ShowProgress = true;
                item.ProgressOpacity = 1;
                item.Progress = 0;
                item.ProgressText = "Подготовка…";
                item.Notify(nameof(item.IsBusy));
                item.Notify(nameof(item.ShowProgress));
                item.Notify(nameof(item.ProgressOpacity));
                item.Notify(nameof(item.Progress));
                item.Notify(nameof(item.ProgressText));
                InstallStatusText.Text = installAfterDownload ? "Скачивание и запуск установщика: " + item.Definition.Name : "Скачивание: " + item.Definition.Name;

                var path = await _downloads.DownloadAsync(item.Definition, progress, CancellationToken.None);
                item.DownloadedFilePath = path;
                item.Progress = 100;
                item.ProgressText = "Загружено: " + FormatBytes(new FileInfo(path).Length);
                item.Notify(nameof(item.Progress));
                item.Notify(nameof(item.ProgressText));
                item.Notify(nameof(item.DownloadedFilePath));
                item.Notify(nameof(item.HasDownloadedFile));
                item.Notify(nameof(item.ShowProgress));

                InstallStatusText.Text = installAfterDownload ? "Установка: " + item.Definition.Name : "Загружено: " + path;

                if (installAfterDownload)
                {
                    await _installation.InstallAsync(item.Definition, path, CancellationToken.None);
                    var detected = _detection.Detect(item.Definition);
                    item.Status = detected.Status;
                    item.InstalledVersion = detected.InstalledVersion;
                    item.IsSelected = false;
                    item.IsBusy = false;
                    item.Progress = item.Status == LibraryInstallStatus.Installed ? 100 : 0;
                    if (item.Status == LibraryInstallStatus.Installed)
                    {
                        DeleteDownloadedInstaller(item.Definition);
                        item.DownloadedFilePath = null;
                        item.Notify(nameof(item.DownloadedFilePath));
                        item.Notify(nameof(item.HasDownloadedFile));
                    }
                    item.Notify(nameof(item.IsBusy));
                    item.Notify(nameof(item.ShowProgress));
                    item.Refresh();
                    UpdateSummary();
                    InstallStatusText.Text = item.Status == LibraryInstallStatus.Installed
                        ? "Установлено: " + item.Definition.Name
                        : "Установщик завершён, но компонент не обнаружен: " + item.Definition.Name;
                }
                else
                {
                    item.IsBusy = false;
                    item.Notify(nameof(item.IsBusy));
                    item.Notify(nameof(item.ShowProgress));
                }
                await HideProgressAfterDelayAsync(item);
            }
            catch (Exception ex)
            {
                item.IsBusy = false;
                item.ShowProgress = false;
                item.Notify(nameof(item.IsBusy));
                item.Notify(nameof(item.ShowProgress));
                InstallStatusText.Text = "Ошибка загрузки: " + ex.Message;
                AppDialog.ShowInfo(Window.GetWindow(this), item.Definition.Name, ex.Message);
            }
        }

        private async Task InstallWindowsFeatureAsync(LibraryItem item)
        {
            try
            {
                item.IsBusy = true;
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = "Включение компонента Windows: " + item.Definition.Name;
                await _installation.InstallWindowsFeatureAsync(item.Definition, CancellationToken.None);
                var detected = _detection.Detect(item.Definition);
                item.Status = detected.Status;
                item.InstalledVersion = detected.InstalledVersion;
                item.IsBusy = false;
                item.Notify(nameof(item.IsBusy));
                item.Refresh();
                InstallStatusText.Text = "Компонент Windows включён: " + item.Definition.Name;
            }
            catch (Exception ex)
            {
                item.IsBusy = false;
                item.Notify(nameof(item.IsBusy));
                InstallStatusText.Text = "Ошибка включения компонента: " + ex.Message;
                AppDialog.ShowInfo(Window.GetWindow(this), item.Definition.Name, ex.Message);
            }
        }

        private async Task HideProgressAfterDelayAsync(LibraryItem item)
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            if (item.IsBusy) return;
            for (var opacity = 1d; opacity >= 0; opacity -= 0.1d)
            {
                item.ProgressOpacity = Math.Max(0, opacity);
                item.Notify(nameof(item.ProgressOpacity));
                await Task.Delay(40);
            }
            item.ShowProgress = false;
            item.ProgressOpacity = 1;
            item.Notify(nameof(item.ShowProgress));
            item.Notify(nameof(item.ProgressOpacity));
        }

        private static string FormatProgress(DownloadProgress progress)
        {
            var received = FormatBytes(progress.BytesReceived);
            return progress.TotalBytes.HasValue
                ? "Загружено: " + received + " / " + FormatBytes(progress.TotalBytes.Value)
                : "Загружено: " + received;
        }

        private static string FormatBytes(long value)
        {
            if (value < 1024) return value + " Б";
            if (value < 1024 * 1024) return (value / 1024d).ToString("0.0") + " КБ";
            if (value < 1024L * 1024 * 1024) return (value / (1024d * 1024)).ToString("0.0") + " МБ";
            return (value / (1024d * 1024 * 1024)).ToString("0.0") + " ГБ";
        }

        private static void OpenFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private static void DeleteDownloadedInstaller(LibraryDefinition definition)
        {
            if (definition == null) return;
            var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WpfApp1", "Libraries");
            var candidates = new[]
            {
                definition.FileName,
                definition.Id + ".exe",
                definition.Id + ".msi",
                System.IO.Path.GetFileName(definition.DownloadUrl ?? string.Empty)
            };

            foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var path = System.IO.Path.Combine(folder, candidate);
                if (System.IO.File.Exists(path))
                    try { System.IO.File.Delete(path); } catch { }
            }
        }

        private static void OpenSource(LibraryItem item)
        {
            if (item?.Definition == null || string.IsNullOrWhiteSpace(item.Definition.SourceUrl)) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = item.Definition.SourceUrl, UseShellExecute = true }); }
            catch { }
        }

    }
}
