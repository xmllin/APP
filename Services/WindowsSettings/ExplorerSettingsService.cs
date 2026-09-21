using System;
using Microsoft.Win32;
using WpfApp1.Infrastructure.Registry;

namespace WpfApp1.Services.WindowsSettings
{
    public sealed class ExplorerSettingsService
    {
        private const string HomeKey = @"Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}";
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

        public void SetHomeVisibility(bool visible)
        {
            _backup.BackupCurrentUserOnce("ExplorerHomeVisibility", HomeKey, "System.IsPinnedToNameSpaceTree");
            _backup.BackupCurrentUserOnce("ExplorerHomeGraphFolder", HomeKey, "");
            _registry.WriteCurrentUser(HomeKey, "System.IsPinnedToNameSpaceTree", visible ? 1 : 0, RegistryValueKind.DWord);
            _registry.WriteCurrentUser(HomeKey, "", "CLSID_MSGraphHomeFolder", RegistryValueKind.String);
        }
    }
}
