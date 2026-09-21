using System;
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
                        SetDword(DataCollection, "AllowTelemetry", disabled ? 0 : 1);
                        SetDword(DataCollection, "MaxTelemetryAllowed", disabled ? 0 : 1);
                        SetDword(DataCollection, "AllowDeviceNameInDiagnosticData", disabled ? 0 : 1);
                        SetDword(DataCollection, "AllowWAPPReports", disabled ? 0 : 1);
                        SetDword(DataCollection, "DoNotShowFeedbackNotifications", disabled ? 1 : 0);
                        SetDword(DataCollection, "DisableDiagnosticDataViewer", disabled ? 1 : 0);
                        SetDword(AppCompat, "AITEnable", disabled ? 0 : 1);
                        SetDword(AppCompat, "AllowTelemetry", disabled ? 0 : 1);
                        SetDword(AppCompat, "DisableEngine", disabled ? 1 : 0);
                        SetDword(AppCompat, "DisableInventory", disabled ? 1 : 0);
                        SetDword(AppCompat, "DisablePCA", disabled ? 1 : 0);
                        SetDword(AppCompat, "DisableUAR", disabled ? 1 : 0);
                        return SettingOperationResult.Ok("Параметры телеметрии сохранены.", true);
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
