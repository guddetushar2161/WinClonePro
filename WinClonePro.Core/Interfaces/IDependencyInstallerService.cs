using System;
using System.Threading;
using System.Threading.Tasks;

namespace WinClonePro.Core.Interfaces;

public interface IDependencyInstallerService
{
    Task<bool> EnsureAdkDeploymentToolsInstalledAsync(IProgress<int> progress, CancellationToken ct);
}
