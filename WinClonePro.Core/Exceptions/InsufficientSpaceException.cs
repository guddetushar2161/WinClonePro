using System;
using System.Globalization;

namespace WinClonePro.Core.Exceptions;

public class InsufficientSpaceException : Exception
{
    public long RequiredBytes { get; }
    public long AvailableBytes { get; }
    public string DriveLetter { get; }

    public InsufficientSpaceException(long requiredBytes, long availableBytes, string driveLetter)
        : base(BuildMessage(requiredBytes, availableBytes, driveLetter))
    {
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        DriveLetter = driveLetter ?? "";
    }

    public InsufficientSpaceException(string message, long requiredBytes, long availableBytes, string driveLetter)
        : base(message)
    {
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        DriveLetter = driveLetter ?? "";
    }

    public InsufficientSpaceException(string message, Exception innerException, long requiredBytes, long availableBytes, string driveLetter)
        : base(message, innerException)
    {
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        DriveLetter = driveLetter ?? "";
    }

    private static string BuildMessage(long requiredBytes, long availableBytes, string driveLetter)
    {
        var requiredGb = requiredBytes / 1024d / 1024d / 1024d;
        var availableGb = availableBytes / 1024d / 1024d / 1024d;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Not enough space. Need {requiredGb:F1} GB, only {availableGb:F1} GB available on {driveLetter}:");
    }
}

