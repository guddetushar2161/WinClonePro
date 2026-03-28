using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Interfaces;

public interface IDismService
{
    Task<CaptureResult> CaptureImageAsync(CaptureRequest request, IProgress<int> progress, CancellationToken ct);
    Task<bool> ApplyImageAsync(ApplyRequest request, IProgress<int> progress, CancellationToken ct);
    Task<bool> CheckImageIntegrityAsync(string wimPath, CancellationToken ct);
    Task<List<ImageInfo>> GetWimInfoAsync(string wimPath, CancellationToken ct);
}

