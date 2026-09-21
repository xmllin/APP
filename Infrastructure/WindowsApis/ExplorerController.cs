using System;
using System.Diagnostics;
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
            // OptimizerDuck 2.27.6 перезапускает shell через cmd:
            // taskkill ... && start explorer.exe. Важный момент — Explorer
            // стартует через shell, а не как прямой дочерний процесс нашего
            // elevated-приложения.
            var result = await _processes.RunAsync(
                "cmd.exe",
                "/c taskkill /f /im explorer.exe && start explorer.exe",
                token,
                false,
                30).ConfigureAwait(false);

            if (result.ExitCode != 0)
                throw new InvalidOperationException(
                    "Команда перезапуска Проводника завершилась с кодом " + result.ExitCode + ".");

            for (var i = 0; i < 50; i++)
            {
                token.ThrowIfCancellationRequested();
                if (Process.GetProcessesByName("explorer").Length > 0)
                    return;

                await Task.Delay(100, token).ConfigureAwait(false);
            }

            throw new InvalidOperationException("explorer.exe не запустился после перезапуска.");
        }
    }
}
