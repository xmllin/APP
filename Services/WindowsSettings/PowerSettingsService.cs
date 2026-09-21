using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WpfApp1.Domain.WindowsSettings;
using WpfApp1.Infrastructure.Processes;

namespace WpfApp1.Services.WindowsSettings
{
    public sealed class PowerSettingsService
    {
        private static readonly Dictionary<string, string> Schemes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Сбалансированная"] = "381b4222-f694-41f0-9685-ff5bb260df2e",
            ["Высокая производительность"] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
            ["Экономия энергии"] = "a1841308-3541-4fab-bc81-f71556f20b4a"
        };

        private readonly SystemProcessRunner _processes;

        public PowerSettingsService(SystemProcessRunner processes = null)
        {
            _processes = processes ?? new SystemProcessRunner();
        }

        public async Task<string> GetActiveSchemeGuidAsync(CancellationToken token)
        {
            var result = await _processes.RunAsync("powercfg.exe", "/getactivescheme", token, false, 30).ConfigureAwait(false);
            var match = Regex.Match(result.StandardOutput ?? string.Empty, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return match.Success ? match.Value.ToLowerInvariant() : string.Empty;
        }

        public async Task<SettingOperationResult> ApplyAsync(string schemeName, CancellationToken token)
        {
            if (!Schemes.TryGetValue(schemeName ?? string.Empty, out var guid))
                return SettingOperationResult.Fail("Неизвестная схема электропитания.");
            try
            {
                var result = await _processes.RunAsync("powercfg.exe", "/setactive " + guid, token, false, 30).ConfigureAwait(false);
                if (result.ExitCode != 0) return SettingOperationResult.Fail(result.StandardError);

                if (string.Equals(schemeName, "Высокая производительность", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var command in new[]
                    {
                        "/change monitor-timeout-ac 0", "/change monitor-timeout-dc 0",
                        "/change standby-timeout-ac 0", "/change standby-timeout-dc 0",
                        "/change disk-timeout-ac 0", "/change disk-timeout-dc 0"
                    })
                    {
                        var timeoutResult = await _processes.RunAsync("powercfg.exe", command, token, false, 30).ConfigureAwait(false);
                        if (timeoutResult.ExitCode != 0)
                        {
                            var error = string.IsNullOrWhiteSpace(timeoutResult.StandardError)
                                ? "powercfg завершился с ошибкой при настройке таймеров."
                                : timeoutResult.StandardError.Trim();
                            return SettingOperationResult.Fail("Схема высокой производительности включена, но не удалось применить все таймеры: " + error);
                        }
                    }

                    var activeGuid = await GetActiveSchemeGuidAsync(token).ConfigureAwait(false);
                    if (!string.Equals(activeGuid, guid, StringComparison.OrdinalIgnoreCase))
                        return SettingOperationResult.Fail("Схема электропитания не стала активной после применения.");
                }
                return SettingOperationResult.Ok("Схема электропитания применена.");
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public async Task<SettingOperationResult> SetHibernationAsync(bool disabled, CancellationToken token)
        {
            try
            {
                var result = await _processes.RunAsync("powercfg.exe", disabled ? "/h off" : "/h on", token, false, 30).ConfigureAwait(false);
                return result.ExitCode == 0
                    ? SettingOperationResult.Ok(disabled ? "Гибернация отключена." : "Гибернация включена.")
                    : SettingOperationResult.Fail(string.IsNullOrWhiteSpace(result.StandardError) ? "powercfg завершился с ошибкой." : result.StandardError.Trim());
            }
            catch (Exception ex) { return SettingOperationResult.Fail(ex.Message); }
        }

        public static int GetSchemeIndex(string guid)
        {
            if (string.Equals(guid, Schemes["Высокая производительность"], StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(guid, Schemes["Экономия энергии"], StringComparison.OrdinalIgnoreCase)) return 2;
            return 0;
        }
    }
}
