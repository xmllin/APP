using System;
using Microsoft.Win32;
using WpfApp1.Domain.WindowsSettings;
using WpfApp1.Infrastructure.Registry;

namespace WpfApp1.Services.WindowsSettings
{
    public sealed class TaskbarSettingsState
    {
        public bool AutoHide { get; set; }
        public bool Badges { get; set; }
        public bool Flashing { get; set; }
        public bool MultiMonitor { get; set; }
        public bool ShareWindow { get; set; }
        public bool ShowDesktop { get; set; }
        public bool EndTask { get; set; }
        public bool Widgets { get; set; }
        public bool TaskViewButton { get; set; }
        public bool LastActiveClick { get; set; }
        public int SearchBoxTaskbarMode { get; set; }
        public int Alignment { get; set; }
        public int MultiMonitorMode { get; set; }
        public int GroupingMode { get; set; }
        public int MultiMonitorGroupingMode { get; set; }
    }

    public sealed class TaskbarSettingsService
    {
        private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private const string AlignmentPath = Advanced + @"\TaskbarAl";
        private const string AutoHidePath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StuckRects3";
        private const string BadgesPath = Advanced + @"\TaskbarBadges";
        private const string FlashingPath = Advanced + @"\TaskbarFlashing";
        private const string SharePath = Advanced + @"\TaskbarSn";
        private const string ShowDesktopPath = Advanced + @"\TaskbarSd";
        private const string GlomPath = Advanced + @"\TaskbarGlomLevel";
        private const string MultiMonitorPath = Advanced + @"\MMTaskbarEnabled";
        private const string MultiMonitorModePath = Advanced + @"\MMTaskbarMode";
        private const string MultiMonitorGlomPath = Advanced + @"\MMTaskbarGlomLevel";
        private const string DeveloperPath = Advanced + @"\TaskbarDeveloperSettings";

        private readonly RegistrySettingsStore _registry;
        private readonly SettingBackupService _backup;

        public TaskbarSettingsService(RegistrySettingsStore registry = null, SettingBackupService backup = null)
        {
            _registry = registry ?? new RegistrySettingsStore();
            _backup = backup ?? new SettingBackupService(_registry);
        }

        public TaskbarSettingsState ReadState()
        {
            return new TaskbarSettingsState
            {
                AutoHide = ReadAutoHide(),
                Badges = ReadBool(BadgesPath, "SystemSettings_Taskbar_Badging", "SystemSettings_DesktopTaskbar_Badging", true),
                Flashing = ReadBool(FlashingPath, "SystemSettings_DesktopTaskbar_Flashing", null, true),
                MultiMonitor = ReadBool(MultiMonitorPath, "SystemSettings_Taskbar_MultiMon", "SystemSettings_DesktopTaskbar_MultiMon", true),
                ShareWindow = ReadBool(SharePath, "SystemSettings_DesktopTaskbar_Sn", null, true),
                ShowDesktop = ReadBool(ShowDesktopPath, "SystemSettings_DesktopTaskbar_Sd", null, true),
                EndTask = Convert.ToInt32(_registry.ReadCurrentUser(DeveloperPath, "TaskbarEndTask").Value ?? 0) == 1,
                Widgets = ReadBool(Advanced, "TaskbarDa", null, true),
                TaskViewButton = ReadBool(Advanced, "ShowTaskViewButton", null, true),
                LastActiveClick = ReadBool(Advanced, "LastActiveClick", null, false),
                SearchBoxTaskbarMode = ReadIntWithFallback(@"Software\Microsoft\Windows\CurrentVersion\Search", "SearchboxTaskbarMode", 3),
                Alignment = ReadIntWithFallback(AlignmentPath, "TaskbarAl", 1, "SystemSettings_DesktopTaskbar_Al"),
                MultiMonitorMode = ReadIntWithFallback(MultiMonitorModePath, "MMTaskbarMode", 0, "SystemSettings_Taskbar_MultiMonTaskbarMode", "SystemSettings_DesktopTaskbar_MultiMonTaskbarMode"),
                GroupingMode = ReadIntWithFallback(GlomPath, "TaskbarGlomLevel", 0, "SystemSettings_DesktopTaskbar_GroupingMode"),
                MultiMonitorGroupingMode = ReadIntWithFallback(MultiMonitorGlomPath, "MMTaskbarGlomLevel", 0, "SystemSettings_DesktopTaskbar_GroupingMode")
            };
        }

        public SettingOperationResult SetAlignment(int value)
        {
            return WriteDwordSetting("TaskbarAlignment", AlignmentPath, "TaskbarAl", Math.Clamp(value, 0, 1), true);
        }

        public SettingOperationResult SetMultiMonitorMode(int value)
        {
            return WriteDwordSetting("TaskbarMultiMonitorMode", MultiMonitorModePath, "MMTaskbarMode", Math.Clamp(value, 0, 2), true);
        }

        public SettingOperationResult SetGroupingMode(int value)
        {
            return WriteDwordSetting("TaskbarGroupingMode", GlomPath, "TaskbarGlomLevel", Math.Clamp(value, 0, 2), true);
        }

        public SettingOperationResult SetMultiMonitorGroupingMode(int value)
        {
            return WriteDwordSetting("TaskbarMultiMonitorGroupingMode", MultiMonitorGlomPath, "MMTaskbarGlomLevel", Math.Clamp(value, 0, 2), true);
        }

        public SettingOperationResult SetAutoHide(bool enabled)
        {
            try
            {
                var names = new[] { "SystemSettings_DesktopTaskbar_Autohide", "SystemSettings_Taskbar_Autohide", "Settings" };
                var found = false;
                foreach (var name in names)
                {
                    var snapshot = _registry.ReadCurrentUser(AutoHidePath, name);
                    if (!snapshot.Exists || snapshot.Value is not byte[] source || source.Length <= 8) continue;
                    _backup.BackupCurrentUserOnce("TaskbarAutoHide_" + name, AutoHidePath, name);
                    var bytes = (byte[])source.Clone();
                    if (bytes[8] == 122 || bytes[8] == 123)
                        bytes[8] = (byte)(enabled ? 123 : 122);
                    else if (bytes[8] == 2 || bytes[8] == 3)
                        bytes[8] = (byte)(enabled ? 3 : 2);
                    else
                        throw new InvalidOperationException("Не распознан формат бинарных настроек автоскрытия панели задач.");
                    _registry.WriteCurrentUser(AutoHidePath, name, bytes, RegistryValueKind.Binary);
                    found = true;
                }

                if (!found) return SettingOperationResult.Fail("Не найден поддерживаемый бинарный блок автоскрытия панели задач.");
                if (ReadAutoHide() != enabled) return SettingOperationResult.Fail("Windows не сохранила выбранное состояние автоскрытия панели задач.");
                return SettingOperationResult.Ok("Автоскрытие панели задач сохранено.", true);
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public SettingOperationResult SetBadges(bool enabled)
        {
            return SetBool("TaskbarBadges", BadgesPath, new[] { "SystemSettings_Taskbar_Badging", "SystemSettings_DesktopTaskbar_Badging" }, enabled);
        }

        public SettingOperationResult SetFlashing(bool enabled)
        {
            return SetBool("TaskbarFlashing", FlashingPath, new[] { "SystemSettings_DesktopTaskbar_Flashing" }, enabled);
        }

        public SettingOperationResult SetMultiMonitor(bool enabled)
        {
            var result = SetBool("TaskbarMultiMonitor", MultiMonitorPath, new[] { "SystemSettings_Taskbar_MultiMon", "SystemSettings_DesktopTaskbar_MultiMon" }, enabled);
            if (!result.Success) return result;
            try
            {
                _backup.BackupCurrentUserOnce("TaskbarMultiMonitor_Legacy", MultiMonitorPath, "MMTaskbarEnabled");
                _registry.WriteCurrentUser(MultiMonitorPath, "MMTaskbarEnabled", enabled ? 1 : 0, RegistryValueKind.DWord);
                return VerifyBool(MultiMonitorPath, new[] { "SystemSettings_Taskbar_MultiMon", "SystemSettings_DesktopTaskbar_MultiMon" }, enabled)
                    ? SettingOperationResult.Ok("Показ панели задач на всех дисплеях сохранён.", true)
                    : SettingOperationResult.Fail("Windows не сохранила настройку нескольких дисплеев.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public SettingOperationResult SetShareWindow(bool enabled)
        {
            return SetBool("TaskbarShareWindow", SharePath, new[] { "SystemSettings_DesktopTaskbar_Sn" }, enabled);
        }

        public SettingOperationResult SetShowDesktop(bool enabled)
        {
            return SetBool("TaskbarShowDesktop", ShowDesktopPath, new[] { "SystemSettings_DesktopTaskbar_Sd" }, enabled);
        }

        public SettingOperationResult SetWidgets(bool enabled)
        {
            return SetBool("TaskbarWidgets", Advanced, new[] { "TaskbarDa" }, enabled);
        }

        public SettingOperationResult SetTaskViewButton(bool enabled)
        {
            return SetBool("TaskbarTaskViewButton", Advanced, new[] { "ShowTaskViewButton" }, enabled);
        }

        public SettingOperationResult SetLastActiveClick(bool enabled)
        {
            return SetBool("TaskbarLastActiveClick", Advanced, new[] { "LastActiveClick" }, enabled);
        }

        public SettingOperationResult SetSearchBoxTaskbarMode(int value)
        {
            return WriteStringSetting(
                "TaskbarSearchBoxMode",
                @"Software\Microsoft\Windows\CurrentVersion\Search",
                "SearchboxTaskbarMode",
                Math.Clamp(value, 0, Environment.OSVersion.Version.Build >= 22000 ? 3 : 2),
                true);
        }

        public SettingOperationResult SetEndTask(bool enabled)
        {
            try
            {
                _backup.BackupCurrentUserOnce("TaskbarEndTask", DeveloperPath, "TaskbarEndTask");
                _registry.WriteCurrentUser(DeveloperPath, "TaskbarEndTask", enabled ? 1 : 0, RegistryValueKind.DWord);
                var value = _registry.ReadCurrentUser(DeveloperPath, "TaskbarEndTask").Value;
                return Convert.ToInt32(value ?? 0) == (enabled ? 1 : 0)
                    ? SettingOperationResult.Ok("Завершение задач из панели задач сохранено.")
                    : SettingOperationResult.Fail("Windows не сохранила настройку завершения задач.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private SettingOperationResult WriteDwordSetting(string backupName, string path, string name, int value, bool restart)
        {
            try
            {
                _backup.BackupCurrentUserOnce(backupName, path, name);
                _registry.WriteCurrentUser(path, name, value, RegistryValueKind.DWord);
                var actual = _registry.ReadCurrentUser(path, name).Value;
                return ConvertToInt(actual, int.MinValue) == value
                    ? SettingOperationResult.Ok("Настройка панели задач сохранена.", restart)
                    : SettingOperationResult.Fail("Windows не сохранила выбранное значение панели задач.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private SettingOperationResult WriteStringSetting(string backupName, string path, string primaryName, int value, bool restart, string secondaryName = null)
        {
            try
            {
                _backup.BackupCurrentUserOnce(backupName + "_Primary", path, primaryName);
                _registry.WriteCurrentUser(path, primaryName, value.ToString(), RegistryValueKind.String);
                if (!string.IsNullOrWhiteSpace(secondaryName))
                {
                    _backup.BackupCurrentUserOnce(backupName + "_Secondary", path, secondaryName);
                    _registry.WriteCurrentUser(path, secondaryName, value.ToString(), RegistryValueKind.String);
                }
                var state = ReadIntString(path, primaryName, -1, secondaryName);
                return state == value
                    ? SettingOperationResult.Ok("Настройка панели задач сохранена.", restart)
                    : SettingOperationResult.Fail("Windows не сохранила выбранное значение панели задач.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private int ReadIntString(string path, string primaryName, int fallback, params string[] secondaryNames)
        {
            var first = _registry.ReadCurrentUser(path, primaryName);
            if (TryParseInt(first.Value, out var value)) return Math.Max(0, value);

            foreach (var name in secondaryNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var snapshot = _registry.ReadCurrentUser(path, name);
                if (TryParseInt(snapshot.Value, out value)) return Math.Max(0, value);
            }

            return fallback;
        }

        private SettingOperationResult SetBool(string backupName, string path, string[] names, bool enabled)
        {
            try
            {
                foreach (var name in names)
                {
                    _backup.BackupCurrentUserOnce(backupName + "_" + name, path, name);
                    _registry.WriteCurrentUser(path, name, enabled ? "1" : "0", RegistryValueKind.String);
                }
                return VerifyBool(path, names, enabled)
                    ? SettingOperationResult.Ok("Настройка панели задач сохранена.", true)
                    : SettingOperationResult.Fail("Windows не сохранила выбранное состояние панели задач.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private bool VerifyBool(string path, string[] names, bool expected)
        {
            foreach (var name in names)
            {
                var snapshot = _registry.ReadCurrentUser(path, name);
                if (!snapshot.Exists || !string.Equals(Convert.ToString(snapshot.Value), expected ? "1" : "0", StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private bool ReadBool(string path, string primary, string secondary, bool fallback)
        {
            var first = _registry.ReadCurrentUser(path, primary);
            if (first.Exists) return Convert.ToString(first.Value) == "1";
            if (!string.IsNullOrWhiteSpace(secondary))
            {
                var second = _registry.ReadCurrentUser(path, secondary);
                if (second.Exists) return Convert.ToString(second.Value) == "1";
            }
            return fallback;
        }

        private int ReadIntWithFallback(string path, string primary, int fallback, params string[] secondaryNames)
        {
            var first = _registry.ReadCurrentUser(path, primary);
            if (TryParseInt(first.Value, out var value)) return Math.Max(0, value);

            foreach (var secondary in secondaryNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(secondary)) continue;
                var snapshot = _registry.ReadCurrentUser(path, secondary);
                if (TryParseInt(snapshot.Value, out value)) return Math.Max(0, value);
            }
            return fallback;
        }

        private static bool TryParseInt(object value, out int result)
        {
            if (value is int i) { result = i; return true; }
            return int.TryParse(Convert.ToString(value), out result);
        }

        private static int ConvertToInt(object value, int fallback)
        {
            try { return value == null ? fallback : Convert.ToInt32(value); } catch { return fallback; }
        }

        private bool ReadAutoHide()
        {
            var names = new[] { "SystemSettings_DesktopTaskbar_Autohide", "SystemSettings_Taskbar_Autohide", "Settings" };
            bool? result = null;
            foreach (var name in names)
            {
                var snapshot = _registry.ReadCurrentUser(AutoHidePath, name);
                var bytes = snapshot.Value as byte[];
                if (bytes == null || bytes.Length <= 8) continue;
                bool? current = null;
                if (bytes[8] == 123 || bytes[8] == 3) current = true;
                else if (bytes[8] == 122 || bytes[8] == 2) current = false;
                if (!current.HasValue) continue;
                if (result.HasValue && result.Value != current.Value) return false;
                result = current;
            }
            return result.GetValueOrDefault(false);
        }
    }
}
