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
using Nexora.Services;
using Nexora.Services.Libraries;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Nexora.Infrastructure.Registry;
using Nexora.Infrastructure.Processes;
using Nexora.Services.WindowsSettings;
using Nexora.Domain.WindowsSettings;

namespace Nexora.Pages
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
				var result = await _windowsUpdate.SetDisabledAsync(false, CancellationToken.None);
				if (!result.Success) throw new InvalidOperationException(result.Error);
				if (DisableWindowsUpdateToggle != null)
				{
					DisableWindowsUpdateToggle.IsChecked = false;
					UpdateWindowsFeatureLabel("DisableWindowsUpdate", false);
				}
				MessageBox.Show("Windows Update включён. Политики и службы обновлений восстановлены.", "Windows Update", MessageBoxButton.OK, MessageBoxImage.Information);
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


        private void OpenDiskCleanup_Click(object sender, RoutedEventArgs e)
        {
            _main?.Navigate("diskcleanup");
        }
    }
}