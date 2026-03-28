using System;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Interfaces;

public interface IEmbeddedToolService
{
    Task<EmbeddedToolExtractionResult> ExtractToolsAsync(IProgress<int> progress, CancellationToken ct);
}
