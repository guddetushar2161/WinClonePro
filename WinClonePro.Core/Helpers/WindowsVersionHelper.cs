using System;
using System.IO;
using System.Management;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WinClonePro.Core.Helpers;

[SupportedOSPlatform("windows")]
public static class WindowsVersionHelper
{
    private const string CurrentVersionKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    public static int GetBuildNumber()
    {
        try
        {
            var val = Registry.GetValue(CurrentVersionKey, "CurrentBuild", "0")?.ToString() ?? "0";
            return int.TryParse(val, out var build) ? build : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static string GetDisplayVersion()
    {
        try
        {
            var displayVersion = Registry.GetValue(CurrentVersionKey, "DisplayVersion", "")?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(displayVersion))
            {
                return displayVersion;
            }

            var releaseId = Registry.GetValue(CurrentVersionKey, "ReleaseId", "")?.ToString() ?? "";
            if (!string.IsNullOrWhiteSpace(releaseId))
            {
                return releaseId;
            }
        }
        catch
        {
            // ignored
        }

        return GetBuildNumber().ToString();
    }

    public static string GetEdition()
    {
        try
        {
            return Registry.GetValue(CurrentVersionKey, "EditionID", "")?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    public static bool IsWindows11() => GetBuildNumber() >= 22000;

    public static bool IsWinPE()
    {
        return File.Exists(@"X:\Windows\System32\winpe.ini")
               || Environment.GetEnvironmentVariable("WINPE") is not null;
    }

    public static string GetSystemDriveLetter()
    {
        try
        {
            return Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
        }
        catch
        {
            return "C:\\";
        }
    }

    public static string GetComputerModel()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model FROM Win32_ComputerSystem");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                var model = obj["Model"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(model))
                {
                    return model;
                }
            }
        }
        catch
        {
            // ignored
        }

        return Environment.MachineName;
    }
}

