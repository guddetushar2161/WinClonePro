using System;

namespace WinClonePro.Core.Models;

public sealed class CaptureResult
{
    public bool Success { get; init; }
    public string WimPath { get; init; } = "";
    public long SizeBytes { get; init; }
    public TimeSpan Duration { get; init; }
    public bool IntegrityCheckPassed { get; init; }
    public string? ErrorMessage { get; init; }
}

