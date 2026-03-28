using System.Collections.Generic;

namespace WinClonePro.Core.Models;

public sealed class SystemCheckResult
{
    public bool IsAdministrator { get; init; }
    public ToolResolution DismTool { get; init; } = ToolResolution.Missing("dism.exe");
    public ToolResolution DiskPartTool { get; init; } = ToolResolution.Missing("diskpart.exe");
    public ToolResolution BcdBootTool { get; init; } = ToolResolution.Missing("bcdboot.exe");
    public bool AdkInstalled { get; init; }
    public bool WinPeToolsAvailable { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();

    public bool DismAvailable => DismTool.IsAvailable;
    public bool DiskPartAvailable => DiskPartTool.IsAvailable;
    public bool BcdBootAvailable => BcdBootTool.IsAvailable;
    public bool AreCoreToolsAvailable => DismAvailable && DiskPartAvailable && BcdBootAvailable;

    public bool IsHealthy => IsAdministrator && AreCoreToolsAvailable && AdkInstalled && Errors.Count == 0;
}

