using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.Infrastructure.Processes;

namespace WpfApp1.Infrastructure.WindowsApis
{
    public sealed class ExplorerController
    {
        private readonly SystemProcessRunner _processes;

        public ExplorerController(SystemProcessRunner processes = null)
        {
            _processes = processes ?? new SystemProcessRunner();
        }

        public async Task RestartAsync(CancellationToken token)
        {
            await _processes.RunAsync("taskkill.exe", "/F /IM explorer.exe", token, false, 30).ConfigureAwait(false);
            for (var i = 0; i < 30; i++)
            {
                token.ThrowIfCancellationRequested();
                if (Process.GetProcessesByName("explorer").Length == 0) break;
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            await Task.Delay(250, token).ConfigureAwait(false);
            Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "explorer.exe"),
                UseShellExecute = true,
                WorkingDirectory = Environment.SystemDirectory
            });
        }
    }
}
