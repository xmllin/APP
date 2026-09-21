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
		private ComboBox CreateTaskbarCombo(string tag, IEnumerable<string> items, double width)
		{
			var combo = new ComboBox
			{
				Tag = tag,
				Width = width,
				Style = (Style)FindResource("DarkComboBoxStyle"),
				ItemContainerStyle = (Style)FindResource("DarkComboBoxItemStyle")
			};
			foreach (var item in items) combo.Items.Add(item);
			combo.SelectionChanged += TaskbarCombo_SelectionChanged;
			return combo;
		}

				private void TaskbarCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_loadingExplorerSettings || !(sender is ComboBox combo) || combo.SelectedIndex < 0) return;

			var tag = combo.Tag as string;
			SettingOperationResult result;
			switch (tag)
			{
				case "TaskbarAlignment": result = _taskbarSettings.SetAlignment(combo.SelectedIndex); break;
				case "TaskbarMultiMonitorMode": result = _taskbarSettings.SetMultiMonitorMode(combo.SelectedIndex); break;
				case "TaskbarGlomLevel": result = _taskbarSettings.SetGroupingMode(combo.SelectedIndex); break;
				case "TaskbarMultiMonitorGlomLevel": result = _taskbarSettings.SetMultiMonitorGroupingMode(combo.SelectedIndex); break;
				default: return;
			}

			if (!result.Success)
			{
				MessageBox.Show("Не удалось изменить настройку панели задач: " + result.Error, "Панель задач", MessageBoxButton.OK, MessageBoxImage.Warning);
				LoadExplorerSettings();
				return;
			}
			if (result.RequiresRestart) ShowToast(result.Message + " Перезапуск Проводника требуется для применения.");
		}

		private bool VerifyTaskbarToggleState(string tag, bool enabled)
		{
			var state = _taskbarSettings.ReadState();
			switch (tag)
			{
				case "TaskbarAutoHide": return state.AutoHide == enabled;
				case "TaskbarBadges": return state.Badges == enabled;
				case "TaskbarFlashing": return state.Flashing == enabled;
				case "TaskbarMultiMonitor": return state.MultiMonitor == enabled;
				case "TaskbarShareWindow": return state.ShareWindow == enabled;
				case "TaskbarShowDesktop": return state.ShowDesktop == enabled;
				default: return true;
			}
		}

		private static bool IsTaskbarLiveReloadTag(string tag)
		{
			return tag == "TaskbarAutoHide"
				|| tag == "TaskbarBadges"
				|| tag == "TaskbarFlashing"
				|| tag == "TaskbarMultiMonitor"
				|| tag == "TaskbarShareWindow"
				|| tag == "TaskbarShowDesktop";
		}

    }
}
