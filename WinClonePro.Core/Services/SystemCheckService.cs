using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class SystemCheckService : ISystemCheckService
{
    private readonly IToolResolver _toolResolver;

    public SystemCheckService(IToolResolver toolResolver)
    {
        _toolResolver = toolResolver ?? throw new ArgumentNullException(nameof(toolResolver));
    }

    public async Task<SystemCheckResult> RunAllChecksAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

            var dism = await _toolResolver.ResolveAsync("dism.exe", ct).ConfigureAwait(false);
            var diskPart = await _toolResolver.ResolveAsync("diskpart.exe", ct).ConfigureAwait(false);
            var bcdBoot = await _toolResolver.ResolveAsync("bcdboot.exe", ct).ConfigureAwait(false);
            var adkInstalled = await _toolResolver.IsAdkInstalledAsync(ct).ConfigureAwait(false);
            var winPeToolsAvailable = await _toolResolver.AreWinPeToolsAvailableAsync(ct).ConfigureAwait(false);

            Log.Information(
                "System check: IsAdministrator={IsAdministrator}, DismSource={DismSource}, DiskPartSource={DiskPartSource}, BcdBootSource={BcdBootSource}, AdkInstalled={AdkInstalled}, WinPeToolsAvailable={WinPeToolsAvailable}",
                isAdmin,
                dism.Source,
                diskPart.Source,
                bcdBoot.Source,
                adkInstalled,
                winPeToolsAvailable);

            if (!isAdmin)
            {
                errors.Add("Administrator privileges are required.");
            }

            if (!dism.IsAvailable)
            {
                errors.Add("DISM (dism.exe) is missing. Install Windows ADK or ensure DISM is available.");
            }
            else if (dism.Source != DependencySource.System)
            {
                warnings.Add($"DISM will run from {dism.Source}: {dism.ResolvedPath}");
            }

            if (!diskPart.IsAvailable)
            {
                errors.Add("DiskPart (diskpart.exe) is missing.");
            }
            else if (diskPart.Source != DependencySource.System)
            {
                warnings.Add($"DiskPart will run from {diskPart.Source}: {diskPart.ResolvedPath}");
            }

            if (!bcdBoot.IsAvailable)
            {
                errors.Add("BCDBoot (bcdboot.exe) is missing.");
            }
            else if (bcdBoot.Source != DependencySource.System)
            {
                warnings.Add($"BCDBoot will run from {bcdBoot.Source}: {bcdBoot.ResolvedPath}");
            }

            if (!adkInstalled)
            {
                errors.Add("Windows ADK Deployment Tools are not installed.");
            }

            if (!winPeToolsAvailable)
            {
                warnings.Add("WinPE media tools are unavailable. WinPE creation will remain disabled until the WinPE add-on or embedded tools are present.");
            }

            return new SystemCheckResult
            {
                IsAdministrator = isAdmin,
                DismTool = dism,
                DiskPartTool = diskPart,
                BcdBootTool = bcdBoot,
                AdkInstalled = adkInstalled,
                WinPeToolsAvailable = winPeToolsAvailable,
                Errors = errors,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SystemCheckService failed unexpectedly; returning unhealthy result.");
            errors.Add("System check failed unexpectedly.");

            return new SystemCheckResult
            {
                IsAdministrator = false,
                DismTool = ToolResolution.Missing("dism.exe"),
                DiskPartTool = ToolResolution.Missing("diskpart.exe"),
                BcdBootTool = ToolResolution.Missing("bcdboot.exe"),
                AdkInstalled = false,
                WinPeToolsAvailable = false,
                Errors = errors
            };
        }
    }
}
