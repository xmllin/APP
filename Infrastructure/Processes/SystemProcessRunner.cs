using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1.Infrastructure.Processes
{
    public sealed class ProcessResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }
    }

    public sealed class SystemProcessRunner
    {
        public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken token, bool administrator = false, int timeoutSeconds = 120)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = administrator,
                Verb = administrator ? "runas" : string.Empty,
                CreateNoWindow = !administrator,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = !administrator,
                RedirectStandardError = !administrator
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Не удалось запустить процесс: " + fileName);
                Task<string> outputTask = !administrator ? process.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);
                Task<string> errorTask = !administrator ? process.StandardError.ReadToEndAsync() : Task.FromResult(string.Empty);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        try { if (!process.HasExited) process.Kill(true); } catch { }
                        if (token.IsCancellationRequested) throw;
                        throw new TimeoutException("Процесс превысил допустимое время ожидания: " + fileName);
                    }
                }

                return new ProcessResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = await outputTask.ConfigureAwait(false),
                    StandardError = await errorTask.ConfigureAwait(false)
                };
            }
        }

        public Task<ProcessResult> RunElevatedAsync(string fileName, string arguments, CancellationToken token, int timeoutSeconds = 300)
        {
            return RunAsync(fileName, arguments, token, true, timeoutSeconds);
        }
    }
}
