using System;
using System.Collections.Generic;
using System.Globalization;

namespace WinClonePro.Core.Models;

public sealed class DiskInfo
{
    public int Index { get; init; }
    public string Model { get; init; } = "";
    public string SerialNumber { get; init; } = "";
    public double SizeGB { get; init; }
    public string MediaType { get; init; } = "Unknown"; // SSD / HDD / NVMe / Removable / Unknown
    public string Status { get; init; } = "Unknown"; // OK / Degraded / Unknown
    public bool IsBootDisk { get; init; }
    public int PartitionCount { get; init; }
    public List<string> DriveLetters { get; init; } = new();
    public List<string> VolumeLabels { get; init; } = new();
    public string SystemDriveLetter { get; init; } = "";
    public bool ContainsOS { get; init; }

    public string FriendlySize => FormatSizeGb(SizeGB);
    public string DriveLettersDisplay => string.Join(", ", DriveLetters);

    public string HealthBadge => Status.Trim().ToUpperInvariant() switch
    {
        "OK" => "Healthy",
        "DEGRADED" => "Warning",
        _ => "Critical"
    };

    private static string FormatSizeGb(double sizeGb)
    {
        if (sizeGb <= 0)
        {
            return "0 GB";
        }

        // Keep it simple for UI: round to nearest whole GB.
        var rounded = (long)Math.Round(sizeGb, MidpointRounding.AwayFromZero);
        return rounded.ToString(CultureInfo.InvariantCulture) + " GB";
    }
}

