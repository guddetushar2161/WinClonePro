using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using WinClonePro.Core.Exceptions;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;

namespace WinClonePro.Core.Services;

[SupportedOSPlatform("windows")]
public class DismService : IDismService
{
    private static readonly Regex ProgressRegex = new(@"\[\s*=*\s*([\d.]+)%", RegexOptions.Compiled);

    private readonly IProcessRunner _processRunner;
    private readonly IDiskService _diskService;
    private readonly DiskPartScriptBuilder _diskPartScriptBuilder;
    private readonly IToolResolver _toolResolver;
    private readonly ISystemIo _systemIo;

    public DismService(
        IProcessRunner processRunner,
        IDiskService diskService,
        DiskPartScriptBuilder diskPartScriptBuilder,
        IToolResolver toolResolver,
        ISystemIo systemIo)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _diskService = diskService ?? throw new ArgumentNullException(nameof(diskService));
        _diskPartScriptBuilder = diskPartScriptBuilder ?? throw new ArgumentNullException(nameof(diskPartScriptBuilder));
        _toolResolver = toolResolver ?? throw new ArgumentNullException(nameof(toolResolver));
        _systemIo = systemIo ?? throw new ArgumentNullException(nameof(systemIo));
    }

    public async Task<CaptureResult> CaptureImageAsync(CaptureRequest request, IProgress<int> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();
        var outputWimPath = request.OutputWimPath ?? "";
        // STEP 1 — pre-flight checks
        var sourceRoot = NormalizeDriveRoot(request.SourceDrive);
        if (!_systemIo.DirectoryExists(sourceRoot))
        {
            throw new ArgumentException("Source drive not found");
        }

        var build = WindowsVersionHelper.GetBuildNumber();
        if (build < 19041)
        {
            throw new NotSupportedException("Source OS must be Windows 10 2004 or later");
        }

        var sourceMetrics = _systemIo.GetDriveMetrics(sourceRoot);
        var sourceUsedBytes = sourceMetrics.TotalSize - sourceMetrics.AvailableFreeSpace;
        var requiredBytes = (long)(sourceUsedBytes * 0.7d);

        var outRoot = _systemIo.GetPathRoot(outputWimPath) ?? "";
        if (string.IsNullOrWhiteSpace(outRoot))
        {
            throw new ArgumentException("Output path is invalid.", nameof(request.OutputWimPath));
        }

        var outputMetrics = _systemIo.GetDriveMetrics(outRoot);
        var availableBytes = outputMetrics.AvailableFreeSpace;
        if (availableBytes < requiredBytes)
        {
            throw new InsufficientSpaceException(requiredBytes, availableBytes, outRoot.TrimEnd('\\'));
        }

        var outputDir = _systemIo.GetDirectoryName(outputWimPath) ?? "";
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            throw new ArgumentException("Output directory is invalid.", nameof(request.OutputWimPath));
        }

        _systemIo.CreateDirectory(outputDir);
        BackupExistingWimIfAny(outputWimPath);

        Log.Information("Capture requested. sourceDrive {SourceDrive} outputWimPath {OutputWimPath} imageName {ImageName} compression {Compression} verify {Verify}",
            request.SourceDrive, outputWimPath, request.ImageName, request.Compression, request.VerifyAfterCapture);

        // STEP 2 — build DISM command
            var compression = request.Compression switch
            {
                CompressionType.None => "none",
                CompressionType.Fast => "fast",
                CompressionType.Maximum => "max",
                _ => "fast"
            };

            var imageName = string.IsNullOrWhiteSpace(request.ImageName) ? "WinClone Capture" : request.ImageName;
            var dismPath = await ResolveDismExePathAsync(ct).ConfigureAwait(false);
        var args =
                "/Capture-Image " +
                $"/ImageFile:\"{outputWimPath}\" " +
                $"/CaptureDir:\"{sourceRoot}\" " +
                $"/Name:\"{imageName}\" " +
                $"/Compress:{compression} " +
                "/CheckIntegrity " +
                "/Verify " +
                "/EA";

            // STEP 3 — run with real-time parsing
            var captureOut = new List<string>();
            var lineProgress = new Progress<string>(line =>
            {
                captureOut.Add(line);
                if (TryParseDismProgress(line, out var pct))
                {
                    progress?.Report(pct);
                }

                if (line.Contains("Error", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Error("[DISM] {Line}", line);
                }
                else if (line.Contains("Warning", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("[DISM] {Line}", line);
                }
                else
                {
                    Log.Information("[DISM] {Line}", line);
                }
            });

        await _processRunner.RunAsync(dismPath, args, lineProgress, ct).ConfigureAwait(false);

        // Append additional volumes if requested
        if (request.AdditionalVolumes is { Count: > 0 })
        {
            foreach (var vol in request.AdditionalVolumes)
            {
                ct.ThrowIfCancellationRequested();

                var v = NormalizeDriveRoot(vol).TrimEnd('\\');
                if (string.IsNullOrWhiteSpace(v) || !_systemIo.DirectoryExists(NormalizeDriveRoot(v)))
                {
                    Log.Warning("Skipping additional volume capture because volume is invalid/missing. volume {Volume}", vol);
                    continue;
                }

                Log.Information("Appending volume to WIM. volume {Volume} wim {WimPath}", v, outputWimPath);

                var appendArgs =
                    "/Append-Image " +
                    $"/ImageFile:\"{outputWimPath}\" " +
                    $"/CaptureDir:\"{NormalizeDriveRoot(v)}\" " +
                    $"/Name:\"{imageName} ({v})\" " +
                    $"/Compress:{compression} " +
                    "/CheckIntegrity " +
                    "/Verify " +
                    "/EA";

                await _processRunner.RunAsync(dismPath, appendArgs, lineProgress, ct).ConfigureAwait(false);
            }
        }

            // STEP 4 — post-capture integrity
            var integrityPassed = true;
            if (request.VerifyAfterCapture)
            {
                var checkArgs = $"/Check-Image /WimFile:\"{outputWimPath}\" /CheckIntegrity";
                var checkOutput = new List<string>();
                var checkProgress = new Progress<string>(line =>
                {
                    checkOutput.Add(line);
                    Log.Information("[DISM Check-Image] {Line}", line);
                });

                try
                {
                    await _processRunner.RunAsync(dismPath, checkArgs, checkProgress, ct).ConfigureAwait(false);
                    integrityPassed = ContainsSuccessLine(checkOutput);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "DISM /Check-Image failed after capture.");
                    integrityPassed = false;
                }

                if (integrityPassed)
                {
                    Log.Information("Capture integrity check passed for {WimPath}", outputWimPath);
                }
                else
                {
                    Log.Warning("Capture integrity check failed for {WimPath}", outputWimPath);
                }
            }

            // STEP 5 — write sidecar JSON
            var wimSize = _systemIo.FileExists(outputWimPath) ? _systemIo.GetFileLength(outputWimPath) : 0;
            var sidecarPath = Path.ChangeExtension(outputWimPath, ".json");
            var sidecar = new
            {
                CapturedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                SourceDrive = request.SourceDrive,
                ImageName = imageName,
                OSBuild = build.ToString(CultureInfo.InvariantCulture),
                OSVersion = WindowsVersionHelper.GetDisplayVersion(),
                ComputerModel = WindowsVersionHelper.GetComputerModel(),
                CompressionType = compression,
                SizeBytes = wimSize,
                IntegrityCheckPassed = integrityPassed,
                WinCloneProVersion = "1.0.0"
            };
            var json = JsonSerializer.Serialize(sidecar, new JsonSerializerOptions { WriteIndented = true });
            _systemIo.WriteAllText(sidecarPath, json);

            stopwatch.Stop();
        stopwatch.Stop();
        return new CaptureResult
        {
            Success = true,
            WimPath = outputWimPath,
            SizeBytes = wimSize,
            Duration = stopwatch.Elapsed,
            IntegrityCheckPassed = integrityPassed,
            ErrorMessage = null
        };
    }

    public async Task<bool> CheckImageIntegrityAsync(string wimPath, CancellationToken ct)
    {
        try
        {
            if (!_systemIo.FileExists(wimPath))
            {
                return false;
            }

            var dismPath = await ResolveDismExePathAsync(ct).ConfigureAwait(false);
            var lines = new List<string>();
            var output = new Progress<string>(line =>
            {
                lines.Add(line);
                Log.Information("[DISM Check-Image] {Line}", line);
            });

            await _processRunner.RunAsync(dismPath, $"/Check-Image /WimFile:\"{wimPath}\" /CheckIntegrity", output, ct)
                .ConfigureAwait(false);
            return ContainsSuccessLine(lines);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CheckImageIntegrityAsync failed for {WimPath}", wimPath);
            return false;
        }
    }

    public async Task<List<ImageInfo>> GetWimInfoAsync(string wimPath, CancellationToken ct)
    {
        try
        {
            if (!_systemIo.FileExists(wimPath))
            {
                return new List<ImageInfo>();
            }

            var dismPath = await ResolveDismExePathAsync(ct).ConfigureAwait(false);
            var lines = new List<string>();
            var output = new Progress<string>(line =>
            {
                lines.Add(line);
                Log.Information("[DISM Get-WimInfo] {Line}", line);
            });

            await _processRunner.RunAsync(dismPath, $"/Get-WimInfo /WimFile:\"{wimPath}\"", output, ct).ConfigureAwait(false);
            return ParseWimInfo(lines);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetWimInfoAsync failed for {WimPath}", wimPath);
            return new List<ImageInfo>();
        }
    }

    public async Task<bool> ApplyImageAsync(ApplyRequest request, IProgress<int> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        Log.Information("Apply requested. wimPath {WimPath} imageIndex {ImageIndex} targetDiskIndex {TargetDiskIndex} injectDrivers {InjectDrivers} driverFolder {DriverFolder}",
            request.WimPath, request.ImageIndex, request.TargetDiskIndex, request.InjectDrivers, request.DriverFolderPath);

        if (request.TargetDiskIndex < 0)
        {
            throw new ArgumentException("TargetDiskIndex must be >= 0", nameof(request.TargetDiskIndex));
        }

        // Validate WIM path before touching any disk.
        if (!_systemIo.FileExists(request.WimPath))
        {
            throw new ArgumentException("WIM path not found.", nameof(request.WimPath));
        }

        // Validate WIM contains at least one image
        var wimInfo = await GetWimInfoAsync(request.WimPath, ct).ConfigureAwait(false);
        if (wimInfo.Count == 0)
        {
            throw new Exception("WIM file contains no valid images");
        }

        // STEP 1 — SAFETY CHECK (MANDATORY)
        DiskInfo? targetDisk = null;
        try
        {
            var disks = await _diskService.GetAllDisksAsync(ct).ConfigureAwait(false);
            targetDisk = disks.Find(d => d.Index == request.TargetDiskIndex);

            if (targetDisk is null)
            {
                throw new ArgumentException($"Target disk index {request.TargetDiskIndex} was not found.");
            }

            if (targetDisk.IsBootDisk)
            {
                throw new SafetyViolationException("CRITICAL: Cannot deploy image to the active OS disk.");
            }

            // Validate target disk size is >= selected image size (best-effort using WIM-reported image size).
            var image = wimInfo.Find(i => i.Index == request.ImageIndex) ?? wimInfo[0];
            var targetDiskBytes = (long)(targetDisk.SizeGB * 1024d * 1024d * 1024d);
            if (targetDiskBytes > 0 && image.SizeBytes > 0 && targetDiskBytes < image.SizeBytes)
            {
                throw new InsufficientSpaceException(
                    requiredBytes: image.SizeBytes,
                    availableBytes: targetDiskBytes,
                    driveLetter: $"Disk {targetDisk.Index}");
            }
        }
        catch (SafetyViolationException)
        {
            Log.Error("ApplyImageAsync blocked by safety rules. targetDiskIndex {TargetDiskIndex}", request.TargetDiskIndex);
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ApplyImageAsync failed during safety checks. targetDiskIndex {TargetDiskIndex}", request.TargetDiskIndex);
            throw;
        }

        // STEP 2 — DISKPART EXECUTION
        string efiDriveLetter;
        string winDriveLetter;
        string scriptPath;
        try
        {
            scriptPath = _diskPartScriptBuilder.CreateGptPartitionScript(
                request.TargetDiskIndex,
                out efiDriveLetter,
                out winDriveLetter);

            await _diskPartScriptBuilder.RunDiskPartAsync(scriptPath, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ApplyImageAsync failed during diskpart execution. targetDiskIndex {TargetDiskIndex}", request.TargetDiskIndex);
            throw;
        }

        // STEP 3 — APPLY IMAGE
        try
        {
            var dismPath = await ResolveDismExePathAsync(ct).ConfigureAwait(false);
            var applyProgress = new Progress<string>(line =>
            {
                if (TryParseDismProgress(line, out var pct))
                {
                    progress?.Report(pct);
                }

                if (line.Contains("Error", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Error("[DISM Apply] {Line}", line);
                }
                else if (line.Contains("Warning", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("[DISM Apply] {Line}", line);
                }
                else
                {
                    Log.Information("[DISM Apply] {Line}", line);
                }
            });

            var applyArgs =
                "/Apply-Image " +
                $"/ImageFile:\"{request.WimPath}\" " +
                $"/Index:{request.ImageIndex} " +
                $"/ApplyDir:\"{winDriveLetter}:\\\\\" " +
                "/CheckIntegrity";

            await _processRunner.RunAsync(dismPath, applyArgs, applyProgress, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ApplyImageAsync failed during DISM Apply-Image stage.");
            throw;
        }

        // STEP 4 — BCDBOOT
        try
        {
            var bcdOut = new Progress<string>(line => Log.Information("[BCDBoot] {Line}", line));
            var bcdBoot = await _toolResolver.ResolveAsync("bcdboot.exe", ct).ConfigureAwait(false);
            if (!bcdBoot.IsAvailable)
            {
                throw new FileNotFoundException("BCDBoot.exe not found.");
            }

            await _processRunner.RunAsync(
                bcdBoot.ResolvedPath,
                $"{winDriveLetter}:\\Windows /s {efiDriveLetter}: /f UEFI",
                bcdOut,
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ApplyImageAsync failed during BCDBoot stage.");
            throw;
        }

        // STEP 5 — OPTIONAL DRIVER INJECTION
        try
        {
            if (request.InjectDrivers)
            {
                if (!_systemIo.DirectoryExists(request.DriverFolderPath))
                {
                    throw new ArgumentException("Driver folder not found.", nameof(request.DriverFolderPath));
                }

                var dismPath = await ResolveDismExePathAsync(ct).ConfigureAwait(false);
                var driverProgress = new Progress<string>(line => Log.Information("[DISM Drivers] {Line}", line));

                var driverArgs =
                    $"/Image:\"{winDriveLetter}:\\\\\" " +
                    $"/Add-Driver /Driver:\"{request.DriverFolderPath}\" /Recurse /ForceUnsigned";

                await _processRunner.RunAsync(dismPath, driverArgs, driverProgress, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ApplyImageAsync failed during driver injection stage.");
            throw;
        }

        // STEP 6 — SUCCESS RETURN (ONLY if all steps succeeded)
        return true;
    }

    public static bool TryParseDismProgress(string line, out int percent)
    {
        percent = 0;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = ProgressRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pct))
        {
            return false;
        }

        percent = (int)pct;
        return true;
    }

    private static string NormalizeDriveRoot(string drive)
    {
        var d = (drive ?? "").Trim();
        if (d.EndsWith("\\", StringComparison.Ordinal))
        {
            return d;
        }

        return d + "\\";
    }

    private void BackupExistingWimIfAny(string wimPath)
    {
        if (!_systemIo.FileExists(wimPath))
        {
            return;
        }

        var dir = Path.GetDirectoryName(wimPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(wimPath);
        var ext = Path.GetExtension(wimPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var backup = Path.Combine(dir, $"{name}.bak.{timestamp}{ext}");
        _systemIo.MoveFile(wimPath, backup, overwrite: false);
    }

    private static bool ContainsSuccessLine(List<string> lines)
    {
        return lines.Exists(
            l => l.Contains("The operation completed successfully", StringComparison.OrdinalIgnoreCase));
    }

    private static List<ImageInfo> ParseWimInfo(List<string> lines)
    {
        var list = new List<ImageInfo>();
        ImageInfoBuilder? current = null;

        foreach (var line in lines)
        {
            var trimmed = (line ?? "").Trim();
            if (trimmed.StartsWith("Index", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null)
                {
                    list.Add(current.Build());
                }

                current = new ImageInfoBuilder
                {
                    Index = ParseIntAfterColon(trimmed)
                };
            }
            else if (current is not null && trimmed.StartsWith("Name", StringComparison.OrdinalIgnoreCase))
            {
                current.Name = StringAfterColon(trimmed);
            }
            else if (current is not null && trimmed.StartsWith("Description", StringComparison.OrdinalIgnoreCase))
            {
                current.Description = StringAfterColon(trimmed);
            }
            else if (current is not null && trimmed.StartsWith("Size", StringComparison.OrdinalIgnoreCase))
            {
                current.SizeBytes = ParseSizeBytes(StringAfterColon(trimmed));
            }
        }

        if (current is not null)
        {
            list.Add(current.Build());
        }

        return list;
    }

    private static string StringAfterColon(string line)
    {
        var i = line.IndexOf(':');
        return i < 0 ? "" : line[(i + 1)..].Trim();
    }

    private static int ParseIntAfterColon(string line)
    {
        var value = StringAfterColon(line);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : 0;
    }

    private static long ParseSizeBytes(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        var cleaned = raw.Replace(",", "", StringComparison.Ordinal);
        var digits = Regex.Replace(cleaned, @"[^\d]", "");
        return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private async Task<string> ResolveDismExePathAsync(CancellationToken ct)
    {
        var resolution = await _toolResolver.ResolveAsync("dism.exe", ct).ConfigureAwait(false);
        if (resolution.IsAvailable)
        {
            return resolution.ResolvedPath;
        }

        throw new FileNotFoundException("DISM.exe not found. Ensure Windows ADK is installed.");
    }

    private sealed class ImageInfoBuilder
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public long SizeBytes { get; set; }

        public ImageInfo Build()
        {
            return new ImageInfo
            {
                Index = Index,
                Name = Name,
                Description = Description,
                SizeBytes = SizeBytes,
                CapturedAt = DateTime.UtcNow
            };
        }
    }
}

