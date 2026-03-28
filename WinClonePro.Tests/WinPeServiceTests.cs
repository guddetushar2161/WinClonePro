using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public class WinPeServiceTests
{
    [Fact]
    public async Task CreateWinPeAsync_AdkMissing_ThrowsFileNotFoundException()
    {
        var processRunner = new Mock<IProcessRunner>(MockBehavior.Strict);
        var diskService = new Mock<IDiskService>(MockBehavior.Loose);
        var io = new FakeSystemIo();
        var toolResolver = CreateToolResolver(copypeAvailable: false, makeWinPeMediaAvailable: false);

        var service = new WinPeService(new AppSettings(), processRunner.Object, diskService.Object, io, toolResolver.Object);

        var progress = new Progress<int>();
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.CreateWinPeAsync(Path.GetTempPath(), progress, CancellationToken.None));

        Assert.Contains("Windows PE tools are unavailable", ex.Message, StringComparison.OrdinalIgnoreCase);
        processRunner.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateBootableUsbAsync_BootDisk_ThrowsSafetyViolationException()
    {
        var processRunner = new Mock<IProcessRunner>(MockBehavior.Loose);
        var diskService = new Mock<IDiskService>();

        diskService.Setup(x => x.GetAllDisksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DiskInfo>
            {
                new()
                {
                    Index = 2,
                    Model = "Disk",
                    SerialNumber = "ABC",
                    SizeGB = 10,
                    IsBootDisk = true
                }
            });

        var io = new FakeSystemIo { FileExistsResult = true };
        var toolResolver = CreateToolResolver();

        var service = new WinPeService(new AppSettings(), processRunner.Object, diskService.Object, io, toolResolver.Object);

        await Assert.ThrowsAsync<SafetyViolationException>(() =>
            service.CreateBootableUsbAsync(@"C:\fake.iso", 2, new Progress<int>(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateWinPeAsync_ReturnsIsoPath_WhenOutputPathValid()
    {
        var processRunner = new Mock<IProcessRunner>(MockBehavior.Strict);
        processRunner.Setup(x => x.RunAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var diskService = new Mock<IDiskService>(MockBehavior.Loose);
        var io = new FakeSystemIo { FileExistsResult = true };
        var toolResolver = CreateToolResolver();

        var service = new WinPeService(new AppSettings(), processRunner.Object, diskService.Object, io, toolResolver.Object);

        var outputPath = Path.Combine(Path.GetTempPath(), "WinClonePE_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputPath);

        var progressValues = new List<int>();
        var progress = new Progress<int>(v => progressValues.Add(v));

        var isoPath = await service.CreateWinPeAsync(outputPath, progress, CancellationToken.None);

        Assert.Equal(Path.Combine(outputPath, "WinClonePE.iso"), isoPath);
        Assert.True(progressValues.Any());
        processRunner.VerifyAll();
    }

    [Fact]
    public async Task CreateWinPeAsync_ReportsProgress_SequenceContainsExpectedValues()
    {
        var processRunner = new Mock<IProcessRunner>(MockBehavior.Strict);
        processRunner.Setup(x => x.RunAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IProgress<string>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<int>()))
            .Returns(Task.CompletedTask);

        var diskService = new Mock<IDiskService>(MockBehavior.Loose);
        var io = new FakeSystemIo { FileExistsResult = true };
        var toolResolver = CreateToolResolver();

        var service = new WinPeService(new AppSettings(), processRunner.Object, diskService.Object, io, toolResolver.Object);

        var outputPath = Path.Combine(Path.GetTempPath(), "WinClonePE_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputPath);

        var values = new List<int>();
        var progress = new Progress<int>(v => values.Add(v));

        await service.CreateWinPeAsync(outputPath, progress, CancellationToken.None);

        Assert.Contains(10, values);
        Assert.Contains(20, values);
        Assert.Contains(45, values);
        Assert.Contains(70, values);
        Assert.Contains(100, values);
        processRunner.VerifyAll();
    }

    [Fact]
    public async Task InjectDriversAsync_DriverFolderMissing_SkipsAndReturnsFalse()
    {
        var processRunner = new Mock<IProcessRunner>(MockBehavior.Strict);
        var diskService = new Mock<IDiskService>(MockBehavior.Loose);
        var io = new FakeSystemIo
        {
            DirectoryExistsResult = false
        };
        var toolResolver = CreateToolResolver();

        var service = new WinPeService(new AppSettings(), processRunner.Object, diskService.Object, io, toolResolver.Object);

        var ok = await service.InjectDriversAsync(@"C:\winpe", @"C:\drivers_missing", CancellationToken.None);

        Assert.False(ok);
        processRunner.VerifyNoOtherCalls();
    }

    private sealed class FakeSystemIo : ISystemIo
    {
        public bool DirectoryExistsResult { get; set; } = true;
        public bool FileExistsResult { get; set; } = true;
        public Dictionary<string, bool> FileExistsMap { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool DirectoryExists(string path) => DirectoryExistsResult;
        public void CreateDirectory(string path) => Directory.CreateDirectory(path);

        public bool FileExists(string path)
        {
            if (FileExistsMap.TryGetValue(path, out var value))
            {
                return value;
            }

            return FileExistsResult;
        }

        public void MoveFile(string sourcePath, string destinationPath, bool overwrite) => File.Move(sourcePath, destinationPath, overwrite);
        public void WriteAllText(string path, string content) => File.WriteAllText(path, content);
        public Task WriteAllBytesAsync(string path, byte[] content, CancellationToken ct) => File.WriteAllBytesAsync(path, content, ct);
        public long GetFileLength(string path) => new FileInfo(path).Length;
        public string? GetPathRoot(string path) => Path.GetPathRoot(path);
        public string? GetDirectoryName(string path) => Path.GetDirectoryName(path);
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption) => Directory.EnumerateFiles(path, searchPattern, searchOption);
        public void DeleteFile(string path) => File.Delete(path);
        public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

        public DriveMetrics GetDriveMetrics(string rootPath)
        {
            var driveInfo = new DriveInfo(rootPath);
            return new DriveMetrics(driveInfo.TotalSize, driveInfo.AvailableFreeSpace);
        }
    }

    private static Mock<IToolResolver> CreateToolResolver(
        bool copypeAvailable = true,
        bool makeWinPeMediaAvailable = true,
        bool dismAvailable = true,
        bool diskPartAvailable = true)
    {
        var toolResolver = new Mock<IToolResolver>();
        toolResolver.Setup(x => x.ResolveAsync("copype.cmd", It.IsAny<CancellationToken>()))
            .ReturnsAsync(copypeAvailable
                ? new ToolResolution
                {
                    ToolName = "copype.cmd",
                    ResolvedPath = @"C:\tools\copype.cmd",
                    Source = DependencySource.Embedded
                }
                : ToolResolution.Missing("copype.cmd"));
        toolResolver.Setup(x => x.ResolveAsync("MakeWinPEMedia.cmd", It.IsAny<CancellationToken>()))
            .ReturnsAsync(makeWinPeMediaAvailable
                ? new ToolResolution
                {
                    ToolName = "MakeWinPEMedia.cmd",
                    ResolvedPath = @"C:\tools\MakeWinPEMedia.cmd",
                    Source = DependencySource.Embedded
                }
                : ToolResolution.Missing("MakeWinPEMedia.cmd"));
        toolResolver.Setup(x => x.ResolveAsync("dism.exe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dismAvailable
                ? new ToolResolution
                {
                    ToolName = "dism.exe",
                    ResolvedPath = @"C:\Windows\System32\dism.exe",
                    Source = DependencySource.System
                }
                : ToolResolution.Missing("dism.exe"));
        toolResolver.Setup(x => x.ResolveAsync("diskpart.exe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(diskPartAvailable
                ? new ToolResolution
                {
                    ToolName = "diskpart.exe",
                    ResolvedPath = @"C:\Windows\System32\diskpart.exe",
                    Source = DependencySource.System
                }
                : ToolResolution.Missing("diskpart.exe"));
        toolResolver.Setup(x => x.IsAdkInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        toolResolver.Setup(x => x.AreWinPeToolsAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(copypeAvailable && makeWinPeMediaAvailable);
        return toolResolver;
    }
}
