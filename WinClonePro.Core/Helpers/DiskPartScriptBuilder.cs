using System;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Helpers;

[SupportedOSPlatform("windows")]
public sealed class DiskPartScriptBuilder
{
    private readonly AppSettings _settings;
    private readonly IProcessRunner _processRunner;
    private readonly ISystemIo _systemIo;
    private readonly IToolResolver _toolResolver;

    public DiskPartScriptBuilder(
        AppSettings settings,
        IProcessRunner processRunner,
        ISystemIo systemIo,
        IToolResolver toolResolver)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _systemIo = systemIo ?? throw new ArgumentNullException(nameof(systemIo));
        _toolResolver = toolResolver ?? throw new ArgumentNullException(nameof(toolResolver));
    }

    public string CreateGptPartitionScript(int diskIndex, out string efiDriveLetter, out string winDriveLetter)
    {
        efiDriveLetter = "S";
        winDriveLetter = "W";

        var script = new StringBuilder()
            .AppendLine($"select disk {diskIndex}")
            .AppendLine("clean")
            .AppendLine("convert gpt")
            .AppendLine("create partition efi size=512")
            .AppendLine("format quick fs=fat32 label=\"System\"")
            .AppendLine("assign letter=S")
            .AppendLine("create partition msr size=128")
            .AppendLine("create partition primary")
            .AppendLine("format quick fs=ntfs label=\"Windows\"")
            .AppendLine("assign letter=W")
            .AppendLine("exit")
            .ToString();

        _systemIo.CreateDirectory(_settings.TemporaryWorkingRootPath);

        var path = Path.Combine(_settings.TemporaryWorkingRootPath, $"winclone_diskpart_{Guid.NewGuid():N}.txt");
        _systemIo.WriteAllText(path, script);
        return path;
    }

    public async Task RunDiskPartAsync(string scriptPath, CancellationToken ct)
    {
        try
        {
            var progress = new Progress<string>(line => Log.Information("[DiskPart] {Line}", line));
            var exe = await _toolResolver.ResolveAsync("diskpart.exe", ct).ConfigureAwait(false);
            if (!exe.IsAvailable)
            {
                throw new FileNotFoundException("DiskPart.exe not found.");
            }

            await _processRunner.RunAsync(exe.ResolvedPath, $"/s \"{scriptPath}\"", progress, ct).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (_systemIo.FileExists(scriptPath))
                {
                    _systemIo.DeleteFile(scriptPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed deleting diskpart script {ScriptPath}", scriptPath);
            }
        }
    }
}
