using System;
using System.IO;
using System.Runtime.Versioning;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Helpers;

[SupportedOSPlatform("windows")]
public static class ToolLocator
{
    public static string GetEmbeddedToolPath(AppSettings settings, string fileName) =>
        Path.Combine(settings.ToolsDirectoryPath, fileName);

    public static string GetDownloadPath(AppSettings settings, string fileName) =>
        Path.Combine(settings.DownloadsDirectoryPath, fileName);
}

