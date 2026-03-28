namespace WinClonePro.Core.Models;

public enum BootstrapStage
{
    Idle = 0,
    ExtractingTools = 1,
    CheckingSystem = 2,
    InstallingComponents = 3,
    Finalizing = 4,
    Ready = 5,
    Failed = 6
}
