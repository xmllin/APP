using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WpfApp1.Infrastructure.PowerShell;
using WpfApp1.Infrastructure.Processes;

namespace WpfApp1.Services
{
    public sealed class AppRemover
    {
        private static readonly string[] XboxPackageNames =
        {
            "Microsoft.GamingApp",
            "Microsoft.XboxApp",
            "Microsoft.XboxGamingOverlay",
            "Microsoft.XboxIdentityProvider",
            "Microsoft.Xbox.TCUI"
        };

        private readonly PowerShellRunner _powershell;

        public AppRemover(PowerShellRunner powershell = null)
        {
            _powershell = powershell ?? new PowerShellRunner(new SystemProcessRunner());
        }

        public async Task<bool> IsOutlookInstalledAsync(CancellationToken token)
        {
            return await IsAppxPackageInstalledAsync("Microsoft.OutlookForWindows", token).ConfigureAwait(false);
        }

        public bool IsOneDriveInstalled()
        {
            if (System.Diagnostics.Process.GetProcessesByName("OneDrive").Length > 0) return true;
            var candidates = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "OneDrive", "OneDrive.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft OneDrive", "OneDrive.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft OneDrive", "OneDrive.exe")
            };
            return candidates.Any(File.Exists) || IsOneDriveUninstallEntryPresent();
        }

        public async Task<bool> IsXboxInstalledAsync(CancellationToken token)
        {
            foreach (var package in XboxPackageNames)
                if (await IsAppxPackageInstalledAsync(package, token).ConfigureAwait(false)) return true;
            return false;
        }

        public Task RemoveOutlookAsync(CancellationToken token) => RemoveAppAsync(new[] { "Microsoft.OutlookForWindows" }, token);

        public Task RemoveXboxAsync(CancellationToken token) => RemoveAppAsync(XboxPackageNames, token);

        public async Task RemoveOneDriveAsync(CancellationToken token)
        {
            const string script = @"
$ErrorActionPreference = 'Stop'

Get-Process OneDrive -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep -Milliseconds 500

$setups = @(
    ""$env:SystemRoot\SysWOW64\OneDriveSetup.exe"",
    ""$env:SystemRoot\System32\OneDriveSetup.exe"",
    ""$env:LOCALAPPDATA\Microsoft\OneDrive\Update\OneDriveSetup.exe"",
    ""$env:ProgramFiles\Microsoft OneDrive\OneDriveSetup.exe"",
    ""${env:ProgramFiles(x86)}\Microsoft OneDrive\OneDriveSetup.exe""
)

$setup = $setups |
    Where-Object { $_ -and (Test-Path -LiteralPath $_) } |
    Select-Object -First 1

if (-not $setup)
{
    throw 'OneDriveSetup.exe не найден.'
}

$p = Start-Process `
    -FilePath $setup `
    -ArgumentList '/uninstall' `
    -Wait `
    -PassThru `
    -WindowStyle Hidden

if ($p.ExitCode -ne 0)
{
    throw ""OneDriveSetup завершился с кодом $($p.ExitCode).""
}
";

            await _powershell
                .RunAsync(script, token, true, 180)
                .ConfigureAwait(false);
        }

        private async Task<bool> IsAppxPackageInstalledAsync(string packageName, CancellationToken token)
        {
            var safe = (packageName ?? string.Empty).Replace("'", "''");
            var script = $@"
$found = $false
try {{ $found = @((Get-AppxPackage -Name '{safe}' -ErrorAction SilentlyContinue)).Count -gt 0 }} catch {{}}
try {{ if (-not $found) {{ $found = @((Get-AppxPackage -AllUsers -Name '{safe}' -ErrorAction SilentlyContinue)).Count -gt 0 }} }} catch {{}}
try {{ if (-not $found) {{ $found = @(Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object {{ $_.DisplayName -eq '{safe}' }}).Count -gt 0 }} }} catch {{}}
[bool]$found";
            var output = await _powershell.RunAsync(script, token, false, 30).ConfigureAwait(false);
            return output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
        }

        private async Task RemoveAppAsync(IEnumerable<string> packageNames, CancellationToken token)
        {
            var literals = string.Join(",", packageNames.Select(x => "'" + (x ?? string.Empty).Replace("'", "''") + "'"));
            var script = "$ErrorActionPreference='Stop'; $names=@(" + literals + "); foreach($name in $names){ " +
                "Get-AppxPackage -AllUsers -Name $name -ErrorAction SilentlyContinue | ForEach-Object { Remove-AppxPackage -AllUsers -Package $_.PackageFullName -ErrorAction Stop }; " +
                "Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -eq $name } | ForEach-Object { Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction Stop | Out-Null }" +
                " }";
            await _powershell.RunAsync(script, token, true, 180).ConfigureAwait(false);
        }

        private static bool IsOneDriveUninstallEntryPresent()
        {
            foreach (var uninstallRoot in new[]
            {
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall")
            })
            {
                using (uninstallRoot)
                {
                    if (uninstallRoot == null) continue;
                    foreach (var name in uninstallRoot.GetSubKeyNames())
                    {
                        using (var item = uninstallRoot.OpenSubKey(name))
                        {
                            var displayName = item?.GetValue("DisplayName") as string;
                            if (!string.IsNullOrWhiteSpace(displayName) && displayName.IndexOf("OneDrive", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
