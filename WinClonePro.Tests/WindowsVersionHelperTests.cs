using System.Runtime.Versioning;
using WinClonePro.Core.Helpers;
using Xunit;

namespace WinClonePro.Tests;

public class WindowsVersionHelperTests
{
    [Fact]
    [SupportedOSPlatform("windows")]
    public void GetBuildNumber_ReturnsPositiveValue()
    {
        var build = WindowsVersionHelper.GetBuildNumber();
        Assert.True(build > 0);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void GetSystemDriveLetter_ReturnsValidDriveRoot()
    {
        var root = WindowsVersionHelper.GetSystemDriveLetter();
        Assert.False(string.IsNullOrWhiteSpace(root));
        Assert.True(root.Length >= 3);
        Assert.Equal(':', root[1]);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void GetDisplayVersion_ReturnsNonEmptyString()
    {
        var ver = WindowsVersionHelper.GetDisplayVersion();
        Assert.False(string.IsNullOrWhiteSpace(ver));
    }
}

