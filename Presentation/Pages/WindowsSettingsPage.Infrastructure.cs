using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using WpfApp1.Services;
using WpfApp1.Services.Libraries;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.RegularExpressions;
using WpfApp1.Infrastructure.Registry;
using WpfApp1.Infrastructure.Processes;
using WpfApp1.Services.WindowsSettings;
using WpfApp1.Domain.WindowsSettings;

namespace WpfApp1.Pages
{
    public partial class WindowsSettingsPage : UserControl
    {
		[DllImport("shell32.dll")]
		private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);

		private static void DeleteUserValue(string path, string name)
		{
			using (var key = Registry.CurrentUser.OpenSubKey(path, writable: true))
			{
				key?.DeleteValue(name, throwOnMissingValue: false);
			}
		}

		private static void BackupUserString(string path, string name, string backupName)
		{
			using (var source = Registry.CurrentUser.OpenSubKey(path))
			using (var backup = Registry.CurrentUser.CreateSubKey(UserSettingsBackupPath + "\\" + backupName))
			{
				if (backup == null || backup.GetValue("Saved") != null) return;
				var value = source?.GetValue(name) as string;
				backup.SetValue("HadValue", value == null ? 0 : 1, RegistryValueKind.DWord);
				if (value != null) backup.SetValue("Saved", value, RegistryValueKind.String);
			}
		}

		private static void RestoreUserString(string path, string name, string backupName, string fallback)
		{
			using (var backup = Registry.CurrentUser.OpenSubKey(UserSettingsBackupPath + "\\" + backupName))
			{
				if (backup?.GetValue("HadValue", 0) is int hadValue)
				{
					if (hadValue == 1)
					{
						WriteUserString(path, name, backup.GetValue("Saved", fallback) as string ?? fallback);
						return;
					}
					DeleteUserValue(path, name);
					return;
				}
			}
			DeleteUserValue(path, name);
		}

		private static void BackupNamingTemplateValue(string name, string backupName)
		{
			using (var source = Registry.CurrentUser.OpenSubKey(NamingTemplatesPath))
			using (var backup = Registry.CurrentUser.CreateSubKey(UserSettingsBackupPath + "\\" + backupName))
			{
				if (backup == null || backup.GetValue("Saved") != null) return;
				var value = source?.GetValue(name) as string;
				backup.SetValue("HadValue", value == null ? 0 : 1, RegistryValueKind.DWord);
				if (value != null) backup.SetValue("Saved", value, RegistryValueKind.String);
			}
		}

		private static void RestoreNamingTemplateValue(string name, string backupName, string fallback)
		{
			using (var backup = Registry.CurrentUser.OpenSubKey(UserSettingsBackupPath + "\\" + backupName))
			{
				if (backup?.GetValue("HadValue", 0) is int hadValue && hadValue == 1)
				{
					WriteUserString(NamingTemplatesPath, name, backup.GetValue("Saved", fallback) as string ?? fallback);
					return;
				}
			}
			WriteUserString(NamingTemplatesPath, name, fallback);
		}

		private static void BackupMachineMultiString(string path, string name, string backupName)
		{
			using (var source = Registry.LocalMachine.OpenSubKey(path))
			using (var backup = Registry.LocalMachine.CreateSubKey(MachineSettingsBackupPath + "\\" + backupName))
			{
				if (backup == null || backup.GetValue(name) != null) return;
				var value = source?.GetValue(name) as string[];
				if (value != null) backup.SetValue(name, value, RegistryValueKind.MultiString);
			}
		}

		private static void WriteTelemetryLog(string setting, bool disabled)
		{
			var directory = System.IO.Path.GetDirectoryName(TelemetryLogPath);
			if (!string.IsNullOrEmpty(directory)) System.IO.Directory.CreateDirectory(directory);
			var status = disabled ? "disabled" : "enabled";
			var text = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + setting + " -> " + status + Environment.NewLine;
			System.IO.File.AppendAllText(TelemetryLogPath, text);
		}

		private static bool IsAdministrator()
		{
			using (var identity = WindowsIdentity.GetCurrent())
			{
				var principal = new WindowsPrincipal(identity);
				return principal.IsInRole(WindowsBuiltInRole.Administrator);
			}
		}

		private static int ReadDword(string path, string name, int fallback)
		{
			using (var key = Registry.CurrentUser.OpenSubKey(path))
			{
				var value = key?.GetValue(name, fallback);
				return Convert.ToInt32(value);
			}
		}

		private static string ReadString(string path, string name, string fallback)
		{
			using (var key = Registry.CurrentUser.OpenSubKey(path))
				return key?.GetValue(name, fallback) as string ?? fallback;
		}

		private static void WriteDword(string path, string name, int value)
		{
			using (var key = Registry.CurrentUser.CreateSubKey(path))
			{
				if (key == null)
					throw new InvalidOperationException("Не удалось открыть раздел реестра HKCU: " + path);
				key.SetValue(name, value, RegistryValueKind.DWord);
				if (Convert.ToInt32(key.GetValue(name, null)) != value)
					throw new InvalidOperationException("Не удалось проверить запись реестра " + name + ".");
			}
		}

		private static int ReadUserDword(string path, string name, int fallback)
		{
			using (var key = Registry.CurrentUser.OpenSubKey(path))
			{
				var value = key?.GetValue(name, null);
				if (value == null) return fallback;
				try { return Convert.ToInt32(value); }
				catch { return fallback; }
			}
		}

		private static string ReadUserString(string path, string name, string fallback)
		{
			using (var key = Registry.CurrentUser.OpenSubKey(path))
				return key?.GetValue(name, fallback) as string ?? fallback;
		}

		private static void WriteUserString(string path, string name, string value)
		{
			using (var key = Registry.CurrentUser.CreateSubKey(path))
			{
				if (key == null)
					throw new InvalidOperationException("Не удалось открыть раздел реестра HKCU: " + path);

				key.SetValue(name, value, RegistryValueKind.String);
				var written = key.GetValue(name, null) as string;
				if (!string.Equals(written, value, StringComparison.Ordinal))
					throw new InvalidOperationException("Не удалось записать значение реестра " + name + ".");
			}
		}

		private static void WriteUserDword(string path, string name, int value)
		{
			using (var key = Registry.CurrentUser.CreateSubKey(path))
			{
				if (key == null)
					throw new InvalidOperationException("Не удалось открыть раздел реестра HKCU: " + path);

				key.SetValue(name, value, RegistryValueKind.DWord);
				var written = key.GetValue(name, null);
				if (written == null || Convert.ToInt32(written) != value)
					throw new InvalidOperationException("Не удалось записать значение реестра " + name + ".");
			}
		}

		private static string ReadMachineString(string path, string name, string fallback)
		{
			using (var key = Registry.LocalMachine.OpenSubKey(path))
				return key?.GetValue(name, fallback) as string ?? fallback;
		}

		private static void WriteMachineString(string path, string name, string value)
		{
			using (var key = Registry.LocalMachine.CreateSubKey(path))
			{
				if (key == null)
					throw new InvalidOperationException("Не удалось открыть раздел реестра HKLM: " + path);
				key.SetValue(name, value, RegistryValueKind.String);
				var written = key.GetValue(name, null) as string;
				if (!string.Equals(written, value, StringComparison.Ordinal))
					throw new InvalidOperationException("Не удалось проверить запись реестра " + name + ".");
			}
		}

		private static int ReadMachineDword(string path, string name, int fallback)
		{
			using (var key = Registry.LocalMachine.OpenSubKey(path))
			{
				var value = key?.GetValue(name, fallback);
				return Convert.ToInt32(value);
			}
		}

		private static void WriteMachineDword(string path, string name, int value)
		{
			using (var key = Registry.LocalMachine.CreateSubKey(path))
			{
				if (key == null)
					throw new InvalidOperationException("Не удалось открыть раздел реестра HKLM: " + path);
				key.SetValue(name, value, RegistryValueKind.DWord);
				if (Convert.ToInt32(key.GetValue(name, null)) != value)
					throw new InvalidOperationException("Не удалось проверить запись реестра " + name + ".");
			}
		}

		private static void SetShortcutSuffix(bool removeSuffix)
		{
			using (var key = Registry.CurrentUser.CreateSubKey(NamingTemplatesPath))
			{
				if (removeSuffix) key?.SetValue("ShortcutNameTemplate", "%s", RegistryValueKind.ExpandString);
				else key?.DeleteValue("ShortcutNameTemplate", false);
			}
		}

		private static void RefreshExplorer()
		{
			SHChangeNotify(0x08000000u, 0x0000u, IntPtr.Zero, IntPtr.Zero);
		}

    }
}
