using System.Collections.Generic;

namespace WinClonePro.Core.Models;

public sealed class CaptureRequest
{
    public string SourceDrive { get; init; } = ""; // e.g. "C:"
    public string OutputWimPath { get; init; } = "";
    public string ImageName { get; init; } = "";
    public CompressionType Compression { get; init; } = CompressionType.Fast;
    public bool VerifyAfterCapture { get; init; } = true;
    public List<string> AdditionalVolumes { get; init; } = new();
}

