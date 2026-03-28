using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Interfaces;

public interface IToolResolver
{
    Task<ToolResolution> ResolveAsync(string fileName, CancellationToken ct);
    Task<bool> IsAdkInstalledAsync(CancellationToken ct);
    Task<bool> AreWinPeToolsAvailableAsync(CancellationToken ct);
}
