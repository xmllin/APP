using System;
using System.Security;
using Microsoft.Win32;

namespace WpfApp1.Infrastructure.Registry
{
    public sealed class RegistrySettingsStore
    {
        public RegistryValueSnapshot ReadCurrentUser(string path, string name)
        {
            return Read(Microsoft.Win32.Registry.CurrentUser, path, name);
        }

        public RegistryValueSnapshot ReadLocalMachine(string path, string name)
        {
            return Read(Microsoft.Win32.Registry.LocalMachine, path, name);
        }

        public void WriteCurrentUser(string path, string name, object value, RegistryValueKind kind)
        {
            Write(Microsoft.Win32.Registry.CurrentUser, path, name, value, kind);
        }

        public void WriteLocalMachine(string path, string name, object value, RegistryValueKind kind)
        {
            Write(Microsoft.Win32.Registry.LocalMachine, path, name, value, kind);
        }

        public void DeleteCurrentUser(string path, string name)
        {
            Delete(Microsoft.Win32.Registry.CurrentUser, path, name);
        }

        public void DeleteLocalMachine(string path, string name)
        {
            Delete(Microsoft.Win32.Registry.LocalMachine, path, name);
        }

        private static RegistryValueSnapshot Read(RegistryKey root, string path, string name)
        {
            try
            {
                // Read-only access must never request write permissions. Some HKLM keys
                // can still be denied by ACLs or system policy; treat such a key as
                // unavailable instead of crashing the settings page.
                using (var key = root.OpenSubKey(path, false))
                {
                    if (key == null)
                        return new RegistryValueSnapshot { Exists = false };

                    object value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (value == null)
                        return new RegistryValueSnapshot { Exists = false };

                    return new RegistryValueSnapshot
                    {
                        Exists = true,
                        Kind = key.GetValueKind(name),
                        Value = CloneValue(value)
                    };
                }
            }
            catch (UnauthorizedAccessException)
            {
                return new RegistryValueSnapshot { Exists = false };
            }
            catch (SecurityException)
            {
                return new RegistryValueSnapshot { Exists = false };
            }
        }

        private static void Write(RegistryKey root, string path, string name, object value, RegistryValueKind kind)
        {
            using (var key = root.CreateSubKey(path))
            {
                if (key == null)
                    throw new InvalidOperationException("Не удалось открыть раздел реестра: " + path);
                key.SetValue(name, value, kind);
                var written = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (!ValuesEqual(written, value))
                    throw new InvalidOperationException("Не удалось проверить запись реестра: " + name);
            }
        }

        private static void Delete(RegistryKey root, string path, string name)
        {
            using (var key = root.OpenSubKey(path, true))
            {
                if (key == null) return;
                try { key.DeleteValue(name, false); } catch (ArgumentException) { }
            }
        }

        private static object CloneValue(object value)
        {
            if (value is byte[] bytes) return (byte[])bytes.Clone();
            if (value is string[] strings) return (string[])strings.Clone();
            return value;
        }

        private static bool ValuesEqual(object left, object right)
        {
            if (left is byte[] lb && right is byte[] rb)
            {
                if (lb.Length != rb.Length) return false;
                for (var i = 0; i < lb.Length; i++) if (lb[i] != rb[i]) return false;
                return true;
            }
            if (left is string[] ls && right is string[] rs)
            {
                if (ls.Length != rs.Length) return false;
                for (var i = 0; i < ls.Length; i++) if (!string.Equals(ls[i], rs[i], StringComparison.Ordinal)) return false;
                return true;
            }
            return Equals(left, right);
        }
    }
}
