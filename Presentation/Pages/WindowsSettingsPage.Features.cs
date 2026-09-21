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
		private void WindowsFeatureToggle_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (!(sender is ToggleButton toggle)) return;
			var tag = toggle.Tag as string;
			if (RequiresAdministratorAccess(tag) && !IsAdministrator())
			{
				e.Handled = true;
				if (tag != "AutoGameModeEnabled")
				{
					MessageBox.Show("Для изменения системных настроек сначала нажмите «Перезапустить от администратора».", "Настройки Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
				}

			}
		}

		private ToggleButton CreateManagedToggle(string tag)
		{
			var toggle = CreateAdditionalToggle(tag);
			_managedToggles[tag] = toggle;
			return toggle;
		}

		private ToggleButton CreateAdditionalToggle(string tag)
		{
			var toggle = new ToggleButton
			{
				Tag = tag,
				Style = (Style)FindResource("ExplorerToggleButton")
			};
			toggle.Checked += WindowsFeatureToggle_Changed;
			toggle.Unchecked += WindowsFeatureToggle_Changed;
			ApplyAdminToggleState(toggle);
			return toggle;
		}

		private ToggleButton CreateGameModeToggle()
		{
			_gameModeToggle = CreateAdditionalToggle("AutoGameModeEnabled");
			ApplyAdminToggleState(_gameModeToggle);
			SetToggle(_gameModeToggle, IsGameModeEnabled());
			return _gameModeToggle;
		}

		private static bool IsGameModeEnabled()
		{
			// Windows 11 exposes Game Mode under HKCU\Software\Microsoft\GameBar.
			// Do not mirror a machine-level value here: the Windows Settings toggle is user-scoped.
			return ReadDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 0) == 1;
		}

		private void ApplyAdminToggleState(ToggleButton toggle)
		{
			var tag = toggle.Tag as string;
			if (!RequiresAdministratorAccess(tag))
			{
				if (!IsAdministrator())
				{
					toggle.IsEnabled = true;
					toggle.IsHitTestVisible = true;
					toggle.Opacity = 1;
				}
				return;
			}
			if (!IsAdministrator())
			{
				toggle.IsEnabled = false;
				toggle.IsHitTestVisible = false;
				toggle.Opacity = 0.5;
				toggle.Cursor = Cursors.Arrow;
			}
			else
			{
				toggle.IsEnabled = true;
				toggle.IsHitTestVisible = true;
				toggle.Opacity = 1;
				toggle.Cursor = Cursors.Hand;
			}
		}

		private static bool RequiresAdministratorAccess(string tag)
		{
			if (string.IsNullOrEmpty(tag)) return false;
			return tag == "ShortcutArrow"
				|| tag == "BackgroundRecording"
				|| tag == "DeveloperMode"
				|| tag == "LongPathsEnabled"
				|| tag == "NumLockOnBoot"
				|| tag == "DisableHibernation"
				|| tag == "DisableUSBPowerSaving"
				|| tag == "DisableSystemThrottling"
				|| tag == "DisableSettings365Ads"
				|| tag == "HardwareGpuScheduling"
				|| tag == "PowerShellScripts"
				|| tag == "AutoGameModeEnabled"
				|| tag == "UacNeverNotify"
				|| tag == "Gallery"
				|| tag.StartsWith("Disable", StringComparison.Ordinal) && !IsUserSettingTag(tag)
				|| tag == "HideDownloads"
				|| tag == "HideDocuments"
				|| tag == "HideVideos"
				|| tag == "HidePictures"
				|| tag == "HideMusic"
				|| tag == "HideDesktop";
		}

		private Border CreateHagsRow()
		{
			_hagsToggle = CreateAdditionalToggle("HardwareGpuScheduling");
			var supported = IsHardwareGpuSchedulingSupported();
			_hagsToggle.IsEnabled = supported && IsAdministrator();
			SetToggle(_hagsToggle, supported && IsHardwareGpuSchedulingEnabled());
			var infoButton = new Button
			{
				Content = "ⓘ Подробнее",
				Style = (Style)FindResource("GhostButton"),
				Padding = new Thickness(10, 5, 10, 5),
				Margin = new Thickness(0, 0, 8, 0),
				HorizontalAlignment = HorizontalAlignment.Right
			};
			infoButton.Click += HagsInfoButton_Click;
			var row = new Border { Style = (Style)FindResource("SettingRow") };
			var grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			var text = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
			text.Children.Add(new TextBlock { Text = "Планирование GPU", FontWeight = FontWeights.SemiBold });
			text.Children.Add(new TextBlock { Text = "Аппаратное планирование графического процессора", Foreground = new SolidColorBrush(Color.FromRgb(130, 165, 207)), FontSize = 11 });
			var warning = new TextBlock
			{
				Text = "Для применения HAGS требуется перезагрузка Windows.",
				Foreground = new SolidColorBrush(Color.FromRgb(255, 196, 90)),
				FontSize = 10,
				Margin = new Thickness(0, 4, 0, 0)
			};
			text.Children.Add(warning);
			var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
			controls.Children.Add(infoButton);
			controls.Children.Add(_hagsToggle);
			Grid.SetColumn(text, 0);
			Grid.SetColumn(controls, 1);
			grid.Children.Add(text);
			grid.Children.Add(controls);
			row.Child = grid;
			return row;
		}

		private Border CreateAdditionalToggleRow(ToggleButton toggle, string title, string description)
		{
			if (toggle.Parent is Panel oldPanel) oldPanel.Children.Remove(toggle);
			var label = new TextBlock
			{
				Text = "Отключено",
				Foreground = new SolidColorBrush(Colors.Red),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 10, 0)
			};
			_additionalStatusLabels[toggle.Tag as string] = label;
			var currentState = toggle.IsChecked == true;
			SetStatusLabel(label, currentState ? "Включено" : "Отключено", currentState);
			var row = new Border { Style = (Style)FindResource("SettingRow") };
			var grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			var text = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
			text.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold });
			text.Children.Add(new TextBlock { Text = description, Foreground = new SolidColorBrush(Color.FromRgb(130, 165, 207)), FontSize = 11 });
			var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			controls.Children.Add(label);
			controls.Children.Add(toggle);
			Grid.SetColumn(text, 0);
			Grid.SetColumn(controls, 1);
			grid.Children.Add(text);
			grid.Children.Add(controls);
			row.Child = grid;
			return row;
		}

				private async Task LoadPowerShellScriptsStateAsync()
		{
			if (_powerShellScriptsToggle == null) return;
			try
			{
				var policy = await _libraryInstallation.GetPowerShellScriptsPolicyAsync(CancellationToken.None);
				var enabled = IsPowerShellScriptsEnabled(policy);
				_loadingExplorerSettings = true;
				SetToggle(_powerShellScriptsToggle, enabled);
				SetStatusLabel(_powerShellScriptsLabel, enabled ? "Включено" : "Отключено", enabled);
			}
			catch
			{
				SetStatusLabel(_powerShellScriptsLabel, "Неизвестно", false);
			}
			finally
			{
				_loadingExplorerSettings = false;
				ApplyPowerShellScriptsAdminState();
			}
		}

		private async void PowerShellScriptsToggle_Changed(object sender, RoutedEventArgs e)
		{
			if (_loadingExplorerSettings || _powerShellScriptsBusy || _powerShellScriptsToggle == null) return;
			_powerShellScriptsBusy = true;
			var enabled = _powerShellScriptsToggle.IsChecked == true;
			ApplyPowerShellScriptsAdminState();
			try
			{
				var policy = await _libraryInstallation.SetPowerShellScriptsEnabledAsync(enabled, CancellationToken.None);
				if (string.IsNullOrWhiteSpace(policy))
					policy = await _libraryInstallation.GetPowerShellScriptsPolicyAsync(CancellationToken.None);
				if (IsPowerShellScriptsEnabled(policy) != enabled)
					throw new InvalidOperationException("Windows не подтвердила изменение политики PowerShell.");
				SetStatusLabel(_powerShellScriptsLabel, enabled ? "Включено" : "Отключено", enabled);
			}
			catch (Exception exception)
			{
				_loadingExplorerSettings = true;
				SetToggle(_powerShellScriptsToggle, !enabled);
				_loadingExplorerSettings = false;
				SetStatusLabel(_powerShellScriptsLabel, "Ошибка", false);
				AppDialog.ShowInfo(Window.GetWindow(this), "PowerShell", NotificationFormatter.FormatGeneralError(exception));
			}
			finally
			{
				_powerShellScriptsBusy = false;
				ApplyPowerShellScriptsAdminState();
			}
		}

		private void ApplyPowerShellScriptsAdminState()
		{
			if (_powerShellScriptsToggle == null) return;
			var isAdmin = IsAdministrator();
			var allowed = isAdmin && !_powerShellScriptsBusy;
			_powerShellScriptsToggle.IsEnabled = allowed;
			_powerShellScriptsToggle.IsHitTestVisible = allowed;
			_powerShellScriptsToggle.Opacity = isAdmin ? 1.0 : 0.55;
			_powerShellScriptsToggle.Cursor = allowed ? Cursors.Hand : Cursors.Arrow;
		}

		private static bool IsPowerShellScriptsEnabled(string policy)
		{
			return string.Equals(policy, "RemoteSigned", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(policy, "Unrestricted", StringComparison.OrdinalIgnoreCase)
				|| string.Equals(policy, "Bypass", StringComparison.OrdinalIgnoreCase);
		}

		private async void WindowsFeatureToggle_Changed(object sender, RoutedEventArgs e)
		{
			if (_loadingExplorerSettings || !(sender is ToggleButton toggle)) return;
			var disabled = toggle.IsChecked == true;
			var tag = toggle.Tag as string;
			if (tag == "HardwareGpuScheduling" && !IsHardwareGpuSchedulingSupported())
			{
				_loadingExplorerSettings = true;
				try { SetToggle(toggle, false); }
				finally { _loadingExplorerSettings = false; }
				MessageBox.Show("HAGS недоступен на этом оборудовании или драйвере.", "Планирование GPU", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			if (!IsUserSettingTag(tag) && !IsAdministrator())
			{
				_loadingExplorerSettings = true;
				try { SetToggle(toggle, !disabled); }
				finally { _loadingExplorerSettings = false; }
				if (tag != "AutoGameModeEnabled")
				{
					MessageBox.Show("Для изменения системных настроек сначала нажмите «Перезапустить от администратора».", "Настройки Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
				}
				return;
			}
			try
			{
				if (tag == "DisableTelemetry")
				{
					var telemetryOperation = await ApplyWindowsFeatureSettingAsync(tag, disabled);
					if (!telemetryOperation.Success)
						throw new InvalidOperationException(telemetryOperation.Error);
					SetTelemetryGroupVisualState(disabled);
					RefreshExplorer();
					return;
				}

				var operation = await ApplyWindowsFeatureSettingAsync(tag, disabled);
				if (!operation.Success) throw new InvalidOperationException(operation.Error);
				UpdateWindowsFeatureLabel(tag, disabled);
				if (operation.RequiresRestart && !string.Equals(tag, "UacNeverNotify", StringComparison.Ordinal))
					ShowToast(operation.Message + " Требуется перезапуск для полного применения.");
				if (IsTaskbarLiveReloadTag(tag) && !VerifyTaskbarToggleState(tag, disabled))
					throw new InvalidOperationException("Windows не сохранила выбранное состояние панели задач.");
				if (!IsTaskbarLiveReloadTag(tag)) RefreshExplorer();
			}
			catch (Exception exception)
			{
				var message = IsAdministrator()
					? "Не удалось изменить настройку Windows: " + exception.Message
					: "Для изменения системных настроек сначала нажмите «Перезапустить от администратора».";
				MessageBox.Show(message, "Настройки Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
				LoadExplorerSettings();
			}
		}

		private void SetTelemetryGroupVisualState(bool disabled)
		{
			_loadingExplorerSettings = true;
			try
			{
				SetToggle(DisableTelemetryToggle, disabled);
				SetToggle(DisableAppDiagnosticsToggle, disabled);
				SetToggle(DisableActivityToggle, disabled);
				SetToggle(DisablePerformanceToggle, disabled);
				SetToggle(DisableKeystrokesToggle, disabled);
				SetToggle(DisableVoiceDataToggle, disabled);
			}
			finally
			{
				_loadingExplorerSettings = false;
			}
		}

		private void UpdateWindowsFeatureLabel(string tag, bool disabled)
		{
			var label = disabled ? "Включено" : "Отключено";
			var statusIsEnabled = disabled;
			switch (tag)
			{
				case "HardwareGpuScheduling":
					if (!IsHardwareGpuSchedulingSupported())
					{
						if (_additionalStatusLabels.TryGetValue(tag, out var unavailableLabel))
						{
							SetStatusLabel(unavailableLabel, "Недоступно", false);
							unavailableLabel.Width = 90;
						}
						break;
					}
					if (_additionalStatusLabels.TryGetValue(tag, out var hagsLabel)) SetStatusLabel(hagsLabel, label, statusIsEnabled);
					break;
				case "DisableWindowsUpdate": SetStatusLabel(DisableWindowsUpdateLabel, label, statusIsEnabled); break;
				case "DisableDriverUpdates": SetStatusLabel(DisableDriverUpdatesLabel, label, statusIsEnabled); break;
				case "DisableReservedStorage": SetStatusLabel(DisableReservedStorageLabel, label, statusIsEnabled); break;
				case "DisableTelemetry": SetStatusLabel(DisableTelemetryLabel, label, statusIsEnabled); break;
				case "DisableAppDiagnostics": SetStatusLabel(DisableAppDiagnosticsLabel, label, statusIsEnabled); break;
				case "DisableActivity": SetStatusLabel(DisableActivityLabel, label, statusIsEnabled); break;
				case "DisablePerformance": SetStatusLabel(DisablePerformanceLabel, label, statusIsEnabled); break;
				case "DisableKeystrokes": SetStatusLabel(DisableKeystrokesLabel, label, statusIsEnabled); break;
				case "DisableVoiceData": SetStatusLabel(DisableVoiceDataLabel, label, statusIsEnabled); break;
							case "DisableStickyKeys": SetAdditionalStatusLabel("DisableStickyKeys", label, statusIsEnabled); break;
							case "DisableBingSearch": SetAdditionalStatusLabel("DisableBingSearch", label, statusIsEnabled); break;
				case "DisableHibernation": SetStatusLabel(DisableHibernationLabel, label, statusIsEnabled); break;
				case "DisableSmartScreen": SetStatusLabel(DisableSmartScreenLabel, label, statusIsEnabled); break;
				case "DisableMemoryIntegrity": SetStatusLabel(DisableMemoryIntegrityLabel, label, statusIsEnabled); break;
				case "DisableVbs": SetStatusLabel(_disableVbsLabel, label, statusIsEnabled); break;
				case "UacNeverNotify": SetStatusLabel(DisableUacLabel, label, statusIsEnabled); break;
				case "DisablePageFile": SetStatusLabel(DisablePageFileLabel, label, statusIsEnabled); break;
				case "DisableBitLockerAutoEncryption": SetStatusLabel(DisableBitLockerAutoEncryptionLabel, label, statusIsEnabled); break;
				    default:
					    if (_additionalStatusLabels.TryGetValue(tag, out var additionalLabel)) SetStatusLabel(additionalLabel, label, statusIsEnabled);
					    break;
			}
		}

		private void SetAdditionalStatusLabel(string tag, string text, bool enabled)
		{
			if (_additionalStatusLabels.TryGetValue(tag, out var label)) SetStatusLabel(label, text, enabled);
		}

		private bool IsUacNeverNotifyEnabled() => _securitySettings.IsUacNeverNotify();

		private void SetUacNeverNotify(bool neverNotify)
		{
			var result = _securitySettings.SetUacNeverNotify(neverNotify);
			if (!result.Success) throw new InvalidOperationException(result.Error);
		}

		private async Task<SettingOperationResult> ApplyWindowsFeatureSettingAsync(string tag, bool disabled)
		{
			SettingOperationResult result;
			switch (tag)
			{
				case "OpenThisPc": return _explorerSettings.SetLaunchToThisPc(disabled);
				case "ExplorerItemCheckboxes": return _explorerSettings.SetItemCheckboxes(disabled);
				case "TaskbarWidgets": return _taskbarSettings.SetWidgets(disabled);
				case "TaskbarTaskViewButton": return _taskbarSettings.SetTaskViewButton(disabled);
				case "TaskbarLastActiveClick": return _taskbarSettings.SetLastActiveClick(disabled);
				case "ShowUserFiles":
				case "ShowNetworkIcon":
				case "ShowControlPanel":
				case "ShowDesktopIcons":
				case "ShortcutArrow":
				case "ToastNotifications":
				case "ClassicContextMenu":
				case "SystemSuggestions":
				case "ExplorerSyncNotifications":
				case "ExplorerCompactMode":
				case "SnapAssistFlyout":
				case "ClipboardHistory":
				case "WindowShake":
				case "GameBar":
				case "BackgroundRecording":
				case "FullscreenOptimizations":
				case "NumLockOnBoot":
				case "DeveloperMode":
				case "LongPathsEnabled":
				case "SpeedUpExplorerAndMenus":
				case "DisableStartMenuWebSearch":
				case "DisableStartRecommended":
				case "DisableSettings365Ads":
				case "DisablePreinstalledApps":
					return _interfaceSettings.Apply(tag, disabled);

				case "DisableHibernation":
					return await _powerSettings.SetHibernationAsync(disabled, CancellationToken.None);
				case "DisableUSBPowerSaving":
					return await _powerSettings.SetUsbPowerSavingDisabledAsync(disabled, CancellationToken.None);
				case "DisableSystemThrottling":
					return _powerSettings.SetSystemPowerThrottlingDisabled(disabled);

				case "DisableTelemetry":
				case "DisableAppDiagnostics":
				case "DisableActivity":
				case "DisablePerformance":
				case "DisableKeystrokes":
				case "DisableVoiceData":
				case "DisableErrorReporting":
				case "DisableAdvertisingAndSuggestions":
				case "DisableNewsAndInterests":
				case "HideMeetNowButton":
				case "DisableLocationAndSensors":
				case "DisableAutoLogger":
				case "DisableCortana":
				case "DisableCopilot":
				case "DisableContentDeliveryManager":
				case "DisableFindMyDevice":
				case "DisableDeliveryOptimization":
					result = _privacySettings.Apply(tag, disabled);
					return result ?? SettingOperationResult.Fail("Не удалось обработать настройку конфиденциальности.");

				case "DisableStickyKeys":
					SetStickyKeysDisabled(disabled);
					return SettingOperationResult.Ok("Залипание клавиш сохранено.");
				case "DisableBingSearch":
					SetBingSearchDisabled(disabled);
					return SettingOperationResult.Ok("Поиск Windows сохранён.");
				case "DisableSmartScreen":
					return _securitySettings.SetSmartScreenDisabled(disabled);
				case "DisableMemoryIntegrity":
					return _securitySettings.SetMemoryIntegrityDisabled(disabled);
				case "DisableVbs":
					return _securitySettings.SetVbsDisabled(disabled);
				case "EnableTaskbarEndTask":
					return _taskbarSettings.SetEndTask(disabled);
				case "TaskbarAutoHide":
					return _taskbarSettings.SetAutoHide(disabled);
				case "TaskbarBadges":
					return _taskbarSettings.SetBadges(disabled);
				case "TaskbarFlashing":
					return _taskbarSettings.SetFlashing(disabled);
				case "TaskbarMultiMonitor":
					return _taskbarSettings.SetMultiMonitor(disabled);
				case "TaskbarShareWindow":
					return _taskbarSettings.SetShareWindow(disabled);
				case "TaskbarShowDesktop":
					return _taskbarSettings.SetShowDesktop(disabled);
				case "DisableLockScreenBlur":
					return _securitySettings.SetLockScreenBlurDisabled(disabled);
				case "EnableDarkTheme":
					WriteDword(ThemePersonalizePath, "AppsUseLightTheme", disabled ? 0 : 1);
					WriteDword(ThemePersonalizePath, "SystemUsesLightTheme", disabled ? 0 : 1);
					return SettingOperationResult.Ok("Тёмная тема сохранена.");
				case "ReduceContextMenuDelay":
					WriteUserString(DesktopSettingsPath, "MenuShowDelay", disabled ? "50" : "400");
					return SettingOperationResult.Ok("Задержка контекстного меню сохранена.");
				case "EnableClipboard":
					WriteDword(ClipboardPath, "EnableClipboardHistory", disabled ? 1 : 0);
					return SettingOperationResult.Ok("История буфера обмена сохранена.");
				case "DisableWindowsAds":
					SetWindowsAdsDisabled(disabled);
					return SettingOperationResult.Ok("Рекламные предложения Windows сохранены.");
				case "AutoGameModeEnabled":
					WriteDword(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", disabled ? 1 : 0);
					return SettingOperationResult.Ok("Игровой режим сохранён.");
				case "UacNeverNotify":
					return _securitySettings.SetUacNeverNotify(disabled);
				case "DisablePageFile":
					SetPageFileDisabled(disabled);
					return SettingOperationResult.Ok("Файл подкачки сохранён.", true);
				case "DisableBitLockerAutoEncryption":
					return _securitySettings.SetBitLockerAutoEncryptionDisabled(disabled);
				default:
					return SettingOperationResult.Ok("Настройка сохранена.");
			}
		}

		private static void SetTelemetryDword(string name, int value)
		{
			WriteMachineDword(DataCollectionPolicyPath, name, value);
			WriteMachineDword(LegacyDataCollectionPolicyPath, name, value);
			WriteMachineDword(LegacyDataCollectionPolicy32Path, name, value);
		}

		private static bool AreStickyKeysDisabled()
		{
			return ReadUserString(AccessibilityStickyKeysPath, "Flags", string.Empty) == "26"
				&& ReadUserString(AccessibilityKeyboardResponsePath, "Flags", string.Empty) == "2"
				&& ReadUserString(AccessibilityToggleKeysPath, "Flags", string.Empty) == "34";
		}

		private static void SetStickyKeysDisabled(bool disabled)
		{
			if (disabled)
			{
				BackupUserString(AccessibilityStickyKeysPath, "Flags", "StickyKeys");
				BackupUserString(AccessibilityKeyboardResponsePath, "Flags", "KeyboardResponse");
				BackupUserString(AccessibilityToggleKeysPath, "Flags", "ToggleKeys");
				WriteUserString(AccessibilityStickyKeysPath, "Flags", "26");
				WriteUserString(AccessibilityKeyboardResponsePath, "Flags", "2");
				WriteUserString(AccessibilityToggleKeysPath, "Flags", "34");
				return;
			}

			RestoreUserString(AccessibilityStickyKeysPath, "Flags", "StickyKeys", "510");
			RestoreUserString(AccessibilityKeyboardResponsePath, "Flags", "KeyboardResponse", "126");
			RestoreUserString(AccessibilityToggleKeysPath, "Flags", "ToggleKeys", "62");
		}

		private static bool IsBingSearchDisabled()
		{
			var currentUserSearchDisabled = ReadDword(SearchPolicyPath, SearchRegistryValue, 1) == 0 &&
				ReadDword(SearchPolicyPath, CortanaConsentValue, 1) == 0 &&
				ReadDword(SearchPolicyPath, ConnectedSearchUseWebValue, 1) == 0;
			return currentUserSearchDisabled || ReadDword(SearchExplorerPolicyPath, "DisableSearchBoxSuggestions", 0) == 1;
		}

		private static void SetBingSearchDisabled(bool disabled)
		{
			var value = disabled ? 0 : 1;
			WriteDword(SearchPolicyPath, SearchRegistryValue, value);
			WriteDword(SearchPolicyPath, CortanaConsentValue, value);
			WriteDword(SearchPolicyPath, ConnectedSearchUseWebValue, value);
			WriteDword(SearchExplorerPolicyPath, "DisableSearchBoxSuggestions", disabled ? 1 : 0);
		}

		private static bool IsWindowsAdsDisabled()
		{
			var names = new[]
			{
				"ContentDeliveryAllowed", "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled",
				"PreInstalledAppsEverEnabled", "SilentInstalledAppsEnabled", "SystemPaneSuggestionsEnabled",
				"SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled",
				"SubscribedContent-353694Enabled", "SubscribedContent-353696Enabled",
				"SubscribedContent-310093Enabled", "RotatingLockScreenEnabled", "RotatingLockScreenOverlayEnabled"
			};
			return names.All(name => ReadDword(ContentDeliveryManagerPath, name, 1) == 0);
		}

		private static void SetWindowsAdsDisabled(bool disabled)
		{
			var names = new[]
			{
				"ContentDeliveryAllowed", "OemPreInstalledAppsEnabled", "PreInstalledAppsEnabled",
				"PreInstalledAppsEverEnabled", "SilentInstalledAppsEnabled", "SystemPaneSuggestionsEnabled",
				"SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled",
				"SubscribedContent-353694Enabled", "SubscribedContent-353696Enabled",
				"SubscribedContent-310093Enabled", "RotatingLockScreenEnabled", "RotatingLockScreenOverlayEnabled"
			};
			foreach (var name in names) WriteDword(ContentDeliveryManagerPath, name, disabled ? 0 : 1);
		}

		private int ReadHighlightColorIndex()
		{
			var value = ReadUserString(ColorsPath, "Hilight", "51 153 255").Replace(",", string.Empty).Trim();
			var colors = new[] { "51 153 255", "0 100 100", "180 0 180", "0 90 30", "100 40 0", "135 0 0", "15 0 120", "0 0 0", "40 40 40" };
			for (var index = 0; index < colors.Length; index++)
				if (string.Equals(value, colors[index], StringComparison.OrdinalIgnoreCase)) return index;
			return 0;
		}

		private void HighlightColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_loadingExplorerSettings || _highlightColorCombo.SelectedIndex < 0) return;
			var colors = new[] { "51 153 255", "0 100 100", "180 0 180", "0 90 30", "100 40 0", "135 0 0", "15 0 120", "0 0 0", "40 40 40" };
			var tracking = new[] { "0 102 204", "0 100 100", "110 0 110", "0 90 30", "100 40 0", "135 0 0", "15 0 120", "0 0 0", "40 40 40" };
			var index = Math.Min(_highlightColorCombo.SelectedIndex, colors.Length - 1);
			using (var key = Registry.CurrentUser.CreateSubKey(ColorsPath))
			{
				key?.SetValue("Hilight", colors[index], RegistryValueKind.String);
				key?.SetValue("HightLight", colors[index], RegistryValueKind.String);
				key?.SetValue("HotTrackingColor", tracking[index], RegistryValueKind.String);
			}
			RefreshExplorer();
		}

		private StackPanel CreateHighlightColorControls()
		{
			var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			controls.Children.Add(_highlightColorCombo);
			controls.Children.Add(_highlightColorApplyButton);
			return controls;
		}

		private void ShowToast(string message, bool isError = false)
		{
			if (ToastText == null || ToastNotification == null) return;
			ToastText.Text = message;
			ToastText.Foreground = isError
				? new SolidColorBrush(Color.FromRgb(255, 204, 204))
				: new SolidColorBrush(Color.FromRgb(241, 246, 255));
			ToastNotification.Background = isError
				? new SolidColorBrush(Color.FromRgb(64, 22, 22))
				: new SolidColorBrush(Color.FromRgb(23, 58, 97));
			ToastNotification.BorderBrush = isError
				? new SolidColorBrush(Color.FromRgb(181, 67, 67))
				: new SolidColorBrush(Color.FromRgb(43, 108, 203));
			ToastNotification.Visibility = Visibility.Visible;
			_toastTimer.Stop();
			_toastTimer.Start();
		}

		private void ToastTimer_Tick(object sender, EventArgs e)
		{
			if (ToastNotification != null) ToastNotification.Visibility = Visibility.Collapsed;
			_toastTimer.Stop();
		}

		private void ApplyHighlightColor()
		{
			try
			{
				if (_highlightColorCombo == null || _highlightColorCombo.SelectedIndex < 0) return;
				var colors = new[] { "51 153 255", "0 100 100", "180 0 180", "0 90 30", "100 40 0", "135 0 0", "15 0 120", "0 0 0", "40 40 40" };
				var tracking = new[] { "0 102 204", "0 100 100", "110 0 110", "0 90 30", "100 40 0", "135 0 0", "15 0 120", "0 0 0", "40 40 40" };
				var index = Math.Min(_highlightColorCombo.SelectedIndex, colors.Length - 1);
				using (var key = Registry.CurrentUser.CreateSubKey(ColorsPath))
				{
					key?.SetValue("Hilight", colors[index], RegistryValueKind.String);
					key?.SetValue("HightLight", colors[index], RegistryValueKind.String);
					key?.SetValue("HotTrackingColor", tracking[index], RegistryValueKind.String);
				}
				RefreshExplorer();
				ShowToast("Цвет выделения применён.");
			}
			catch (Exception exception)
			{
				ShowToast("Не удалось применить цвет выделения: " + exception.Message, true);
			}
		}

		private void DefaultNameTemplateApply_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var value = (_defaultNameTemplateBox.Text ?? string.Empty).Trim();
				if (value.Length == 0)
				{
					DefaultNameTemplateReset_Click(sender, e);
					return;
				}

				if (value.IndexOfAny(InvalidNamingTemplateCharacters) >= 0)
				{
					ShowToast("Имя содержит недопустимые символы: \\ / ? : * \" < > |", true);
					return;
				}

				// Windows использует RenameNameTemplate как пользовательский шаблон
				// для новых объектов. Не записываем «Новая папка» в RenameFileTemplate:
				// это могло заставлять новые файлы получать имя папки.
				BackupNamingTemplateValue("RenameNameTemplate", RenameNameTemplateBackupName);
				WriteUserString(NamingTemplatesPath, "RenameNameTemplate", value);
				DeleteUserValue(NamingTemplatesPath, "RenameFileTemplate");
				_defaultNameTemplateBox.Text = value;
				RefreshExplorer();
				ShowToast("Шаблон имени применён.");
			}
			catch (Exception exception)
			{
				ShowToast("Не удалось применить шаблон имени: " + exception.Message, true);
			}
		}

		private void DefaultNameTemplateReset_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				// Настоящее «По умолчанию» — удалить пользовательский шаблон.
				// Windows после этого возвращает собственные локализованные названия
				// («Новая папка», «Новый текстовый документ» и т.п.).
				DeleteUserValue(NamingTemplatesPath, "RenameNameTemplate");
				DeleteUserValue(NamingTemplatesPath, "RenameFileTemplate");
				_defaultNameTemplateBox.Text = string.Empty;
				RefreshExplorer();
				ShowToast("Названия новых файлов и папок возвращены к настройкам Windows.");
			}
			catch (Exception exception)
			{
				ShowToast("Не удалось восстановить стандартные названия: " + exception.Message, true);
			}
		}

		private static bool IsPageFileDisabled()
		{
			using (var key = Registry.LocalMachine.OpenSubKey(MemoryManagementPath))
			{
				var value = key?.GetValue("PagingFiles") as string[];
				return value != null && value.All(string.IsNullOrWhiteSpace);
			}
		}

		private static void SetPageFileDisabled(bool disabled)
		{
			if (disabled)
			{
				BackupMachineMultiString(MemoryManagementPath, "PagingFiles", "PageFile");
				using (var key = Registry.LocalMachine.CreateSubKey(MemoryManagementPath))
					key?.SetValue("PagingFiles", new string[0], RegistryValueKind.MultiString);
				return;
			}

			using (var backup = Registry.LocalMachine.OpenSubKey(MachineSettingsBackupPath + "\\PageFile"))
			{
				var original = backup?.GetValue("PagingFiles") as string[];
				if (original != null && original.Length > 0)
				{
					using (var key = Registry.LocalMachine.CreateSubKey(MemoryManagementPath))
						key?.SetValue("PagingFiles", original, RegistryValueKind.MultiString);
				}
				else
				{
					using (var key = Registry.LocalMachine.OpenSubKey(MemoryManagementPath, true))
						key?.DeleteValue("PagingFiles", false);
				}
			}
		}

    }
}
