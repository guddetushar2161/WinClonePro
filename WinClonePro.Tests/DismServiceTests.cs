using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using WinClonePro.Core.Exceptions;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;
using WinClonePro.Core.Services;
using Xunit;

namespace WinClonePro.Tests;

[SupportedOSPlatform("windows")]
public class DismServiceTests
{
    [Fact]
    public async Task CaptureImageAsync_NonExistentSourceDrive_ThrowsArgumentException()
    {
        var service = CreateService(
            io: new FakeSystemIo
            {
                DirectoryExistsResult = false
            });

        var req = new CaptureRequest
        {
            SourceDrive = "Z:",
            OutputWimPath = @"C:\temp\test.wim",
            ImageName = "t"
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CaptureImageAsync(req, new Progress<int>(), CancellationToken.None));
    }

    [Fact]
    public async Task CaptureImageAsync_InsufficientSpace_ThrowsInsufficientSpaceException()
    {
        var io = new FakeSystemIo
        {
            DirectoryExistsResult = true,
            PathRootResult = "D:\\",
            DirectoryNameResult = @"D:\temp",
            SourceMetrics = new DriveMetrics(TotalSize: 200L * 1024 * 1024 * 1024, AvailableFreeSpace: 20L * 1024 * 1024 * 1024),
            OutputMetrics = new DriveMetrics(TotalSize: 100L * 1024 * 1024 * 1024, AvailableFreeSpace: 1L * 1024 * 1024 * 1024)
        };

        var service = CreateService(io: io);
        var req = new CaptureRequest
        {
            SourceDrive = "C:",
            OutputWimPath = @"D:\temp\test.wim",
            ImageName = "t"
        };

        var ex = await Assert.ThrowsAsync<InsufficientSpaceException>(() =>
            service.CaptureImageAsync(req, new Progress<int>(), CancellationToken.None));

        Assert.True(ex.RequiredBytes > ex.AvailableBytes);
        Assert.Equal(io.OutputMetrics.AvailableFreeSpace, ex.AvailableBytes);
    }

    [Fact]
    public async Task GetWimInfoAsync_NonExistentWim_ReturnsEmptyList()
    {
        var io = new FakeSystemIo { FileExistsResult = false };
        var service = CreateService(io: io);

        var result = await service.GetWimInfoAsync(@"C:\missing.wim", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ApplyImageAsync_TargetBootDisk_ThrowsSafetyViolationException()
    {
        var disks = new List<DiskInfo>
        {
            new()
            {
                Index = 1,
                Model = "Disk",
                SerialNumber = "ABC",
                IsBootDisk = true
            }
        };

        var diskService = new Mock<IDiskService>();
        diskService.Setup(x => x.GetAllDisksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(disks);

        var processRunner = new Mock<IProcessRunner>();
        processRunner.Setup(x => x.RunAsync(
                It.IsAny<string>(),
                It.Is<string>(a => a.Contains("/Get-WimInfo", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()))
            .Returns<string, string, IProgress<string>, CancellationToken, int>((_, __, output, ___, ____) =>
            {
                output.Report("Index : 1");
                output.Report("Name : Test");
                output.Report("Description : Test");
                output.Report("Size : 1 bytes");
                return Task.CompletedTask;
            });

        var toolResolver = new Mock<IToolResolver>();
        toolResolver.Setup(x => x.ResolveAsync("dism.exe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolution
            {
                ToolName = "dism.exe",
                ResolvedPath = @"C:\Windows\System32\dism.exe",
                Source = DependencySource.System
            });
        toolResolver.Setup(x => x.ResolveAsync("diskpart.exe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolution
            {
                ToolName = "diskpart.exe",
                ResolvedPath = @"C:\Windows\System32\diskpart.exe",
                Source = DependencySource.System
            });
        toolResolver.Setup(x => x.ResolveAsync("bcdboot.exe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolution
            {
                ToolName = "bcdboot.exe",
                ResolvedPath = @"C:\Windows\System32\bcdboot.exe",
                Source = DependencySource.System
            });

        var io = new FakeSystemIo
        {
            FileExistsResult = true
        };

        var builder = new DiskPartScriptBuilder(new AppSettings(), processRunner.Object, io, toolResolver.Object);
        var service = new DismService(processRunner.Object, diskService.Object, builder, toolResolver.Object, io);

        var req = new ApplyRequest
        {
            WimPath = @"C:\img.wim",
            TargetDiskIndex = 1,
            ImageIndex = 1
        };

        await Assert.ThrowsAsync<SafetyViolationException>(() =>
            service.ApplyImageAsync(req, new Progress<int>(), CancellationToken.None));
    }

    [Fact]
    public void TryParseDismProgress_ExtractsExpectedPercentages()
    {
        var lines = new[]
        {
            "[==                       5.0%                      ==]",
            "[=========                23.4%                     ==]",
            "[==========================100.0%====================]"
        };

        var values = new List<int>();
        foreach (var line in lines)
        {
            if (DismService.TryParseDismProgress(line, out var pct))
            {
                values.Add(pct);
            }
        }

        Assert.Equal(new[] { 5, 23, 100 }, values);
    }

    private static DismService CreateService(FakeSystemIo? io = null)
    {
        var processRunner = new Mock<IProcessRunner>();
        var diskService = new Mock<IDiskService>();
        var toolResolver = new Mock<IToolResolver>();
        diskService.Setup(x => x.GetAllDisksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DiskInfo>());

        toolResolver.Setup(x => x.ResolveAsync("dism.exe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolution
            {
                ToolName = "dism.exe",
                ResolvedPath = @"C:\Windows\System32\dism.exe",
                Source = DependencySource.System
            });
        toolResolver.Setup(x => x.ResolveAsync("diskpart.exe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolution
            {
                ToolName = "diskpart.exe",
                ResolvedPath = @"C:\Windows\System32\diskpart.exe",
                Source = DependencySource.System
            });
        toolResolver.Setup(x => x.ResolveAsync("bcdboot.exe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolution
            {
                ToolName = "bcdboot.exe",
                ResolvedPath = @"C:\Windows\System32\bcdboot.exe",
                Source = DependencySource.System
            });

        var effectiveIo = io ?? new FakeSystemIo();
        var builder = new DiskPartScriptBuilder(new AppSettings(), processRunner.Object, effectiveIo, toolResolver.Object);
        return new DismService(processRunner.Object, diskService.Object, builder, toolResolver.Object, effectiveIo);
    }

    private sealed class FakeSystemIo : ISystemIo
    {
        public bool DirectoryExistsResult { get; set; } = true;
        public bool FileExistsResult { get; set; } = false;
        public string PathRootResult { get; set; } = "C:\\";
        public string DirectoryNameResult { get; set; } = @"C:\temp";
        public DriveMetrics SourceMetrics { get; set; } =
            new(TotalSize: 100L * 1024 * 1024 * 1024, AvailableFreeSpace: 50L * 1024 * 1024 * 1024);
        public DriveMetrics OutputMetrics { get; set; } =
            new(TotalSize: 100L * 1024 * 1024 * 1024, AvailableFreeSpace: 50L * 1024 * 1024 * 1024);

        public bool DirectoryExists(string path) => DirectoryExistsResult;
        public void CreateDirectory(string path) { }
        public bool FileExists(string path) => FileExistsResult;
        public void MoveFile(string sourcePath, string destinationPath, bool overwrite) { }
        public void WriteAllText(string path, string content) { }
        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct) => Task.CompletedTask;
        public long GetFileLength(string path) => 10;
        public string? GetPathRoot(string path) => PathRootResult;
        public string? GetDirectoryName(string path) => DirectoryNameResult;
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => Array.Empty<string>();
        public void DeleteFile(string path) { }
        public void DeleteDirectory(string path, bool recursive) { }

        public DriveMetrics GetDriveMetrics(string rootPath)
        {
            if (rootPath.StartsWith("D", StringComparison.OrdinalIgnoreCase))
            {
                return OutputMetrics;
            }

            if (rootPath.StartsWith("C", StringComparison.OrdinalIgnoreCase))
            {
                return SourceMetrics;
            }

            return SourceMetrics;
        }
    }
}

