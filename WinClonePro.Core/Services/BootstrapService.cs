using System;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class BootstrapService : IBootstrapService
{
    private readonly IEmbeddedToolService _embeddedToolService;
    private readonly ISystemCheckService _systemCheckService;
    private readonly IDependencyInstallerService _dependencyInstallerService;

    public BootstrapService(
        IEmbeddedToolService embeddedToolService,
        ISystemCheckService systemCheckService,
        IDependencyInstallerService dependencyInstallerService)
    {
        _embeddedToolService = embeddedToolService ?? throw new ArgumentNullException(nameof(embeddedToolService));
        _systemCheckService = systemCheckService ?? throw new ArgumentNullException(nameof(systemCheckService));
        _dependencyInstallerService = dependencyInstallerService ?? throw new ArgumentNullException(nameof(dependencyInstallerService));
    }

    public async Task<BootstrapResult> PrepareSystemAsync(IProgress<BootstrapProgressState> progress, CancellationToken ct)
    {
        try
        {
            Report(progress, BootstrapStage.ExtractingTools, 5, "Extracting tools", "Preparing embedded fallback tools...");
            var extractionProgress = new Progress<int>(value =>
                Report(progress, BootstrapStage.ExtractingTools, Math.Clamp(5 + (int)(value * 0.20), 5, 25), "Extracting tools", "Preparing embedded fallback tools..."));

            var extractionResult = await _embeddedToolService.ExtractToolsAsync(extractionProgress, ct).ConfigureAwait(false);
            foreach (var warning in extractionResult.Warnings)
            {
                Log.Warning("{Warning}", warning);
            }

            Report(progress, BootstrapStage.CheckingSystem, 30, "Checking system", "Validating required tools and permissions...");
            var systemCheck = await _systemCheckService.RunAllChecksAsync(ct).ConfigureAwait(false);

            if (!systemCheck.IsAdministrator)
            {
                return Fail(progress, systemCheck, "WinClone Pro must run as Administrator.");
            }

            if (!systemCheck.AreCoreToolsAvailable)
            {
                return Fail(progress, systemCheck, "Required Windows deployment tools are unavailable.");
            }

            if (!systemCheck.AdkInstalled)
            {
                Report(progress, BootstrapStage.InstallingComponents, 45, "Installing components", "Installing required components...");
                var installProgress = new Progress<int>(value =>
                    Report(progress, BootstrapStage.InstallingComponents, Math.Clamp(45 + (int)(value * 0.45), 45, 90), "Installing components", "Installing required components..."));

                var installed = await _dependencyInstallerService
                    .EnsureAdkDeploymentToolsInstalledAsync(installProgress, ct)
                    .ConfigureAwait(false);

                Report(progress, BootstrapStage.CheckingSystem, 92, "Checking system", "Re-validating installed components...");
                systemCheck = await _systemCheckService.RunAllChecksAsync(ct).ConfigureAwait(false);

                if (!installed || !systemCheck.AdkInstalled)
                {
                    return Fail(progress, systemCheck, "Windows ADK Deployment Tools could not be installed automatically.");
                }
            }

            Report(progress, BootstrapStage.Finalizing, 97, "Finalizing", "Starting the WinClone Pro interface...");
            Report(progress, BootstrapStage.Ready, 100, "Finalizing", "System ready.");

            return new BootstrapResult
            {
                Success = true,
                SystemCheckResult = systemCheck
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "BootstrapService failed unexpectedly.");
            var failedResult = new SystemCheckResult
            {
                Errors = { "Bootstrap failed unexpectedly." }
            };

            return Fail(progress, failedResult, "WinClone Pro could not prepare the system.");
        }
    }

    private static BootstrapResult Fail(IProgress<BootstrapProgressState> progress, SystemCheckResult systemCheck, string message)
    {
        Report(progress, BootstrapStage.Failed, 100, "Finalizing", message);
        return new BootstrapResult
        {
            Success = false,
            FailureMessage = message,
            SystemCheckResult = systemCheck
        };
    }

    private static void Report(
        IProgress<BootstrapProgressState> progress,
        BootstrapStage stage,
        int progressPercentage,
        string currentStep,
        string detail)
    {
        progress?.Report(new BootstrapProgressState
        {
            Stage = stage,
            ProgressPercentage = progressPercentage,
            CurrentStep = currentStep,
            Detail = detail
        });
    }
}
