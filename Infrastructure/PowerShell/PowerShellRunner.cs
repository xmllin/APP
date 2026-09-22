using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nexora.Infrastructure.Processes;

namespace Nexora.Infrastructure.PowerShell
{
    public sealed class PowerShellRunner
    {
        private readonly SystemProcessRunner _processes;

        public PowerShellRunner(SystemProcessRunner processes = null)
        {
            _processes = processes ?? new SystemProcessRunner();
        }

        public async Task<string> RunAsync(string command, CancellationToken token, bool administrator = false, int timeoutSeconds = 120)
        {
            if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("Команда PowerShell не задана.", nameof(command));
            var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
            var result = await _processes.RunAsync(
                "powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded,
                token,
                administrator,
                timeoutSeconds).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError)
                    ? "Операция PowerShell завершилась с ошибкой."
                    : result.StandardError.Trim());
            }
            return result.StandardOutput ?? string.Empty;
        }
    }
}
