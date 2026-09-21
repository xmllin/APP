using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WpfApp1.Models;

namespace WpfApp1.Services.Libraries
{
    public sealed class LibraryInstallationService
    {
        public async Task InstallAsync(LibraryDefinition definition, string installerPath, CancellationToken token)
        {
            if (definition == null || string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath)) throw new InvalidOperationException("Установщик компонента не найден.");
            var startInfo = new ProcessStartInfo { FileName = installerPath, Arguments = definition.SilentArguments ?? string.Empty, UseShellExecute = true, Verb = "runas", WorkingDirectory = Path.GetDirectoryName(installerPath) };
            using (var process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Не удалось запустить установщик.");
                await process.WaitForExitAsync(token);
                if (process.ExitCode != 0 && process.ExitCode != 3010)
                    throw new InvalidOperationException("Установщик завершился с кодом " + process.ExitCode + ".");
            }
        }

        public async Task InstallWindowsFeatureAsync(LibraryDefinition definition, CancellationToken token)
        {
            await RunPowerShellAsync("Enable-WindowsOptionalFeature -Online -FeatureName NetFx3 -All -NoRestart -ErrorAction Stop", token);
        }

        public async Task UninstallWindowsFeatureAsync(LibraryDefinition definition, CancellationToken token)
        {
            await RunPowerShellAsync("Disable-WindowsOptionalFeature -Online -FeatureName NetFx3 -NoRestart -ErrorAction Stop", token);
        }

        public async Task<string> SetPowerShellScriptsEnabledAsync(bool enabled, CancellationToken token)
        {
            var policy = enabled ? "RemoteSigned" : "Restricted";
            var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(systemFolder, "WindowsPowerShell\\v1.0\\powershell.exe"),
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy " + policy + " -Force\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };
            using (var process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Не удалось запустить Windows PowerShell.");
                await process.WaitForExitAsync(token);
                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    if (error.IndexOf("ExecutionPolicyOverride", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        error.IndexOf("PermissionDenied", StringComparison.OrdinalIgnoreCase) >= 0)
                        return null;
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "PowerShell вернул ошибку." : error.Trim());
                }
                return policy;
            }
        }

        public async Task<string> GetPowerShellScriptsPolicyAsync(CancellationToken token)
        {
            var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(systemFolder, "WindowsPowerShell\\v1.0\\powershell.exe"),
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"(Get-ExecutionPolicy -Scope CurrentUser)\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            using (var process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Не удалось запустить Windows PowerShell.");
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(token);
                return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output.Trim() : "Unknown";
            }
        }

        private static async Task RunPowerShellAsync(string command, CancellationToken token)
        {
            var systemFolder = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(systemFolder, "WindowsPowerShell\\v1.0\\powershell.exe"),
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"$ErrorActionPreference='Stop'; " + command + "\"",
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = systemFolder
            };
            using (var process = Process.Start(startInfo))
            {
                if (process == null) throw new InvalidOperationException("Не удалось запустить Windows PowerShell.");
                await process.WaitForExitAsync(token);
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("Операция Windows завершилась с кодом " + process.ExitCode + ".");
            }
        }

        public async Task UninstallAsync(LibraryDefinition definition, CancellationToken token)
        {
            if (definition == null) throw new InvalidOperationException("Компонент не указан.");

            var uninstallCommand = FindUninstallCommand(definition);
            if (string.IsNullOrWhiteSpace(uninstallCommand))
                throw new InvalidOperationException("Для этого компонента не найдено корректного удаления из системы.");

            var psi = CreateUninstallStartInfo(uninstallCommand);

            using (var process = Process.Start(psi))
            {
                if (process == null) throw new InvalidOperationException("Не удалось запустить удаление компонента.");
                await process.WaitForExitAsync(token);
                if (process.ExitCode == 1602) throw new OperationCanceledException();
                if (process.ExitCode != 0) throw new InvalidOperationException("Удаление завершилось с кодом " + process.ExitCode + ".");
            }
        }

        public async Task RepairAsync(LibraryDefinition definition, CancellationToken token)
        {
            if (definition == null) throw new InvalidOperationException("Компонент не указан.");

            var repairCommand = FindRepairCommand(definition);
            if (string.IsNullOrWhiteSpace(repairCommand))
                throw new InvalidOperationException("Для этого компонента не найдена программа восстановления.");

            using (var process = Process.Start(CreateUninstallStartInfo(repairCommand)))
            {
                if (process == null) throw new InvalidOperationException("Не удалось запустить восстановление компонента.");
                await process.WaitForExitAsync(token);
                if (process.ExitCode == 1602) throw new OperationCanceledException();
                if (process.ExitCode != 0) throw new InvalidOperationException("Восстановление завершилось с кодом " + process.ExitCode + ".");
            }
        }

        public string FindInstallLocation(LibraryDefinition definition)
        {
            if (definition == null) return string.Empty;
            var names = BuildDisplayNameCandidates(definition);

            foreach (var root in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, root))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key == null) continue;
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using (var item = key.OpenSubKey(subKeyName))
                        {
                            var displayName = item?.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(displayName) || !names.Any(name => displayName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                            var location = item.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrWhiteSpace(location))
                            {
                                location = Environment.ExpandEnvironmentVariables(location.Trim().Trim('"'));
                                if (Directory.Exists(location)) return location;
                            }

                            var displayIcon = item.GetValue("DisplayIcon") as string;
                            var iconPath = ExtractExecutablePath(displayIcon);
                            if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                                return Path.GetDirectoryName(iconPath);
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static ProcessStartInfo CreateUninstallStartInfo(string uninstallCommand)
        {
            var command = uninstallCommand.Trim();
            string fileName;
            string arguments;

            if (command.StartsWith("\"", StringComparison.Ordinal))
            {
                var closingQuote = command.IndexOf('"', 1);
                if (closingQuote < 0) throw new InvalidOperationException("Некорректная команда удаления компонента.");
                fileName = command.Substring(1, closingQuote - 1);
                arguments = command.Substring(closingQuote + 1).Trim();
            }
            else
            {
                var separator = command.IndexOf(' ');
                fileName = separator < 0 ? command : command.Substring(0, separator);
                arguments = separator < 0 ? string.Empty : command.Substring(separator + 1).Trim();
            }

            if (Path.GetFileNameWithoutExtension(fileName).Equals("msiexec", StringComparison.OrdinalIgnoreCase))
                arguments = NormalizeMsiUninstallArguments(arguments);

            return new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(fileName) ?? Environment.GetFolderPath(Environment.SpecialFolder.System)
            };
        }

        private static string NormalizeMsiUninstallArguments(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments)) return arguments;
            var normalized = arguments.Trim();
            if (normalized.StartsWith("/I", StringComparison.OrdinalIgnoreCase))
                return "/X" + normalized.Substring(2);
            if (normalized.StartsWith("/package", StringComparison.OrdinalIgnoreCase))
                return "/X" + normalized.Substring("/package".Length);
            return normalized;
        }

        private static string NormalizeRepairCommand(string command)
        {
            var normalized = command.Trim();
            string fileName;
            string arguments;
            if (normalized.StartsWith("\"", StringComparison.Ordinal))
            {
                var closingQuote = normalized.IndexOf('"', 1);
                if (closingQuote < 0) return normalized;
                fileName = normalized.Substring(1, closingQuote - 1);
                arguments = normalized.Substring(closingQuote + 1).Trim();
            }
            else
            {
                var separator = normalized.IndexOf(' ');
                fileName = separator < 0 ? normalized : normalized.Substring(0, separator);
                arguments = separator < 0 ? string.Empty : normalized.Substring(separator + 1).Trim();
            }

            if (Path.GetFileNameWithoutExtension(fileName).Equals("msiexec", StringComparison.OrdinalIgnoreCase))
            {
                var brace = arguments.IndexOf('{');
                var productCode = brace >= 0 ? arguments.Substring(brace) : arguments;
                return fileName + " /fa " + productCode;
            }

            if (arguments.IndexOf("/repair", StringComparison.OrdinalIgnoreCase) >= 0)
                return normalized;
            if (arguments.IndexOf("/modify", StringComparison.OrdinalIgnoreCase) >= 0)
                arguments = System.Text.RegularExpressions.Regex.Replace(arguments, "/modify", "/repair", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            else if (arguments.IndexOf("/uninstall", StringComparison.OrdinalIgnoreCase) >= 0)
                arguments = System.Text.RegularExpressions.Regex.Replace(arguments, "/uninstall", "/repair", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            else
                arguments = (arguments + " /repair").Trim();

            return "\"" + fileName + "\" " + arguments;
        }

        private static string ExtractExecutablePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var path = value.Trim().Trim('"');
            var comma = path.LastIndexOf(',');
            if (comma > 0) path = path.Substring(0, comma).Trim().Trim('"');
            return Environment.ExpandEnvironmentVariables(path);
        }

        private static string FindUninstallCommand(LibraryDefinition definition)
        {
            var names = BuildDisplayNameCandidates(definition);

            foreach (var root in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, root))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key == null) continue;
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using (var item = key.OpenSubKey(subKeyName))
                        {
                            if (item == null) continue;
                            var displayName = item.GetValue("DisplayName") as string;
                            var uninstallString = item.GetValue("UninstallString") as string;
                            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(uninstallString)) continue;
                            if (string.Equals(definition.Category, "Visual C++ Redistributable", StringComparison.OrdinalIgnoreCase) &&
                                displayName.IndexOf("Redistributable", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;
                            if (names.Any(name => displayName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0))
                                return uninstallString;
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static string FindRepairCommand(LibraryDefinition definition)
        {
            var names = BuildDisplayNameCandidates(definition);

            foreach (var root in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, root))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key == null) continue;
                    foreach (var subKeyName in key.GetSubKeyNames())
                    {
                        using (var item = key.OpenSubKey(subKeyName))
                        {
                            if (item == null) continue;
                            var displayName = item.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(displayName) || !names.Any(name => displayName.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                            if (string.Equals(definition.Category, "Visual C++ Redistributable", StringComparison.OrdinalIgnoreCase) &&
                                displayName.IndexOf("Redistributable", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;

                            var modifyString = item.GetValue("ModifyString") as string;
                            var modifyPath = item.GetValue("ModifyPath") as string;
                            var maintenanceCommand = !string.IsNullOrWhiteSpace(modifyString) ? modifyString : modifyPath;
                            if (!string.IsNullOrWhiteSpace(maintenanceCommand)) return NormalizeRepairCommand(maintenanceCommand);

                            var repairString = item.GetValue("RepairString") as string;
                            if (!string.IsNullOrWhiteSpace(repairString)) return NormalizeRepairCommand(repairString);
                        }
                    }
                }
            }

            return string.Empty;
        }

        private static string[] BuildDisplayNameCandidates(LibraryDefinition definition)
        {
            return new[]
            {
                definition.DetectionValue,
                definition.Name,
                definition.Name?.Replace("Microsoft Visual C++ ", "Microsoft Visual C++ Redistributable "),
                definition.Name?.Replace("Microsoft Visual C++ ", "Microsoft Visual C++ Redistributable " + (definition.Version ?? string.Empty) + " "),
                string.IsNullOrWhiteSpace(definition.Version) ? null : "Microsoft Visual C++ " + definition.Version + " Redistributable",
                string.IsNullOrWhiteSpace(definition.Version) ? null : "Microsoft Visual C++ " + definition.Version
            }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}
