using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Models;
using WinClonePro.Core.Services;
using Xunit;

namespace WinClonePro.Tests;

public class DiskServiceTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task GetAllDisksAsync_ReturnsAtLeastOneDisk_OnRealHardware()
    {
        var svc = new DiskService(new WmiQueryRunner());

        var disks = await svc.GetAllDisksAsync(CancellationToken.None);

        Assert.NotNull(disks);
        Assert.True(disks.Count >= 1);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task GetAllDisksAsync_NoDiskHasNullOrEmptyModelOrSerialNumber()
    {
        var svc = new DiskService(new WmiQueryRunner());

        var disks = await svc.GetAllDisksAsync(CancellationToken.None);

        Assert.All(disks, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Model));
            Assert.False(string.IsNullOrWhiteSpace(d.SerialNumber));
        });
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task GetAllDisksAsync_BootDiskIsIdentified()
    {
        var svc = new DiskService(new WmiQueryRunner());

        var disks = await svc.GetAllDisksAsync(CancellationToken.None);

        Assert.Contains(disks, d => d.IsBootDisk);
    }

    [Fact]
    public async Task MediaTypeDetection_UsesMockWmiData()
    {
        var fake = new FakeWmiQueryRunner();
        var fakeIo = new FakeSystemIo();
        // DiskDrive record
        fake.AddResult(
            "SELECT * FROM Win32_DiskDrive",
            new Dictionary<string, object?>
            {
                ["Index"] = 0,
                ["Model"] = "Samsung NVMe",
                ["SerialNumber"] = "ABC123",
                ["Size"] = 512L * 1024 * 1024 * 1024,
                ["MediaType"] = "Fixed hard disk media",
                ["Status"] = "OK",
                ["RotationRate"] = 0,
                ["DeviceID"] = @"\\.\PHYSICALDRIVE0"
            });

        // Partitions for disk: include EFI + another partition
        fake.AddResult(
            "ASSOCIATORS OF {Win32_DiskDrive.DeviceID=\"\\\\\\\\.\\\\PHYSICALDRIVE0\"} WHERE AssocClass = Win32_DiskDriveToDiskPartition",
            new Dictionary<string, object?>
            {
                ["DeviceID"] = "Disk #0, Partition #1",
                ["Type"] = "GPT: System"
            },
            new Dictionary<string, object?>
            {
                ["DeviceID"] = "Disk #0, Partition #2",
                ["Type"] = "Installable File System"
            });

        // Logical disks for partitions: make partition #2 map to C:
        fake.AddResult(
            "ASSOCIATORS OF {Win32_DiskPartition.DeviceID=\"Disk #0, Partition #1\"} WHERE AssocClass = Win32_LogicalDiskToPartition");
        fake.AddResult(
            "ASSOCIATORS OF {Win32_DiskPartition.DeviceID=\"Disk #0, Partition #2\"} WHERE AssocClass = Win32_LogicalDiskToPartition",
            new Dictionary<string, object?>
            {
                ["DeviceID"] = GetSystemDriveLetterForTest()
            });

        fakeIo.WindowsPaths.Add($"{GetSystemDriveLetterForTest()}\\Windows");
        var svc = new DiskService(fake, fakeIo);
        var disks = await svc.GetAllDisksAsync(CancellationToken.None);

        Assert.Single(disks);
        Assert.Equal("NVMe", disks[0].MediaType);
        Assert.True(disks[0].IsBootDisk);
        Assert.True(disks[0].ContainsOS);
        Assert.Equal(GetSystemDriveLetterForTest(), disks[0].SystemDriveLetter);
        Assert.Contains(GetSystemDriveLetterForTest(), disks[0].DriveLetters);
    }

    private static string GetSystemDriveLetterForTest()
    {
        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrWhiteSpace(sys) || sys.Length < 2)
        {
            return "C:";
        }

        return sys.Substring(0, 2);
    }

    private sealed class FakeWmiQueryRunner : IWmiQueryRunner
    {
        private readonly Dictionary<string, List<Dictionary<string, object?>>> _map =
            new(StringComparer.OrdinalIgnoreCase);

        public void AddResult(string wql, params Dictionary<string, object?>[] rows)
        {
            _map[wql] = rows.ToList();
        }

        public Task<List<Dictionary<string, object?>>> QueryAsync(string wql, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(_map.TryGetValue(wql, out var rows) ? rows : new List<Dictionary<string, object?>>());
        }
    }

    private sealed class FakeSystemIo : ISystemIo
    {
        public HashSet<string> WindowsPaths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public bool DirectoryExists(string path) => WindowsPaths.Contains(path);
        public void CreateDirectory(string path) { }
        public bool FileExists(string path) => true;
        public void MoveFile(string sourcePath, string destinationPath, bool overwrite) { }
        public void WriteAllText(string path, string content) { }
        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct) => Task.CompletedTask;
        public long GetFileLength(string path) => 0;
        public string? GetPathRoot(string path) => "C:\\";
        public string? GetDirectoryName(string path) => "C:\\";
        public DriveMetrics GetDriveMetrics(string rootPath) => new(0, 0);
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => Array.Empty<string>();
        public void DeleteFile(string path) { }
        public void DeleteDirectory(string path, bool recursive) { }
    }
}

