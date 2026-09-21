using System;
using Microsoft.Win32;
using WpfApp1.Domain.WindowsSettings;
using WpfApp1.Infrastructure.Registry;

namespace WpfApp1.Services.WindowsSettings
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
                WriteMachine(SystemPolicyPath, "EnableLUA", 1);
                WriteMachine(SystemPolicyPath, "ConsentPromptBehaviorAdmin", enabled ? 0 : 5);
                WriteMachine(SystemPolicyPath, "ConsentPromptBehaviorUser", enabled ? 0 : 3);
                WriteMachine(SystemPolicyPath, "PromptOnSecureDesktop", enabled ? 0 : 1);
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
            try
            {
                BackupMachine(SystemPolicyPath, "EnableSmartScreen");
                _backup.BackupCurrentUserOnce("Security_SmartScreenEnabled", @"Software\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled");
                WriteMachine(SystemPolicyPath, "EnableSmartScreen", disabled ? 0 : 1);
                _registry.WriteCurrentUser(@"Software\Microsoft\Windows\CurrentVersion\Explorer", "SmartScreenEnabled", disabled ? "Off" : "Warn", RegistryValueKind.String);
                return SettingOperationResult.Ok("SmartScreen сохранён.", true);
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

        private int ReadMachineInt(string path, string name, int fallback)
        {
            var value = _registry.ReadLocalMachine(path, name).Value;
            try { return value == null ? fallback : Convert.ToInt32(value); } catch { return fallback; }
        }
    }
}
