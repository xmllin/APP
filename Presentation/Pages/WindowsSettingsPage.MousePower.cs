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
		private void RefreshMouseSettings()
		{
			_loadingExplorerSettings = true;
			try
			{
				var mouse = _mouseSettings.ReadState();
				if (_mouseSpeedSlider != null) _mouseSpeedSlider.Value = mouse.Speed;
				if (_mouseScrollSlider != null) _mouseScrollSlider.Value = mouse.ScrollLines;
				if (_mouseAccelerationToggle != null)
				{
					_mouseAccelerationToggle.IsChecked = !mouse.AccelerationEnabled;
					UpdateMouseAccelerationLabel();
				}
			}
			finally
			{
				_loadingExplorerSettings = false;
			}
		}

		private void ApplyMouseRegistryValues(bool showToast = false)
		{
			var results = new List<SettingOperationResult>();
			if (_mouseSpeedSlider != null) results.Add(_mouseSettings.SetSpeed((int)Math.Round(_mouseSpeedSlider.Value)));
			if (_mouseScrollSlider != null) results.Add(_mouseSettings.SetScrollLines((int)Math.Round(_mouseScrollSlider.Value)));
			if (_mouseAccelerationToggle != null) results.Add(_mouseSettings.SetAccelerationEnabled(_mouseAccelerationToggle.IsChecked != true));
			var failed = results.FirstOrDefault(x => !x.Success);
			if (failed != null) throw new InvalidOperationException(failed.Error);
			UpdateMouseAccelerationLabel();
			if (showToast) ShowToast("Параметры мыши применены.");
		}

		private void MouseSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (_loadingExplorerSettings || e.OldValue == e.NewValue) return;
			try
			{
				SettingOperationResult result;
				if (ReferenceEquals(sender, _mouseSpeedSlider)) result = _mouseSettings.SetSpeed((int)Math.Round(e.NewValue));
				else if (ReferenceEquals(sender, _mouseScrollSlider)) result = _mouseSettings.SetScrollLines((int)Math.Round(e.NewValue));
				else return;
				if (!result.Success) throw new InvalidOperationException(result.Error);
			}
			catch (Exception exception) { ShowToast("Не удалось применить параметры мыши: " + exception.Message, true); }
		}

		private void MouseAccelerationToggle_Changed(object sender, RoutedEventArgs e)
		{
			UpdateMouseAccelerationLabel();
			if (_loadingExplorerSettings || _mouseAccelerationToggle == null) return;
			var result = _mouseSettings.SetAccelerationEnabled(_mouseAccelerationToggle.IsChecked != true);
			if (!result.Success) ShowToast("Не удалось изменить ускорение мыши: " + result.Error, true);
		}

		private void UpdateMouseAccelerationLabel()
		{
			var enabled = _mouseAccelerationToggle != null && _mouseAccelerationToggle.IsChecked == true;
			var statusText = enabled ? "Включено" : "Отключено";
			var color = enabled ? System.Windows.Media.Color.FromRgb(76, 175, 80) : System.Windows.Media.Color.FromRgb(255, 85, 85);
			if (MouseAccelerationStatusText != null)
			{
				MouseAccelerationStatusText.Text = statusText;
				MouseAccelerationStatusText.Foreground = new System.Windows.Media.SolidColorBrush(color);
			}
			if (_mouseAccelerationLabel != null)
			{
				_mouseAccelerationLabel.Text = statusText;
				_mouseAccelerationLabel.Foreground = new System.Windows.Media.SolidColorBrush(color);
			}
		}

		private void MouseSettingsApply_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				ApplyMouseRegistryValues(true);
			}
			catch (Exception exception)
			{
				ShowToast("Не удалось применить параметры мыши: " + exception.Message, true);
			}
		}

		private async void PowerSchemeApplyButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (PowerSchemeComboBox == null || PowerSchemeComboBox.SelectedIndex < 0) return;
				var schemeName = PowerSchemeComboBox.SelectedItem as string;
				var result = await _powerSettings.ApplyAsync(schemeName, CancellationToken.None);
				if (!result.Success) throw new InvalidOperationException(result.Error);
				ShowToast(result.Message);
			}
			catch (Exception exception) { ShowToast("Не удалось применить схему электропитания: " + exception.Message, true); }
		}

		private void MouseSpeedDefault_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var result = _mouseSettings.SetSpeed(10);
				if (!result.Success) throw new InvalidOperationException(result.Error);
				if (_mouseSpeedSlider != null) _mouseSpeedSlider.Value = 10;
				ShowToast("Скорость указателя сброшена по умолчанию.");
			}
			catch (Exception exception) { ShowToast("Не удалось сбросить скорость указателя: " + exception.Message, true); }
		}

		private void MouseScrollDefault_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var result = _mouseSettings.SetScrollLines(5);
				if (!result.Success) throw new InvalidOperationException(result.Error);
				if (_mouseScrollSlider != null) _mouseScrollSlider.Value = 5;
				ShowToast("Прокрутка сброшена по умолчанию.");
			}
			catch (Exception exception) { ShowToast("Не удалось сбросить прокрутку: " + exception.Message, true); }
		}

		private double CalculateTightComboBoxWidth(IEnumerable<string> items)
		{
			var dpi = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
			var typeface = new Typeface("Segoe UI");
			var max = items.Select(text =>
			{
				var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 13, Brushes.White, dpi);
				return formatted.WidthIncludingTrailingWhitespace;
			}).DefaultIfEmpty(0).Max();
			// Text width is the limiting factor; add only the space needed by the
			// ComboBox chrome (left/right padding and drop-down arrow).
			return Math.Ceiling(max + 44);
		}

		private void HagsInfoButton_Click(object sender, RoutedEventArgs e)
		{
			MessageBox.Show(
				"Использует аппаратное планирование GPU. Результат зависит от видеокарты, драйвера и приложения. Для изменения требуется поддерживаемый GPU/драйвер и перезагрузка Windows.",
				"Планирование GPU",
				MessageBoxButton.OK,
				MessageBoxImage.Information);
		}

    }
}
