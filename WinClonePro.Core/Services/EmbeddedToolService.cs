using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class EmbeddedToolService : IEmbeddedToolService
{
    private readonly AppSettings _settings;
    private readonly ISystemIo _systemIo;
    private readonly IDigitalSignatureVerifier _signatureVerifier;
    private readonly Assembly _resourceAssembly;

    public EmbeddedToolService(
        AppSettings settings,
        ISystemIo systemIo,
        IDigitalSignatureVerifier signatureVerifier)
        : this(settings, systemIo, signatureVerifier, typeof(AppSettings).Assembly)
    {
    }

    internal EmbeddedToolService(
        AppSettings settings,
        ISystemIo systemIo,
        IDigitalSignatureVerifier signatureVerifier,
        Assembly resourceAssembly)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _systemIo = systemIo ?? throw new ArgumentNullException(nameof(systemIo));
        _signatureVerifier = signatureVerifier ?? throw new ArgumentNullException(nameof(signatureVerifier));
        _resourceAssembly = resourceAssembly ?? throw new ArgumentNullException(nameof(resourceAssembly));
    }

    public async Task<EmbeddedToolExtractionResult> ExtractToolsAsync(IProgress<int> progress, CancellationToken ct)
    {
        _systemIo.CreateDirectory(_settings.ToolsDirectoryPath);

        var warnings = new List<string>();
        var extracted = new List<string>();
        var resourceNames = _resourceAssembly.GetManifestResourceNames();
        var requestedTools = _settings.RequiredToolNames.Concat(_settings.OptionalToolNames).ToArray();

        progress?.Report(5);

        for (var index = 0; index < requestedTools.Length; index++)
        {
            ct.ThrowIfCancellationRequested();

            var toolName = requestedTools[index];
            var resourceName = FindResourceName(resourceNames, toolName);
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                if (Array.Exists(_settings.RequiredToolNames, x => x.Equals(toolName, StringComparison.OrdinalIgnoreCase)))
                {
                    warnings.Add($"Embedded fallback for {toolName} was not packaged.");
                }

                var missingProgress = 10 + (int)(((index + 1d) / requestedTools.Length) * 90d);
                progress?.Report(missingProgress);
                continue;
            }

            await using var stream = _resourceAssembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                warnings.Add($"Embedded resource stream could not be opened for {toolName}.");
                continue;
            }

            await using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, ct).ConfigureAwait(false);

            var targetPath = ToolLocator.GetEmbeddedToolPath(_settings, toolName);
            await _systemIo.WriteAllBytesAsync(targetPath, memory.ToArray(), ct).ConfigureAwait(false);

            if (toolName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                var signatureValid = await _signatureVerifier.HasValidSignatureAsync(targetPath, ct).ConfigureAwait(false);
                if (!signatureValid)
                {
                    warnings.Add($"Embedded tool {toolName} failed signature validation and was skipped.");
                    _systemIo.DeleteFile(targetPath);
                    continue;
                }
            }

            extracted.Add(targetPath);
            Log.Information("Extracted embedded tool {ToolName} to {TargetPath}", toolName, targetPath);

            var progressValue = 10 + (int)(((index + 1d) / requestedTools.Length) * 90d);
            progress?.Report(progressValue);
        }

        return new EmbeddedToolExtractionResult
        {
            ExtractedCount = extracted.Count,
            ExtractedFiles = extracted,
            Warnings = warnings
        };
    }

    private static string FindResourceName(string[] resourceNames, string toolName)
    {
        return resourceNames.FirstOrDefault(name =>
            name.EndsWith($".{toolName}", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith($"/{toolName}", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith($":{toolName}", StringComparison.OrdinalIgnoreCase) ||
            name.Equals(toolName, StringComparison.OrdinalIgnoreCase)) ?? "";
    }
}
