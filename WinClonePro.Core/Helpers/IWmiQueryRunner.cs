using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WinClonePro.Core.Helpers;

public interface IWmiQueryRunner
{
    Task<List<Dictionary<string, object?>>> QueryAsync(string wql, CancellationToken ct);
}

