using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Exceptions;

namespace WinClonePro.Core.Helpers;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task RunAsync(
        string exe,
        string args,
        IProgress<string> output,
        CancellationToken ct,
        int timeoutMinutes = 120)
    {
        if (string.IsNullOrWhiteSpace(exe))
        {
            throw new ArgumentException("Executable path cannot be null or whitespace.", nameof(exe));
        }

        ArgumentNullException.ThrowIfNull(output);

        var normalizedArgs = args ?? string.Empty;
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(timeoutMinutes));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        Process? process = null;
        var stderrBuilder = new StringBuilder();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = normalizedArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process: {exe}");
            }

            using var __ = linkedCts.Token.Register(
                () =>
                {
                    try
                    {
                        if (process is not null && !process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch
                    {
                        // Ignored: best-effort kill.
                    }
                });

            var stdoutTask = ReadStdoutLinesAsync(process.StandardOutput, output, linkedCts.Token);
            var stderrTask = ReadStderrLinesAsync(process.StandardError, stderrBuilder, linkedCts.Token);
            var exitTask = process.WaitForExitAsync(linkedCts.Token);

            await Task.WhenAll(exitTask, stdoutTask, stderrTask).ConfigureAwait(false);

            var exitCode = process.ExitCode;
            var stderr = stderrBuilder.ToString();

            if (exitCode != 0)
            {
                throw new ProcessFailedException(exe, exitCode, stderr);
            }
        }
        catch (OperationCanceledException ex)
        {
            Log.Error(ex, "ProcessRunner cancellation. exe {Exe} args {Args}", exe, args);

            if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                throw new TimeoutException(
                    $"Process timed out after {timeoutMinutes} minute(s). Exe: {exe}. Args: {args}.");
            }

            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ProcessRunner failure. exe {Exe} args {Args}", exe, args);
            throw;
        }
        finally
        {
            try
            {
                if (process is not null && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignored.
            }

            process?.Dispose();
        }
    }

    private static async Task ReadStdoutLinesAsync(
        System.IO.StreamReader stdout,
        IProgress<string> output,
        CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await stdout.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            Log.Information("stdout {Line}", line);
            output.Report(line);
        }
    }

    private static async Task ReadStderrLinesAsync(
        System.IO.StreamReader stderr,
        StringBuilder stderrBuilder,
        CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await stderr.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            stderrBuilder.AppendLine(line);
            Log.Error("stderr {Line}", line);
        }
    }
}

