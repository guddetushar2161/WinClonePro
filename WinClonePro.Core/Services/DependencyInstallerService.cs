using System;
using System.IO;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class DependencyInstallerService : IDependencyInstallerService
{
    private const int MaxInstallAttempts = 2;

    private readonly AppSettings _settings;
    private readonly IProcessRunner _processRunner;
    private readonly ISystemIo _systemIo;
    private readonly IToolResolver _toolResolver;
    private readonly IDigitalSignatureVerifier _signatureVerifier;

    public DependencyInstallerService(
        AppSettings settings,
        IProcessRunner processRunner,
        ISystemIo systemIo,
        IToolResolver toolResolver,
        IDigitalSignatureVerifier signatureVerifier)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _systemIo = systemIo ?? throw new ArgumentNullException(nameof(systemIo));
        _toolResolver = toolResolver ?? throw new ArgumentNullException(nameof(toolResolver));
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
    }

    public async Task<bool> EnsureAdkDeploymentToolsInstalledAsync(IProgress<int> progress, CancellationToken ct)
    {
        progress?.Report(5);

        if (await _toolResolver.IsAdkInstalledAsync(ct).ConfigureAwait(false))
        {
            Log.Information("ADK deployment tools already available.");
            progress?.Report(100);
            return true;
        }

        _systemIo.CreateDirectory(_settings.DownloadsDirectoryPath);

        for (var attempt = 1; attempt <= MaxInstallAttempts; attempt++)
        {
            try
            {
                progress?.Report(10);
                await EnsureInstallerDownloadedAsync(progress ?? new Progress<int>(_ => { }), ct).ConfigureAwait(false);

                progress?.Report(65);

                var signatureValid = await _signatureVerifier
                    .HasValidSignatureAsync(_settings.AdkInstallerPath, ct)
                    .ConfigureAwait(false);

                if (!signatureValid)
                {
                    Log.Error("ADK installer signature validation failed for {InstallerPath}", _settings.AdkInstallerPath);
                    _systemIo.DeleteFile(_settings.AdkInstallerPath);
                    return false;
                }

                progress?.Report(75);

                var args = "/quiet /norestart /ceip off /features OptionId.DeploymentTools";
                var output = new Progress<string>(line => Log.Information("[ADK setup] {Line}", line));
                await _processRunner
                    .RunAsync(_settings.AdkInstallerPath, args, output, ct, timeoutMinutes: 240)
                    .ConfigureAwait(false);

                progress?.Report(95);

                var installed = await _toolResolver.IsAdkInstalledAsync(ct).ConfigureAwait(false);
                Log.Information("ADK installation attempt {Attempt} completed. installed={Installed}", attempt, installed);

                if (installed)
                {
                    progress?.Report(100);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to install ADK Deployment Tools on attempt {Attempt}", attempt);
                if (attempt >= MaxInstallAttempts)
                {
                    return false;
                }

                await Task.Delay(TimeSpan.FromSeconds(3), ct).ConfigureAwait(false);
            }
        }

        return false;
    }

    private async Task EnsureInstallerDownloadedAsync(IProgress<int> progress, CancellationToken ct)
    {
        if (_systemIo.FileExists(_settings.AdkInstallerPath))
        {
            Log.Information("Using cached ADK installer at {InstallerPath}", _settings.AdkInstallerPath);
            return;
        }

        Log.Information(
            "Downloading ADK installer from {Url} to {InstallerPath}",
            _settings.AdkInstallerDownloadUrl,
            _settings.AdkInstallerPath);

        await DownloadFileAsync(_settings.AdkInstallerDownloadUrl, _settings.AdkInstallerPath, progress, ct)
            .ConfigureAwait(false);
    }

    private static async Task DownloadFileAsync(string url, string destination, IProgress<int> progress, CancellationToken ct)
    {
        var tempDestination = destination + ".download";
        try
        {
            using var http = new HttpClient();
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using (var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            {
                await using var output = File.Open(tempDestination, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[1024 * 256];
                long readTotal = 0;
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    var read = await input.ReadAsync(buffer, ct).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    readTotal += read;

                    if (total.HasValue && total.Value > 0)
                    {
                        var pct = 10 + (int)Math.Clamp((readTotal * 50d) / total.Value, 0, 50);
                        progress?.Report(pct);
                    }
                }
            }

            await MoveDownloadedFileWithRetryAsync(tempDestination, destination, ct).ConfigureAwait(false);
        }
        catch
        {
            if (File.Exists(tempDestination))
            {
                File.Delete(tempDestination);
            }

            throw;
        }
    }

    private static async Task MoveDownloadedFileWithRetryAsync(string source, string destination, CancellationToken ct)
    {
        const int maxMoveAttempts = 5;

        for (var attempt = 1; attempt <= maxMoveAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                File.Move(source, destination, overwrite: true);
                return;
            }
            catch (IOException) when (attempt < maxMoveAttempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), ct).ConfigureAwait(false);
            }
        }
    }
}
