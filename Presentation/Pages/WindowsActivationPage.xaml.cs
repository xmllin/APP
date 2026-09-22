#nullable enable

using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Nexora.Models;
using Nexora.Services;
using Nexora.Services.Downloads;

namespace Nexora.Pages
{
    public partial class WindowsActivationPage : UserControl
    {
        public ObservableCollection<MakuOsBuild> MakuOsBuilds { get; } = new ObservableCollection<MakuOsBuild>
        {
            new MakuOsBuild("MakuOS 11 Pro", "Windows 11 24H2 · Pro", "MakuOS 11 Pro|Win11_Pro.iso|https://www.mediafire.com/file/6t86h95mx7giqk9/MakuOS+11+Pro+24H2.iso/file|https://pixeldrain.com/u/JrXsz7kN|https://mega.nz/file/fI8FEYZK#EdEisvTe4wfxpLIyqejInVvQ1XKdwo7QmhA32d9ZoI8", "https://adderly.top/makuos_11"),
            new MakuOsBuild("MakuOS 10 Pro", "Windows 10 22H2 · Pro", "MakuOS 10 Pro|Win10_Pro.iso|https://www.mediafire.com/file/rtd4fjqk3gy2e76/MakuOS+10+Pro+22H2.iso/file|https://pixeldrain.com/u/Etnqpk1D|https://mega.nz/file/3A02VYob#dR0kYrik5JRyGO8SMi7Rs_cjg6m8yLnUz_DZ3dsXdMQ", "https://adderly.top/makuos_10"),
            new MakuOsBuild("MakuOS 11 Lite", "Windows 11 22H2 · Lite", "MakuOS 11 Lite|Win11_Lite.iso|https://www.mediafire.com/file/deo0rghoe0xtu3w/MakuOS+11+22H2+Lite+V2.iso/file|https://pixeldrain.com/u/6aCam7Zz|https://drive.google.com/file/d/1HgPtqVOt9_-FcTQpCs74bhMumJtW4tKc/view?usp=sharing", "https://adderly.top/makuos_11_Lite"),
            new MakuOsBuild("MakuOS 10 Mini", "Windows 10 22H2 · Mini", "MakuOS 10 Mini|Win10_Mini.iso|https://www.mediafire.com/file/e6kisqlr4jewmuy/MakuOS+10+Mini+22H2+V2.iso/file|https://pixeldrain.com/u/1dLSSry6|https://mega.nz/file/ak4BjSyZ#22pFRyEVzjzqgCiZIYSH6-6RE2_TWqBZ6Jq9jpGlPkE", "https://adderly.top/makuos_mini"),
            new MakuOsBuild("MakuOS 10 Lite", "Windows 10 1809 · Lite", "MakuOS 10 Lite|Win10_Lite.iso|https://pixeldrain.com/u/KHVPVhyh|https://drive.google.com/file/d/1BffklbcGBZ4u-u-X8l8kzArzguIA4AYi/view?usp=sharing", "https://adderly.top/makuos_10_Lite"),
            new MakuOsBuild("MakuOS 11 LTSC", "Windows 11 24H2 · LTSC", "MakuOS 11 LTSC|Win11_LTSC.iso|https://www.mediafire.com/file/csfkh2un0f4mnxh/MakuOS+11+LTSC+24H2+V2.iso/file|https://pixeldrain.com/u/TL34HRpL|https://mega.nz/file/NA8nHYLB#_wdbQ0DC8xeCIQAC_1mzEXI5b2oWvFXnIXRL2ZiV3Co", "https://adderly.top/makuos_11_ltsc"),
            new MakuOsBuild("MakuOS 10 LTSC", "Windows 10 21H2 · LTSC", "MakuOS 10 LTSC|Win10_LTSC.iso|https://www.mediafire.com/file/2eopkqeefuysa74/MakuOS+10+LTSC+V2.iso/file|https://pixeldrain.com/u/yBuCjqNx|https://mega.nz/file/McMUADLY#RPUk5-y4Lie0HuWNoYXCxYEmpDc-qEkhd4OJoE1_e48", "https://adderly.top/makuos_10_ltsc"),
            new MakuOsBuild("MakuOS 8.1", "Windows 8.1 · Pro", "MakuOS 8.1|Win8.1.iso|https://www.mediafire.com/file/nz2zcu8iq3y5jew/MakuOS+8.1.iso/file|https://pixeldrain.com/u/QuKAqNin", "https://adderly.top/makuos_8"),
            new MakuOsBuild("MakuOS 7", "Windows 7 · Pro", "MakuOS 7|Win7.iso|https://www.mediafire.com/file/sl3yrml4gt1v3u6/MakuOS+7.iso/file|https://pixeldrain.com/u/SHW1DnMn", "https://adderly.top/makuos_7")
        };

        public sealed class MakuOsBuild
        {
            public string Name { get; }
            public string Description { get; }
            public string DownloadTag { get; }
            public string FilesUrl { get; }

            public MakuOsBuild(string name, string description, string downloadTag, string filesUrl)
            {
                Name = name;
                Description = description;
                DownloadTag = downloadTag;
                FilesUrl = filesUrl;
            }
        }
        // Официальные страницы Microsoft.
        private const string Windows11DownloadPage =
            "https://www.microsoft.com/ru-ru/software-download/windows11";

        private const string Windows10DownloadPage =
            "https://www.microsoft.com/ru-ru/software-download/windows10ISO";

        private const string MicrosoftApiBase =
            "https://www.microsoft.com/ru-ru/api/controls/contentinclude/html";

        private static readonly string ActivationStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nexora",
            "windows-activation-state.txt");

        private readonly MasActivationService _activationService =
            new MasActivationService();

        private readonly WindowsIsoDownloadService _isoDownloadService =
            new WindowsIsoDownloadService();

        private CancellationTokenSource? _downloadCts;
        private PauseController? _downloadPauseController;
        private string? _activeIsoDownloadKey;
        private bool _activationInProgress;

        public WindowsActivationPage()
        {
            InitializeComponent();
            LoadSavedActivationState();

            Loaded += WindowsActivationPage_Loaded;

            _activationService.ProgressChanged +=
                OnActivationProgressChanged;
        }

        private async void WindowsActivationPage_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_activationInProgress)
            {
                ActivationStatusText.Text = "Выполняется активация. Это может занять до минуты";
                ActivationStatusText.Foreground =
                    new SolidColorBrush(Color.FromRgb(255, 211, 78));
                ActivationProgressBar.Visibility = Visibility.Visible;
                ActivationProgressBar.IsIndeterminate = true;
                ActivateWindowsButton.IsEnabled = false;
                return;
            }

            // Не перечитываем статус при каждом возвращении на вкладку,
            // чтобы текущий текст и результат операции не сбрасывались.
        }

        // ============================================================
        // АКТИВАЦИЯ
        // ============================================================

        private void LoadSavedActivationState()
        {
            try
            {
                if (!File.Exists(ActivationStatePath))
                    return;

                string savedState =
                    File.ReadAllText(ActivationStatePath).Trim();

                bool isActivated = savedState == "1";

                ActivationStatusText.Text = isActivated
                    ? "Windows активирована"
                    : "Windows не активирована";

                ActivationStatusText.Foreground =
                    new SolidColorBrush(
                        isActivated
                            ? Color.FromRgb(50, 205, 50)
                            : Color.FromRgb(255, 211, 78));
            }
            catch
            {
                // Кэш статуса не должен мешать реальной проверке.
            }
        }

        private static void SaveActivationState(bool isActivated)
        {
            try
            {
                string? directory =
                    Path.GetDirectoryName(ActivationStatePath);

                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(
                    ActivationStatePath,
                    isActivated ? "1" : "0");
            }
            catch
            {
                // Ничего страшного — статус будет проверен снова.
            }
        }

        private void OnActivationProgressChanged(
            object? sender,
            ActivationProgressEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.Stage)
                {
                    case ActivationStage.Started:
                    case ActivationStage.Extracting:
                    case ActivationStage.RunningScript:

                        ActivationStatusText.Text = "Выполняется активация. Это может занять до минуты";
                        ActivationStatusText.Foreground =
                            new SolidColorBrush(
                                Color.FromRgb(255, 211, 78));

                        ActivationProgressBar.Visibility =
                            Visibility.Visible;

                        ActivationProgressBar.IsIndeterminate = true;

                        ActivateWindowsButton.IsEnabled = false;

                        break;

                    case ActivationStage.Completed:

                        _activationInProgress = false;
                        ActivationStatusText.Text = "Windows активирована";
                        ActivationStatusText.Foreground =
                            new SolidColorBrush(
                                Color.FromRgb(50, 205, 50));

                        HideProgress();

                        break;

                    case ActivationStage.Failed:

                        _activationInProgress = false;
                        ActivationStatusText.Text =
                            "Windows не активирована";

                        ActivationStatusText.Foreground =
                            new SolidColorBrush(
                                Color.FromRgb(255, 99, 71));

                        HideProgress();

                        break;
                }
            });
        }

        private void HideProgress()
        {
            ActivationProgressBar.IsIndeterminate = false;
            ActivationProgressBar.Visibility =
                Visibility.Collapsed;

            ActivateWindowsButton.IsEnabled = true;
        }

        private async Task RefreshActivationStatusAsync()
        {
            try
            {
                var status =
                    await _activationService.GetStatusAsync(
                        CancellationToken.None);

                SaveActivationState(status.IsActivated);

                ActivationStatusText.Text =
                    status.IsActivated
                        ? "Windows активирована"
                        : "Windows не активирована";

                ActivationStatusText.Foreground =
                    new SolidColorBrush(
                        status.IsActivated
                            ? Color.FromRgb(50, 205, 50)
                            : Color.FromRgb(255, 211, 78));
            }
            catch
            {
                ActivationStatusText.Text =
                    "Не удалось проверить активацию";

                ActivationStatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(255, 99, 71));
            }
        }

        private async void ActivateWindowsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                _activationInProgress = true;
                ActivateWindowsButton.IsEnabled = false;
                ActivationStatusText.Text = "Выполняется активация. Это может занять до минуты";
                ActivationStatusText.Foreground =
                    new SolidColorBrush(Color.FromRgb(255, 211, 78));
                ActivationProgressBar.Visibility = Visibility.Visible;
                ActivationProgressBar.IsIndeterminate = true;

                await _activationService.ActivateAsync(
                    CancellationToken.None);

                SaveActivationState(true);

                }
            catch (Exception exception)
            {
                AppDialog.ShowError(
                    Window.GetWindow(this),
                    "Не удалось выполнить операцию",
                    exception.Message);
            }
            finally
            {
                _activationInProgress = false;
                ActivateWindowsButton.IsEnabled = true;
            }
        }

        // ============================================================
        // WINDOWS 11 ISO
        // ============================================================

        private async void DownloadWindows11Button_Click(
            object sender,
            RoutedEventArgs e)
        {
            await DownloadWindowsIsoAsync(
                windowsVersion: "Windows 11",
                pageUrl: Windows11DownloadPage,
                defaultFileName: "Win11_Russian_x64.iso",
                productSegment: "windows11",
                preferArchitecture: "IsoX64");
        }

        // ============================================================
        // WINDOWS 10 ISO
        // ============================================================

        private async void DownloadWindows10Button_Click(
            object sender,
            RoutedEventArgs e)
        {
            await DownloadWindowsIsoAsync(
                windowsVersion: "Windows 10",
                pageUrl: Windows10DownloadPage,
                defaultFileName: "Win10_Russian_x64.iso",
                productSegment: "windows10ISO",
                preferArchitecture: "IsoX64");
        }

        // ============================================================
        // ОСНОВНОЙ ПРОЦЕСС СКАЧИВАНИЯ
        // ============================================================

        private async Task DownloadWindowsIsoAsync(
            string windowsVersion,
            string pageUrl,
            string defaultFileName,
            string productSegment,
            string preferArchitecture)
        {
            if (_downloadCts != null)
            {
                AppDialog.ShowInfo(
                    Window.GetWindow(this),
                    "Загрузка",
                    "Загрузка ISO уже выполняется.");

                return;
            }

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Title = $"Сохранить ISO — {windowsVersion}",
                Filter = "ISO Image (*.iso)|*.iso",
                FileName = defaultFileName,
                AddExtension = true,
                OverwritePrompt = true
            };

            bool? result =
                saveDialog.ShowDialog(
                    Window.GetWindow(this));

            if (result != true)
                return;

            string destinationPath =
                saveDialog.FileName;

            _activeIsoDownloadKey = "windows-iso-" + Guid.NewGuid().ToString("N");
            var mainWindow = MainWindow.Current;
            if (mainWindow != null && !mainWindow.TryRegisterDownload(
                    windowsVersion + " ISO",
                    _activeIsoDownloadKey,
                    out _downloadCts,
                    out _downloadPauseController))
            {
                _activeIsoDownloadKey = null;
                return;
            }
            _downloadCts ??= new CancellationTokenSource();
            mainWindow?.ShowNotification(
                $"Загрузка «{windowsVersion} ISO» начата.",
                NotificationKind.Info,
                "download-start:" + _activeIsoDownloadKey);

            try
            {
                SetDownloadState(true);

                DownloadStatusText.Text =
                    $"Получение ссылки Microsoft для {windowsVersion}...";

                DownloadStatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(130, 165, 207));

                Progress<DownloadProgress> progress =
                    new Progress<DownloadProgress>(details =>
                    {
                        ActivationProgressBar.IsIndeterminate = false;
                        ActivationProgressBar.Minimum = 0;
                        ActivationProgressBar.Maximum = 100;
                        ActivationProgressBar.Value = details.Progress;
                        DownloadStatusText.Text =
                            $"Скачивание {windowsVersion}: " +
                            $"{details.Progress:0}%";

                        if (mainWindow != null && _activeIsoDownloadKey != null)
                            mainWindow.UpdateDownloadProgress(
                                _activeIsoDownloadKey,
                                details,
                                windowsVersion + " ISO");
                    });

                string? isoUrl =
                    await MicrosoftIsoLinkResolver
                        .GetIsoDownloadUrlAsync(
                            pageUrl,
                            productSegment,
                            preferArchitecture,
                            _downloadCts.Token);

                if (string.IsNullOrWhiteSpace(isoUrl))
                {
                    // Важный fallback:
                    // не оставляем пользователя с ошибкой,
                    // а открываем официальный поток Microsoft.
                    OpenOfficialDownloadPage(pageUrl);

                    AppDialog.ShowInfo(
                        Window.GetWindow(this),
                        "Не удалось получить прямую ссылку",
                        "Microsoft не вернула прямую ссылку на ISO. " +
                        "Официальная страница Microsoft открыта для скачивания образа.");

                    if (mainWindow != null && _activeIsoDownloadKey != null)
                        mainWindow.RemoveDownload(_activeIsoDownloadKey);

                    return;
                }

                DownloadStatusText.Text =
                    "Скачивание ISO с серверов Microsoft...";

                string finalPath =
                    await _isoDownloadService.DownloadIsoAsync(
                        isoUrl,
                        destinationPath,
                        progress,
                        _downloadCts.Token,
                        _downloadPauseController);

                ActivationProgressBar.Value = 100;
                DownloadStatusText.Text =
                    "ISO успешно скачан";

                DownloadStatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(50, 205, 50));

                if (mainWindow != null && _activeIsoDownloadKey != null)
                    mainWindow.CompleteDownload(
                        _activeIsoDownloadKey,
                        windowsVersion + " ISO");

                AppDialog.ShowInfo(
                    Window.GetWindow(this),
                    "Загрузка завершена",
                    $"ISO успешно сохранён:\n\n{finalPath}");
            }
            catch (OperationCanceledException)
            {
                DownloadStatusText.Text =
                    "Загрузка отменена";

                DownloadStatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(255, 211, 78));

                if (mainWindow != null && _activeIsoDownloadKey != null)
                    mainWindow.RemoveDownload(_activeIsoDownloadKey);
            }
            catch (HttpRequestException exception)
            {
                DownloadStatusText.Text =
                    "Ошибка загрузки";

                DownloadStatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(255, 99, 71));

                AppDialog.ShowError(
                    Window.GetWindow(this),
                    "Ошибка загрузки ISO",
                    $"Не удалось скачать образ Microsoft.\n\n{exception.Message}");

                if (mainWindow != null && _activeIsoDownloadKey != null)
                    mainWindow.RemoveDownload(_activeIsoDownloadKey);
            }
            catch (Exception exception)
            {
                DownloadStatusText.Text =
                    "Ошибка загрузки";

                DownloadStatusText.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(255, 99, 71));

                AppDialog.ShowError(
                    Window.GetWindow(this),
                    "Ошибка",
                    exception.Message);

                if (mainWindow != null && _activeIsoDownloadKey != null)
                    mainWindow.RemoveDownload(_activeIsoDownloadKey);
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
                _downloadPauseController = null;
                _activeIsoDownloadKey = null;

                SetDownloadState(false);

                ActivationProgressBar.IsIndeterminate =
                    false;

                if (DownloadStatusText.Text != "ISO успешно скачан")
                {
                    ActivationProgressBar.Visibility =
                        Visibility.Collapsed;
                    DownloadStatusText.Text = string.Empty;
                }
            }
        }

        private void SetDownloadState(bool downloading)
        {
            // Во время загрузки блокируем кнопки ISO,
            // чтобы случайно не запустить две загрузки одновременно.

            foreach (Button button in FindVisualChildren<Button>(this))
            {
                if (button == ActivateWindowsButton)
                    continue;

                if (button.Name == "DownloadWindows11Button" ||
                    button.Name == "DownloadWindows10Button")
                {
                    button.IsEnabled = !downloading;
                }
            }
        }

        // ============================================================
        // FALLBACK — ОФИЦИАЛЬНАЯ СТРАНИЦА MICROSOFT
        // ============================================================

        private static void OpenOfficialDownloadPage(string url)
        {
            try
            {
                Process.Start(
                    new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"Не удалось открыть страницу Microsoft:\n\n{exception.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenMakuOsBuildButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string url)
                OpenOfficialDownloadPage(url);
        }

        private async void DownloadMakuOsFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is string tag))
                return;

            var parts = tag.Split('|');
            if (parts.Length < 3)
                return;

            var name = parts[0];
            var fileName = parts[1];
            var sources = new System.Collections.Generic.List<string>();
            for (var i = 2; i < parts.Length; i++)
                if (!string.IsNullOrWhiteSpace(parts[i])) sources.Add(parts[i]);

            await DownloadMakuOsWithFallbackAsync(name, fileName, sources);
        }

        private async Task DownloadMakuOsWithFallbackAsync(string name, string fileName, System.Collections.Generic.IReadOnlyList<string> sources)
        {
            if (_downloadCts != null)
            {
                AppDialog.ShowInfo(Window.GetWindow(this), "Загрузка", "Загрузка ISO уже выполняется.");
                return;
            }

            _activeIsoDownloadKey = "makuos-" + Guid.NewGuid().ToString("N");
            var mainWindow = MainWindow.Current;
            if (mainWindow == null || !mainWindow.TryRegisterDownload(name, _activeIsoDownloadKey, out _downloadCts, out _downloadPauseController))
            {
                _activeIsoDownloadKey = null;
                return;
            }

            SetDownloadState(true);
            mainWindow.ShowNotification(
                $"Загрузка «{name}» начата.",
                NotificationKind.Info,
                "download-start:" + _activeIsoDownloadKey);
            var destinationFolder = DownloadSettings.GetFolder();
            Directory.CreateDirectory(destinationFolder);
            var destinationPath = Path.Combine(destinationFolder, fileName);
            try
            {
                var progress = new Progress<DownloadProgress>(details =>
                {
                    ActivationProgressBar.IsIndeterminate = details.TotalBytes == null;
                    ActivationProgressBar.Value = Math.Max(0, details.Progress);
                    DownloadStatusText.Text = details.Progress >= 0
                        ? $"Скачивание {name}: {details.Progress:0}%"
                        : $"Скачивание {name}...";
                    mainWindow.UpdateDownloadProgress(_activeIsoDownloadKey, details, name);
                });

                Exception lastError = null;
                for (var index = 0; index < sources.Count; index++)
                {
                    _downloadCts.Token.ThrowIfCancellationRequested();
                    try
                    {
                        DownloadStatusText.Text = $"Подключение к зеркалу {index + 1} из {sources.Count}...";
                        var directUrl = await _isoDownloadService.ResolveDownloadUrlAsync(sources[index], _downloadCts.Token);
                        if (string.IsNullOrWhiteSpace(directUrl))
                            throw new InvalidOperationException("Зеркало не вернуло прямую ссылку на файл.");

                        await _isoDownloadService.DownloadIsoAsync(directUrl, destinationPath, progress, _downloadCts.Token, _downloadPauseController);
                        mainWindow.CompleteDownload(_activeIsoDownloadKey, name);
                        DownloadStatusText.Text = $"{name} успешно скачан";
                        DownloadStatusText.Foreground = new SolidColorBrush(Color.FromRgb(50, 205, 50));
                        AppDialog.ShowInfo(Window.GetWindow(this), "Загрузка завершена", $"{name} сохранён:\n\n{destinationPath}");
                        return;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception exception) { lastError = exception; }
                }

                throw new InvalidOperationException("Не удалось скачать файл ни с одного зеркала.", lastError);
            }
            catch (OperationCanceledException)
            {
                mainWindow.RemoveDownload(_activeIsoDownloadKey);
                DeletePartialDownload(destinationPath);
                DownloadStatusText.Text = "Загрузка отменена";
            }
            catch (Exception exception)
            {
                mainWindow.RemoveDownload(_activeIsoDownloadKey);
                DeletePartialDownload(destinationPath);
                AppDialog.ShowError(Window.GetWindow(this), "Ошибка загрузки", exception.Message);
            }
            finally
            {
                _downloadCts?.Dispose();
                _downloadCts = null;
                _downloadPauseController = null;
                _activeIsoDownloadKey = null;
                SetDownloadState(false);
                ActivationProgressBar.Visibility = Visibility.Collapsed;
                if (DownloadStatusText.Text != $"{name} успешно скачан")
                    DownloadStatusText.Text = string.Empty;
            }
        }

        private static void DeletePartialDownload(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Файл может быть временно занят сетевым потоком.
            }
        }

        // ============================================================
        // ПОИСК BUTTON В VISUAL TREE
        // ============================================================

        private static System.Collections.Generic.IEnumerable<T>
            FindVisualChildren<T>(DependencyObject dependencyObject)
            where T : DependencyObject
        {
            if (dependencyObject == null)
                yield break;

            for (int i = 0;
                 i < System.Windows.Media.VisualTreeHelper
                         .GetChildrenCount(dependencyObject);
                 i++)
            {
                DependencyObject child =
                    System.Windows.Media.VisualTreeHelper
                        .GetChild(dependencyObject, i);

                if (child is T typedChild)
                    yield return typedChild;

                foreach (T descendant
                         in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    // =================================================================
    // ISO LINK RESOLVER
    // =================================================================

    internal static class MicrosoftIsoLinkResolver
    {
        private const string ApiBase =
            "https://www.microsoft.com/ru-ru/api/controls/contentinclude/html";

        private const string LanguagePageId =
            "a8f8f489-4c7f-463a-9ca6-5cff94d8d041";

        private const string DownloadPageId =
            "a224afab-2097-4dfa-a2ba-463eb191a285";

        public static async Task<string?> GetIsoDownloadUrlAsync(
            string pageUrl,
            string productSegment,
            string wantedType,
            CancellationToken cancellationToken)
        {
            CookieContainer cookies =
                new CookieContainer();

            using HttpClientHandler handler =
                new HttpClientHandler
                {
                    CookieContainer = cookies,
                    UseCookies = true,
                    AllowAutoRedirect = true,
                    AutomaticDecompression =
                        DecompressionMethods.GZip |
                        DecompressionMethods.Deflate |
                        DecompressionMethods.Brotli
                };

            using HttpClient client =
                new HttpClient(handler);

            client.Timeout =
                TimeSpan.FromSeconds(30);

            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/153.0 Safari/537.36");

            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

            // ---------------------------------------------------------
            // 1. Получаем официальную страницу Microsoft.
            // ---------------------------------------------------------

            using HttpResponseMessage pageResponse =
                await client.GetAsync(
                    pageUrl,
                    cancellationToken);

            string pageHtml =
                await pageResponse.Content
                    .ReadAsStringAsync(cancellationToken);

            if (!pageResponse.IsSuccessStatusCode ||
                string.IsNullOrWhiteSpace(pageHtml))
            {
                return null;
            }

            // ---------------------------------------------------------
            // 2. Извлекаем ProductEditionId из страницы.
            // ---------------------------------------------------------

            string? productEditionId =
                ExtractProductEditionId(pageHtml);

            if (string.IsNullOrWhiteSpace(productEditionId))
                return null;

            string sessionId =
                Guid.NewGuid().ToString();

            // ---------------------------------------------------------
            // 3. Получаем список SKU языков.
            // ---------------------------------------------------------

            string skuUrl =
                BuildSkuInformationUrl(
                    productSegment,
                    productEditionId,
                    sessionId);

            using HttpResponseMessage skuResponse =
                await client.PostAsync(
                    skuUrl,
                    new StringContent(
                        string.Empty,
                        Encoding.UTF8),
                    cancellationToken);

            string skuHtml =
                await skuResponse.Content
                    .ReadAsStringAsync(cancellationToken);

            if (!skuResponse.IsSuccessStatusCode ||
                string.IsNullOrWhiteSpace(skuHtml))
            {
                return null;
            }

            // ---------------------------------------------------------
            // 4. Ищем SKU для русского языка.
            // ---------------------------------------------------------

            string? skuId =
                ExtractSkuId(
                    skuHtml,
                    "Russian");

            if (string.IsNullOrWhiteSpace(skuId))
            {
                // Иногда Microsoft возвращает локализованное имя.
                skuId =
                    ExtractSkuId(
                        skuHtml,
                        "Russian (Русский)");
            }

            if (string.IsNullOrWhiteSpace(skuId))
                return null;

            // ---------------------------------------------------------
            // 5. Получаем реальные ссылки на ISO.
            // ---------------------------------------------------------

            string downloadUrl =
                BuildDownloadLinksUrl(
                    productSegment,
                    skuId,
                    sessionId);

            using HttpResponseMessage linkResponse =
                await client.PostAsync(
                    downloadUrl,
                    new StringContent(
                        string.Empty,
                        Encoding.UTF8),
                    cancellationToken);

            string linksHtml =
                await linkResponse.Content
                    .ReadAsStringAsync(cancellationToken);

            if (!linkResponse.IsSuccessStatusCode ||
                string.IsNullOrWhiteSpace(linksHtml))
            {
                return null;
            }

            // ---------------------------------------------------------
            // 6. Ищем x64 ISO.
            // ---------------------------------------------------------

            string? isoUrl =
                ExtractIsoUrl(
                    linksHtml,
                    wantedType);

            if (string.IsNullOrWhiteSpace(isoUrl))
            {
                // В качестве резервного варианта ищем любую .iso ссылку.
                isoUrl =
                    ExtractAnyIsoUrl(linksHtml);
            }

            return isoUrl;
        }

        private static string? ExtractProductEditionId(
            string html)
        {
            // Примеры:
            // option value="2093">Windows 11
            // productEditionId=2093

            Match match =
                Regex.Match(
                    html,
                    @"(?:option\s+value|productEditionId)\s*=\s*[""']?(\d+)",
                    RegexOptions.IgnoreCase);

            if (match.Success)
                return match.Groups[1].Value;

            return null;
        }

        private static string? ExtractSkuId(
            string html,
            string language)
        {
            MatchCollection matches =
                Regex.Matches(
                    WebUtility.HtmlDecode(html),
                    @"<option[^>]+value=""([^""]+)""[^>]*>\s*" +
                    Regex.Escape(language) +
                    @"\s*</option>",
                    RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                if (!match.Success)
                    continue;

                string value = match.Groups[1].Value;

                try
                {
                    using JsonDocument document =
                        JsonDocument.Parse(value);

                    if (document.RootElement.TryGetProperty(
                        "id",
                        out JsonElement idElement))
                    {
                        return idElement
                            .GetString();
                    }
                }
                catch
                {
                    // Пробуем следующий вариант.
                }
            }

            // Дополнительный вариант:
            // ищем JSON внутри HTML напрямую.

            Match fallback =
                Regex.Match(
                    WebUtility.HtmlDecode(html),
                    @"\{\s*""id""\s*:\s*""([^""]+)""\s*,\s*""language""\s*:\s*""" +
                    Regex.Escape(language) +
                    @"""",
                    RegexOptions.IgnoreCase);

            return fallback.Success
                ? fallback.Groups[1].Value
                : null;
        }

        private static string? ExtractIsoUrl(
            string html,
            string wantedType)
        {
            html = WebUtility.HtmlDecode(html);

            // Ищем href рядом с IsoX64.
            Match match =
                Regex.Match(
                    html,
                    $@"href\s*=\s*[""']([^""']+\.iso[^""']*)[""'][^>]*>?" +
                    $@"(?:\s*<[^>]*>)*\s*{Regex.Escape(wantedType)}",
                    RegexOptions.IgnoreCase);

            if (match.Success)
                return WebUtility.HtmlDecode(
                    match.Groups[1].Value);

            // Второй распространённый вариант:
            // сначала IsoX64, затем href.
            match =
                Regex.Match(
                    html,
                    $@"href\s*=\s*[""']([^""']+)[""'][^>]*>.*?" +
                    Regex.Escape(wantedType),
                    RegexOptions.IgnoreCase |
                    RegexOptions.Singleline);

            if (match.Success &&
                match.Groups[1].Value.Contains(
                    ".iso",
                    StringComparison.OrdinalIgnoreCase))
            {
                return WebUtility.HtmlDecode(
                    match.Groups[1].Value);
            }

            return null;
        }

        private static string? ExtractAnyIsoUrl(
            string html)
        {
            html = WebUtility.HtmlDecode(html);

            Match match =
                Regex.Match(
                    html,
                    @"href\s*=\s*[""']([^""']+\.iso[^""']*)[""']",
                    RegexOptions.IgnoreCase);

            if (!match.Success)
                return null;

            return match.Groups[1].Value;
        }

        private static string BuildSkuInformationUrl(
            string productSegment,
            string productEditionId,
            string sessionId)
        {
            return
                $"{ApiBase}" +
                $"?pageId={LanguagePageId}" +
                $"&host=www.microsoft.com" +
                $"&segments=software-download,{productSegment}" +
                $"&query=" +
                $"&action=getskuinformationbyproductedition" +
                $"&sessionId={Uri.EscapeDataString(sessionId)}" +
                $"&productEditionId={Uri.EscapeDataString(productEditionId)}" +
                $"&sdVersion=2";
        }

        private static string BuildDownloadLinksUrl(
            string productSegment,
            string skuId,
            string sessionId)
        {
            return
                $"{ApiBase}" +
                $"?pageId={DownloadPageId}" +
                $"&host=www.microsoft.com" +
                $"&segments=software-download,{productSegment}" +
                $"&query=" +
                $"&action=GetProductDownloadLinksBySku" +
                $"&sessionId={Uri.EscapeDataString(sessionId)}" +
                $"&skuId={Uri.EscapeDataString(skuId)}" +
                $"&language=Russian" +
                $"&sdVersion=2";
        }
    }

    // =================================================================
    // ISO DOWNLOADER
    // =================================================================

    internal sealed class WindowsIsoDownloadService
    {
        private readonly HttpClient _httpClient;

        public WindowsIsoDownloadService()
        {
            HttpClientHandler handler =
                new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    AutomaticDecompression =
                        DecompressionMethods.GZip |
                        DecompressionMethods.Deflate |
                        DecompressionMethods.Brotli
                };

            _httpClient =
                new HttpClient(handler);

            _httpClient.Timeout =
                Timeout.InfiniteTimeSpan;

            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/153.0 Safari/537.36");
        }

        public async Task<string?> ResolveDownloadUrlAsync(string sourceUrl, CancellationToken cancellationToken)
        {
            if (sourceUrl.Contains("pixeldrain.com/u/", StringComparison.OrdinalIgnoreCase))
            {
                var fileId = sourceUrl.Substring(sourceUrl.LastIndexOf('/') + 1).Trim();
                return string.IsNullOrWhiteSpace(fileId)
                    ? null
                    : "https://pixeldrain.com/api/file/" + fileId;
            }

            if (sourceUrl.Contains("mediafire.com/file/", StringComparison.OrdinalIgnoreCase))
            {
                using var response = await _httpClient.GetAsync(sourceUrl, cancellationToken);
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var match = Regex.Match(html, "https?://download[^\\\"'<>\\s]+", RegexOptions.IgnoreCase);
                return match.Success ? WebUtility.HtmlDecode(match.Value) : null;
            }

            return null;
        }

        public async Task<string> DownloadIsoAsync(
            string isoUrl,
            string destinationPath,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancellationToken,
            PauseController? pauseController = null)
        {
            using HttpResponseMessage response =
                await _httpClient.GetAsync(
                    isoUrl,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            response.EnsureSuccessStatusCode();

            long? contentLength =
                response.Content.Headers.ContentLength;

            await using Stream input =
                await response.Content
                    .ReadAsStreamAsync(cancellationToken);

            string? directory =
                Path.GetDirectoryName(destinationPath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            await using FileStream output =
                new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    1024 * 1024,
                    useAsync: true);

            byte[] buffer =
                new byte[1024 * 1024];

            long totalRead = 0;

            while (true)
            {
                if (pauseController != null)
                    await pauseController.WaitIfPausedAsync(cancellationToken);

                int bytesRead =
                    await input.ReadAsync(
                        buffer.AsMemory(
                            0,
                            buffer.Length),
                        cancellationToken);

                if (bytesRead <= 0)
                    break;

                await output.WriteAsync(
                    buffer.AsMemory(
                        0,
                        bytesRead),
                    cancellationToken);

                totalRead += bytesRead;

                if (contentLength.HasValue &&
                    contentLength.Value > 0)
                {
                    double percentage =
                        totalRead * 100.0 /
                        contentLength.Value;

                    percentage =
                        Math.Clamp(
                            percentage,
                            0,
                            100);

                    progress?.Report(new DownloadProgress
                    {
                        BytesReceived = totalRead,
                        TotalBytes = contentLength,
                        Progress = percentage
                    });
                }
            }

            await output.FlushAsync(
                cancellationToken);

            return destinationPath;
        }
    }
}