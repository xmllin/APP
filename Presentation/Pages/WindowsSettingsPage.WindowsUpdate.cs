using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfApp1.Services;
using WpfApp1.Services.Libraries;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using WpfApp1.Infrastructure.Registry;
using WpfApp1.Infrastructure.Processes;
using WpfApp1.Services.WindowsSettings;
using WpfApp1.Domain.WindowsSettings;

namespace WpfApp1.Pages
{
    public partial class WindowsSettingsPage : UserControl
    {
		private void PauseWindowsUpdateButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var limitResult = _windowsUpdate.MaximizePauseLimit();
				if (!limitResult.Success) throw new InvalidOperationException(limitResult.Error);
				_windowsUpdate.PauseUntil(new DateTime(2077, 1, 1, 0, 0, 0, DateTimeKind.Utc));
				UpdatePauseStatus();
			}
			catch (Exception exception) { MessageBox.Show("Не удалось приостановить Windows Update: " + exception.Message, "Windows Update", MessageBoxButton.OK, MessageBoxImage.Warning); }
		}

		private async void StartWindowsUpdateButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var result = await _windowsUpdate.StartServiceAsync(CancellationToken.None);
				if (!result.Success) throw new InvalidOperationException(result.Error);
				MessageBox.Show(result.Message, "Windows Update", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception exception) { MessageBox.Show("Не удалось запустить Windows Update: " + exception.Message, "Windows Update", MessageBoxButton.OK, MessageBoxImage.Warning); }
		}

		private async void ClearWindowsUpdateCacheButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				await _windowsUpdate.ClearCacheAsync(CancellationToken.None);
				ShowToast("Кэш Windows Update очищен.");
			}
			catch (Exception exception) { MessageBox.Show("Не удалось очистить кэш Windows Update: " + exception.Message, "Windows Update", MessageBoxButton.OK, MessageBoxImage.Warning); }
		}

		private void UpdatePauseStatus()
		{
			var expiry = _windowsUpdate.GetPauseExpiry();
			if (expiry.HasValue && expiry.Value > DateTime.UtcNow)
				PauseWindowsUpdateStatus.Text = "Пауза до " + expiry.Value.ToLocalTime().ToString("dd.MM.yyyy");
			else
				PauseWindowsUpdateStatus.Text = "Дата окончания не установлена";
		}

		private void ClearTempButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var tempPath = System.IO.Path.GetTempPath();
				if (!System.IO.Directory.Exists(tempPath))
				{
					TempCleanupStatus.Text = "Папка Temp не найдена.";
					return;
				}

				long deletedBytes = 0;
				int deletedFiles = 0, deletedDirectories = 0, skipped = 0;
				ClearTempDirectoryContents(tempPath, ref deletedFiles, ref deletedDirectories, ref skipped, ref deletedBytes);
				TempCleanupStatus.Text = $"Освобождено: {FormatSize(deletedBytes)}; удалено: {deletedFiles} файлов, {deletedDirectories} папок; пропущено: {skipped}.";
				ShowToast($"Папка Temp очищена. Освобождено {FormatSize(deletedBytes)}.");
			}
			catch (Exception exception)
			{
				TempCleanupStatus.Text = "Очистка не завершена: " + exception.Message;
				ShowToast("Не удалось очистить папку Temp: " + exception.Message, true);
			}
		}

		private static void ClearTempDirectoryContents(string root, ref int deletedFiles, ref int deletedDirectories, ref int skipped, ref long deletedBytes)
		{
			IEnumerable<string> entries;
			try { entries = System.IO.Directory.EnumerateFileSystemEntries(root).ToList(); }
			catch { skipped++; return; }

			foreach (var entry in entries)
			{
				try
				{
					var attributes = System.IO.File.GetAttributes(entry);
					if ((attributes & System.IO.FileAttributes.ReparsePoint) != 0) { skipped++; continue; }
					if (System.IO.Directory.Exists(entry))
					{
						ClearTempDirectoryContents(entry, ref deletedFiles, ref deletedDirectories, ref skipped, ref deletedBytes);
						System.IO.Directory.Delete(entry, recursive: false);
						deletedDirectories++;
					}
					else if (System.IO.File.Exists(entry))
					{
						var fileInfo = new System.IO.FileInfo(entry);
						if ((attributes & System.IO.FileAttributes.ReadOnly) != 0)
							System.IO.File.SetAttributes(entry, attributes & ~System.IO.FileAttributes.ReadOnly);
						deletedBytes += fileInfo.Length;
						System.IO.File.Delete(entry);
						deletedFiles++;
					}
				}
				catch { skipped++; }
			}
		}

		private static string FormatSize(long byteCount)
		{
			const double kb = 1024d;
			const double mb = kb * 1024d;
			const double gb = mb * 1024d;
			if (byteCount >= gb) return $"{byteCount / gb:0.##} ГБ";
			if (byteCount >= mb) return $"{byteCount / mb:0.##} МБ";
			if (byteCount >= kb) return $"{byteCount / kb:0.##} КБ";
			return $"{byteCount} Б";
		}

    }
}
