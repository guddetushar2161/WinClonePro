using System;

namespace WinClonePro.Core.Exceptions;

public class ProcessFailedException : Exception
{
    public int ExitCode { get; }
    public string ProcessName { get; }
    public string Stderr { get; }

    public ProcessFailedException(string processName, int exitCode, string stderr)
        : base($"{processName} failed with exit code {exitCode}")
    {
        ProcessName = processName ?? "";
        ExitCode = exitCode;
        Stderr = stderr ?? "";
    }

    public ProcessFailedException(string processName, int exitCode, string stderr, Exception innerException)
        : base($"{processName} failed with exit code {exitCode}", innerException)
    {
        ProcessName = processName ?? "";
        ExitCode = exitCode;
        Stderr = stderr ?? "";
    }
}

