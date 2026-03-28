using System.Collections.Generic;

namespace WinClonePro.Core.Models;

public sealed class EmbeddedToolExtractionResult
{
    public int ExtractedCount { get; init; }
    public List<string> ExtractedFiles { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}
