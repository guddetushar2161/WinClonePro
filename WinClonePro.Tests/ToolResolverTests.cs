using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Models;
using WinClonePro.Core.Services;
using Xunit;

namespace WinClonePro.Tests;

public sealed class ToolResolverTests
{
    [Fact]
    public async Task ResolveAsync_PrefersSystemTools_OverEmbeddedAndAdk()
    {
        var root = CreateTempRoot();

        try
        {
            var windowsDir = Path.Combine(root, "Windows");
            var system32Dir = Path.Combine(windowsDir, "System32");
            var programDataRoot = Path.Combine(root, "ProgramData");
            var embeddedToolsDir = Path.Combine(programDataRoot, "tools");
            var adkRoot = Path.Combine(root, "Windows Kits", "10", "Assessment and Deployment Kit");
            var adkToolsDir = Path.Combine(adkRoot, "Deployment Tools", "amd64");

            Directory.CreateDirectory(system32Dir);
            Directory.CreateDirectory(embeddedToolsDir);
            Directory.CreateDirectory(adkToolsDir);

            var systemPath = Path.Combine(system32Dir, "dism.exe");
            var embeddedPath = Path.Combine(embeddedToolsDir, "dism.exe");
            var adkPath = Path.Combine(adkToolsDir, "dism.exe");

            File.WriteAllText(systemPath, "system");
            File.WriteAllText(embeddedPath, "embedded");
            File.WriteAllText(adkPath, "adk");

            var settings = new AppSettings(
                appDataRootPath: Path.Combine(root, "AppData"),
                programDataRootPath: programDataRoot,
                windowsDirectoryPath: windowsDir,
                adkRootPath: adkRoot);

            var resolver = new ToolResolver(settings, new SystemIo());

            var result = await resolver.ResolveAsync("dism.exe", CancellationToken.None);

            Assert.True(result.IsAvailable);
            Assert.Equal(DependencySource.System, result.Source);
            Assert.Equal(systemPath, result.ResolvedPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ResolveAsync_UsesEmbeddedTool_WhenSystemToolIsMissing()
    {
        var root = CreateTempRoot();

        try
        {
            var windowsDir = Path.Combine(root, "Windows");
            var programDataRoot = Path.Combine(root, "ProgramData");
            var embeddedToolsDir = Path.Combine(programDataRoot, "tools");
            var adkRoot = Path.Combine(root, "Windows Kits", "10", "Assessment and Deployment Kit");
            var adkToolsDir = Path.Combine(adkRoot, "Deployment Tools", "amd64");

            Directory.CreateDirectory(embeddedToolsDir);
            Directory.CreateDirectory(adkToolsDir);

            var embeddedPath = Path.Combine(embeddedToolsDir, "diskpart.exe");
            var adkPath = Path.Combine(adkToolsDir, "diskpart.exe");

            File.WriteAllText(embeddedPath, "embedded");
            File.WriteAllText(adkPath, "adk");

            var settings = new AppSettings(
                appDataRootPath: Path.Combine(root, "AppData"),
                programDataRootPath: programDataRoot,
                windowsDirectoryPath: windowsDir,
                adkRootPath: adkRoot);

            var resolver = new ToolResolver(settings, new SystemIo());

            var result = await resolver.ResolveAsync("diskpart.exe", CancellationToken.None);

            Assert.True(result.IsAvailable);
            Assert.Equal(DependencySource.Embedded, result.Source);
            Assert.Equal(embeddedPath, result.ResolvedPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task AreWinPeToolsAvailableAsync_ReturnsTrue_WhenCopypeAndMakeMediaExistInAdk()
    {
        var root = CreateTempRoot();

        try
        {
            var windowsDir = Path.Combine(root, "Windows");
            var programDataRoot = Path.Combine(root, "ProgramData");
            var adkRoot = Path.Combine(root, "Windows Kits", "10", "Assessment and Deployment Kit");
            var winPeDir = Path.Combine(adkRoot, "Windows Preinstallation Environment");

            Directory.CreateDirectory(winPeDir);
            File.WriteAllText(Path.Combine(winPeDir, "copype.cmd"), "copype");
            File.WriteAllText(Path.Combine(winPeDir, "MakeWinPEMedia.cmd"), "make");

            var settings = new AppSettings(
                appDataRootPath: Path.Combine(root, "AppData"),
                programDataRootPath: programDataRoot,
                windowsDirectoryPath: windowsDir,
                adkRootPath: adkRoot);

            var resolver = new ToolResolver(settings, new SystemIo());

            var result = await resolver.AreWinPeToolsAvailableAsync(CancellationToken.None);

            Assert.True(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "WinClonePro_ToolResolver_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
