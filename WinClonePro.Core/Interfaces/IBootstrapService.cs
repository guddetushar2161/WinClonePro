using System;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Interfaces;

public interface IBootstrapService
{
    Task<BootstrapResult> PrepareSystemAsync(IProgress<BootstrapProgressState> progress, CancellationToken ct);
}
