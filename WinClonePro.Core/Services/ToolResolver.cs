using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class ToolResolver : IToolResolver
{
    private readonly AppSettings _settings;
    private readonly ISystemIo _systemIo;

    public ToolResolver(AppSettings settings, ISystemIo systemIo)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _systemIo = systemIo ?? throw new ArgumentNullException(nameof(systemIo));
    }

    public Task<ToolResolution> ResolveAsync(string fileName, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Tool name cannot be empty.", nameof(fileName));
        }

        var systemPath = Path.Combine(_settings.System32DirectoryPath, fileName);
        if (_systemIo.FileExists(systemPath))
        {
            return Task.FromResult(new ToolResolution
            {
                ToolName = fileName,
                ResolvedPath = systemPath,
                Source = DependencySource.System
            });
        }

        var embeddedPath = ToolLocator.GetEmbeddedToolPath(_settings, fileName);
        if (_systemIo.FileExists(embeddedPath))
        {
            return Task.FromResult(new ToolResolution
            {
                ToolName = fileName,
                ResolvedPath = embeddedPath,
                Source = DependencySource.Embedded
            });
        }

        var adkPath = FindInRoots(fileName, _settings.GetAdkSearchRoots());
        if (!string.IsNullOrWhiteSpace(adkPath))
        {
            return Task.FromResult(new ToolResolution
            {
                ToolName = fileName,
                ResolvedPath = adkPath,
                Source = DependencySource.Adk
            });
        }

        return Task.FromResult(ToolResolution.Missing(fileName));
    }

    public Task<bool> IsAdkInstalledAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var dismUnderAdk = FindInRoots("dism.exe", _settings.GetAdkSearchRoots());
        return Task.FromResult(!string.IsNullOrWhiteSpace(dismUnderAdk));
    }

    public async Task<bool> AreWinPeToolsAvailableAsync(CancellationToken ct)
    {
        var copype = await ResolveAsync("copype.cmd", ct).ConfigureAwait(false);
        var makeWinPeMedia = await ResolveAsync("MakeWinPEMedia.cmd", ct).ConfigureAwait(false);
        return copype.IsAvailable && makeWinPeMedia.IsAvailable;
    }

    private string FindInRoots(string fileName, string[] roots)
    {
        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            try
            {
                if (!_systemIo.DirectoryExists(root))
                {
                    continue;
                }

                var directPath = Path.Combine(root, fileName);
                if (_systemIo.FileExists(directPath))
                {
                    return directPath;
                }

                var match = _systemIo
                    .EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(match))
                {
                    return match;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed searching for tool {ToolName} under {Root}", fileName, root);
            }
        }

        return "";
    }
}
