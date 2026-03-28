namespace WinClonePro.Core.Models;

public sealed class BootstrapProgressState
{
    public BootstrapStage Stage { get; init; }
    public int ProgressPercentage { get; init; }
    public string CurrentStep { get; init; } = "";
    public string Detail { get; init; } = "";
}
