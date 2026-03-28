using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Services;

public sealed class DiskService : IDiskService
{
    private readonly IWmiQueryRunner _wmi;
    private readonly ISystemIo _systemIo;

    public DiskService(IWmiQueryRunner wmi, ISystemIo? systemIo = null)
    {
        _wmi = wmi ?? throw new ArgumentNullException(nameof(wmi));
        _systemIo = systemIo ?? new SystemIo();
    }

    public async Task<List<DiskInfo>> GetAllDisksAsync(CancellationToken ct)
    {
        try
        {
            var systemDriveLetter = GetSystemDriveLetter();

            // Win32_DiskDrive: Index, Model, SerialNumber, Size, MediaType, Status, RotationRate, DeviceID
            var disks = await _wmi.QueryAsync(
                "SELECT * FROM Win32_DiskDrive",
                ct).ConfigureAwait(false);

            var diskInfos = new List<DiskInfo>(capacity: disks.Count);

            foreach (var d in disks)
            {
                ct.ThrowIfCancellationRequested();

                var index = ParseInt(d.GetValueOrDefault("Index"));
                var deviceId = AsString(d.GetValueOrDefault("DeviceID"));

                var model = AsString(d.GetValueOrDefault("Model"));
                var serial = AsString(d.GetValueOrDefault("SerialNumber"));
                var sizeBytes = ParseLong(d.GetValueOrDefault("Size"));
                var sizeGb = sizeBytes > 0 ? sizeBytes / 1024d / 1024d / 1024d : 0d;

                var mediaTypeRaw = AsString(d.GetValueOrDefault("MediaType"));
                var statusRaw = AsString(d.GetValueOrDefault("Status"));
                var rotationRate = ParseLong(d.GetValueOrDefault("RotationRate"));

                var normalizedStatus = NormalizeStatus(statusRaw);
                var mediaType = DetectMediaType(model, mediaTypeRaw, rotationRate);

                var partitionInfos = await GetPartitionsForDiskAsync(deviceId, ct).ConfigureAwait(false);
                var partitionCount = partitionInfos.Count;

                var hasEfiPartition = partitionInfos.Any(p =>
                    p.Type.Contains("GPT: System", StringComparison.OrdinalIgnoreCase));

                var hasSystemVolume = false;
                var allDriveLetters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allVolumeLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var containsOs = false;
                var osDrive = "";
                foreach (var part in partitionInfos)
                {
                    ct.ThrowIfCancellationRequested();

                    var logicals = await GetLogicalDisksForPartitionAsync(part.DeviceId, ct).ConfigureAwait(false);
                    foreach (var (drive, label) in logicals)
                    {
                        if (!string.IsNullOrWhiteSpace(drive))
                        {
                            allDriveLetters.Add(drive);
                        }

                        if (!string.IsNullOrWhiteSpace(label))
                        {
                            allVolumeLabels.Add(label);
                        }

                        if (drive.Equals(systemDriveLetter, StringComparison.OrdinalIgnoreCase))
                        {
                            hasSystemVolume = true;
                        }

                        if (!containsOs)
                        {
                            var windowsPath = $"{drive}\\Windows";
                            if (_systemIo.DirectoryExists(windowsPath))
                            {
                                containsOs = true;
                                osDrive = drive;
                            }
                        }
                    }

                    if (hasSystemVolume)
                    {
                        // Keep going to gather full drive/label mapping.
                    }
                }

                var isBootDisk = hasSystemVolume && hasEfiPartition;

                diskInfos.Add(new DiskInfo
                {
                    Index = index,
                    Model = model,
                    SerialNumber = serial,
                    SizeGB = sizeGb,
                    MediaType = mediaType,
                    Status = normalizedStatus,
                    IsBootDisk = isBootDisk,
                    PartitionCount = partitionCount,
                    DriveLetters = allDriveLetters.OrderBy(x => x).ToList(),
                    VolumeLabels = allVolumeLabels.OrderBy(x => x).ToList(),
                    ContainsOS = containsOs,
                    SystemDriveLetter = osDrive
                });
            }

            return diskInfos
                .OrderBy(d => d.Index)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DiskService failed in GetAllDisksAsync.");
            throw;
        }
    }

    public async Task<bool> IsDiskHealthyAsync(int diskIndex, CancellationToken ct)
    {
        try
        {
            var disks = await GetAllDisksAsync(ct).ConfigureAwait(false);
            var disk = disks.FirstOrDefault(d => d.Index == diskIndex);
            if (disk is null)
            {
                return false;
            }

            return disk.HealthBadge.Equals("Healthy", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DiskService failed in IsDiskHealthyAsync. diskIndex {DiskIndex}", diskIndex);
            throw;
        }
    }

    internal static string DetectMediaType(string model, string mediaTypeRaw, long rotationRate)
    {
        var mt = (mediaTypeRaw ?? string.Empty).Trim();
        var m = (model ?? string.Empty).Trim();

        if (mt.Contains("Removable", StringComparison.OrdinalIgnoreCase))
        {
            return "Removable";
        }

        if (m.Contains("NVME", StringComparison.OrdinalIgnoreCase))
        {
            return "NVMe";
        }

        if (mt.Contains("SSD", StringComparison.OrdinalIgnoreCase) || rotationRate == 0)
        {
            return "SSD";
        }

        if (string.IsNullOrWhiteSpace(mt) && string.IsNullOrWhiteSpace(m))
        {
            return "Unknown";
        }

        return "HDD";
    }

    private static string NormalizeStatus(string statusRaw)
    {
        var s = (statusRaw ?? string.Empty).Trim();
        if (s.Equals("OK", StringComparison.OrdinalIgnoreCase))
        {
            return "OK";
        }

        if (s.Contains("Degraded", StringComparison.OrdinalIgnoreCase))
        {
            return "Degraded";
        }

        return "Unknown";
    }

    private async Task<List<(string DeviceId, string Type)>> GetPartitionsForDiskAsync(string diskDeviceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(diskDeviceId))
        {
            return new List<(string, string)>();
        }

        // Assoc query: DiskDrive -> DiskPartition
        var escaped = EscapeWmiStringLiteral(diskDeviceId);
        var wql = $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID=\"{escaped}\"}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";

        var partitions = await _wmi.QueryAsync(wql, ct).ConfigureAwait(false);
        var list = new List<(string DeviceId, string Type)>(partitions.Count);

        foreach (var p in partitions)
        {
            ct.ThrowIfCancellationRequested();
            var deviceId = AsString(p.GetValueOrDefault("DeviceID"));
            var type = AsString(p.GetValueOrDefault("Type"));
            list.Add((deviceId, type));
        }

        return list;
    }

    private async Task<List<(string DriveLetter, string VolumeLabel)>> GetLogicalDisksForPartitionAsync(string partitionDeviceId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(partitionDeviceId))
        {
            return new List<(string, string)>();
        }

        var escaped = EscapeWmiStringLiteral(partitionDeviceId);
        var wql = $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID=\"{escaped}\"}} WHERE AssocClass = Win32_LogicalDiskToPartition";

        var logicals = await _wmi.QueryAsync(wql, ct).ConfigureAwait(false);
        var list = new List<(string DriveLetter, string VolumeLabel)>(logicals.Count);

        foreach (var l in logicals)
        {
            ct.ThrowIfCancellationRequested();
            var id = AsString(l.GetValueOrDefault("DeviceID"));
            var label = AsString(l.GetValueOrDefault("VolumeName"));
            if (!string.IsNullOrWhiteSpace(id))
            {
                list.Add((id, label));
            }
        }

        return list;
    }

    private static string GetSystemDriveLetter()
    {
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrWhiteSpace(sys))
        {
            return "C:";
        }

        try
        {
            var root = Path.GetPathRoot(sys) ?? "C:\\";
            var drive = root.TrimEnd('\\');
            if (drive.EndsWith(':'))
            {
                return drive;
            }

            return drive + ":";
        }
        catch
        {
            return "C:";
        }
    }

    private static string AsString(object? value) => value?.ToString() ?? "";

    private static string EscapeWmiStringLiteral(string value)
    {
        // WMI object paths in associators queries commonly require escaping backslashes and quotes.
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static int ParseInt(object? value)
    {
        var s = value?.ToString() ?? "";
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
    }

    private static long ParseLong(object? value)
    {
        var s = value?.ToString() ?? "";
        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : 0L;
    }
}

