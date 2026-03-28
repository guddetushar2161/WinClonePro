using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Interfaces;

public interface IDiskService
{
    Task<List<DiskInfo>> GetAllDisksAsync(CancellationToken ct);
    Task<bool> IsDiskHealthyAsync(int diskIndex, CancellationToken ct);
}

