using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Win32;
using Nexora.Domain.WindowsSettings;
using Nexora.Infrastructure.Registry;

namespace Nexora.Services.WindowsSettings
{
    public sealed class WindowsInterfaceSettingsService
    {
        private const string ExplorerAdvanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string ContentDelivery = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private const string SearchPolicy = @"Software\Policies\Microsoft\Windows\Explorer";
        private const string SearchPath = @"Software\Microsoft\Windows\CurrentVersion\Search";
        private const string GameBarPath = @"Software\Microsoft\GameBar";
        private const string GameConfigPath = @"System\GameConfigStore";
        private const string GameDvrPath = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
        private const string AppModelUnlock = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock";
        private const string FileSystemPath = @"SYSTEM\CurrentControlSet\Control\FileSystem";
        private const string KeyboardPath = @"Control Panel\Keyboard";
        private const string DesktopIconsPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";
        private const string ShellIconsPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";
        private const string ToastPath = @"Software\Microsoft\Windows\CurrentVersion\PushNotifications";
        private const string ClipboardPath = @"Software\Microsoft\Clipboard";
        private const string NamingTemplatesPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\NamingTemplates";
        private const string DirectXUserGpuPreferences = @"Software\Microsoft\DirectX\UserGpuPreferences";
        private const string DirectXGlobalSettings = "DirectXUserGlobalSettings";
        private readonly RegistrySettingsStore _registry;
        private readonly SettingBackupService _backup;

        public WindowsInterfaceSettingsService(RegistrySettingsStore registry = null, SettingBackupService backup = null)
        {
            _registry = registry ?? new RegistrySettingsStore();
            _backup = backup ?? new SettingBackupService(_registry);
        }

        public bool IsEnabled(string tag)
        {
            switch (tag)
            {
                case "HideUserFiles": return ReadDword(DesktopIconsPath, "{59031a47-3f72-44a7-89c5-5595fe6b30ee}", 1) != 0;
                case "HideNetworkIcon": return ReadDword(DesktopIconsPath, "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", 1) != 0;
                case "HideControlPanel": return ReadDword(DesktopIconsPath, "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", 1) != 0;
                case "ShowDesktopIcons": return ReadDword(ExplorerAdvanced, "HideIcons", 0) == 0;
                case "ShortcutArrow": return !IsShortcutArrowVisible();
                case "ToastNotifications": return ReadDword(ToastPath, "ToastEnabled", 1) != 0;
                case "ClassicContextMenu": return IsClassicContextMenuEnabled();

                case "TaskbarWidgets": return ReadDword(ExplorerAdvanced, "TaskbarDa", 1) != 0;
                case "TaskbarTaskViewButton": return ReadDword(ExplorerAdvanced, "ShowTaskViewButton", 1) != 0;
                case "ExplorerSyncNotifications": return ReadDword(ExplorerAdvanced, "ShowSyncProviderNotifications", 1) != 0;
                case "SystemSuggestions": return ReadDword(ContentDelivery, "SystemPaneSuggestionsEnabled", 1) != 0;
                case "ExplorerCompactMode": return ReadDword(ExplorerAdvanced, "UseCompactMode", 0) != 0;
                case "SnapAssistFlyout": return ReadDword(ExplorerAdvanced, "EnableSnapAssistFlyout", 1) != 0;
                case "ExplorerItemCheckboxes": return ReadDword(ExplorerAdvanced, "AutoCheckSelect", 0) != 0;
                case "ClipboardHistory": return ReadDword(ClipboardPath, "EnableClipboardHistory", 0) != 0;
                case "WindowShake": return ReadDword(ExplorerAdvanced, "DisallowShaking", 0) == 0;

                case "GameBar":
                    return ReadDword(GameConfigPath, "GameBarEnabled", 1) != 0
                        && ReadOptionalUserDword(GameBarPath, "ShowStartupPanel", 1) == 1
                        && ReadOptionalUserDword(GameBarPath, "UseNexusForGameBarEnabled", 1) == 1
                        && ReadOptionalUserDword(GameBarPath, "GamePanelStartupTipIndex", 3) == 3;
                case "BackgroundRecording":
                    return ReadDword(GameDvrPath, "AppCaptureEnabled", 1) != 0
                        && ReadOptionalUserDword(GameDvrPath, "HistoricalCaptureEnabled", 1) == 1
                        && ReadDword(GameConfigPath, "GameDVR_Enabled", 1) != 0
                        && ReadOptionalUserDword(GameConfigPath, "GameDVR_HistoricalCaptureEnabled", 1) == 1
                        && ReadMachineDword(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 1) != 0;
                case "FullscreenOptimizations":
                    return IsFullscreenWindowedGamesOptimizationEnabled();

                        case "NumLockOnBoot":
                    return ReadDefaultUserDword("InitialKeyboardIndicators", 0) == 2
                        && ReadUserDword(KeyboardPath, "InitialKeyboardIndicators", 0) == 2;
                case "DeveloperMode": return ReadMachineDword(AppModelUnlock, "AllowDevelopmentWithoutDevLicense", 0) == 1;
                case "LongPathsEnabled": return ReadMachineDword(FileSystemPath, "LongPathsEnabled", 0) == 1;

                case "SpeedUpExplorerAndMenus":
                    return ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", -1) == 0
                        && string.Equals(ReadUserString(@"Control Panel\Desktop", "MenuShowDelay", "400"), "0", StringComparison.Ordinal);
                case "DisableStartMenuWebSearch":
                    return ReadUserDword(SearchPolicy, "DisableSearchBoxSuggestions", 0) == 1;
                case "DisableStartRecommended":
                    return ReadUserDword(SearchPolicy, "HideRecommendedSection", 0) == 1;
                case "DisableSettings365Ads":
                    return ReadMachineDword(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableConsumerAccountStateContent", 0) == 1;
                case "DisablePreinstalledApps":
                    return ReadDword(ContentDelivery, "PreInstalledAppsEnabled", 1) == 0
                        && ReadDword(ContentDelivery, "PreInstalledAppsEverEnabled", 1) == 0
                        && ReadDword(ContentDelivery, "OemPreInstalledAppsEnabled", 1) == 0
                        && ReadDword(ContentDelivery, "SilentInstalledAppsEnabled", 1) == 0;
                default:
                    return false;
            }
        }

        public SettingOperationResult Apply(string tag, bool enabled)
        {
            try
            {
                switch (tag)
                {
                    case "HideUserFiles": return SetUserDword("HideUserFiles", DesktopIconsPath, "{59031a47-3f72-44a7-89c5-5595fe6b30ee}", enabled ? 1 : 0);
                    case "HideNetworkIcon": return SetUserDword("HideNetworkIcon", DesktopIconsPath, "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", enabled ? 1 : 0);
                    case "HideControlPanel": return SetUserDword("HideControlPanel", DesktopIconsPath, "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", enabled ? 1 : 0);
                    case "ShowDesktopIcons": return SetUserDword("ShowDesktopIcons", ExplorerAdvanced, "HideIcons", enabled ? 0 : 1);
                    case "ShortcutArrow": return SetShortcutArrow(!enabled);
                    case "ToastNotifications":
                        {
                            var r = SetUserDword("ToastNotifications", ToastPath, "ToastEnabled", enabled ? 1 : 0);
                            if (!r.Success) return r;
                            return SetUserDword("ToastNotificationsLock", ToastPath, "LockScreenToastEnabled", enabled ? 1 : 0);
                        }
                    case "ClassicContextMenu": return SetClassicContextMenu(enabled);

                    case "TaskbarWidgets": return SetUserDword("TaskbarWidgets", ExplorerAdvanced, "TaskbarDa", enabled ? 1 : 0, true);
                    case "TaskbarTaskViewButton": return SetUserDword("TaskbarTaskViewButton", ExplorerAdvanced, "ShowTaskViewButton", enabled ? 1 : 0, true);
                    case "ExplorerSyncNotifications": return SetUserDword("ExplorerSyncNotifications", ExplorerAdvanced, "ShowSyncProviderNotifications", enabled ? 1 : 0);
                    case "SystemSuggestions": return SetUserDword("SystemSuggestions", ContentDelivery, "SystemPaneSuggestionsEnabled", enabled ? 1 : 0);
                    case "ExplorerCompactMode": return SetUserDword("ExplorerCompactMode", ExplorerAdvanced, "UseCompactMode", enabled ? 1 : 0);
                    case "SnapAssistFlyout": return SetUserDword("SnapAssistFlyout", ExplorerAdvanced, "EnableSnapAssistFlyout", enabled ? 1 : 0);
                    case "ExplorerItemCheckboxes": return SetUserDword("ExplorerItemCheckboxes", ExplorerAdvanced, "AutoCheckSelect", enabled ? 1 : 0);
                    case "ClipboardHistory": return SetUserDword("ClipboardHistory", ClipboardPath, "EnableClipboardHistory", enabled ? 1 : 0);
                    case "WindowShake": return SetUserDword("WindowShake", ExplorerAdvanced, "DisallowShaking", enabled ? 0 : 1);

                    case "GameBar": return ApplyGameBar(enabled);
                    case "BackgroundRecording": return ApplyBackgroundRecording(enabled);
                    case "FullscreenOptimizations": return ApplyFullscreenOptimizations(enabled);

                    case "NumLockOnBoot":
                        {
                            var r = SetUserDword("NumLockCurrentUser", KeyboardPath, "InitialKeyboardIndicators", enabled ? 2 : 0);
                            if (!r.Success) return r;
                            return SetDefaultUserKeyboardIndicators(enabled ? 2 : 0);
                        }
                    case "DeveloperMode": return SetMachineDword("DeveloperMode", AppModelUnlock, "AllowDevelopmentWithoutDevLicense", enabled ? 1 : 0, false);
                    case "LongPathsEnabled": return SetMachineDword("LongPathsEnabled", FileSystemPath, "LongPathsEnabled", enabled ? 1 : 0);

                    case "SpeedUpExplorerAndMenus":
                        {
                            const string serializePath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize";
                            const string desktopPath = @"Control Panel\Desktop";

                            if (!enabled)
                            {
                                var restoredDelay = _backup.TryRestoreCurrentUser("SpeedUpExplorer", serializePath, "StartupDelayInMSec");
                                var restoredMenu = _backup.TryRestoreCurrentUser("SpeedUpMenus", desktopPath, "MenuShowDelay");

                                if (!restoredDelay)
                                {
                                    var fallbackExplorer = SetUserDword("SpeedUpExplorerFallback", serializePath, "StartupDelayInMSec", 2000);
                                    if (!fallbackExplorer.Success) return fallbackExplorer;
                                }

                                if (!restoredMenu)
                                {
                                    var b = SetUserString("SpeedUpMenusFallback", desktopPath, "MenuShowDelay", "400");
                                    if (!b.Success) return b;
                                }

                                return IsEnabled("SpeedUpExplorerAndMenus")
                                    ? SettingOperationResult.Fail("Не удалось восстановить исходные задержки Проводника и меню.")
                                    : SettingOperationResult.Ok("Исходные задержки Проводника и меню восстановлены.");
                            }

                            var explorerWrite = SetUserDword("SpeedUpExplorer", serializePath, "StartupDelayInMSec", 0);
                            if (!explorerWrite.Success) return explorerWrite;
                            return SetUserString("SpeedUpMenus", desktopPath, "MenuShowDelay", "0");
                        }
                    case "DisableStartMenuWebSearch": return SetUserDword("DisableStartMenuWebSearch", SearchPolicy, "DisableSearchBoxSuggestions", enabled ? 1 : 0);
                    case "DisableStartRecommended": return SetUserDword("DisableStartRecommended", SearchPolicy, "HideRecommendedSection", enabled ? 1 : 0);
                    case "DisableSettings365Ads": return SetMachineDword("DisableSettings365Ads", @"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableConsumerAccountStateContent", enabled ? 1 : 0);
                    case "DisablePreinstalledApps":
                        {
                            var value = enabled ? 0 : 1;
                            var r = SetUserDword("PreinstalledApps", ContentDelivery, "PreInstalledAppsEnabled", value);
                            if (!r.Success) return r;
                            r = SetUserDword("PreinstalledAppsEver", ContentDelivery, "PreInstalledAppsEverEnabled", value);
                            if (!r.Success) return r;
                            r = SetUserDword("PreinstalledAppsOEM", ContentDelivery, "OemPreInstalledAppsEnabled", value);
                            if (!r.Success) return r;
                            return SetUserDword("PreinstalledAppsSilent", ContentDelivery, "SilentInstalledAppsEnabled", value);
                        }
                    default:
                        return SettingOperationResult.Fail("Неизвестная системная настройка: " + tag);
                }
            }
            catch (Exception ex)
            {
                return SettingOperationResult.Fail(ex.Message);
            }
        }

        private SettingOperationResult ApplyGameBar(bool enabled)
        {
            var r = SetUserDword("GameBarEnabled", GameConfigPath, "GameBarEnabled", enabled ? 1 : 0);
            if (!r.Success) return r;
            r = SetUserDword("GameBarStartupPanel", GameBarPath, "ShowStartupPanel", enabled ? 1 : 0);
            if (!r.Success) return r;
            r = SetUserDword("GameBarNexus", GameBarPath, "UseNexusForGameBarEnabled", enabled ? 1 : 0);
            if (!r.Success) return r;
            return SetUserDword("GameBarStartupTip", GameBarPath, "GamePanelStartupTipIndex", enabled ? 3 : 0);
        }
        private SettingOperationResult ApplyBackgroundRecording(bool enabled)
        {
            var value = enabled ? 1 : 0;
            var r = SetUserDword("GameDvrAppCapture", GameDvrPath, "AppCaptureEnabled", value);
            if (!r.Success) return r;
            r = SetUserDword("GameDvrHistorical", GameDvrPath, "HistoricalCaptureEnabled", value);
            if (!r.Success) return r;
            r = SetUserDword("GameDvrEnabled", GameConfigPath, "GameDVR_Enabled", value);
            if (!r.Success) return r;
            r = SetUserDword("GameDvrHistoricalConfig", GameConfigPath, "GameDVR_HistoricalCaptureEnabled", value);
            if (!r.Success) return r;
            return SetMachineDword("GameDvrPolicy", @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", value);
        }

        private SettingOperationResult ApplyFullscreenOptimizations(bool enabled)
        {
            try
            {
                _backup.BackupCurrentUserOnce("FullscreenWindowedGames", DirectXUserGpuPreferences, DirectXGlobalSettings);
                var current = ReadUserString(DirectXUserGpuPreferences, DirectXGlobalSettings, string.Empty);
                var updated = SetSemicolonSetting(current, "SwapEffectUpgradeEnable", enabled ? "1" : "0");
                _registry.WriteCurrentUser(DirectXUserGpuPreferences, DirectXGlobalSettings, updated, RegistryValueKind.String);

                return IsFullscreenWindowedGamesOptimizationEnabled() == enabled
                    ? SettingOperationResult.Ok("Настройка «Оптимизация для игр в оконном режиме» сохранена.")
                    : SettingOperationResult.Fail("Windows не сохранила настройку «Оптимизация для игр в оконном режиме».");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private bool IsFullscreenWindowedGamesOptimizationEnabled()
        {
            var current = ReadUserString(DirectXUserGpuPreferences, DirectXGlobalSettings, string.Empty);
            foreach (var part in (current ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split(new[] { '=' }, 2);
                if (pair.Length != 2 || !string.Equals(pair[0].Trim(), "SwapEffectUpgradeEnable", StringComparison.OrdinalIgnoreCase))
                    continue;
                return string.Equals(pair[1].Trim(), "1", StringComparison.Ordinal);
            }
            return false;
        }

        private static string SetSemicolonSetting(string current, string name, string value)
        {
            var parts = new List<string>();
            var found = false;
            foreach (var part in (current ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split(new[] { '=' }, 2);
                if (pair.Length == 2 && string.Equals(pair[0].Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    if (!found) parts.Add(name + "=" + value);
                    found = true;
                }
                else
                {
                    var trimmed = part.Trim();
                    if (!string.IsNullOrWhiteSpace(trimmed))
                        parts.Add(trimmed);
                }
            }

            if (!found) parts.Add(name + "=" + value);
            return string.Join(";", parts) + ";";
        }

        private SettingOperationResult SetShortcutArrow(bool visible)
        {
            var backupName = "ShortcutArrow";
            _backup.BackupLocalMachineOnce(backupName, ShellIconsPath, "29");
            if (visible)
                _registry.DeleteLocalMachine(ShellIconsPath, "29");
            else
                _registry.WriteLocalMachine(ShellIconsPath, "29", @"%windir%\System32\shell32.dll,-50", RegistryValueKind.String);

            return IsShortcutArrowVisible() == visible
                ? SettingOperationResult.Ok("Стрелки ярлыков сохранены.", true)
                : SettingOperationResult.Fail("Windows не сохранила настройку стрелок ярлыков.");
        }

        private bool IsShortcutArrowVisible()
        {
            var snapshot = _registry.ReadLocalMachine(ShellIconsPath, "29");
            if (!snapshot.Exists || snapshot.Value == null) return true;
            var value = Convert.ToString(snapshot.Value) ?? string.Empty;
            if (value.IndexOf("blank.ico", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("transparent.ico", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return value.IndexOf("shell32.dll,-50", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private SettingOperationResult SetClassicContextMenu(bool enabled)
        {
            const string basePath = @"SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
            if (enabled)
            {
                _registry.WriteCurrentUser(basePath + @"\InprocServer32", "", "", RegistryValueKind.String);
            }
            else
            {
                _registry.DeleteCurrentUser(basePath, "");
                using (var parent = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Classes\CLSID", true))
                    parent?.DeleteSubKeyTree("{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", false);
            }
            return IsClassicContextMenuEnabled() == enabled
                ? SettingOperationResult.Ok("Классическое контекстное меню сохранено.", true)
                : SettingOperationResult.Fail("Windows не сохранила классическое контекстное меню.");
        }

        private bool IsClassicContextMenuEnabled()
        {
            const string path = @"SOFTWARE\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32";
            using (var key = Registry.CurrentUser.OpenSubKey(path))
                return key != null;
        }

        private SettingOperationResult SetDefaultUserKeyboardIndicators(int value)
        {
            try
            {
                using (var key = Registry.Users.OpenSubKey(@".DEFAULT\Control Panel\Keyboard", true))
                {
                    if (key == null) return SettingOperationResult.Fail("Не удалось открыть параметры клавиатуры для экрана входа.");
                    key.SetValue("InitialKeyboardIndicators", value.ToString(), RegistryValueKind.String);
                }
                return SettingOperationResult.Ok("NumLock при загрузке сохранён.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private SettingOperationResult SetUserDword(string backupName, string path, string name, int value, bool restart = false)
        {
            _backup.BackupCurrentUserOnce(backupName, path, name);
            _registry.WriteCurrentUser(path, name, value, RegistryValueKind.DWord);
            return Convert.ToInt32(_registry.ReadCurrentUser(path, name).Value ?? int.MinValue) == value
                ? SettingOperationResult.Ok("Настройка сохранена.", restart)
                : SettingOperationResult.Fail("Windows не сохранила выбранную настройку.");
        }

        private SettingOperationResult SetUserString(string backupName, string path, string name, string value)
        {
            _backup.BackupCurrentUserOnce(backupName, path, name);
            _registry.WriteCurrentUser(path, name, value, RegistryValueKind.String);
            return string.Equals(Convert.ToString(_registry.ReadCurrentUser(path, name).Value), value, StringComparison.OrdinalIgnoreCase)
                ? SettingOperationResult.Ok("Настройка сохранена.")
                : SettingOperationResult.Fail("Windows не сохранила выбранное значение.");
        }

        private SettingOperationResult SetMachineDword(string backupName, string path, string name, int value, bool restart = true)
        {
            _backup.BackupLocalMachineOnce(backupName, path, name);
            _registry.WriteLocalMachine(path, name, value, RegistryValueKind.DWord);
            return Convert.ToInt32(_registry.ReadLocalMachine(path, name).Value ?? int.MinValue) == value
                ? SettingOperationResult.Ok("Настройка сохранена.", restart)
                : SettingOperationResult.Fail("Windows не сохранила выбранную настройку.");
        }

        private int ReadOptionalUserDword(string path, string name, int fallback)
        {
            var snapshot = _registry.ReadCurrentUser(path, name);
            return snapshot.Exists ? ConvertToInt(snapshot.Value, fallback) : fallback;
        }

        private int ReadDword(string path, string name, int fallback) =>
            ConvertToInt(_registry.ReadCurrentUser(path, name).Value, fallback);

        private int ReadMachineDword(string path, string name, int fallback) =>
            ConvertToInt(_registry.ReadLocalMachine(path, name).Value, fallback);

        private int ReadUserDword(string path, string name, int fallback) =>
            ConvertToInt(_registry.ReadCurrentUser(path, name).Value, fallback);

        private int ReadDefaultUserDword(string name, int fallback)
        {
            try
            {
                using (var key = Registry.Users.OpenSubKey(@".DEFAULT\Control Panel\Keyboard"))
                    return ConvertToInt(key?.GetValue(name), fallback);
            }
            catch { return fallback; }
        }

        private string ReadUserString(string path, string name, string fallback)
        {
            var snapshot = _registry.ReadCurrentUser(path, name);
            return snapshot.Exists && snapshot.Value != null ? Convert.ToString(snapshot.Value) ?? fallback : fallback;
        }

        private static int ConvertToInt(object value, int fallback)
        {
            try { return value == null ? fallback : Convert.ToInt32(value); } catch { return fallback; }
        }
    }
}
