using System;
using System.IO;
using Microsoft.Win32;
using WpfApp1.Domain.WindowsSettings;
using WpfApp1.Infrastructure.Registry;

namespace WpfApp1.Services.WindowsSettings
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
                case "ShowUserFiles": return ReadDword(DesktopIconsPath, "{59031a47-3f72-44a7-89c5-5595fe6b30ee}", 1) == 0;
                case "ShowNetworkIcon": return ReadDword(DesktopIconsPath, "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", 1) == 0;
                case "ShowControlPanel": return ReadDword(DesktopIconsPath, "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", 1) == 0;
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

                case "GameBar": return ReadDword(GameConfigPath, "GameBarEnabled", 1) != 0;
                case "BackgroundRecording":
                    return ReadDword(GameDvrPath, "AppCaptureEnabled", 1) != 0
                        && ReadDword(GameConfigPath, "GameDVR_Enabled", 1) != 0
                        && ReadMachineDword(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 1) != 0;
                case "FullscreenOptimizations":
                    return ReadDword(GameConfigPath, "GameDVR_DXGIHonorFSEWindowsCompatible", 0) == 0
                        && ReadDword(GameConfigPath, "GameDVR_FSEBehavior", 0) == 0
                        && ReadDword(GameConfigPath, "GameDVR_FSEBehaviorMode", 0) == 0
                        && ReadDword(GameConfigPath, "GameDVR_HonorUserFSEBehaviorMode", 0) == 0;

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
                    case "ShowUserFiles": return SetUserDword("ShowUserFiles", DesktopIconsPath, "{59031a47-3f72-44a7-89c5-5595fe6b30ee}", enabled ? 0 : 1);
                    case "ShowNetworkIcon": return SetUserDword("ShowNetworkIcon", DesktopIconsPath, "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", enabled ? 0 : 1);
                    case "ShowControlPanel": return SetUserDword("ShowControlPanel", DesktopIconsPath, "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", enabled ? 0 : 1);
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
                    case "DeveloperMode": return SetMachineDword("DeveloperMode", AppModelUnlock, "AllowDevelopmentWithoutDevLicense", enabled ? 1 : 0);
                    case "LongPathsEnabled": return SetMachineDword("LongPathsEnabled", FileSystemPath, "LongPathsEnabled", enabled ? 1 : 0);

                    case "SpeedUpExplorerAndMenus":
                        {
                            var a = SetUserDword("SpeedUpExplorer", @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec", enabled ? 0 : 2000);
                            if (!a.Success) return a;
                            return SetUserString("SpeedUpMenus", @"Control Panel\Desktop", "MenuShowDelay", enabled ? "0" : "400");
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
            return SetUserDword("GameBarNexus", GameBarPath, "UseNexusForGameBarEnabled", enabled ? 1 : 0);
        }

        private SettingOperationResult ApplyBackgroundRecording(bool enabled)
        {
            var value = enabled ? 1 : 0;
            var r = SetUserDword("GameDvrAppCapture", GameDvrPath, "AppCaptureEnabled", value);
            if (!r.Success) return r;
            r = SetUserDword("GameDvrEnabled", GameConfigPath, "GameDVR_Enabled", value);
            if (!r.Success) return r;
            return SetMachineDword("GameDvrPolicy", @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", value);
        }

        private SettingOperationResult ApplyFullscreenOptimizations(bool enabled)
        {
            var values = enabled
                ? new[] { ("GameDVR_DXGIHonorFSEWindowsCompatible", 0), ("GameDVR_FSEBehavior", 0), ("GameDVR_FSEBehaviorMode", 0), ("GameDVR_HonorUserFSEBehaviorMode", 0) }
                : new[] { ("GameDVR_DXGIHonorFSEWindowsCompatible", 1), ("GameDVR_FSEBehavior", 2), ("GameDVR_FSEBehaviorMode", 2), ("GameDVR_HonorUserFSEBehaviorMode", 1) };
            foreach (var item in values)
            {
                var r = SetUserDword("Fullscreen_" + item.Item1, GameConfigPath, item.Item1, item.Item2);
                if (!r.Success) return r;
            }
            return SettingOperationResult.Ok("Настройка полноэкранной оптимизации сохранена.", true);
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
            const string inproc = basePath + @"\InprocServer32";
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
                    key.SetValue("InitialKeyboardIndicators", value, RegistryValueKind.String);
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

        private SettingOperationResult SetMachineDword(string backupName, string path, string name, int value)
        {
            _backup.BackupLocalMachineOnce(backupName, path, name);
            _registry.WriteLocalMachine(path, name, value, RegistryValueKind.DWord);
            return Convert.ToInt32(_registry.ReadLocalMachine(path, name).Value ?? int.MinValue) == value
                ? SettingOperationResult.Ok("Настройка сохранена.", true)
                : SettingOperationResult.Fail("Windows не сохранила выбранную настройку.");
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

        private int ReadUserString(string path, string name, string fallback)
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
