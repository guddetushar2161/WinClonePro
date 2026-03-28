namespace WinClonePro.Core.Models;

public sealed class ApplyRequest
{
    public string WimPath { get; init; } = "";
    public int ImageIndex { get; init; } = 1;
    public int TargetDiskIndex { get; init; }
    public string TargetDriveLetter { get; init; } = "";
    public bool InjectDrivers { get; init; } = false;
    public string DriverFolderPath { get; init; } = "";
}

