using System;
using System.IO;
using System.Runtime.Versioning;

namespace WinClonePro.Core.Models;

[SupportedOSPlatform("windows")]
public sealed class AppSettings
{
    public string ApplicationFolderName { get; }
    public string AppDataRootPath { get; }
    public string ProgramDataRootPath { get; }
    public string LogDirectoryPath { get; }
    public string ToolsDirectoryPath { get; }
    public string DownloadsDirectoryPath { get; }
    public string TemporaryWorkingRootPath { get; }
    public string WindowsDirectoryPath { get; }
    public string System32DirectoryPath { get; }
    public string WindowsKitsRootPath { get; }
    public string AdkRootPath { get; }
    public string AdkInstallerPath { get; }
    public string AdkInstallerDownloadUrl { get; }
    public string WinPeAddonInstallerPath { get; }
    public string WinPeAddonDownloadUrl { get; }
    public string[] RequiredToolNames { get; } = ["dism.exe", "diskpart.exe", "bcdboot.exe"];
    public string[] OptionalToolNames { get; } = ["copype.cmd", "MakeWinPEMedia.cmd"];

    public AppSettings(
        string applicationFolderName = "WinClonePro",
        string? appDataRootPath = null,
        string? programDataRootPath = null,
        string? windowsDirectoryPath = null,
        string? adkRootPath = null,
        string? adkInstallerDownloadUrl = null,
        string? winPeAddonDownloadUrl = null)
    {
        ApplicationFolderName = applicationFolderName;

        AppDataRootPath = appDataRootPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), applicationFolderName);

        ProgramDataRootPath = programDataRootPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), applicationFolderName);

        LogDirectoryPath = Path.Combine(AppDataRootPath, "logs");
        ToolsDirectoryPath = Path.Combine(ProgramDataRootPath, "tools");
        DownloadsDirectoryPath = Path.Combine(ProgramDataRootPath, "downloads");
        TemporaryWorkingRootPath = Path.Combine(Path.GetTempPath(), applicationFolderName);

        WindowsDirectoryPath = windowsDirectoryPath
            ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        System32DirectoryPath = Path.Combine(WindowsDirectoryPath, "System32");

        WindowsKitsRootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Windows Kits",
            "10");

        AdkRootPath = adkRootPath ?? Path.Combine(WindowsKitsRootPath, "Assessment and Deployment Kit");
        AdkInstallerPath = Path.Combine(DownloadsDirectoryPath, "adksetup.exe");
        AdkInstallerDownloadUrl = adkInstallerDownloadUrl ?? "https://go.microsoft.com/fwlink/?linkid=2289980";
        WinPeAddonInstallerPath = Path.Combine(DownloadsDirectoryPath, "adkwinpesetup.exe");
        WinPeAddonDownloadUrl = winPeAddonDownloadUrl ?? "https://go.microsoft.com/fwlink/?linkid=2289981";
    }

    public string[] GetAdkSearchRoots()
    {
        return
        [
            Path.Combine(AdkRootPath, "Deployment Tools"),
            Path.Combine(AdkRootPath, "Windows Preinstallation Environment"),
            AdkRootPath,
            WindowsKitsRootPath
        ];
    }
}

