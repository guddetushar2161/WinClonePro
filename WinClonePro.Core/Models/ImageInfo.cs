using System;
using System.Globalization;

namespace WinClonePro.Core.Models;

public sealed class ImageInfo
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public long SizeBytes { get; init; }
    public DateTime CapturedAt { get; init; }

    public string FriendlySize => FormatBytes(SizeBytes);

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        var formatted = unit <= 1
            ? Math.Round(size, 0).ToString(CultureInfo.InvariantCulture)
            : Math.Round(size, 1).ToString(CultureInfo.InvariantCulture);

        return $"{formatted} {units[unit]}";
    }
}

