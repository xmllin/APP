using System;
using Microsoft.Win32;
using Nexora.Domain.WindowsSettings;
using Nexora.Infrastructure.Registry;

namespace Nexora.Services.WindowsSettings
{
    public sealed class SecuritySettingsService
    {
        private const string SystemPolicyPath = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string GraphicsDriversPath = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers";
        private const string HvcIPath = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
        private const string DeviceGuardPath = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
        private const string LockScreenPolicyPath = @"Software\Policies\Microsoft\Windows\System";
        private const string BitLockerPath = @"SYSTEM\CurrentControlSet\Control\BitLocker";
        private readonly RegistrySettingsStore _registry;
        private readonly SettingBackupService _backup;

        public SecuritySettingsService(RegistrySettingsStore registry = null, SettingBackupService backup = null)
        {
            _registry = registry ?? new RegistrySettingsStore();
            _backup = backup ?? new SettingBackupService(_registry);
        }

        public bool IsUacNeverNotify()
        {
            return ReadMachineInt(SystemPolicyPath, "EnableLUA", 1) == 1
                && ReadMachineInt(SystemPolicyPath, "ConsentPromptBehaviorAdmin", 5) == 0
                && ReadMachineInt(SystemPolicyPath, "ConsentPromptBehaviorUser", 3) == 0
                && ReadMachineInt(SystemPolicyPath, "PromptOnSecureDesktop", 1) == 0;
        }

        public SettingOperationResult SetUacNeverNotify(bool enabled)
        {
            try
            {
                BackupMachine(SystemPolicyPath, "EnableLUA");
                BackupMachine(SystemPolicyPath, "ConsentPromptBehaviorAdmin");
                BackupMachine(SystemPolicyPath, "ConsentPromptBehaviorUser");
                BackupMachine(SystemPolicyPath, "PromptOnSecureDesktop");

                if (enabled)
                {
                    WriteMachine(SystemPolicyPath, "EnableLUA", 1);
                    WriteMachine(SystemPolicyPath, "ConsentPromptBehaviorAdmin", 0);
                    WriteMachine(SystemPolicyPath, "ConsentPromptBehaviorUser", 0);
                    WriteMachine(SystemPolicyPath, "PromptOnSecureDesktop", 0);
                }
                else
                {
                    RestoreBackedUpMachine(SystemPolicyPath, "EnableLUA", 1);
                    RestoreBackedUpMachine(SystemPolicyPath, "ConsentPromptBehaviorAdmin", 5);
                    RestoreBackedUpMachine(SystemPolicyPath, "ConsentPromptBehaviorUser", 3);
                    RestoreBackedUpMachine(SystemPolicyPath, "PromptOnSecureDesktop", 1);
                }

                return IsUacNeverNotify() == enabled
                    ? SettingOperationResult.Ok("Настройка UAC сохранена.", true)
                    : SettingOperationResult.Fail("Windows не сохранила настройку UAC.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public bool IsHardwareGpuSchedulingSupported() => _registry.ReadLocalMachine(GraphicsDriversPath, "HwSchMode").Exists;

        public bool IsHardwareGpuSchedulingEnabled() => ReadMachineInt(GraphicsDriversPath, "HwSchMode", 1) == 2;

        public SettingOperationResult SetHardwareGpuScheduling(bool enabled)
        {
            if (!IsHardwareGpuSchedulingSupported()) return SettingOperationResult.Fail("HAGS недоступен на этом оборудовании или драйвере.");
            try
            {
                BackupMachine(GraphicsDriversPath, "HwSchMode");
                WriteMachine(GraphicsDriversPath, "HwSchMode", enabled ? 2 : 1);
                return IsHardwareGpuSchedulingEnabled() == enabled
                    ? SettingOperationResult.Ok("Планирование GPU сохранено.", true)
                    : SettingOperationResult.Fail("Windows не сохранила настройку HAGS.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public SettingOperationResult SetLockScreenBlurDisabled(bool disabled)
        {
            return WriteMachineSetting(LockScreenPolicyPath, "DisableAcrylicBackgroundOnLogon", disabled ? 1 : 0, false);
        }

        public SettingOperationResult SetSmartScreenDisabled(bool disabled)
        {
            const string explorerPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer";
            const string machineName = "EnableSmartScreen";
            const string userName = "SmartScreenEnabled";
            try
            {
                BackupMachine(SystemPolicyPath, machineName);
                _backup.BackupCurrentUserOnce("Security_SmartScreenEnabled", explorerPath, userName);

                var machineSnapshot = _registry.ReadLocalMachine(SystemPolicyPath, machineName).Clone();
                var userSnapshot = _registry.ReadCurrentUser(explorerPath, userName).Clone();

                try
                {
                    WriteMachine(SystemPolicyPath, machineName, disabled ? 0 : 1);
                    _registry.WriteCurrentUser(explorerPath, userName, disabled ? "Off" : "Warn", RegistryValueKind.String);

                    var machineValue = ReadMachineInt(SystemPolicyPath, machineName, disabled ? 0 : 1);
                    var userValue = _registry.ReadCurrentUser(explorerPath, userName).Value as string;
                    if (machineValue != (disabled ? 0 : 1) ||
                        !string.Equals(userValue, disabled ? "Off" : "Warn", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Windows не сохранила настройку SmartScreen.");

                    return SettingOperationResult.Ok("SmartScreen сохранён.", true);
                }
                catch
                {
                    RestoreSnapshot(machineSnapshot, true, SystemPolicyPath, machineName);
                    RestoreSnapshot(userSnapshot, false, explorerPath, userName);
                    throw;
                }
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public SettingOperationResult SetMemoryIntegrityDisabled(bool disabled)
        {
            return WriteMachineSetting(HvcIPath, "Enabled", disabled ? 0 : 1, true);
        }

        public SettingOperationResult SetVbsDisabled(bool disabled)
        {
            return WriteMachineSetting(DeviceGuardPath, "EnableVirtualizationBasedSecurity", disabled ? 0 : 1, true);
        }

        public SettingOperationResult SetBitLockerAutoEncryptionDisabled(bool disabled)
        {
            return WriteMachineSetting(BitLockerPath, "PreventDeviceEncryption", disabled ? 1 : 0, true);
        }

        private SettingOperationResult WriteMachineSetting(string path, string name, int value, bool restart)
        {
            try
            {
                BackupMachine(path, name);
                WriteMachine(path, name, value);
                return ReadMachineInt(path, name, value) == value
                    ? SettingOperationResult.Ok("Настройка безопасности сохранена.", restart)
                    : SettingOperationResult.Fail("Windows не сохранила настройку безопасности.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private void BackupMachine(string path, string name) => _backup.BackupLocalMachineOnce("Security_" + path + "_" + name, path, name);
        private void WriteMachine(string path, string name, int value) => _registry.WriteLocalMachine(path, name, value, RegistryValueKind.DWord);

        private void RestoreBackedUpMachine(string path, string name, int fallback)
        {
            if (!_backup.TryRestoreLocalMachine("Security_" + path + "_" + name, path, name))
                WriteMachine(path, name, fallback);
        }

        private void RestoreSnapshot(RegistryValueSnapshot snapshot, bool machine, string path, string name)
        {
            if (!snapshot.Exists)
            {
                if (machine) _registry.DeleteLocalMachine(path, name);
                else _registry.DeleteCurrentUser(path, name);
                return;
            }

            if (machine) _registry.WriteLocalMachine(path, name, snapshot.Value, snapshot.Kind);
            else _registry.WriteCurrentUser(path, name, snapshot.Value, snapshot.Kind);
        }

        private int ReadMachineInt(string path, string name, int fallback)
        {
            var value = _registry.ReadLocalMachine(path, name).Value;
            try { return value == null ? fallback : Convert.ToInt32(value); } catch { return fallback; }
        }
    }
}
