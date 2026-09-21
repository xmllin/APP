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
            try
            {
                var kill = await _processes.RunAsync(
                    "taskkill.exe",
                    "/F /IM explorer.exe",
                    token,
                    false,
                    15).ConfigureAwait(false);

                var explorerStillRunning = Process.GetProcessesByName("explorer").Length > 0;
                if (kill.ExitCode != 0 && explorerStillRunning)
                {
                    throw new InvalidOperationException(
                        "Не удалось завершить explorer.exe. Код: " + kill.ExitCode + ".");
                }

                token.ThrowIfCancellationRequested();

                // Не используем SystemProcessRunner для запуска Explorer:
                // его stdout/stderr перенаправляются в pipe, и explorer.exe
                // может унаследовать эти дескрипторы, из-за чего RunAsync
                // ждёт закрытия pipe значительно дольше самого запуска shell.
                using (var starter = Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    UseShellExecute = true,
                    WorkingDirectory = Environment.SystemDirectory,
                    WindowStyle = ProcessWindowStyle.Hidden
                }))
                {
                    if (starter == null)
                        throw new InvalidOperationException("Не удалось запустить explorer.exe.");
                }

                // Ждём только появления процесса, а не завершения стартующей
                // команды. Это существенно сокращает время, когда исчезает shell.
                for (var i = 0; i < 60; i++)
                {
                    token.ThrowIfCancellationRequested();
                    if (Process.GetProcessesByName("explorer").Length > 0)
                        return;

                    await Task.Delay(50, token).ConfigureAwait(false);
                }

                throw new InvalidOperationException("explorer.exe не появился после перезапуска.");
            }
            catch (Exception) when (Process.GetProcessesByName("explorer").Length > 0)
            {
                // Если Explorer уже поднялся, не превращаем долгий вспомогательный
                // этап в ложную ошибку для пользователя.
                return;
            }
        }
    }
}
