using WpfApp1.Infrastructure.Registry;
using WpfApp1.Services.WindowsSettings;

namespace WpfApp1.Services
{
    public static class ExplorerSettings
    {
        private static readonly ExplorerSettingsService Service = new ExplorerSettingsService(new RegistrySettingsStore());

        public static bool IsHomeVisible() => Service.IsHomeVisible();
        public static void SetHomeVisibility(bool visible) => Service.SetHomeVisibility(visible);
    }
}
