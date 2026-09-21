using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace WpfApp1.Services
{
    /// <summary>
    /// Этапы процесса активации.
    /// </summary>
    public enum ActivationStage
    {
        Started,        // Процесс начался
        Extracting,     // Распаковка скрипта
        RunningScript,  // Скрипт выполняется
        Completed,      // Успешно завершено
        Failed          // Ошибка
    }

    /// <summary>
    /// Данные о текущем этапе активации.
    /// </summary>
    public class ActivationProgressEventArgs : EventArgs
    {
        public ActivationStage Stage { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public ActivationProgressEventArgs(ActivationStage stage, string message, Exception exception = null)
        {
            Stage = stage;
            Message = message;
            Exception = exception;
        }
    }

    /// <summary>
    /// Сервис активации Windows методом HWID через встроенный скрипт MAS.
    /// </summary>
    public class MasActivationService
    {
        private const string ScriptFileName = "HWID_Activation.cmd";
        private const string BaseFolderName = "MAS_Activation";

        /// <summary>
        /// Событие, которое сообщает о смене этапа активации.
        /// Подписывайтесь из UI-потока (или маршалируйте через Dispatcher).
        /// </summary>
        public event EventHandler<ActivationProgressEventArgs> ProgressChanged;

        private void Report(ActivationStage stage, string message, Exception ex = null)
        {
            ProgressChanged?.Invoke(this, new ActivationProgressEventArgs(stage, message, ex));
        }

        /// <summary>
        /// Запускает HWID-активацию Windows.
        /// </summary>
        public async Task ActivateAsync(CancellationToken cancellationToken)
        {
            Report(ActivationStage.Started, "Запуск активации Windows…");

            // ВАЖНО: используем %LOCALAPPDATA%, а не %TEMP%,
            // потому что скрипт MAS отказывается работать из папки с "Temp" в пути.
            string baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                BaseFolderName);

            Directory.CreateDirectory(baseDir);
            string workDir = Path.Combine(baseDir, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDir);

            try
            {
                // 1. Распаковка встроенного скрипта.
                Report(ActivationStage.Extracting, "Подготовка скрипта активации…");
                string scriptPath = Path.Combine(workDir, ScriptFileName);
                await ExtractResourceBySuffixAsync(ScriptFileName, scriptPath);

                string outputPath = Path.Combine(workDir, "output.txt");

                // 2. Запуск cmd.exe с повышением прав.
                Report(ActivationStage.RunningScript, "Выполняется активация. Это может занять до минуты…");

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C \"\"{scriptPath}\" /HWID /NoEditionChange > \"{outputPath}\" 2>&1\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = workDir
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        var ex = new InvalidOperationException("Не удалось запустить процесс активации.");
                        Report(ActivationStage.Failed, ex.Message, ex);
                        throw ex;
                    }

                    await process.WaitForExitAsync(cancellationToken);

                    string output = File.Exists(outputPath)
                        ? await File.ReadAllTextAsync(outputPath, cancellationToken)
                        : string.Empty;

                    // 3. Проверка результата.
                    bool success =
                        output.Contains("permanently activated", StringComparison.OrdinalIgnoreCase) ||
                        output.Contains("Activation successful", StringComparison.OrdinalIgnoreCase) ||
                        output.Contains("Product activation successful", StringComparison.OrdinalIgnoreCase) ||
                        output.Contains("already permanently activated", StringComparison.OrdinalIgnoreCase) ||
                        output.Contains("Activation is not required", StringComparison.OrdinalIgnoreCase);

                    if (!success)
                    {
                        // Отдельно обрабатываем ошибку «запуск из temp-папки».
                        if (output.IndexOf("launched from the temp folder", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var ex = new InvalidOperationException(
                                "Скрипт MAS отказался работать: он определил, что запущен из временной папки. " +
                                "Убедитесь, что используется версия скрипта без проверки пути, " +
                                "или измените папку распаковки в коде.\n\n" +
                                $"Вывод скрипта:\n{output}");
                            Report(ActivationStage.Failed, ex.Message, ex);
                            throw ex;
                        }

                        var failEx = new InvalidOperationException(
                            $"Скрипт MAS не смог активировать Windows (exit code {process.ExitCode}).\n\n" +
                            $"Вывод:\n{output}");
                        Report(ActivationStage.Failed, failEx.Message, failEx);
                        throw failEx;
                    }

                    Report(ActivationStage.Completed, "Активация Windows успешно завершена.");
                }
            }
            catch (OperationCanceledException)
            {
                // Отмена пользователем — не считаем это ошибкой.
                Report(ActivationStage.Failed, "Активация отменена.");
                throw;
            }
            catch (Exception ex)
            {
                // Если ошибка ещё не была зарепорчена — репортим здесь.
                // (Двойной Failed при этом не возникнет, потому что выше мы уже сделали throw.)
                if (!(ex is InvalidOperationException))
                    Report(ActivationStage.Failed, ex.Message, ex);
                throw;
            }
            finally
            {
                try { Directory.Delete(workDir, recursive: true); } catch { }

                try
                {
                    if (Directory.Exists(baseDir) &&
                        !Directory.EnumerateFileSystemEntries(baseDir).Any())
                    {
                        Directory.Delete(baseDir);
                    }
                }
                catch { /* ignore */ }
            }
        }

        /// <summary>
        /// Возвращает текущий статус активации Windows через WMI.
        /// </summary>
        public async Task<ActivationStatus> GetStatusAsync(CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                const string query =
                    "SELECT Name, Description, LicenseStatus, PartialProductKey " +
                    "FROM SoftwareLicensingProduct " +
                    "WHERE ApplicationID='55c92734-d682-4d71-983e-d6ec3f16059f' " +
                    "AND PartialProductKey IS NOT NULL";

                using var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    int licenseStatus = Convert.ToInt32(obj["LicenseStatus"] ?? 0);
                    return new ActivationStatus
                    {
                        IsActivated = licenseStatus == 1,
                        Description = obj["Name"]?.ToString() ?? "Windows",
                        PartialProductKey = obj["PartialProductKey"]?.ToString() ?? string.Empty
                    };
                }

                return new ActivationStatus
                {
                    IsActivated = false,
                    Description = "Не удалось получить информацию о лицензии",
                    PartialProductKey = string.Empty
                };
            }, cancellationToken);
        }

        private static async Task ExtractResourceBySuffixAsync(string suffix, string destinationPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

            if (resourceName == null)
                throw new FileNotFoundException(
                    $"Встроенный ресурс '*{suffix}' не найден. " +
                    $"Убедитесь, что файл добавлен в проект и его Build Action = Embedded Resource.");

            await using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new FileNotFoundException($"Не удалось открыть поток ресурса '{resourceName}'.");

            await using var fileStream = File.Create(destinationPath);
            await stream.CopyToAsync(fileStream);
        }
    }

    public class ActivationStatus
    {
        public bool IsActivated { get; set; }
        public string Description { get; set; } = string.Empty;
        public string PartialProductKey { get; set; } = string.Empty;
    }
}