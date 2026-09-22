using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Nexora.Domain.WindowsSettings;
using Nexora.Infrastructure.FileSystem;
using Nexora.Infrastructure.Processes;
using Nexora.Infrastructure.Registry;

namespace Nexora.Services.WindowsSettings
{
    public sealed class WindowsUpdateService
    {
        private const string PolicyPath = @"Software\Policies\Microsoft\Windows\WindowsUpdate";
        private const string SettingsPath = @"Software\Microsoft\WindowsUpdate\UX\Settings";
        private const string StatusPath = @"Software\Microsoft\WindowsUpdate\UpdatePolicy\Settings";
        private const string HostsBackupName = "windows-update-hosts";
        private readonly RegistrySettingsStore _registry;
        private readonly SettingBackupService _backup;
        private readonly FileBackupService _fileBackup;
        private readonly SystemProcessRunner _processes;

        public WindowsUpdateService(RegistrySettingsStore registry = null, SettingBackupService backup = null, FileBackupService fileBackup = null, SystemProcessRunner processes = null)
        {
            _registry = registry ?? new RegistrySettingsStore();
            _backup = backup ?? new SettingBackupService(_registry);
            _fileBackup = fileBackup ?? new FileBackupService();
            _processes = processes ?? new SystemProcessRunner();
        }

        public async Task<SettingOperationResult> SetDisabledAsync(bool disabled, CancellationToken token)
        {
            try
            {
                var hosts = GetHostsPath();
                if (disabled)
                {
                    BackupMachine(PolicyPath, "NoAutoUpdate");
                    BackupMachine(PolicyPath, "AUOptions");
                    BackupMachine(PolicyPath, "DoNotConnectToWindowsUpdateInternetLocations");
                    BackupMachine(PolicyPath, "DisableWindowsUpdateAccess");
                    BackupMachine(PolicyPath, "DisableDualScan");
                    BackupMachine(PolicyPath + @"\AU", "NoAutoUpdate");
                    foreach (var service in new[] { "wuauserv", "UsoSvc", "WaaSMedicSvc" }) BackupMachine(@"SYSTEM\CurrentControlSet\Services\" + service, "Start");
                    _fileBackup.BackupOnce(HostsBackupName, hosts);

                    WriteMachine(PolicyPath, "DoNotConnectToWindowsUpdateInternetLocations", 1);
                    WriteMachine(PolicyPath, "DisableWindowsUpdateAccess", 1);
                    WriteMachine(PolicyPath, "DisableDualScan", 1);
                    WriteMachine(PolicyPath, "NoAutoUpdate", 1);
                    WriteMachine(PolicyPath, "AUOptions", 1);
                    WriteMachine(PolicyPath + @"\AU", "NoAutoUpdate", 1);
                    WriteMachine(@"SYSTEM\CurrentControlSet\Services\wuauserv", "Start", 4);
                    WriteMachine(@"SYSTEM\CurrentControlSet\Services\UsoSvc", "Start", 4);
                    WriteMachine(@"SYSTEM\CurrentControlSet\Services\WaaSMedicSvc", "Start", 4);
                    ApplyHostsBlock(true);

                    await RunBestEffortAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\WindowsUpdate\\Scheduled Start\" /disable", token);
                    await RunBestEffortAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\UpdateOrchestrator\\Universal Orchestrator Start\" /disable", token);
                    foreach (var command in new[]
                    {
                        ("taskkill.exe", "/f /im wuauclt.exe"),
                        ("taskkill.exe", "/f /im updatenotificationmgr.exe"),
                        ("net.exe", "stop wuauserv /y"),
                        ("net.exe", "stop bits /y"),
                        ("net.exe", "stop UsoSvc /y"),
                        ("cmd.exe", "/c ipconfig /flushdns")
                    }) await RunBestEffortAsync(command.Item1, command.Item2, token);
                }
                else
                {
                    RestoreMachine(PolicyPath, "NoAutoUpdate");
                    RestoreMachine(PolicyPath, "AUOptions");
                    RestoreMachine(PolicyPath, "DoNotConnectToWindowsUpdateInternetLocations");
                    RestoreMachine(PolicyPath, "DisableWindowsUpdateAccess");
                    RestoreMachine(PolicyPath, "DisableDualScan");
                    RestoreMachine(PolicyPath + @"\AU", "NoAutoUpdate");
                    foreach (var service in new[] { "wuauserv", "UsoSvc", "WaaSMedicSvc" }) RestoreMachine(@"SYSTEM\CurrentControlSet\Services\" + service, "Start");
                    ApplyHostsBlock(false);
                    await RunBestEffortAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\WindowsUpdate\\Scheduled Start\" /enable", token);
                    await RunBestEffortAsync("schtasks.exe", "/change /tn \"\\Microsoft\\Windows\\UpdateOrchestrator\\Universal Orchestrator Start\" /enable", token);
                    await RunBestEffortAsync("net.exe", "start UsoSvc", token);
                    await RunBestEffortAsync("cmd.exe", "/c ipconfig /flushdns", token);
                }

                var state = ReadDisabledState();
                return state == disabled
                    ? SettingOperationResult.Ok(disabled ? "Windows Update отключён." : "Windows Update восстановлен.", true)
                    : SettingOperationResult.Fail("Не удалось подтвердить состояние Windows Update после изменения.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public bool IsPauseLimitMaximized()
        {
            var value = _registry.ReadLocalMachine(SettingsPath, "FlightSettingsMaxPauseDays").Value;
            return value != null && Convert.ToInt64(value) == -1L;
        }

        public SettingOperationResult MaximizePauseLimit()
        {
            try
            {
                _backup.BackupLocalMachineOnce("WU_FlightSettingsMaxPauseDays", SettingsPath, "FlightSettingsMaxPauseDays");
                _registry.WriteLocalMachine(SettingsPath, "FlightSettingsMaxPauseDays", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                return IsPauseLimitMaximized()
                    ? SettingOperationResult.Ok("Максимальный срок паузы Windows Update установлен.")
                    : SettingOperationResult.Fail("Windows не сохранила максимальный срок паузы обновлений.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public void PauseUntil(DateTime expiryUtc)
        {
            var start = new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var expiry = expiryUtc.ToUniversalTime();
            var startValue = start.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            var expiryValue = expiry.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            foreach (var name in new[] { "PauseUpdatesStartTime", "PauseFeatureUpdatesStartTime", "PauseQualityUpdatesStartTime" })
            {
                _backup.BackupCurrentUserOnce("WU_" + name, SettingsPath, name);
                WriteUser(SettingsPath, name, startValue);
            }
            foreach (var name in new[] { "PauseUpdatesExpiryTime", "PauseFeatureUpdatesExpiryTime", "PauseQualityUpdatesExpiryTime", "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesEndTime" })
            {
                _backup.BackupCurrentUserOnce("WU_" + name, SettingsPath, name);
                WriteUser(SettingsPath, name, expiryValue);
            }
            _backup.BackupCurrentUserOnce("WU_PausedFeatureStatus", StatusPath, "PausedFeatureStatus");
            _backup.BackupCurrentUserOnce("WU_PausedQualityStatus", StatusPath, "PausedQualityStatus");
            _backup.BackupCurrentUserOnce("WU_PausedFeatureDate", StatusPath, "PausedFeatureDate");
            _backup.BackupCurrentUserOnce("WU_PausedQualityDate", StatusPath, "PausedQualityDate");
            WriteUserDword(StatusPath, "PausedFeatureStatus", 1);
            WriteUserDword(StatusPath, "PausedQualityStatus", 1);
            WriteUser(StatusPath, "PausedFeatureDate", expiry.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            WriteUser(StatusPath, "PausedQualityDate", expiry.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        public DateTime? GetPauseExpiry()
        {
            var values = new[] { "PauseUpdatesExpiryTime", "PauseFeatureUpdatesExpiryTime", "PauseQualityUpdatesExpiryTime", "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesEndTime" }
                .Select(name => _registry.ReadCurrentUser(SettingsPath, name).Value as string)
                .Where(value => !string.IsNullOrWhiteSpace(value));
            DateTime? latest = null;
            foreach (var value in values)
            {
                if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) && (!latest.HasValue || parsed > latest.Value))
                    latest = parsed;
            }
            return latest;
        }

        public async Task<SettingOperationResult> StartServiceAsync(CancellationToken token)
        {
            try
            {
                var result = await _processes.RunElevatedAsync("net.exe", "start wuauserv", token, 60).ConfigureAwait(false);
                return result.ExitCode == 0
                    ? SettingOperationResult.Ok("Служба Windows Update запущена.")
                    : SettingOperationResult.Fail(string.IsNullOrWhiteSpace(result.StandardError) ? "Не удалось запустить службу Windows Update." : result.StandardError.Trim());
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public async Task ClearCacheAsync(CancellationToken token)
        {
            var command = "/c del /f /s /q \"%windir%\\SoftwareDistribution\\Download\\*\"";
            var result = await _processes.RunElevatedAsync("cmd.exe", command, token, 120).ConfigureAwait(false);
            if (result.ExitCode != 0) throw new InvalidOperationException(result.StandardError);
        }

        private bool ReadDisabledState()
        {
            return Convert.ToInt32(_registry.ReadLocalMachine(PolicyPath, "NoAutoUpdate").Value ?? 0) == 1
                && Convert.ToInt32(_registry.ReadLocalMachine(PolicyPath, "AUOptions").Value ?? 0) == 1;
        }

        private void BackupMachine(string path, string name) => _backup.BackupLocalMachineOnce("WU_" + path + "_" + name, path, name);

        private void RestoreMachine(string path, string name) => _backup.TryRestoreLocalMachine("WU_" + path + "_" + name, path, name);

        private void WriteMachine(string path, string name, int value) => _registry.WriteLocalMachine(path, name, value, RegistryValueKind.DWord);
        private void WriteUser(string path, string name, string value) => _registry.WriteCurrentUser(path, name, value, RegistryValueKind.String);
        private void WriteUserDword(string path, string name, int value) => _registry.WriteCurrentUser(path, name, value, RegistryValueKind.DWord);

        private async Task RunBestEffortAsync(string fileName, string args, CancellationToken token)
        {
            try { await _processes.RunAsync(fileName, args, token, false, 30).ConfigureAwait(false); } catch { }
        }

        private void ApplyHostsBlock(bool enabled)
        {
            var path = GetHostsPath();
            if (!File.Exists(path)) return;

            const string markerStart = "# Nexora Windows Update block START";
            const string markerEnd = "# Nexora Windows Update block END";

            var lines = File.ReadAllLines(path).ToList();
            var cleaned = new System.Collections.Generic.List<string>(lines.Count);
            var insideOwnedBlock = false;

            foreach (var line in lines)
            {
                if (string.Equals(line.Trim(), markerStart, StringComparison.OrdinalIgnoreCase))
                {
                    insideOwnedBlock = true;
                    continue;
                }

                if (string.Equals(line.Trim(), markerEnd, StringComparison.OrdinalIgnoreCase))
                {
                    insideOwnedBlock = false;
                    continue;
                }

                if (!insideOwnedBlock)
                    cleaned.Add(line);
            }

            if (enabled)
            {
                if (cleaned.Count > 0 && !string.IsNullOrWhiteSpace(cleaned[cleaned.Count - 1]))
                    cleaned.Add(string.Empty);

                cleaned.Add(markerStart);
                cleaned.Add("127.0.0.1 index.wp.microsoft.com");
                cleaned.Add("127.0.0.1 update.microsoft.com");
                cleaned.Add("127.0.0.1 slscr.update.microsoft.com");
                cleaned.Add("127.0.0.1 fe2.update.microsoft.com");
                cleaned.Add(markerEnd);
            }

            var tempPath = path + ".nexora.tmp";
            File.WriteAllLines(tempPath, cleaned);
            File.Move(tempPath, path, true);
        }
        private static string GetHostsPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "drivers", "etc", "hosts");
        }
    }
}
