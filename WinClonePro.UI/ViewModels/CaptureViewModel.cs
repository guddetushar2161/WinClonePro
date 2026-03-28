using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;
using Forms = System.Windows.Forms;

namespace WinClonePro.UI.ViewModels;

public partial class CaptureViewModel : ObservableObject
{
    private readonly IDiskService _diskService;
    private readonly IDismService _dismService;
    private CancellationTokenSource? _captureCts;
    private Stopwatch? _captureStopwatch;
    private long _estimatedBytes;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCaptureCommand))]
    private ObservableCollection<DiskInfo> availableDisks = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCaptureCommand))]
    private DiskInfo? selectedSourceDisk;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCaptureCommand))]
    private string outputPath = "";

    [ObservableProperty]
    private string imageName = "";

    [ObservableProperty]
    private CompressionType selectedCompression = CompressionType.Fast;

    [ObservableProperty]
    private bool verifyAfterCapture = true;

    [ObservableProperty]
    private int captureProgress;

    [ObservableProperty]
    private string progressMessage = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCaptureCommand))]
    private bool isCaptureRunning;

    [ObservableProperty]
    private string estimatedTimeRemaining = "";

    [ObservableProperty]
    private string captureSpeed = "";

    [ObservableProperty]
    private CaptureResult? lastResult;

    public bool CanCapture =>
        !IsCaptureRunning &&
        SelectedSourceDisk is not null &&
        !string.IsNullOrWhiteSpace(OutputPath);

    public string EstimatedOutputSize =>
        _estimatedBytes <= 0
            ? "Estimated output size: unknown"
            : $"Estimated output size: {FormatBytes(_estimatedBytes)}";

    public CaptureViewModel(IDiskService diskService, IDismService dismService)
    {
        _diskService = diskService ?? throw new ArgumentNullException(nameof(diskService));
        _dismService = dismService ?? throw new ArgumentNullException(nameof(dismService));

        ImageName = BuildDefaultImageName();
        _ = LoadDisksAsync();
    }

    partial void OnSelectedSourceDiskChanged(DiskInfo? value)
    {
        RecalculateEstimate();
    }

    partial void OnOutputPathChanged(string value)
    {
        OnPropertyChanged(nameof(CanCapture));
    }

    partial void OnIsCaptureRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCapture));
    }

    [RelayCommand]
    private void BrowseOutputPath()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select destination folder for WIM image",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            OutputPath = dialog.SelectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCapture))]
    private async Task StartCaptureAsync()
    {
        if (SelectedSourceDisk is null)
        {
            System.Windows.MessageBox.Show("Select a source disk first.");
            return;
        }

        var sourceDrive = SelectedSourceDisk.SystemDriveLetter;
        if (string.IsNullOrWhiteSpace(sourceDrive))
        {
            throw new InvalidOperationException("Selected disk does not contain a valid Windows installation");
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            System.Windows.MessageBox.Show("Select an output path first.");
            return;
        }

        if (string.IsNullOrWhiteSpace(ImageName))
        {
            ImageName = BuildDefaultImageName();
        }

        var outputWimPath = Path.Combine(OutputPath, $"{SanitizeFileName(ImageName)}.wim");
        var request = new CaptureRequest
        {
            SourceDrive = sourceDrive,
            OutputWimPath = outputWimPath,
            ImageName = ImageName,
            Compression = SelectedCompression,
            VerifyAfterCapture = VerifyAfterCapture
        };

        _captureCts = new CancellationTokenSource();
        _captureStopwatch = Stopwatch.StartNew();
        CaptureProgress = 0;
        ProgressMessage = "Starting capture...";
        EstimatedTimeRemaining = "";
        CaptureSpeed = "";
        LastResult = null;
        IsCaptureRunning = true;

        var uiProgress = new Progress<int>(async pct =>
        {
            if (_captureStopwatch is null)
            {
                return;
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CaptureProgress = pct;
                ProgressMessage = $"Capturing... {pct}%";

                var elapsedSec = Math.Max(_captureStopwatch.Elapsed.TotalSeconds, 1);
                var processedBytes = (long)(_estimatedBytes * (pct / 100d));
                var speed = processedBytes / elapsedSec;
                var remain = Math.Max(_estimatedBytes - processedBytes, 0);
                var etaSeconds = speed > 0 ? remain / speed : 0;

                CaptureSpeed = speed > 0 ? $"{FormatBytes((long)speed)}/s" : "0 B/s";
                EstimatedTimeRemaining = etaSeconds > 0
                    ? $"~{Math.Ceiling(etaSeconds / 60d)} minutes remaining"
                    : "~calculating...";
            });
        });

        try
        {
            var result = await Task.Run(
                () => _dismService.CaptureImageAsync(request, uiProgress, _captureCts.Token),
                _captureCts.Token);

            LastResult = result;
            if (result.Success)
            {
                System.Windows.MessageBox.Show("Capture completed successfully.", "WinClone Pro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show(result.ErrorMessage ?? "Capture failed.", "WinClone Pro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        catch (OperationCanceledException)
        {
            ProgressMessage = "Capture canceled.";
            System.Windows.MessageBox.Show("Capture canceled.", "WinClone Pro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "StartCapture failed.");
            ProgressMessage = "Capture failed.";
            System.Windows.MessageBox.Show(ex.Message, "Capture Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            _captureStopwatch?.Stop();
            IsCaptureRunning = false;
        }
    }

    [RelayCommand]
    private void CancelCapture()
    {
        _captureCts?.Cancel();
    }

    [RelayCommand]
    private void OpenResultFolder()
    {
        if (LastResult is null || string.IsNullOrWhiteSpace(LastResult.WimPath))
        {
            return;
        }

        var folder = Path.GetDirectoryName(LastResult.WimPath);
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            Process.Start("explorer.exe", folder);
        }
    }

    private async Task LoadDisksAsync()
    {
        try
        {
            var disks = await _diskService.GetAllDisksAsync(CancellationToken.None);
            AvailableDisks.Clear();
            foreach (var d in disks)
            {
                AvailableDisks.Add(d);
            }

            SelectedSourceDisk = AvailableDisks.Count > 0 ? AvailableDisks[0] : null;
            RecalculateEstimate();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed loading disks for capture view.");
        }
    }

    private void RecalculateEstimate()
    {
        _estimatedBytes = SelectedSourceDisk is null
            ? 0
            : (long)(SelectedSourceDisk.SizeGB * 1024d * 1024d * 1024d * 0.7d);
        OnPropertyChanged(nameof(EstimatedOutputSize));
    }

    private static string BuildDefaultImageName()
    {
        var model = WindowsVersionHelper.GetComputerModel().Replace(" ", "_", StringComparison.Ordinal);
        var osVersion = WindowsVersionHelper.GetDisplayVersion();
        return $"{model}_{osVersion}_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HHmm}";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        var idx = 0;
        while (size >= 1024 && idx < units.Length - 1)
        {
            size /= 1024;
            idx++;
        }

        return string.Format(CultureInfo.InvariantCulture, "{0:0.#} {1}", size, units[idx]);
    }

    private static string SanitizeFileName(string name)
    {
        var n = name ?? "";
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            n = n.Replace(c, '_');
        }

        return n.Trim();
    }
}

