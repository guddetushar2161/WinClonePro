namespace WinClonePro.Core.Models;

public sealed class ToolResolution
{
    public string ToolName { get; init; } = "";
    public string ResolvedPath { get; init; } = "";
    public DependencySource Source { get; init; }

    public bool IsAvailable => !string.IsNullOrWhiteSpace(ResolvedPath);

    public static ToolResolution Missing(string toolName)
    {
        return new ToolResolution
        {
            ToolName = toolName,
            ResolvedPath = "",
            Source = DependencySource.Missing
        };
    }
}
