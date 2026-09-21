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
		private static bool IsFrequentFoldersEnabled()
		{
			// The current Folder Options checkbox is backed by Advanced\ShowFrequent.
			// Read that value first so a stale compatibility value under Explorer\ShowFrequent
			// cannot override a change made directly in Windows.
			var advanced = ReadUserDwordOptional(ExplorerAdvancedPath, "ShowFrequent");
			if (advanced.HasValue) return advanced.Value != 0;

			// Compatibility fallback for older builds/configurations that do not have it.
			var explorer = ReadUserDwordOptional(ExplorerPath, "ShowFrequent");
			return !explorer.HasValue || explorer.Value != 0;
		}

		private static bool IsRecentFilesBlockedByPolicy()
		{
			return ReadDword(UserWindowsPolicyPath, "NoRecentDocsHistory", 0) == 1
				|| ReadMachineDword(ExplorerMachinePolicyPath, "NoRecentDocsHistory", 0) == 1;
		}

		private static bool IsRecentFilesEnabled()
		{
			if (IsRecentFilesBlockedByPolicy()) return false;

			// The Folder Options checkbox is backed by ShowRecent, while
			// Start_TrackDocs is the system-wide gate for recent items in
			// Start, Jump Lists and File Explorer. Both must permit recents.
			var showRecent = ReadDword(ExplorerPath, "ShowRecent", 1) != 0;
			var trackDocuments = ReadDword(ExplorerAdvancedPath, "Start_TrackDocs", 1) != 0;
			return showRecent && trackDocuments;
		}

		private static void SetRecentFilesEnabled(bool enabled)
		{
			if (enabled && IsRecentFilesBlockedByPolicy())
				throw new InvalidOperationException("Недавние файлы заблокированы политикой NoRecentDocsHistory.");

			// ShowRecent controls the Explorer Privacy checkbox itself.
			WriteDword(ExplorerPath, "ShowRecent", enabled ? 1 : 0);

			// Start_TrackDocs is the global Windows gate. It must be ON for the
			// Explorer checkbox to stay checked and for Recent Files to appear.
			// When disabling only this Explorer option, leave the global Start/Jump
			// Lists preference untouched.
			if (enabled)
				WriteDword(ExplorerAdvancedPath, "Start_TrackDocs", 1);
		}

		private static bool IsGalleryNamespaceRegistered(string namespacePath)
		{
			using (var key = Registry.LocalMachine.OpenSubKey(namespacePath + GalleryId))
			{
				return key != null;
			}
		}

		private static bool IsGalleryVisible()
		{
			var userPinned = ReadDword(GalleryPath, "System.IsPinnedToNameSpaceTree", 1) != 0;
			if (!userPinned) return false;

			// Explorer needs the machine namespace registration if it was deleted.
			if (!IsGalleryNamespaceRegistered(GalleryDesktopNamespacePath)) return false;
			if (Environment.Is64BitOperatingSystem && !IsGalleryNamespaceRegistered(Wow6432GalleryDesktopNamespacePath)) return false;
			return true;
		}

		private static void EnsureGalleryNamespace(string namespacePath)
		{
			using (var key = Registry.LocalMachine.CreateSubKey(namespacePath + GalleryId))
			{
				if (key == null)
					throw new UnauthorizedAccessException("Не удалось восстановить раздел «Галерея» в Проводнике.");
				if (key.GetValue(null) == null)
					key.SetValue(string.Empty, GalleryId, RegistryValueKind.String);
			}
		}

		private static void RemoveGalleryNamespace(string namespacePath)
		{
			using (var parent = Registry.LocalMachine.OpenSubKey(namespacePath.TrimEnd('\\'), writable: true))
			{
				parent?.DeleteSubKeyTree(GalleryId, false);
			}
		}

		private static void SetGalleryVisible(bool visible)
		{
			if (visible)
			{
				var nativeMissing = !IsGalleryNamespaceRegistered(GalleryDesktopNamespacePath);
				var wowMissing = Environment.Is64BitOperatingSystem && !IsGalleryNamespaceRegistered(Wow6432GalleryDesktopNamespacePath);
				if ((nativeMissing || wowMissing) && !IsAdministrator())
					throw new UnauthorizedAccessException("Для восстановления «Галереи» нужны права администратора.");
				if (nativeMissing) EnsureGalleryNamespace(GalleryDesktopNamespacePath);
				if (wowMissing) EnsureGalleryNamespace(Wow6432GalleryDesktopNamespacePath);
				WriteDword(GalleryPath, "System.IsPinnedToNameSpaceTree", 1);
			}
			else
			{
				// Per-user hide works without elevation. If elevated, remove the machine
				// namespace registration too, so the state can be fully reversed later.
				WriteDword(GalleryPath, "System.IsPinnedToNameSpaceTree", 0);
				if (IsAdministrator())
				{
					RemoveGalleryNamespace(GalleryDesktopNamespacePath);
					if (Environment.Is64BitOperatingSystem) RemoveGalleryNamespace(Wow6432GalleryDesktopNamespacePath);
				}
			}
		}

		private static bool IsReservedStorageSupported()
		{
			return Environment.OSVersion.Version.Build >= 17763;
		}

		private void ExplorerToggle_Changed(object sender, RoutedEventArgs e)
		{
			if (_loadingExplorerSettings || !(sender is ToggleButton toggle)) return;
			var enabled = toggle.IsChecked == true;
			var tag = toggle.Tag as string;
			if (RequiresAdministratorAccess(tag) && !IsAdministrator())
			{
				_loadingExplorerSettings = true;
				try { SetToggle(toggle, !enabled); } finally { _loadingExplorerSettings = false; }
				MessageBox.Show("Для изменения этой настройки нужны права администратора. Нажмите «Перезапустить от администратора».", "Настройки Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}
			UpdateToggleLabel(tag, enabled);
			try
			{
				switch (toggle.Tag as string)
				{
					case "HiddenFiles": WriteDword(ExplorerAdvancedPath, "Hidden", enabled ? 1 : 2); break;
					case "FileExtensions": WriteDword(ExplorerAdvancedPath, "HideFileExt", enabled ? 0 : 1); break;
					case "OpenThisPc": WriteDword(ExplorerAdvancedPath, "LaunchTo", enabled ? 1 : 2); break;
					case "ExplorerHome": _explorerSettings.SetHomeVisibility(!enabled); break;
					case "ShowRecentFiles": SetRecentFilesEnabled(enabled); break;
					case "ShowFrequentFolders": WriteDword(ExplorerAdvancedPath, "ShowFrequent", enabled ? 1 : 0); WriteDword(ExplorerPath, "ShowFrequent", enabled ? 1 : 0); break;
					case "Gallery": SetGalleryVisible(!enabled); break;
					case "ShortcutSuffix": SetShortcutSuffix(enabled); break;
					case "ThisPcIcon": WriteDword(HideDesktopIconsPath, ThisPcId, enabled ? 0 : 1); break;
					case "RecycleBinIcon": WriteDword(HideDesktopIconsPath, RecycleBinId, enabled ? 0 : 1); break;
					case "ShowSecondsInSystemClock": WriteDword(ExplorerAdvancedPath, "ShowSecondsInSystemClock", enabled ? 1 : 0); break;
					case "Network": WriteDword(NetworkPath, "System.IsPinnedToNameSpaceTree", enabled ? 0 : 1); break;
					case "HideDownloads": SetNamespaceItemVisibility(DownloadsId, !enabled); break;
					case "HideDocuments": SetNamespaceItemVisibility(DocumentsId, !enabled); break;
					case "HideVideos": SetNamespaceItemVisibility(VideosId, !enabled); break;
					case "HidePictures": SetNamespaceItemVisibility(PicturesId, !enabled); break;
					case "HideMusic": SetNamespaceItemVisibility(MusicId, !enabled); break;
					case "HideDesktop": SetNamespaceItemVisibility(DesktopId, !enabled); break;
				}
				RefreshExplorer();
			}
			catch (Exception exception)
			{
				MessageBox.Show("Не удалось изменить настройку проводника: " + exception.Message, "Настройки Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
				LoadExplorerSettings();
			}
		}

		private void SetToggle(ToggleButton toggle, bool enabled)
		{
			var previousLoading = _loadingExplorerSettings;
			if (!previousLoading)
				_loadingExplorerSettings = true;
			try
			{
				toggle.IsChecked = enabled;
			}
			finally
			{
				_loadingExplorerSettings = previousLoading;
			}
			if (toggle.Template?.FindName("ToggleTrack", toggle) is Border track) track.BorderBrush = new SolidColorBrush(Colors.White);
			var tag = toggle.Tag as string;
			if (_additionalStatusLabels.ContainsKey(tag ?? string.Empty))
				SetAdditionalStatusLabel(tag, enabled ? "Включено" : "Отключено", enabled);
			else if ((tag ?? string.Empty).StartsWith("Disable", StringComparison.Ordinal))
				UpdateWindowsFeatureLabel(tag, enabled);
			else
				UpdateToggleLabel(tag, enabled);
		}

		private void UpdateToggleLabel(string tag, bool enabled)
		{
			var text = enabled ? "Включено" : "Отключено";
			switch (tag)
			{
				case "HiddenFiles": SetStatusLabel(ShowHiddenFilesLabel, text, enabled); break;
				case "FileExtensions": SetStatusLabel(ShowFileExtensionsLabel, text, enabled); break;
				case "OpenThisPc": SetStatusLabel(OpenThisPcLabel, text, enabled); break;
				case "ExplorerHome": SetStatusLabel(ExplorerHomeLabel, text, enabled); break;
				case "ShowRecentFiles": SetStatusLabel(ShowRecentFilesLabel, text, enabled); break;
				case "ShowFrequentFolders": SetStatusLabel(ShowFrequentFoldersLabel, text, enabled); break;
				case "Gallery": SetStatusLabel(ShowGalleryLabel, text, enabled); break;
				case "ShortcutSuffix": SetStatusLabel(RemoveShortcutSuffixLabel, text, enabled); break;
				case "ThisPcIcon": SetStatusLabel(ShowThisPcLabel, text, enabled); break;
				case "RecycleBinIcon": SetStatusLabel(ShowRecycleBinLabel, text, enabled); break;
				case "ShowSecondsInSystemClock": SetStatusLabel(ShowSecondsInSystemClockLabel, text, enabled); break;
				case "Network": SetStatusLabel(HideNetworkLabel, text, enabled); break;
				case "HideDownloads": SetStatusLabel(HideDownloadsLabel, text, enabled); break;
				case "HideDocuments": SetStatusLabel(HideDocumentsLabel, text, enabled); break;
				case "HideVideos": SetStatusLabel(HideVideosLabel, text, enabled); break;
				case "HidePictures": SetStatusLabel(HidePicturesLabel, text, enabled); break;
				case "HideMusic": SetStatusLabel(HideMusicLabel, text, enabled); break;
				case "HideDesktop": SetStatusLabel(HideDesktopLabel, text, enabled); break;
			}
		}

		private static void SetStatusLabel(TextBlock label, string text, bool enabled)
		{
			if (label == null) return;
			label.Text = text;
			label.Width = 112;
			label.TextAlignment = TextAlignment.Right;
			label.VerticalAlignment = VerticalAlignment.Center;
			label.Margin = new Thickness(0, 0, 12, 0);
			label.Foreground = new System.Windows.Media.SolidColorBrush(
				enabled ? System.Windows.Media.Color.FromRgb(50, 205, 50) : System.Windows.Media.Color.FromRgb(255, 0, 0));
		}

		private static string GetFolderDescriptionId(string namespaceId)
		{
			return FolderDescriptionIds.TryGetValue(namespaceId, out var value) ? value : null;
		}

		private static bool IsThisPcPolicyHidden(string folderDescriptionId)
		{
			var nativePath = FolderDescriptionsPath + folderDescriptionId + @"\PropertyBag";
			var wowPath = Wow6432FolderDescriptionsPath + folderDescriptionId + @"\PropertyBag";
			if (string.Equals(ReadMachineString(nativePath, "ThisPCPolicy", string.Empty), "Hide", StringComparison.OrdinalIgnoreCase)) return true;
			if (Environment.Is64BitOperatingSystem && string.Equals(ReadMachineString(wowPath, "ThisPCPolicy", string.Empty), "Hide", StringComparison.OrdinalIgnoreCase)) return true;
			return false;
		}

		private static bool IsNamespaceEntryVisible(string namespacePath, string id)
		{
			using (var key = Registry.LocalMachine.OpenSubKey(namespacePath + id))
			{
				if (key == null) return false;

				var hideIfEnabled = key.GetValue("HideIfEnabled");
				if (hideIfEnabled != null && Convert.ToInt32(hideIfEnabled) != 0) return false;

				var hiddenByDefault = key.GetValue("HiddenByDefault");
				if (hiddenByDefault != null && Convert.ToInt32(hiddenByDefault) != 0) return false;
			}

			return true;
		}

		private static bool IsNamespaceItemVisible(string id)
		{
			// A missing MyComputer\NameSpace entry is itself a hidden state.
			// This is important because tweakers often hide these folders by deleting
			// the namespace registration instead of only setting ThisPCPolicy=Hide.
			var nativeNamespaceVisible = IsNamespaceEntryVisible(MyComputerNameSpacePath, id);
			// The 64-bit Explorer process uses the native namespace. The WOW6432
			// registration is restored/removed alongside it for 32-bit shell clients,
			// but its absence alone must not make the folder look hidden here.
			if (!nativeNamespaceVisible) return false;

			using (var key = Registry.CurrentUser.OpenSubKey(KnownFoldersPath + id))
			{
				var value = key?.GetValue("System.IsPinnedToNameSpaceTree");
				if (value != null && Convert.ToInt32(value) == 0) return false;
			}

			var folderDescriptionId = GetFolderDescriptionId(id);
			return folderDescriptionId == null || !IsThisPcPolicyHidden(folderDescriptionId);
		}

		private static void RestoreNamespaceEntry(string namespacePath, string id)
		{
			using (var key = Registry.LocalMachine.CreateSubKey(namespacePath + id))
			{
				if (key == null) throw new UnauthorizedAccessException("Не удалось восстановить раздел MyComputer\\NameSpace.");
				// A hidden entry is made visible again by removing the hiding flags.
				key.DeleteValue("HideIfEnabled", false);
				key.SetValue("HiddenByDefault", 0, RegistryValueKind.DWord);
			}
		}

		private static void HideNamespaceEntry(string namespacePath, string id)
		{
			// Deleting the namespace registration mirrors the classic Windows
			// mechanism used to remove these entries from This PC.
			using (var parent = Registry.LocalMachine.OpenSubKey(namespacePath.TrimEnd('\\'), writable: true))
				parent?.DeleteSubKeyTree(id, false);
		}

		private static void SetNamespaceItemVisibility(string id, bool visible)
		{
			var folderDescriptionId = GetFolderDescriptionId(id);
			if (!IsAdministrator())
				throw new UnauthorizedAccessException("Для изменения видимости системных папок требуются права администратора.");

			if (folderDescriptionId != null)
			{
				var nativePath = FolderDescriptionsPath + folderDescriptionId + @"\PropertyBag";
				WriteMachineString(nativePath, "ThisPCPolicy", visible ? "Show" : "Hide");
				if (Environment.Is64BitOperatingSystem)
					WriteMachineString(Wow6432FolderDescriptionsPath + folderDescriptionId + @"\PropertyBag", "ThisPCPolicy", visible ? "Show" : "Hide");
			}

			WriteDword(KnownFoldersPath + id, "System.IsPinnedToNameSpaceTree", visible ? 1 : 0);

			if (visible)
			{
				RestoreNamespaceEntry(MyComputerNameSpacePath, id);
				if (Environment.Is64BitOperatingSystem)
					RestoreNamespaceEntry(Wow6432MyComputerNameSpacePath, id);
			}
			else
			{
				HideNamespaceEntry(MyComputerNameSpacePath, id);
				if (Environment.Is64BitOperatingSystem)
					HideNamespaceEntry(Wow6432MyComputerNameSpacePath, id);
			}
		}

		private async void RestartExplorerButton_Click(object sender, RoutedEventArgs e)
		{
			try { await RestartExplorerAsync(); }
			catch (Exception exception)
			{
				MessageBox.Show("Не удалось перезапустить Проводник: " + exception.Message, "Проводник", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
		}

		private void RestartAsAdminButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				if (IsAdministrator())
				{
					MessageBox.Show("Приложение уже запущено с правами администратора.", "Настройки Windows", MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				var processPath = Process.GetCurrentProcess().MainModule.FileName;
				Process.Start(new ProcessStartInfo
				{
					FileName = processPath,
					UseShellExecute = true,
					Verb = "runas",
					WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
				});
				System.Windows.Application.Current.Shutdown();
			}
			catch (Exception exception)
			{
				MessageBox.Show("Перезапуск с правами администратора отменён или не выполнен: " + exception.Message, "Настройки Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
			}
		}

				private async Task RestartExplorerAsync()
		{
			await _explorerController.RestartAsync(CancellationToken.None);
		}


		private async void WebSearchToggleButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				using (var key = Registry.CurrentUser.CreateSubKey(SearchRegistryPath))
				{
					if (key == null)
					{
						WebSearchStatusText.Text = "Не удалось открыть настройки поиска.";
						return;
					}

					var currentValue = key.GetValue(SearchRegistryValue, 1);
					var isEnabled = Convert.ToInt32(currentValue) != 0;
					var value = isEnabled ? 0 : 1;
					key.SetValue(SearchRegistryValue, value, RegistryValueKind.DWord);
					key.SetValue(CortanaConsentValue, value, RegistryValueKind.DWord);
					key.SetValue(ConnectedSearchUseWebValue, value, RegistryValueKind.DWord);
				}

				await RestartWindowsSearchAsync();
				UpdateWebSearchStatus();
			}
			catch (Exception exception)
			{
				WebSearchStatusText.Text = "Не удалось изменить параметр поиска.";
				MessageBox.Show(
					"Не удалось изменить поиск Windows: " + exception.Message,
					"Настройки Windows",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
			}
		}

		private void UpdateWebSearchStatus()
		{
			try
			{
				using (var key = Registry.CurrentUser.OpenSubKey(SearchRegistryPath))
				{
					var value = key?.GetValue(SearchRegistryValue, 1);
					var enabled = Convert.ToInt32(value) != 0;
					WebSearchStatusText.Text = enabled
						? "Сейчас включён"
						: "Сейчас отключён";
					WebSearchToggleButton.Content = enabled
						? "Отключить поиск в интернете"
						: "Включить поиск в интернете";
				}
			}
			catch
			{
				WebSearchStatusText.Text = "Состояние недоступно";
				WebSearchToggleButton.Content = "Изменить параметр";
			}
		}

		private async Task RestartWindowsSearchAsync()
		{
			try
			{
				await _processRunner.RunAsync("taskkill.exe", "/F /IM SearchHost.exe", CancellationToken.None, false, 10);
			}
			catch { }
		}

		private void MouseSettingsButton_Click(object sender, RoutedEventArgs e)
		{
			OpenWindowsSettings("ms-settings:mousetouchpad");
		}

		private static void OpenWindowsSettings(string uri)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = uri,
					UseShellExecute = true
				});
			}
			catch (Exception exception)
			{
				MessageBox.Show(
					"Не удалось открыть параметры Windows: " + exception.Message,
					"Настройки Windows",
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
			}
		}

    }
}
