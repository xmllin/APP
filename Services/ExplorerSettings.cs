using Nexora.Infrastructure.Registry;
using Nexora.Services.WindowsSettings;

namespace Nexora.Services
{
    public static class ExplorerSettings
    {
        private static readonly ExplorerSettingsService Service = new ExplorerSettingsService(new RegistrySettingsStore());

        public static bool IsHomeVisible() => Service.IsHomeVisible();
        public static void SetHomeVisibility(bool visible) => Service.SetHomeVisibility(visible);
    }
}
