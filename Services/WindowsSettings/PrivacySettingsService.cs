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
        private const string ContentDeliveryPath = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private readonly RegistrySettingsStore _registry;
        private readonly SettingBackupService _backup;

        public PrivacySettingsService(RegistrySettingsStore registry = null, SettingBackupService backup = null)
        {
            _registry = registry ?? new RegistrySettingsStore();
            _backup = backup ?? new SettingBackupService(_registry);
        }

        public bool IsDisabled(string tag)
        {
            switch (tag)
            {
                case "DisableErrorReporting":
                    return MachineEquals(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", 1)
                        && MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\HandwritingErrorReports", "PreventHandwritingErrorReports", 1);
                case "DisableAdvertisingAndSuggestions":
                    return MachineEquals(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0)
                        && MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", 1)
                        && UserEquals(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0)
                        && MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", 1)
                        && MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", 1)
                        && MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableSoftLanding", 1)
                        && MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableThirdPartySuggestions", 1);
                case "DisableNewsAndInterests":
                    return MachineEquals(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0)
                        && UserEquals(@"Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", 0);
                case "HideMeetNowButton":
                    return UserEquals(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", 1);
                case "DisableActivityHistory":
                    return MachineEquals(Activity, "PublishUserActivities", 0)
                        && MachineEquals(Activity, "EnableActivityFeed", 0)
                        && MachineEquals(Activity, "PublishUserActivitiesOnUserConsent", 0)
                        && MachineEquals(Activity, "UploadUserActivities", 0);
                case "DisableLocationAndSensors":
                    return MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", 1)
                        && MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableSensors", 1)
                        && MachineEquals(@"SYSTEM\Maps", "AutoUpdateEnabled", 0)
                        && UserEquals(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny")
                        && MachineEquals(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", "Deny");
                case "DisableAutoLogger":
                    foreach (var name in new[] { "AppModel", "Cellcore", "CloudExperienceHostOobe", "DataMarket", "DiagLog", "Diagtrack-Listener", "LwtNetLog", "SQMLogger", "WdiContextLog", "WiFiSession" })
                        if (!MachineEquals(@"SYSTEM\CurrentControlSet\Control\WMI\Autologger\" + name, "Start", 0)) return false;
                    return true;
                case "DisableCortana":
                    return MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", 0)
                        && MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCloudSearch", 0)
                        && UserEquals(@"Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", 0);
                case "DisableCopilot":
                    return MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)
                        && UserEquals(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", 1)
                        && UserEquals(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", 0)
                        && MachineEquals(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked", "{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}", "");
                case "DisableContentDeliveryManager":
                    return UserEquals(ContentDeliveryPath, "ContentDeliveryAllowed", 0)
                        && UserEquals(ContentDeliveryPath, "SubscribedContent-338387Enabled", 0)
                        && UserEquals(ContentDeliveryPath, "SubscribedContent-338388Enabled", 0)
                        && UserEquals(ContentDeliveryPath, "SubscribedContent-338389Enabled", 0)
                        && UserEquals(ContentDeliveryPath, "SubscribedContent-353698Enabled", 0)
                        && UserEquals(ContentDeliveryPath, "SystemPaneSuggestionsEnabled", 0);
                case "DisableFindMyDevice":
                    return MachineEquals(@"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice", 0);
                case "DisableDeliveryOptimization":
                    return MachineEquals(@"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", 0);
                default:
                    return false;
            }
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
                        SetUserString(AppDiagnostics, "Value", disabled ? "Deny" : "Allow");
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
                    case "DisableErrorReporting":
                        return ApplyRegistryGroup(new[]
                        {
                            MachineDword(@"SOFTWARE\Microsoft\Windows\Windows Error Reporting", "Disabled", disabled ? 1 : 0),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\HandwritingErrorReports", "PreventHandwritingErrorReports", disabled ? 1 : 0),
                            MachineDword(@"SYSTEM\CurrentControlSet\Services\WerSvc", "Start", disabled ? 4 : 3),
                            MachineDword(@"SYSTEM\CurrentControlSet\Services\PcaSvc", "Start", disabled ? 4 : 3)
                        }, "Отчёты об ошибках Windows сохранены.");
                    case "DisableAdvertisingAndSuggestions":
                        return ApplyRegistryGroup(new[]
                        {
                            MachineDword(@"SOFTWARE\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", disabled ? 0 : 1),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo", "DisabledByGroupPolicy", disabled ? 1 : 0),
                            UserDword2(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", disabled ? 0 : 1),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableTailoredExperiencesWithDiagnosticData", disabled ? 1 : 0),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableWindowsConsumerFeatures", disabled ? 1 : 0),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableSoftLanding", disabled ? 1 : 0),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\CloudContent", "DisableThirdPartySuggestions", disabled ? 1 : 0),
                            UserDword2(@"Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement", "ScoobeSystemSettingEnabled", disabled ? 0 : 1),
                            UserDword2(@"Software\Microsoft\InputPersonalization", "RestrictImplicitInkCollection", disabled ? 1 : 0),
                            UserDword2(@"Software\Microsoft\InputPersonalization", "RestrictImplicitTextCollection", disabled ? 1 : 0),
                            UserDword2(@"Software\Microsoft\InputPersonalization\TrainedDataStore", "HarvestContacts", disabled ? 0 : 1),
                            UserDword2(@"Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", disabled ? 1 : 0)
                        }, "Реклама и системные предложения сохранены.");
                    case "DisableNewsAndInterests":
                        return ApplyRegistryGroup(new[]
                        {
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", disabled ? 0 : 1),
                            UserDword2(@"Software\Microsoft\Windows\CurrentVersion\Feeds", "ShellFeedsTaskbarViewMode", disabled ? 0 : 1)
                        }, "Новости и интересы сохранены.");
                    case "HideMeetNowButton":
                        return SetUserDword("HideMeetNowButton", @"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "HideSCAMeetNow", disabled ? 1 : 0);
                    case "DisableActivityHistory":
                        return ApplyRegistryGroup(new[]
                        {
                            MachineDword(Activity, "PublishUserActivities", disabled ? 0 : 1),
                            MachineDword(Activity, "EnableActivityFeed", disabled ? 0 : 1),
                            MachineDword(Activity, "PublishUserActivitiesOnUserConsent", disabled ? 0 : 1),
                            MachineDword(Activity, "UploadUserActivities", disabled ? 0 : 1)
                        }, "История активности сохранена.");
                    case "DisableLocationAndSensors":
                        return ApplyRegistryGroup(new[]
                        {
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableLocation", disabled ? 1 : 0),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\LocationAndSensors", "DisableSensors", disabled ? 1 : 0),
                            MachineDword(@"SYSTEM\Maps", "AutoUpdateEnabled", disabled ? 0 : 1),
                            UserDword2(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Sensor\Permissions\{BFA794E4-F964-4FDB-90F6-51056BFE4B44}", "SensorPermissionState", disabled ? 0 : 1),
                            UserString2(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", disabled ? "Deny" : "Allow"),
                            MachineString(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location", "Value", disabled ? "Deny" : "Allow"),
                            UserDword2(@"Software\Microsoft\Windows\CurrentVersion\Geolocation", "Status", disabled ? 0 : 1),
                            MachineDword(@"SYSTEM\CurrentControlSet\Services\lfsvc\Service\Configuration", "Status", disabled ? 0 : 1),
                            MachineDword(@"Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting", "Value", disabled ? 0 : 1),
                            MachineDword(@"Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots", "Value", disabled ? 0 : 1)
                        }, "Геолокация и датчики сохранены.");
                    case "DisableAutoLogger":
                        return ApplyAutologger(disabled);
                    case "DisableCortana":
                        return ApplyRegistryGroup(new[]
                        {
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortana", disabled ? 0 : 1),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCloudSearch", disabled ? 0 : 1),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowCortanaAboveLock", disabled ? 0 : 1),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "AllowSearchToUseLocation", disabled ? 0 : 1),
                            MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", "ConnectedSearchUseWeb", disabled ? 0 : 1),
                            UserDword2(@"Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent", disabled ? 0 : 1),
                            UserDword2(@"Software\Microsoft\Windows\CurrentVersion\Search", "CortanaConsent2", disabled ? 0 : 1)
                        }, "Cortana и облачный поиск сохранены.");
                    case "DisableCopilot":
                        return ApplyCopilot(disabled);
                    case "DisableContentDeliveryManager":
                        return ApplyRegistryGroup(new[]
                        {
                            UserDword2(ContentDeliveryPath, "ContentDeliveryAllowed", disabled ? 0 : 1),
                            UserDword2(ContentDeliveryPath, "SubscribedContent-338387Enabled", disabled ? 0 : 1),
                            UserDword2(ContentDeliveryPath, "SubscribedContent-338388Enabled", disabled ? 0 : 1),
                            UserDword2(ContentDeliveryPath, "SubscribedContent-338389Enabled", disabled ? 0 : 1),
                            UserDword2(ContentDeliveryPath, "SubscribedContent-353698Enabled", disabled ? 0 : 1),
                            UserDword2(ContentDeliveryPath, "SystemPaneSuggestionsEnabled", disabled ? 0 : 1)
                        }, "Доставку контента и предложения сохранено.");
                    case "DisableFindMyDevice":
                        return SetMachineDword2("DisableFindMyDevice", @"SOFTWARE\Policies\Microsoft\FindMyDevice", "AllowFindMyDevice", disabled ? 0 : 1);
                    case "DisableDeliveryOptimization":
                        return SetMachineDword2("DisableDeliveryOptimization", @"SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode", disabled ? 0 : 1);
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
                UserString(AppDiagnostics, "Value", disabled ? "Deny" : "Allow"),

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

        private SettingOperationResult ApplyCopilot(bool disabled)
        {
            try
            {
                var policy = ApplyRegistryGroup(new[]
                {
                    MachineDword(@"SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", disabled ? 1 : 0),
                    UserDword2(@"Software\Policies\Microsoft\Windows\WindowsCopilot", "TurnOffWindowsCopilot", disabled ? 1 : 0),
                    UserDword2(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ShowCopilotButton", disabled ? 0 : 1)
                }, "Copilot сохранён.");
                if (!policy.Success) return policy;

                const string path = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked";
                const string name = "{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}";
                _backup.BackupLocalMachineOnce("Privacy_CopilotBlockedExtension", path, name);

                if (disabled)
                    _registry.WriteLocalMachine(path, name, string.Empty, RegistryValueKind.String);
                else if (!_backup.TryRestoreLocalMachine("Privacy_CopilotBlockedExtension", path, name))
                    _registry.DeleteLocalMachine(path, name);

                return SettingOperationResult.Ok("Copilot сохранён.");
            }
            catch (Exception ex)
            {
                return SettingOperationResult.Fail(ex.Message);
            }
        }

        private SettingOperationResult ApplyAutologger(bool disabled)
        {
            var changes = new System.Collections.Generic.List<RegistryChange>();
            foreach (var name in new[] { "AppModel", "Cellcore", "CloudExperienceHostOobe", "DataMarket", "DiagLog", "Diagtrack-Listener", "LwtNetLog", "SQMLogger", "WdiContextLog", "WiFiSession" })
                changes.Add(MachineDword(@"SYSTEM\CurrentControlSet\Control\WMI\Autologger\" + name, "Start", disabled ? 0 : 1));
            return ApplyRegistryGroup(changes.ToArray(), "WMI AutoLogger сохранён.");
        }

        private SettingOperationResult ApplyRegistryGroup(RegistryChange[] changes, string message)
        {
            try
            {
                foreach (var c in changes)
                    c.Snapshot = c.Machine ? _registry.ReadLocalMachine(c.Path, c.Name).Clone() : _registry.ReadCurrentUser(c.Path, c.Name).Clone();

                foreach (var c in changes)
                {
                    if (c.Machine)
                    {
                        _backup.BackupLocalMachineOnce("Privacy_" + c.Path + "_" + c.Name, c.Path, c.Name);
                        if (c.Kind == RegistryValueKind.String) _registry.WriteLocalMachine(c.Path, c.Name, c.NewValue, c.Kind);
                        else _registry.WriteLocalMachine(c.Path, c.Name, c.NewValue, c.Kind);
                    }
                    else
                    {
                        _backup.BackupCurrentUserOnce("Privacy_" + c.Path + "_" + c.Name, c.Path, c.Name);
                        _registry.WriteCurrentUser(c.Path, c.Name, c.NewValue, c.Kind);
                    }
                }
                return SettingOperationResult.Ok(message);
            }
            catch (Exception ex)
            {
                for (var i = changes.Length - 1; i >= 0; i--)
                {
                    try { RestoreSnapshot(changes[i]); } catch { }
                }
                return SettingOperationResult.Fail(message + " Ошибка: " + ex.Message);
            }
        }

        private static RegistryChange UserDword2(string path, string name, int value) => new RegistryChange { Machine = false, Path = path, Name = name, NewValue = value, Kind = RegistryValueKind.DWord };
        private static RegistryChange UserString2(string path, string name, string value) => new RegistryChange { Machine = false, Path = path, Name = name, NewValue = value, Kind = RegistryValueKind.String };

        private SettingOperationResult SetMachineDword2(string backupName, string path, string name, int value)
        {
            try
            {
                _backup.BackupLocalMachineOnce("Privacy_" + backupName, path, name);
                _registry.WriteLocalMachine(path, name, value, RegistryValueKind.DWord);
                return MachineEquals(path, name, value)
                    ? SettingOperationResult.Ok("Настройка сохранена.")
                    : SettingOperationResult.Fail("Windows не сохранила выбранную настройку.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private bool MachineEquals(string path, string name, object expected)
        {
            var value = _registry.ReadLocalMachine(path, name).Value;
            return value != null && string.Equals(Convert.ToString(value), Convert.ToString(expected), StringComparison.OrdinalIgnoreCase);
        }

        private bool UserEquals(string path, string name, object expected)
        {
            var value = _registry.ReadCurrentUser(path, name).Value;
            return value != null && string.Equals(Convert.ToString(value), Convert.ToString(expected), StringComparison.OrdinalIgnoreCase);
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

        private void SetUserString(string path, string name, string value)
        {
            _backup.BackupCurrentUserOnce("Privacy_" + path + "_" + name, path, name);
            _registry.WriteCurrentUser(path, name, value, RegistryValueKind.String);
        }

        private void SetUserDword(string path, string name, int value)
        {
            _backup.BackupCurrentUserOnce("Privacy_" + path + "_" + name, path, name);
            _registry.WriteCurrentUser(path, name, value, RegistryValueKind.DWord);
        }
    }
}
