using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Nexora.Services;
using Nexora.Models;
using Nexora.Services.Downloads;

namespace Nexora.Pages
{
    public partial class DownloadsPage : UserControl
    {
        private readonly MainWindow _main;

        public DownloadsPage(MainWindow main)
        {
            InitializeComponent();
            _main = main;
            Loaded += (_, __) => 
            {
                ActiveDownloads.ItemsSource = _main.ActiveDownloadsList;
                DownloadFolderText.Text = DownloadSettings.GetFolder();
                RefreshHistory();
            };
        }


        public void RefreshActiveDownloads()
        {
            if (ActiveDownloads.ItemsSource != _main.ActiveDownloadsList)
                ActiveDownloads.ItemsSource = _main.ActiveDownloadsList;
        }

        public void RefreshHistory()
        {
            var items = DownloadHistoryService.Load();
            foreach (var item in items)
            {
                item.Status = !string.IsNullOrWhiteSpace(item.FullPath) && System.IO.File.Exists(item.FullPath)
                    ? "Завершено"
                    : "Файл не найден";
            }
            items = items.OrderByDescending(x => x.DownloadedAt).ToList();
            DownloadItems.ItemsSource = items;
        }

        private void PickFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dialog.Description = "Выберите папку для загрузок";
                    dialog.SelectedPath = DownloadSettings.GetFolder();
                    dialog.ShowNewFolderButton = true;

                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;

                    var path = dialog.SelectedPath;
                    DownloadSettings.SaveFolder(path);
                    DownloadFolderText.Text = path;
                    _main.ShowNotification("Папка для загрузок сохранена.", NotificationKind.Success, "downloads-folder-saved:" + path);
                }
            }
            catch (Exception ex)
            {
                _main.ShowNotification("Не удалось сохранить папку загрузок: " + NotificationFormatter.FormatGeneralError(ex), NotificationKind.Error, "downloads-folder-error");
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var folder = DownloadSettings.GetFolder();
            try
            {
                if (string.IsNullOrWhiteSpace(folder) || !System.IO.Directory.Exists(folder))
                    throw new System.IO.DirectoryNotFoundException("Папка загрузок не найдена.");
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _main.ShowNotification("Не удалось открыть папку загрузок: " + Nexora.Services.NotificationFormatter.FormatGeneralError(ex), Nexora.Services.NotificationKind.Error, "downloads-folder:" + folder);
            }
        }

        private void ClearDownloads_Click(object sender, RoutedEventArgs e)
        {
            DownloadHistoryService.Clear();
            RefreshHistory();
        }

        private void DeleteDownload_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is DownloadRecord record))
                return;

            DownloadHistoryService.Delete(record);
            RefreshHistory();
        }

        private void PauseResumeDownload_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is string key))
                return;

            _main.TryTogglePauseDownload(key);
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is string key))
                return;

            _main.TryCancelDownload(key);
        }
    }
}
