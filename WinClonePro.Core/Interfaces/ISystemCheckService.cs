using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Interfaces;

public interface ISystemCheckService
{
    Task<SystemCheckResult> RunAllChecksAsync(CancellationToken ct);
}

