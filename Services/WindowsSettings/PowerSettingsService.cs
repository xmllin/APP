using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.Domain.WindowsSettings;
using WpfApp1.Infrastructure.Processes;
using WpfApp1.Infrastructure.Registry;

namespace WpfApp1.Services.WindowsSettings
{
    public sealed class PowerSettingsService
    {
        private static readonly Dictionary<string, string> Schemes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Сбалансированная"] = "381b4222-f694-41f0-9685-ff5bb260df2e",
            ["Высокая производительность"] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
            ["Экономия энергии"] = "a1841308-3541-4fab-bc81-f71556f20b4a"
        };

        private readonly SystemProcessRunner _processes;
        private readonly RegistrySettingsStore _registry;
        private readonly SettingBackupService _backup;

        public PowerSettingsService(
            SystemProcessRunner processes = null,
            RegistrySettingsStore registry = null,
            SettingBackupService backup = null)
        {
            _processes = processes ?? new SystemProcessRunner();
            _registry = registry ?? new RegistrySettingsStore();
            _backup = backup ?? new SettingBackupService(_registry);
        }

        public async Task<string> GetActiveSchemeGuidAsync(CancellationToken token)
        {
            var result = await _processes.RunAsync("powercfg.exe", "/getactivescheme", token, false, 30).ConfigureAwait(false);
            var match = Regex.Match(result.StandardOutput ?? string.Empty, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return match.Success ? match.Value.ToLowerInvariant() : string.Empty;
        }

        public async Task<SettingOperationResult> ApplyAsync(string schemeName, CancellationToken token)
        {
            if (!Schemes.TryGetValue(schemeName ?? string.Empty, out var guid))
                return SettingOperationResult.Fail("Неизвестная схема электропитания.");
            try
            {
                var result = await _processes.RunAsync("powercfg.exe", "/setactive " + guid, token, false, 30).ConfigureAwait(false);
                if (result.ExitCode != 0) return SettingOperationResult.Fail(result.StandardError);

                if (string.Equals(schemeName, "Высокая производительность", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var command in new[]
                    {
                        "/change monitor-timeout-ac 0", "/change monitor-timeout-dc 0",
                        "/change standby-timeout-ac 0", "/change standby-timeout-dc 0",
                        "/change disk-timeout-ac 0", "/change disk-timeout-dc 0"
                    })
                    {
                        var timeoutResult = await _processes.RunAsync("powercfg.exe", command, token, false, 30).ConfigureAwait(false);
                        if (timeoutResult.ExitCode != 0)
                        {
                            var error = string.IsNullOrWhiteSpace(timeoutResult.StandardError)
                                ? "powercfg завершился с ошибкой при настройке таймеров."
                                : timeoutResult.StandardError.Trim();
                            return SettingOperationResult.Fail("Схема высокой производительности включена, но не удалось применить все таймеры: " + error);
                        }
                    }

                    var activeGuid = await GetActiveSchemeGuidAsync(token).ConfigureAwait(false);
                    if (!string.Equals(activeGuid, guid, StringComparison.OrdinalIgnoreCase))
                        return SettingOperationResult.Fail("Схема электропитания не стала активной после применения.");
                }
                return SettingOperationResult.Ok("Схема электропитания применена.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private const string PowerSessionPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";
        private const string HibernateEnabledValue = "HibernateEnabled";
        private const string HiberbootEnabledValue = "HiberbootEnabled";

        public bool IsHibernationDisabled()
        {
            var hibernate = _registry.ReadLocalMachine(PowerSessionPath, HibernateEnabledValue).Value;
            var fastStartup = _registry.ReadLocalMachine(PowerSessionPath, HiberbootEnabledValue).Value;

            var hibernateDisabled = hibernate != null && Convert.ToInt32(hibernate) == 0;
            var fastStartupDisabled = fastStartup != null && Convert.ToInt32(fastStartup) == 0;
            return hibernateDisabled && fastStartupDisabled;
        }

        public async Task<SettingOperationResult> SetHibernationAsync(bool disabled, CancellationToken token)
        {
            try
            {
                _backup.BackupLocalMachineOnce("Hibernation_HibernateEnabled", PowerSessionPath, HibernateEnabledValue);
                _backup.BackupLocalMachineOnce("Hibernation_HiberbootEnabled", PowerSessionPath, HiberbootEnabledValue);

                if (disabled)
                {
                    _registry.WriteLocalMachine(PowerSessionPath, HiberbootEnabledValue, 0, Microsoft.Win32.RegistryValueKind.DWord);
                    _registry.WriteLocalMachine(PowerSessionPath, HibernateEnabledValue, 0, Microsoft.Win32.RegistryValueKind.DWord);

                    var result = await _processes.RunElevatedAsync("powercfg.exe", "/h off", token, 60).ConfigureAwait(false);
                    if (result.ExitCode != 0)
                        return SettingOperationResult.Fail(string.IsNullOrWhiteSpace(result.StandardError)
                            ? "Не удалось отключить гибернацию."
                            : result.StandardError.Trim());
                }
                else
                {
                    var result = await _processes.RunElevatedAsync("powercfg.exe", "/h on", token, 60).ConfigureAwait(false);
                    if (result.ExitCode != 0)
                        return SettingOperationResult.Fail(string.IsNullOrWhiteSpace(result.StandardError)
                            ? "Не удалось включить гибернацию."
                            : result.StandardError.Trim());

                    _registry.WriteLocalMachine(PowerSessionPath, HibernateEnabledValue, 1, Microsoft.Win32.RegistryValueKind.DWord);
                    _registry.WriteLocalMachine(PowerSessionPath, HiberbootEnabledValue, 1, Microsoft.Win32.RegistryValueKind.DWord);
                }

                return IsHibernationDisabled() == disabled
                    ? SettingOperationResult.Ok(disabled ? "Гибернация и быстрый запуск отключены." : "Гибернация и быстрый запуск включены.")
                    : SettingOperationResult.Fail("Windows не подтвердила состояние гибернации и быстрого запуска.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public bool IsSystemPowerThrottlingDisabled()
        {
            var value = _registry.ReadLocalMachine(
                @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                "PowerThrottlingOff").Value;
            return Convert.ToInt32(value ?? 0) == 1;
        }

        public SettingOperationResult SetSystemPowerThrottlingDisabled(bool disabled)
        {
            try
            {
                const string path = @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling";
                const string name = "PowerThrottlingOff";

                _backup.BackupLocalMachineOnce("PowerThrottlingOff", path, name);
                _registry.WriteLocalMachine(path, name, disabled ? 1 : 0, Microsoft.Win32.RegistryValueKind.DWord);

                return IsSystemPowerThrottlingDisabled() == disabled
                    ? SettingOperationResult.Ok("Системное дросселирование сохранено.")
                    : SettingOperationResult.Fail("Windows не сохранила настройку системного дросселирования.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public async Task<bool> IsUsbPowerSavingDisabledAsync(CancellationToken token)
        {
            try
            {
                var result = await RunPowerShellAsync(
                    "$devices = @(Get-CimInstance -Namespace root\\wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue | Where-Object { $_.InstanceName -match 'USB\\\\ROOT' }); " +
                    "if ($devices.Count -eq 0) { 'NONE' } elseif (@($devices | Where-Object { $_.Enable -ne $false }).Count -eq 0) { '1' } else { '0' }",
                    token,
                    false).ConfigureAwait(false);
                return string.Equals((result.StandardOutput ?? string.Empty).Trim(), "1", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public async Task<SettingOperationResult> SetUsbPowerSavingDisabledAsync(bool disabled, CancellationToken token)
        {
            const string statePath = "usb-power-states.json";
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appPath = System.IO.Path.Combine(basePath, "WpfApp1");
            var savePath = System.IO.Path.Combine(appPath, statePath);

            try
            {
                Directory.CreateDirectory(appPath);

                if (disabled)
                {
                    var query = await RunPowerShellAsync(
                        "Get-CimInstance -Namespace root\\wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue | " +
                        "Where-Object { $_.InstanceName -match 'USB\\\\ROOT' } | " +
                        "Select-Object InstanceName, Enable | ConvertTo-Json -Compress",
                        token, false);

                    if (string.IsNullOrWhiteSpace(query.StandardOutput))
                        return SettingOperationResult.Ok("USB-энергосбережение не изменено: подходящие устройства не найдены.");

                    await File.WriteAllTextAsync(savePath, query.StandardOutput, token);

                    var apply = await RunPowerShellAsync(
                        "$devices = Get-CimInstance -Namespace root\\wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue | " +
                        "Where-Object { $_.InstanceName -match 'USB\\\\ROOT' }; " +
                        "foreach ($d in $devices) { if ($d.Enable -ne $false) { Set-CimInstance -CimInstance $d -Property @{ Enable = $false } | Out-Null } }",
                        token, true);

                    if (apply.ExitCode != 0)
                        return SettingOperationResult.Fail(string.IsNullOrWhiteSpace(apply.StandardError) ? "Не удалось отключить энергосбережение USB." : apply.StandardError.Trim());

                    return SettingOperationResult.Ok("Энергосбережение USB отключено.");
                }

                if (!File.Exists(savePath))
                {
                    return SettingOperationResult.Ok("Сохранённые состояния USB не найдены; текущее состояние Windows не изменено.");
                }

                var json = await File.ReadAllTextAsync(savePath, token);
                var restoreScript = "$saved = " + EscapePowerShellSingleQuoted(json) + "; " +
                    "$states = $saved | ConvertFrom-Json; if ($states -isnot [array]) { $states = @($states) }; " +
                    "$devices = Get-CimInstance -Namespace root\\wmi -ClassName MSPower_DeviceEnable -ErrorAction SilentlyContinue | Where-Object { $_.InstanceName -match 'USB\\\\ROOT' }; " +
                    "foreach ($d in $devices) { $s = $states | Where-Object { $_.InstanceName -eq $d.InstanceName } | Select-Object -First 1; if ($null -ne $s) { Set-CimInstance -CimInstance $d -Property @{ Enable = [bool]$s.Enable } | Out-Null } }";
                var restore = await RunPowerShellAsync(restoreScript, token, true);
                if (restore.ExitCode != 0)
                    return SettingOperationResult.Fail(string.IsNullOrWhiteSpace(restore.StandardError) ? "Не удалось восстановить энергосбережение USB." : restore.StandardError.Trim());

                File.Delete(savePath);
                return SettingOperationResult.Ok("Энергосбережение USB восстановлено.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private async Task<ProcessResult> RunPowerShellAsync(string command, CancellationToken token, bool administrator)
        {
            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command));
            return await _processes.RunAsync(
                "powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                token,
                administrator,
                120).ConfigureAwait(false);
        }

        private static string EscapePowerShellSingleQuoted(string value)
        {
            return "'" + (value ?? string.Empty).Replace("'", "''") + "'";
        }

        public static int GetSchemeIndex(string guid)
        {
            if (string.Equals(guid, Schemes["Высокая производительность"], StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(guid, Schemes["Экономия энергии"], StringComparison.OrdinalIgnoreCase)) return 2;
            return 0;
        }
    }
}
