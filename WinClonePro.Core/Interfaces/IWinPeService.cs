using System.Threading;
using System.Threading.Tasks;

namespace WinClonePro.Core.Interfaces;

public interface IWinPeService
{
    Task<string> CreateWinPeAsync(string outputPath, IProgress<int> progress, CancellationToken ct);
    Task<bool> InjectDriversAsync(string winPePath, string driverFolder, CancellationToken ct);
    Task<bool> CreateBootableUsbAsync(string isoPath, int diskIndex, IProgress<int> progress, CancellationToken ct);
}

