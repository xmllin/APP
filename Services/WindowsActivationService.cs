using System;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1.Services
{
    public sealed class WindowsActivationStatus
    {
        public bool IsActivated { get; set; }
        public string Description { get; set; }
        public string PartialProductKey { get; set; }
    }

    public sealed class WindowsActivationService
    {
        public Task<WindowsActivationStatus> GetStatusAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Description, LicenseStatus, PartialProductKey FROM SoftwareLicensingProduct " +
                    "WHERE PartialProductKey IS NOT NULL AND LicenseDependsOn IS NULL"))
                {
                    var product = searcher.Get().Cast<ManagementObject>()
                        .FirstOrDefault(item => Convert.ToInt32(item["LicenseStatus"] ?? 0) == 1);
                    if (product == null)
                        return new WindowsActivationStatus { Description = "Windows не активирована" };

                    return new WindowsActivationStatus
                    {
                        IsActivated = true,
                        Description = Convert.ToString(product["Description"]) ?? "Windows активирована",
                        PartialProductKey = Convert.ToString(product["PartialProductKey"])
                    };
                }
            }, token);
        }

        public async Task ActivateAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var scriptPath = System.IO.Path.Combine(AppContext.BaseDirectory, "MAS_AIO.cmd");
            if (!System.IO.File.Exists(scriptPath))
                throw new InvalidOperationException("Файл MAS_AIO.cmd не найден рядом с приложением.");

            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"\"" + scriptPath + "\"\"",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory,
                WindowStyle = ProcessWindowStyle.Hidden
            }))
            {
                if (process == null) throw new InvalidOperationException("Не удалось запустить активацию Windows.");
                await process.WaitForExitAsync(token);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Скрипт MAS_AIO.cmd завершился с кодом " + process.ExitCode + ".");
            }
        }
    }
}
