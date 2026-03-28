namespace WinClonePro.Core.Models;

public sealed class BootstrapResult
{
    public bool Success { get; init; }
    public string FailureMessage { get; init; } = "";
    public SystemCheckResult SystemCheckResult { get; init; } = new();
}
