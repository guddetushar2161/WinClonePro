using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Exceptions;
using WinClonePro.Core.Helpers;
using Xunit;

namespace WinClonePro.Tests;

public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_ReadsStdoutAndReportsProgress()
    {
        var runner = new ProcessRunner();

        var lines = new List<string>();
        var progress = new Progress<string>(line => lines.Add(line));

        await runner.RunAsync(
            exe: "cmd.exe",
            args: "/c \"echo hello\"",
            output: progress,
            ct: CancellationToken.None,
            timeoutMinutes: 1);

        Assert.Contains("hello", lines);
    }

    [Fact]
    public async Task RunAsync_CapturesStderrSeparately_AndThrowsOnNonZeroExitCode()
    {
        var runner = new ProcessRunner();
        var progress = new Progress<string>(_ => { });

        var ex = await Assert.ThrowsAsync<ProcessFailedException>(() =>
            runner.RunAsync(
                exe: "cmd.exe",
                args: "/c \"echo errline 1>&2 & exit /b 5\"",
                output: progress,
                ct: CancellationToken.None,
                timeoutMinutes: 1));

        Assert.Equal(5, ex.ExitCode);
        Assert.Contains("errline", ex.Stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ThrowsTimeoutException_WhenTimeoutExpires()
    {
        var runner = new ProcessRunner();
        var progress = new Progress<string>(_ => { });

        await Assert.ThrowsAsync<TimeoutException>(() =>
            runner.RunAsync(
                exe: "cmd.exe",
                args: "/c \"ping 127.0.0.1 -n 6 > nul\"",
                output: progress,
                ct: CancellationToken.None,
                timeoutMinutes: 0));
    }
}

