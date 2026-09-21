using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WpfApp1.Domain.WindowsSettings;
using WpfApp1.Infrastructure.Registry;

namespace WpfApp1.Services.WindowsSettings
{
    public sealed class MouseSettingsState
    {
        public int Speed { get; set; }
        public int ScrollLines { get; set; }
        public bool AccelerationEnabled { get; set; }
    }

    public sealed class MouseSettingsService
    {
        private const string MousePath = @"Control Panel\Mouse";
        private const string DesktopPath = @"Control Panel\Desktop";
        private const uint SPI_GETMOUSESPEED = 0x0070;
        private const uint SPI_SETMOUSESPEED = 0x0071;
        private const uint SPI_GETWHEELSCROLLLINES = 0x0068;
        private const uint SPI_SETWHEELSCROLLLINES = 0x0069;
        private const uint SPI_GETMOUSE = 0x0003;
        private const uint SPI_SETMOUSE = 0x0004;
        private const uint SPIF_UPDATEINIFILE = 0x0001;
        private const uint SPIF_SENDCHANGE = 0x0002;

        private readonly RegistrySettingsStore _registry;
        private readonly SettingBackupService _backup;

        public MouseSettingsService(RegistrySettingsStore registry = null, SettingBackupService backup = null)
        {
            _registry = registry ?? new RegistrySettingsStore();
            _backup = backup ?? new SettingBackupService(_registry);
        }

        public MouseSettingsState ReadState()
        {
            return new MouseSettingsState
            {
                Speed = GetSpeed(),
                ScrollLines = GetScrollLines(),
                AccelerationEnabled = GetAcceleration()
            };
        }

        public SettingOperationResult SetSpeed(int value)
        {
            try
            {
                value = Math.Clamp(value, 1, 20);
                _backup.BackupCurrentUserOnce("MouseSpeed_MouseSensitivity", MousePath, "MouseSensitivity");
                var native = value;
                var spiSucceeded = SystemParametersInfo(SPI_SETMOUSESPEED, 0, ref native, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                // MouseSensitivity — штатное пользовательское значение Windows.
                // Сохраняем его и как fallback, потому что на некоторых системах
                // SPI_SETMOUSESPEED может вернуть FALSE даже для допустимого 1..20.
                _registry.WriteCurrentUser(MousePath, "MouseSensitivity", value.ToString(), RegistryValueKind.String);
                BroadcastSettingChange();

                VerifySpeed(value);
                if (!spiSucceeded && GetSpeed() != value)
                    throw new InvalidOperationException("Windows не сохранила скорость указателя.");
                return SettingOperationResult.Ok("Скорость указателя применена.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public SettingOperationResult SetScrollLines(int value)
        {
            try
            {
                value = Math.Clamp(value, 1, 100);
                _backup.BackupCurrentUserOnce("MouseScroll_WheelScrollLines", DesktopPath, "WheelScrollLines");
                var spiSucceeded = SystemParametersInfo(SPI_SETWHEELSCROLLLINES, (uint)value, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                _registry.WriteCurrentUser(DesktopPath, "WheelScrollLines", value.ToString(), RegistryValueKind.String);
                BroadcastSettingChange();

                var state = GetScrollLines();
                return state == value
                    ? SettingOperationResult.Ok("Прокрутка применена.")
                    : SettingOperationResult.Fail(
                        spiSucceeded
                            ? "Windows не сохранила количество строк прокрутки."
                            : "Windows не приняла количество строк прокрутки.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public SettingOperationResult SetAccelerationEnabled(bool enabled)
        {
            try
            {
                _backup.BackupCurrentUserOnce("MouseAcceleration_MouseSpeed", MousePath, "MouseSpeed");
                _backup.BackupCurrentUserOnce("MouseAcceleration_Threshold1", MousePath, "MouseThreshold1");
                _backup.BackupCurrentUserOnce("MouseAcceleration_Threshold2", MousePath, "MouseThreshold2");

                var values = GetAccelerationValues();
                if (enabled)
                {
                    values = new[] { 6, 10, 1 };
                }
                else
                {
                    values = new[] { 0, 0, 0 };
                }

                var handle = GCHandle.Alloc(values, GCHandleType.Pinned);
                try
                {
                    if (!SystemParametersInfo(SPI_SETMOUSE, 0, handle.AddrOfPinnedObject(), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
                        throw new InvalidOperationException("Windows не приняла настройку ускорения мыши.");
                }
                finally { handle.Free(); }

                var actual = GetAcceleration();
                return actual == enabled
                    ? SettingOperationResult.Ok(enabled ? "Ускорение мыши включено." : "Ускорение мыши отключено.")
                    : SettingOperationResult.Fail("Windows не сохранила состояние ускорения мыши.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        private int GetSpeed()
        {
            var value = 10;
            if (SystemParametersInfo(SPI_GETMOUSESPEED, 0, ref value, 0))
                return Math.Clamp(value, 1, 20);

            var snapshot = _registry.ReadCurrentUser(MousePath, "MouseSensitivity");
            try
            {
                if (snapshot.Exists && snapshot.Value != null)
                    return Math.Clamp(Convert.ToInt32(snapshot.Value), 1, 20);
            }
            catch { }
            return 10;
        }

        private int GetScrollLines()
        {
            var value = 5;
            if (SystemParametersInfo(SPI_GETWHEELSCROLLLINES, 0, ref value, 0))
                return Math.Clamp(value, 1, 100);

            var snapshot = _registry.ReadCurrentUser(DesktopPath, "WheelScrollLines");
            try
            {
                if (snapshot.Exists && snapshot.Value != null)
                    return Math.Clamp(Convert.ToInt32(snapshot.Value), 1, 100);
            }
            catch { }
            return 5;
        }

        private bool GetAcceleration()
        {
            var values = GetAccelerationValues();
            return values.Length >= 3 && (values[0] != 0 || values[1] != 0 || values[2] != 0);
        }

        private int[] GetAccelerationValues()
        {
            var values = new int[3];
            var handle = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                if (SystemParametersInfo(SPI_GETMOUSE, 0, handle.AddrOfPinnedObject(), 0))
                    return values;
            }
            finally { handle.Free(); }

            return new[]
            {
                ReadRegistryInt(MousePath, "MouseThreshold1", 6),
                ReadRegistryInt(MousePath, "MouseThreshold2", 10),
                ReadRegistryInt(MousePath, "MouseSpeed", 1)
            };
        }

        private int ReadRegistryInt(string path, string name, int fallback)
        {
            var snapshot = _registry.ReadCurrentUser(path, name);
            try
            {
                return snapshot.Exists && snapshot.Value != null ? Convert.ToInt32(snapshot.Value) : fallback;
            }
            catch { return fallback; }
        }

        private void VerifySpeed(int expected)
        {
            var native = GetSpeed();
            if (native != expected) throw new InvalidOperationException("Windows не сохранила скорость указателя.");
        }

        private static void BroadcastSettingChange()
        {
            SendMessageTimeout(
                HWND_BROADCAST,
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                IntPtr.Zero,
                SMTO_ABORTIFHUNG,
                2000,
                out _);
        }

        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint msg,
            UIntPtr wParam,
            IntPtr lParam,
            uint flags,
            uint timeout,
            out UIntPtr result);
    }
}
