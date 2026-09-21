using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WpfApp1.Domain.WindowsSettings;
using WpfApp1.Infrastructure.Registry;

namespace WpfApp1.Services.WindowsSettings
{
    public sealed class PrivacySettingsService
    {
        private const string DataCollection = @"Software\Policies\Microsoft\Windows\DataCollection";
        private const string LegacyDataCollection = @"Software\Microsoft\Windows\CurrentVersion\Policies\DataCollection";
        private const string Legacy32 = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Policies\DataCollection";
        private const string AppCompat = @"Software\Policies\Microsoft\Windows\AppCompat";
        private const string AppPrivacy = @"Software\Policies\Microsoft\Windows\AppPrivacy";
        private const string AppDiagnostics = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\appDiagnostics";
        private const string Activity = @"Software\Policies\Microsoft\Windows\System";
        private const string Input = @"Software\Policies\Microsoft\InputPersonalization";
        private const string LegacyInput = @"Software\Microsoft\InputPersonalization";
        private const string Speech = @"Software\Policies\Microsoft\Speech";
        private const string Voice = @"Software\Microsoft\Speech_OneCore\Settings\OnlineSpeechPrivacy";
        private readonly RegistrySettingsStore _registry;
        private readonly SettingBackupService _backup;

        public PrivacySettingsService(RegistrySettingsStore registry = null, SettingBackupService backup = null)
        {
            _registry = registry ?? new RegistrySettingsStore();
            _backup = backup ?? new SettingBackupService(_registry);
        }

        public SettingOperationResult Apply(string tag, bool disabled)
        {
            try
            {
                switch (tag)
                {
                    case "DisableTelemetry":
                        return ApplyTelemetryGroup(disabled);
                    case "DisableAppDiagnostics":
                        SetDword(AppPrivacy, "LetAppsGetDiagnosticInfo", disabled ? 2 : 0);
                        SetDword(AppPrivacy, "LetAppsAccessDiagnosticInfo", disabled ? 2 : 0);
                        SetMachineString(AppDiagnostics, "Value", disabled ? "Deny" : "Allow");
                        return SettingOperationResult.Ok("Диагностические данные приложений сохранены.");
                    case "DisableActivity":
                        SetDword(Activity, "PublishUserActivities", disabled ? 0 : 1);
                        SetDword(Activity, "UploadUserActivities", disabled ? 0 : 1);
                        SetDword(Activity, "EnableActivityFeed", disabled ? 0 : 1);
                        return SettingOperationResult.Ok("История действий сохранена.");
                    case "DisablePerformance":
                        SetDword(DataCollection, "DisableDiagnosticDataViewer", disabled ? 1 : 0);
                        SetDword(@"Software\Policies\Microsoft\Windows\WDI\{9c5a40da-b965-4fc3-8781-88dd50a6299d}", "ScenarioExecutionEnabled", disabled ? 0 : 1);
                        SetDword(@"Software\Policies\Microsoft\DeviceHealthAttestationService", "EnableDeviceHealthAttestationService", disabled ? 0 : 1);
                        return SettingOperationResult.Ok("Диагностика производительности сохранена.");
                    case "DisableKeystrokes":
                        SetDword(Input, "AllowInputPersonalization", disabled ? 0 : 1);
                        SetDword(Input, "RestrictKeystrokeLogging", disabled ? 1 : 0);
                        SetDword(LegacyInput, "RestrictImplicitTextCollection", disabled ? 1 : 0);
                        SetDword(LegacyInput, "RestrictImplicitInkCollection", disabled ? 1 : 0);
                        return SettingOperationResult.Ok("Сбор данных ввода сохранён.");
                    case "DisableVoiceData":
                        SetUserDword(Voice, "HasAccepted", disabled ? 0 : 1);
                        SetDword(Speech, "AllowSpeechModelUpdate", disabled ? 0 : 1);
                        return SettingOperationResult.Ok("Голосовые данные сохранены.");
                    default:
                        return null;
                }
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private sealed class RegistryChange
        {
            public bool Machine { get; set; }
            public string Path { get; set; }
            public string Name { get; set; }
            public object NewValue { get; set; }
            public RegistryValueKind Kind { get; set; }
            public RegistryValueSnapshot Snapshot { get; set; }
        }

        private SettingOperationResult ApplyTelemetryGroup(bool disabled)
        {
            var changes = new List<RegistryChange>
            {
                MachineDword(DataCollection, "AllowTelemetry", disabled ? 0 : 1),
                MachineDword(DataCollection, "MaxTelemetryAllowed", disabled ? 0 : 1),
                MachineDword(DataCollection, "AllowDeviceNameInDiagnosticData", disabled ? 0 : 1),
                MachineDword(DataCollection, "AllowWAPPReports", disabled ? 0 : 1),
                MachineDword(DataCollection, "DoNotShowFeedbackNotifications", disabled ? 1 : 0),
                MachineDword(DataCollection, "DisableDiagnosticDataViewer", disabled ? 1 : 0),
                MachineDword(AppCompat, "AITEnable", disabled ? 0 : 1),
                MachineDword(AppCompat, "AllowTelemetry", disabled ? 0 : 1),
                MachineDword(AppCompat, "DisableEngine", disabled ? 1 : 0),
                MachineDword(AppCompat, "DisableInventory", disabled ? 1 : 0),
                MachineDword(AppCompat, "DisablePCA", disabled ? 1 : 0),
                MachineDword(AppCompat, "DisableUAR", disabled ? 1 : 0),

                MachineDword(AppPrivacy, "LetAppsGetDiagnosticInfo", disabled ? 2 : 0),
                MachineDword(AppPrivacy, "LetAppsAccessDiagnosticInfo", disabled ? 2 : 0),
                MachineString(AppDiagnostics, "Value", disabled ? "Deny" : "Allow"),

                MachineDword(Activity, "PublishUserActivities", disabled ? 0 : 1),
                MachineDword(Activity, "UploadUserActivities", disabled ? 0 : 1),
                MachineDword(Activity, "EnableActivityFeed", disabled ? 0 : 1),

                MachineDword(DataCollection, "DisableDiagnosticDataViewer", disabled ? 1 : 0),
                MachineDword(@"Software\Policies\Microsoft\Windows\WDI\{9c5a40da-b965-4fc3-8781-88dd50a6299d}", "ScenarioExecutionEnabled", disabled ? 0 : 1),
                MachineDword(@"Software\Policies\Microsoft\DeviceHealthAttestationService", "EnableDeviceHealthAttestationService", disabled ? 0 : 1),

                MachineDword(Input, "AllowInputPersonalization", disabled ? 0 : 1),
                MachineDword(Input, "RestrictKeystrokeLogging", disabled ? 1 : 0),
                MachineDword(LegacyInput, "RestrictImplicitTextCollection", disabled ? 1 : 0),
                MachineDword(LegacyInput, "RestrictImplicitInkCollection", disabled ? 1 : 0),

                UserDword(Voice, "HasAccepted", disabled ? 0 : 1),
                MachineDword(Speech, "AllowSpeechModelUpdate", disabled ? 0 : 1)
            };

            try
            {
                foreach (var change in changes)
                {
                    change.Snapshot = change.Machine
                        ? _registry.ReadLocalMachine(change.Path, change.Name).Clone()
                        : _registry.ReadCurrentUser(change.Path, change.Name).Clone();
                }

                foreach (var change in changes)
                {
                    var backupName = "Privacy_" + change.Path + "_" + change.Name;
                    if (change.Machine)
                    {
                        _backup.BackupLocalMachineOnce(backupName, change.Path, change.Name);
                        _registry.WriteLocalMachine(change.Path, change.Name, change.NewValue, change.Kind);
                    }
                    else
                    {
                        _backup.BackupCurrentUserOnce(backupName, change.Path, change.Name);
                        _registry.WriteCurrentUser(change.Path, change.Name, change.NewValue, change.Kind);
                    }
                }

                return SettingOperationResult.Ok("Параметры телеметрии сохранены.", true);
            }
            catch (Exception ex)
            {
                var rollbackErrors = new List<string>();
                for (var i = changes.Count - 1; i >= 0; i--)
                {
                    var change = changes[i];
                    if (change.Snapshot == null) continue;
                    try
                    {
                        RestoreSnapshot(change);
                    }
                    catch (Exception rollbackException)
                    {
                        rollbackErrors.Add(change.Path + "\\" + change.Name + ": " + rollbackException.Message);
                    }
                }

                var message = "Изменение группы телеметрии отменено: " + ex.Message;
                if (rollbackErrors.Count > 0)
                    message += " Не все исходные значения удалось восстановить: " + string.Join("; ", rollbackErrors);
                return SettingOperationResult.Fail(message);
            }
        }

        private static RegistryChange MachineDword(string path, string name, int value)
        {
            return new RegistryChange
            {
                Machine = true,
                Path = path,
                Name = name,
                NewValue = value,
                Kind = RegistryValueKind.DWord
            };
        }

        private static RegistryChange MachineString(string path, string name, string value)
        {
            return new RegistryChange
            {
                Machine = true,
                Path = path,
                Name = name,
                NewValue = value,
                Kind = RegistryValueKind.String
            };
        }

        private static RegistryChange UserDword(string path, string name, int value)
        {
            return new RegistryChange
            {
                Machine = false,
                Path = path,
                Name = name,
                NewValue = value,
                Kind = RegistryValueKind.DWord
            };
        }

        private void RestoreSnapshot(RegistryChange change)
        {
            if (!change.Snapshot.Exists)
            {
                if (change.Machine)
                    _registry.DeleteLocalMachine(change.Path, change.Name);
                else
                    _registry.DeleteCurrentUser(change.Path, change.Name);
                return;
            }

            if (change.Machine)
                _registry.WriteLocalMachine(change.Path, change.Name, change.Snapshot.Value, change.Snapshot.Kind);
            else
                _registry.WriteCurrentUser(change.Path, change.Name, change.Snapshot.Value, change.Snapshot.Kind);
        }

        private void SetDword(string path, string name, int value)
        {
            _backup.BackupLocalMachineOnce("Privacy_" + path + "_" + name, path, name);
            _registry.WriteLocalMachine(path, name, value, RegistryValueKind.DWord);
        }

        private void SetMachineString(string path, string name, string value)
        {
            _backup.BackupLocalMachineOnce("Privacy_" + path + "_" + name, path, name);
            _registry.WriteLocalMachine(path, name, value, RegistryValueKind.String);
        }

        private void SetUserDword(string path, string name, int value)
        {
            _backup.BackupCurrentUserOnce("Privacy_" + path + "_" + name, path, name);
            _registry.WriteCurrentUser(path, name, value, RegistryValueKind.DWord);
        }
    }
}
