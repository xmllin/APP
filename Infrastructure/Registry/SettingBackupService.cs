using System;
using Microsoft.Win32;

namespace Nexora.Infrastructure.Registry
{
    public sealed class SettingBackupService
    {
        private const string BackupRoot = @"Software\Nexora\SettingsBackup";
        private readonly RegistrySettingsStore _store;

        public SettingBackupService(RegistrySettingsStore store = null)
        {
            _store = store ?? new RegistrySettingsStore();
        }

        public void BackupCurrentUserOnce(string backupName, string path, string valueName)
        {
            Backup(Microsoft.Win32.Registry.CurrentUser, backupName, _store.ReadCurrentUser(path, valueName));
        }

        public void BackupLocalMachineOnce(string backupName, string path, string valueName)
        {
            Backup(Microsoft.Win32.Registry.LocalMachine, backupName, _store.ReadLocalMachine(path, valueName));
        }

        public bool TryRestoreCurrentUser(string backupName, string path, string valueName)
        {
            return Restore(Microsoft.Win32.Registry.CurrentUser, backupName, path, valueName);
        }

        public bool TryRestoreLocalMachine(string backupName, string path, string valueName)
        {
            return Restore(Microsoft.Win32.Registry.LocalMachine, backupName, path, valueName);
        }

        private static void Backup(RegistryKey root, string backupName, RegistryValueSnapshot snapshot)
        {
            using (var key = root.CreateSubKey(BackupRoot))
            {
                if (key == null) throw new InvalidOperationException("Не удалось создать раздел резервных копий.");
                var prefix = Sanitize(backupName);
                if (key.GetValue(prefix + ".Exists", null) != null) return;
                key.SetValue(prefix + ".Exists", snapshot.Exists ? 1 : 0, RegistryValueKind.DWord);
                if (snapshot.Exists)
                {
                    key.SetValue(prefix + ".Kind", (int)snapshot.Kind, RegistryValueKind.DWord);
                    key.SetValue(prefix + ".Value", snapshot.Value, snapshot.Kind);
                }
            }
        }

        private static bool Restore(RegistryKey root, string backupName, string path, string valueName)
        {
            var prefix = Sanitize(backupName);
            RegistryValueSnapshot snapshot;
            using (var key = root.OpenSubKey(BackupRoot, false))
            {
                if (key == null || key.GetValue(prefix + ".Exists", null) == null) return false;
                var exists = Convert.ToInt32(key.GetValue(prefix + ".Exists", 0)) != 0;
                if (!exists)
                {
                    using (var target = root.OpenSubKey(path, true))
                    {
                        if (target != null) target.DeleteValue(valueName, false);
                    }
                    return true;
                }

                var kind = (RegistryValueKind)Convert.ToInt32(key.GetValue(prefix + ".Kind", (int)RegistryValueKind.String));
                snapshot = new RegistryValueSnapshot
                {
                    Exists = true,
                    Kind = kind,
                    Value = CloneValue(key.GetValue(prefix + ".Value", null, RegistryValueOptions.DoNotExpandEnvironmentNames))
                };
            }

            using (var target = root.CreateSubKey(path))
            {
                if (target == null) throw new InvalidOperationException("Не удалось открыть раздел реестра для восстановления.");
                target.SetValue(valueName, snapshot.Value, snapshot.Kind);
            }
            return true;
        }

        private static object CloneValue(object value)
        {
            if (value is byte[] bytes) return (byte[])bytes.Clone();
            if (value is string[] strings) return (string[])strings.Clone();
            return value;
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Имя резервной копии не задано.", nameof(name));
            var chars = name.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-') chars[i] = '_';
            return new string(chars);
        }
    }
}
