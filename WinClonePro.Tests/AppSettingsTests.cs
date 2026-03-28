using System.IO;
using WinClonePro.Core.Models;
using Xunit;

namespace WinClonePro.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Constructor_StoresLogsUnderAppData()
    {
        var settings = new AppSettings(
            appDataRootPath: Path.Combine("C:\\", "Users", "Tester", "AppData", "Roaming", "WinClonePro"),
            programDataRootPath: Path.Combine("C:\\", "ProgramData", "WinClonePro"),
            windowsDirectoryPath: Path.Combine("C:\\", "Windows"));

        Assert.Equal(
            Path.Combine("C:\\", "Users", "Tester", "AppData", "Roaming", "WinClonePro", "logs"),
            settings.LogDirectoryPath);
    }

    [Fact]
    public void Constructor_KeepsToolsAndDownloadsUnderProgramData()
    {
        var settings = new AppSettings(
            appDataRootPath: Path.Combine("C:\\", "Users", "Tester", "AppData", "Roaming", "WinClonePro"),
            programDataRootPath: Path.Combine("C:\\", "ProgramData", "WinClonePro"),
            windowsDirectoryPath: Path.Combine("C:\\", "Windows"));

        Assert.Equal(Path.Combine("C:\\", "ProgramData", "WinClonePro", "tools"), settings.ToolsDirectoryPath);
        Assert.Equal(Path.Combine("C:\\", "ProgramData", "WinClonePro", "downloads"), settings.DownloadsDirectoryPath);
    }
}
