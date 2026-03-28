using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Exceptions;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Services;

[SupportedOSPlatform("windows")]
public sealed class WinPeService : IWinPeService
{
    private readonly AppSettings _settings;
    private readonly IProcessRunner _processRunner;
    private readonly IDiskService _diskService;
    private readonly ISystemIo _systemIo;
    private readonly IToolResolver _toolResolver;

    public WinPeService(
        AppSettings settings,
        IProcessRunner processRunner,
        IDiskService diskService,
        ISystemIo systemIo,
        IToolResolver toolResolver)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _diskService = diskService ?? throw new ArgumentNullException(nameof(diskService));
        _systemIo = systemIo ?? throw new ArgumentNullException(nameof(systemIo));
        _toolResolver = toolResolver ?? throw new ArgumentNullException(nameof(toolResolver));
    }

    public async Task<string> CreateWinPeAsync(string outputPath, IProgress<int> progress, CancellationToken ct)
    {
        var copype = await _toolResolver.ResolveAsync("copype.cmd", ct).ConfigureAwait(false);
        var makeMedia = await _toolResolver.ResolveAsync("MakeWinPEMedia.cmd", ct).ConfigureAwait(false);

        progress?.Report(10);

        if (!copype.IsAvailable || !makeMedia.IsAvailable)
        {
            throw new FileNotFoundException("Windows PE tools are unavailable. Install the WinPE add-on or provide embedded WinPE tools.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be empty.", nameof(outputPath));
        }

        _systemIo.CreateDirectory(outputPath);
        _systemIo.CreateDirectory(_settings.TemporaryWorkingRootPath);

        var workingBase = Path.Combine(_settings.TemporaryWorkingRootPath, "WinClonePE");
        var workingDir = Path.Combine(workingBase, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDir);

        try
        {
            progress?.Report(20);

            var copypeArgs = $"/c \"\"{copype.ResolvedPath}\" amd64 \"{workingDir}\"\"";
            await _processRunner.RunAsync(
                "cmd.exe",
                copypeArgs,
                new Progress<string>(line => Log.Information("[copype] {Line}", line)),
                ct).ConfigureAwait(false);

            progress?.Report(45);

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                exePath = Path.Combine(AppContext.BaseDirectory, "WinClonePro.UI.exe");
            }

            var mediaTargetDir = Path.Combine(workingDir, "media", "WinClonePro");
            Directory.CreateDirectory(mediaTargetDir);

            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                var destinationExe = Path.Combine(mediaTargetDir, Path.GetFileName(exePath));
                File.Copy(exePath, destinationExe, overwrite: true);
            }

            progress?.Report(70);

            var isoPath = Path.Combine(outputPath, "WinClonePE.iso");
            var makeArgs = $"/c \"\"{makeMedia.ResolvedPath}\" /ISO \"{workingDir}\" \"{isoPath}\"\"";
            await _processRunner.RunAsync(
                "cmd.exe",
                makeArgs,
                new Progress<string>(line => Log.Information("[MakeWinPEMedia] {Line}", line)),
                ct).ConfigureAwait(false);

            progress?.Report(100);
            return isoPath;
        }
        finally
        {
            try
            {
                if (Directory.Exists(workingDir))
                {
                    _systemIo.DeleteDirectory(workingDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed cleaning WinPE working directory {WorkingDir}", workingDir);
            }
        }
    }

    public async Task<bool> InjectDriversAsync(string winPePath, string driverFolder, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(winPePath))
            {
                throw new ArgumentException("winPePath is empty.", nameof(winPePath));
            }

            if (string.IsNullOrWhiteSpace(driverFolder))
            {
                return false;
            }

            if (!_systemIo.DirectoryExists(driverFolder))
            {
                Log.Warning("Driver folder missing. Skipping injection {DriverFolder}", driverFolder);
                return false;
            }

            var bootWimPath = Path.Combine(winPePath, "boot.wim");
            if (!_systemIo.FileExists(bootWimPath))
            {
                Log.Error("boot.wim not found at {BootWimPath}", bootWimPath);
                return false;
            }

            var dism = await _toolResolver.ResolveAsync("dism.exe", ct).ConfigureAwait(false);
            if (!dism.IsAvailable)
            {
                throw new FileNotFoundException("DISM.exe not found.");
            }

            var mountDir = Path.Combine(winPePath, "mount");
            Directory.CreateDirectory(mountDir);

            await _processRunner.RunAsync(
                dism.ResolvedPath,
                $"/Mount-Image /ImageFile:\"{bootWimPath}\" /Index:1 /MountDir:\"{mountDir}\"",
                new Progress<string>(line => Log.Information("[dism mount] {Line}", line)),
                ct).ConfigureAwait(false);

            await _processRunner.RunAsync(
                dism.ResolvedPath,
                $"/Image:\"{mountDir}\" /Add-Driver /Driver:\"{driverFolder}\" /Recurse",
                new Progress<string>(line => Log.Information("[dism add-driver] {Line}", line)),
                ct).ConfigureAwait(false);

            await _processRunner.RunAsync(
                dism.ResolvedPath,
                $"/Unmount-Image /MountDir:\"{mountDir}\" /Commit",
                new Progress<string>(line => Log.Information("[dism unmount] {Line}", line)),
                ct).ConfigureAwait(false);

            try
            {
                if (Directory.Exists(mountDir))
                {
                    _systemIo.DeleteDirectory(mountDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed cleaning WinPE mount directory {MountDir}", mountDir);
            }

            return true;
        }
        catch (FileNotFoundException ex)
        {
            Log.Error(ex, "InjectDriversAsync failed due to missing executable or file.");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "InjectDriversAsync failed.");
            return false;
        }
    }

    public async Task<bool> CreateBootableUsbAsync(string isoPath, int diskIndex, IProgress<int> progress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(isoPath))
        {
            throw new ArgumentException("isoPath cannot be empty.", nameof(isoPath));
        }

        if (diskIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(diskIndex));
        }

        var disks = await _diskService.GetAllDisksAsync(ct).ConfigureAwait(false);
        var targetDisk = disks.Find(d => d.Index == diskIndex);
        if (targetDisk is null)
        {
            throw new ArgumentException($"Target disk index {diskIndex} not found.");
        }

        if (targetDisk.IsBootDisk)
        {
            throw new SafetyViolationException("Cannot create bootable USB for active boot disk.");
        }

        if (!_systemIo.FileExists(isoPath))
        {
            throw new FileNotFoundException("ISO not found.", isoPath);
        }

        _systemIo.CreateDirectory(_settings.TemporaryWorkingRootPath);
        var scriptPath = Path.Combine(_settings.TemporaryWorkingRootPath, $"winclone_usb_diskpart_{Guid.NewGuid():N}.txt");

        try
        {
            var script = string.Join(
                Environment.NewLine,
                $"select disk {diskIndex}",
                "clean",
                "convert gpt",
                "create partition primary",
                "format fs=fat32 quick",
                "assign",
                "active",
                "exit");

            _systemIo.WriteAllText(scriptPath, script);

            progress?.Report(10);

            var diskPart = await _toolResolver.ResolveAsync("diskpart.exe", ct).ConfigureAwait(false);
            if (!diskPart.IsAvailable)
            {
                throw new FileNotFoundException("DiskPart.exe not found.");
            }

            await _processRunner.RunAsync(
                diskPart.ResolvedPath,
                $"/s \"{scriptPath}\"",
                new Progress<string>(line => Log.Information("[diskpart] {Line}", line)),
                ct).ConfigureAwait(false);

            progress?.Report(50);

            var escapedIsoPath = isoPath.Replace("'", "''", StringComparison.Ordinal);
            var ps = $"-NoProfile -ExecutionPolicy Bypass -Command " +
                     $"\"$iso = Get-DiskImage -ImagePath '{escapedIsoPath}'; " +
                     $"if (-not $iso) {{ Mount-DiskImage -ImagePath '{escapedIsoPath}' | Out-Null; $iso = Get-DiskImage -ImagePath '{escapedIsoPath}' }}; " +
                     $"$isoVol = ($iso | Get-Volume | Select-Object -First 1); " +
                     $"$isoDrive = $isoVol.DriveLetter; " +
                     $"$usbDrive = (Get-Partition -DiskNumber {diskIndex} | Get-Volume | Where-Object DriveLetter | Select-Object -First 1).DriveLetter; " +
                     $"Copy-Item -Path ($isoDrive + ':\\*') -Destination ($usbDrive + ':\\') -Recurse -Force;\"";

            await _processRunner.RunAsync(
                "powershell.exe",
                ps,
                new Progress<string>(line => Log.Information("[USB copy] {Line}", line)),
                ct).ConfigureAwait(false);

            progress?.Report(100);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CreateBootableUsbAsync failed. isoPath {IsoPath} diskIndex {DiskIndex}", isoPath, diskIndex);
            return false;
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
