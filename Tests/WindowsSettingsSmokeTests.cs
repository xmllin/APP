using System;
using Microsoft.Win32;
using WpfApp1.Infrastructure.Registry;
using Xunit;

namespace WpfApp1.Tests
{
    public sealed class WindowsSettingsSmokeTests
    {
        [Fact]
        public void RegistrySettingsStore_CanRoundTripTemporaryCurrentUserValue()
        {
            var path = @"Software\WpfApp1\Tests\" + Guid.NewGuid().ToString("N");
            const string name = "RoundTrip";
            var store = new RegistrySettingsStore();

            try
            {
                store.WriteCurrentUser(path, name, 123, RegistryValueKind.DWord);
                var snapshot = store.ReadCurrentUser(path, name);

                Assert.True(snapshot.Exists);
                Assert.Equal(RegistryValueKind.DWord, snapshot.Kind);
                Assert.Equal(123, Convert.ToInt32(snapshot.Value));
            }
            finally
            {
                store.DeleteCurrentUser(path, name);
                var separator = path.LastIndexOf('\\');
                if (separator > 0)
                {
                    using (var parent = Registry.CurrentUser.OpenSubKey(path.Substring(0, separator), true))
                        parent?.DeleteSubKey(path.Substring(separator + 1), false);
                }
            }
        }
    }
}
