using System;
using Microsoft.Win32;
using WpfApp1.Domain.WindowsSettings;
using WpfApp1.Infrastructure.Registry;

namespace WpfApp1.Services.WindowsSettings
{
    public sealed class ExplorerSettingsService
    {
        private const string HomeKey = @"Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}";
        private const string ExplorerAdvancedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
        private readonly RegistrySettingsStore _registry;
        private readonly SettingBackupService _backup;

        public ExplorerSettingsService(RegistrySettingsStore registry = null, SettingBackupService backup = null)
        {
            _registry = registry ?? new RegistrySettingsStore();
            _backup = backup ?? new SettingBackupService(_registry);
        }

        public bool IsHomeVisible()
        {
            var value = _registry.ReadCurrentUser(HomeKey, "System.IsPinnedToNameSpaceTree").Value;
            return !(value is int pinned) || pinned != 0;
        }

        public bool IsLaunchToThisPc()
        {
            return Convert.ToInt32(_registry.ReadCurrentUser(ExplorerAdvancedPath, "LaunchTo").Value ?? 2) == 1;
        }

        public SettingOperationResult SetLaunchToThisPc(bool enabled)
        {
            try
            {
                _backup.BackupCurrentUserOnce("ExplorerLaunchTo", ExplorerAdvancedPath, "LaunchTo");
                _registry.WriteCurrentUser(ExplorerAdvancedPath, "LaunchTo", enabled ? 1 : 2, RegistryValueKind.DWord);
                return IsLaunchToThisPc() == enabled
                    ? SettingOperationResult.Ok("Стартовая страница Проводника сохранена.")
                    : SettingOperationResult.Fail("Windows не сохранила стартовую страницу Проводника.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public bool AreItemCheckboxesEnabled()
        {
            return Convert.ToInt32(_registry.ReadCurrentUser(ExplorerAdvancedPath, "AutoCheckSelect").Value ?? 0) != 0;
        }

        public SettingOperationResult SetItemCheckboxes(bool enabled)
        {
            try
            {
                _backup.BackupCurrentUserOnce("ExplorerItemCheckboxes", ExplorerAdvancedPath, "AutoCheckSelect");
                _registry.WriteCurrentUser(ExplorerAdvancedPath, "AutoCheckSelect", enabled ? 1 : 0, RegistryValueKind.DWord);
                return AreItemCheckboxesEnabled() == enabled
                    ? SettingOperationResult.Ok("Флажки элементов Проводника сохранены.")
                    : SettingOperationResult.Fail("Windows не сохранила настройку флажков элементов.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public void SetHomeVisibility(bool visible)
        {
            _backup.BackupCurrentUserOnce("ExplorerHomeVisibility", HomeKey, "System.IsPinnedToNameSpaceTree");
            _backup.BackupCurrentUserOnce("ExplorerHomeGraphFolder", HomeKey, "");
            _registry.WriteCurrentUser(HomeKey, "System.IsPinnedToNameSpaceTree", visible ? 1 : 0, RegistryValueKind.DWord);
            _registry.WriteCurrentUser(HomeKey, "", "CLSID_MSGraphHomeFolder", RegistryValueKind.String);

            if (IsHomeVisible() != visible)
                throw new InvalidOperationException("Windows не сохранила видимость главной страницы Проводника.");
        }
    }
}
