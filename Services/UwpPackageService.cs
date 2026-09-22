using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nexora.Infrastructure.PowerShell;
using Nexora.Infrastructure.Processes;
using Nexora.Models;

namespace Nexora.Services
{
    public sealed class UwpPackageService
    {
        private readonly PowerShellRunner _powershell;

        public UwpPackageService(PowerShellRunner powershell = null)
        {
            _powershell = powershell ?? new PowerShellRunner(new SystemProcessRunner());
        }

        public async Task<bool> IsPackageInstalledAsync(string packageName, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                return false;

            var escaped = packageName.Replace("'", "''");
            var command = "if (@(Get-AppxPackage -AllUsers -Name '" + escaped + "' -ErrorAction SilentlyContinue).Count -gt 0 -or " +
                          "@(Get-AppxPackage -Name '" + escaped + "' -ErrorAction SilentlyContinue).Count -gt 0) { '1' } else { '0' }";
            try
            {
                var output = await _powershell.RunAsync(command, token, false, 30).ConfigureAwait(false);
                return string.Equals((output ?? string.Empty).Trim(), "1", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        public async Task<IReadOnlyList<UwpPackageInfo>> GetInstalledPackagesAsync(CancellationToken token, IProgress<int> progress = null)
        {
            const string command = @"
$items = @();
$successCount = 0;
try { $items += @(Get-AppxPackage -ErrorAction Stop | Where-Object { $_.Name } | ForEach-Object { [pscustomobject]@{ Name=$_.Name; PackageFullName=$_.PackageFullName; Version=$_.Version.ToString(); Publisher=$_.Publisher; InstallLocation=$_.InstallLocation; Scope='CurrentUser' } }); $successCount++ } catch {}
try { $items += @(Get-AppxPackage -AllUsers -ErrorAction Stop | Where-Object { $_.Name } | ForEach-Object { [pscustomobject]@{ Name=$_.Name; PackageFullName=$_.PackageFullName; Version=$_.Version.ToString(); Publisher=$_.Publisher; InstallLocation=$_.InstallLocation; Scope='AllUsers' } }); $successCount++ } catch {}
try { $items += @(Get-AppxProvisionedPackage -Online -ErrorAction Stop | ForEach-Object { [pscustomobject]@{ Name=$_.DisplayName; PackageFullName=$_.PackageName; Version=$_.Version; Publisher=$_.PublisherId; InstallLocation=''; Scope='Provisioned' } }); $successCount++ } catch {}
if($successCount -eq 0) { throw 'Не удалось получить список AppX-пакетов ни одним из доступных способов.' }
$items | Sort-Object Name,Scope | ConvertTo-Json -Compress";
            var output = await _powershell.RunAsync(command, token, false, 90).ConfigureAwait(false);
            progress?.Report(20);
            var result = Parse(output);
            progress?.Report(100);
            return result;
        }

        public async Task RemovePackagesAsync(IEnumerable<UwpPackageInfo> packages, CancellationToken token)
        {
            var selected = (packages ?? Enumerable.Empty<UwpPackageInfo>())
                .Where(x => x != null && x.IsInstalled)
                .ToList();
            if (selected.Count == 0) throw new ArgumentException("Не выбраны установленные приложения.", nameof(packages));

            SaveBackup(selected);
            var literals = string.Join(",", selected.Select(x => "'" + (x.Name ?? string.Empty).Replace("'", "''") + "'"));
            var command = "$ErrorActionPreference='Stop'; $names=@(" + literals + "); " +
                "foreach($name in $names){ " +
                "try { Get-AppxPackage -Name $name | ForEach-Object { Remove-AppxPackage -Package $_.PackageFullName -ErrorAction Stop } } catch { if($Error[0].Exception.Message -notmatch 'not found'){ throw } } " +
                "try { Get-AppxPackage -AllUsers -Name $name | ForEach-Object { Remove-AppxPackage -AllUsers -Package $_.PackageFullName -ErrorAction Stop } } catch {} " +
                "try { Get-AppxProvisionedPackage -Online | Where-Object { $_.DisplayName -eq $name } | ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction Stop | Out-Null } } catch {} " +
                "if(@(Get-AppxPackage -Name $name -ErrorAction SilentlyContinue).Count -gt 0 -or @(Get-AppxPackage -AllUsers -Name $name -ErrorAction SilentlyContinue).Count -gt 0 -or @(Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -eq $name }).Count -gt 0){ throw ('Пакет ' + $name + ' всё ещё установлен или подготовлен.') } }";
            await _powershell.RunAsync(command, token, true, 300).ConfigureAwait(false);
        }

        private static void SaveBackup(IReadOnlyList<UwpPackageInfo> packages)
        {
            try
            {
                var payload = packages.Select(x => new
                {
                    x.Name, x.PackageFullName, x.Version, x.Publisher, x.InstallLocation,
                    x.InstalledForCurrentUser, x.InstalledForAllUsers, x.IsProvisioned,
                    CreatedAtUtc = DateTime.UtcNow
                }).ToList();
                var path = UserDataPath.File("uwp-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private static IReadOnlyList<UwpPackageInfo> Parse(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return new List<UwpPackageInfo>();
            using (var document = JsonDocument.Parse(output))
            {
                var elements = new List<JsonElement>();
                if (document.RootElement.ValueKind == JsonValueKind.Array) elements.AddRange(document.RootElement.EnumerateArray());
                else if (document.RootElement.ValueKind == JsonValueKind.Object) elements.Add(document.RootElement);

                var result = new Dictionary<string, UwpPackageInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var element in elements)
                {
                    var name = GetString(element, "Name");
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (!result.TryGetValue(name, out var item))
                    {
                        item = new UwpPackageInfo { Name = name };
                        result[name] = item;
                    }
                    var scope = GetString(element, "Scope");
                    if (scope.Equals("CurrentUser", StringComparison.OrdinalIgnoreCase)) item.InstalledForCurrentUser = true;
                    else if (scope.Equals("AllUsers", StringComparison.OrdinalIgnoreCase)) item.InstalledForAllUsers = true;
                    else if (scope.Equals("Provisioned", StringComparison.OrdinalIgnoreCase)) item.IsProvisioned = true;
                    item.PackageFullName = FirstNonEmpty(item.PackageFullName, GetString(element, "PackageFullName"));
                    item.Version = FirstNonEmpty(item.Version, GetString(element, "Version"));
                    item.Publisher = FirstNonEmpty(item.Publisher, GetString(element, "Publisher"));
                    item.InstallLocation = FirstNonEmpty(item.InstallLocation, GetString(element, "InstallLocation"));
                    item.IsInstalled = item.InstalledForCurrentUser || item.InstalledForAllUsers || item.IsProvisioned;
                }
                return result.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        private static string GetString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : string.Empty;
        }

        private static string FirstNonEmpty(string first, string second) => string.IsNullOrWhiteSpace(first) ? second : first;
    }
}
