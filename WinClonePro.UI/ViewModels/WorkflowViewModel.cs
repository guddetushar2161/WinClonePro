using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WinClonePro.Core.Exceptions;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;
using WinClonePro.UI.Dialogs;

namespace WinClonePro.UI.ViewModels;

public partial class WorkflowViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly IDiskService _diskService;
    private readonly IDismService _dismService;
    private readonly IWinPeService _winPeService;
    private readonly IConfirmWipeDialogService _confirmWipeDialogService;
    private readonly IDialogMessageService _dialogMessageService;
    private readonly ISystemCheckService _systemCheckService;

    private CancellationTokenSource? _cts;

    [ObservableProperty]
    private int currentStepIndex;

    [ObservableProperty]
    private string currentStepName = "";

    [ObservableProperty]
    private int overallProgress;

    [ObservableProperty]
    private string statusMessage = "";

    [ObservableProperty]
    private ObservableCollection<DiskInfo> availableDisks = new();

    [ObservableProperty]
    private DiskInfo? selectedSourceDisk;

    [ObservableProperty]
    private string captureOutputFolder = "";

    [ObservableProperty]
    private string imageName = "";

    [ObservableProperty]
    private CompressionType selectedCompression = CompressionType.Fast;

    [ObservableProperty]
    private bool verifyAfterCapture = true;

    [ObservableProperty]
    private string winPeOutputFolder = "";

    [ObservableProperty]
    private ObservableCollection<DiskInfo> removableDisks = new();

    [ObservableProperty]
    private DiskInfo? selectedUsbDisk;

    [ObservableProperty]
    private DiskInfo? selectedTargetDisk;

    [ObservableProperty]
    private bool injectDrivers;

    [ObservableProperty]
    private string driverFolderPath = "";

    [ObservableProperty]
    private int deployImageIndex = 1;

    [ObservableProperty]
    private bool isRunning;

    public WorkflowViewModel(
        AppSettings settings,
        IDiskService diskService,
        IDismService dismService,
        IWinPeService winPeService,
        IConfirmWipeDialogService confirmWipeDialogService,
        IDialogMessageService dialogMessageService,
        ISystemCheckService systemCheckService)
    {
        _settings = settings;
        _diskService = diskService;
        _dismService = dismService;
        _winPeService = winPeService;
        _confirmWipeDialogService = confirmWipeDialogService;
        _dialogMessageService = dialogMessageService;
        _systemCheckService = systemCheckService;

        captureOutputFolder = _settings.TemporaryWorkingRootPath;
        winPeOutputFolder = _settings.TemporaryWorkingRootPath;
        imageName = BuildDefaultImageName();
        CurrentStepIndex = 0;
        UpdateStepName();

        _ = LoadDisksAsync();
        _ = LoadSystemChecksAsync();
    }

    private void UpdateStepName()
    {
        CurrentStepName = CurrentStepIndex switch
        {
            0 => "Select Source Disk",
            1 => "Capture Settings",
            2 => "WinPE Creation",
            3 => "USB Creation",
            4 => "Deploy",
            _ => "Unknown Step"
        };
    }

    partial void OnCurrentStepIndexChanged(int value)
    {
        UpdateStepName();
    }

    private async Task LoadDisksAsync()
    {
        try
        {
            var disks = await _diskService.GetAllDisksAsync(CancellationToken.None).ConfigureAwait(true);
            AvailableDisks.Clear();
            foreach (var d in disks)
            {
                AvailableDisks.Add(d);
            }

            var removable = new ObservableCollection<DiskInfo>();
            foreach (var d in disks)
            {
                if (string.Equals(d.MediaType, "Removable", StringComparison.OrdinalIgnoreCase))
                {
                    removable.Add(d);
                }
            }

            RemovableDisks = removable;

            SelectedSourceDisk = AvailableDisks.Count > 0 ? AvailableDisks[0] : null;
            SelectedTargetDisk = AvailableDisks.Count > 0 ? AvailableDisks[0] : null;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Workflow failed to load disks.");
            StatusMessage = "Failed to load disks.";
        }
    }

    partial void OnSelectedSourceDiskChanged(DiskInfo? value)
    {
        if (value is not null && value.ContainsOS && !string.IsNullOrWhiteSpace(value.SystemDriveLetter))
        {
            StatusMessage = $"Detected OS on {value.SystemDriveLetter}";
        }
        else
        {
            StatusMessage = "No OS detected on selected disk.";
        }
    }

    private bool adkInstalled;
    private bool winPeToolsAvailable;

    private async Task LoadSystemChecksAsync()
    {
        try
        {
            var result = await _systemCheckService.RunAllChecksAsync(CancellationToken.None).ConfigureAwait(true);
            adkInstalled = result.AdkInstalled;
            winPeToolsAvailable = result.WinPeToolsAvailable;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed loading system checks for workflow.");
            adkInstalled = false;
            winPeToolsAvailable = false;
        }
    }

    public bool CanGoNext()
    {
        if (IsRunning)
        {
            return false;
        }

        if (CurrentStepIndex == 0)
        {
            if (SelectedSourceDisk is null || !SelectedSourceDisk.ContainsOS)
            {
                return false;
            }
        }

        if (CurrentStepIndex is 2 or 3)
        {
            if (!winPeToolsAvailable)
            {
                return false;
            }
        }

        return CurrentStepIndex switch
        {
            0 => SelectedSourceDisk is not null && SelectedSourceDisk.ContainsOS,
            1 => !string.IsNullOrWhiteSpace(CaptureOutputFolder) &&
                 !string.IsNullOrWhiteSpace(ImageName),
            2 => !string.IsNullOrWhiteSpace(WinPeOutputFolder),
            3 => SelectedUsbDisk is not null,
            4 => SelectedTargetDisk is not null,
            _ => false
        };
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void Next()
    {
        if (CurrentStepIndex < 4)
        {
            CurrentStepIndex++;
        }
    }

    [RelayCommand]
    private void Back()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task StartFullClone()
    {
        if (IsRunning)
        {
            return;
        }

        if (!CanGoNext())
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            IsRunning = true;
            OverallProgress = 0;

            if (!adkInstalled)
            {
                throw new InvalidOperationException("Windows ADK is not installed. WinPE/USB steps are disabled.");
            }

            if (!winPeToolsAvailable)
            {
                throw new InvalidOperationException("WinPE media tools are not available. Install the WinPE add-on or package embedded WinPE tools.");
            }

            // Step 1/2: Capture image
            StatusMessage = "Capturing image...";
            CurrentStepIndex = 1;

            var sourceDrive = WindowsVersionHelper.GetSystemDriveLetter().TrimEnd('\\');
            if (string.IsNullOrWhiteSpace(sourceDrive))
            {
                throw new InvalidOperationException("Unable to determine source drive letter for capture.");
            }

            Directory.CreateDirectory(CaptureOutputFolder);
            var wimPath = Path.Combine(CaptureOutputFolder, $"{ImageName}.wim");

            var captureRequest = new CaptureRequest
            {
                SourceDrive = sourceDrive,
                OutputWimPath = wimPath,
                ImageName = ImageName,
                Compression = SelectedCompression,
                VerifyAfterCapture = VerifyAfterCapture
            };

            var stageProgress = new Progress<int>(pct => { StatusMessage = $"Capturing... {pct}%"; });
            var captureResult = await _dismService.CaptureImageAsync(captureRequest, stageProgress, ct).ConfigureAwait(true);

            if (!captureResult.Success)
            {
                throw new InvalidOperationException(captureResult.ErrorMessage ?? "Capture failed.");
            }

            // Step 3: Create WinPE
            StatusMessage = "Creating WinPE ISO...";
            CurrentStepIndex = 2;

            var winPeProgress = new Progress<int>(pct =>
            {
                OverallProgress = Math.Min(70, pct);
            });

            var isoPath = await _winPeService.CreateWinPeAsync(WinPeOutputFolder, winPeProgress, ct).ConfigureAwait(true);

            // Step 4: Create bootable USB
            StatusMessage = "Preparing bootable USB...";
            CurrentStepIndex = 3;

            if (SelectedUsbDisk is null)
            {
                throw new InvalidOperationException("USB disk not selected.");
            }

            var usbConfirmVm = new ConfirmWipeDialogViewModel();
            var usbConfirmed = await _confirmWipeDialogService
                .ConfirmWipeAsync(usbConfirmVm, SelectedUsbDisk, ct)
                .ConfigureAwait(true);

            if (!usbConfirmed)
            {
                StatusMessage = "USB creation canceled.";
                return;
            }

            var usbProgress = new Progress<int>(pct =>
            {
                // Map 0-100 to 70-95
                var mapped = 70 + (int)(pct * 0.25);
                OverallProgress = Math.Min(95, mapped);
            });

            var okUsb = await _winPeService.CreateBootableUsbAsync(isoPath, SelectedUsbDisk!.Index, usbProgress, ct).ConfigureAwait(true);
            if (!okUsb)
            {
                throw new InvalidOperationException("USB creation failed.");
            }

            // Step 5: Deploy image (optional in this flow, but we perform it because UI step exists)
            StatusMessage = "Deploying image...";
            CurrentStepIndex = 4;

            if (SelectedTargetDisk is null)
            {
                throw new InvalidOperationException("Target disk not selected.");
            }

            if (SelectedTargetDisk.IsBootDisk)
            {
                throw new SafetyViolationException("CRITICAL: Cannot deploy image to the active OS disk.");
            }

            var confirmVm = new ConfirmWipeDialogViewModel();
            var confirmed = await _confirmWipeDialogService
                .ConfirmWipeAsync(confirmVm, SelectedTargetDisk, ct)
                .ConfigureAwait(true);
            if (!confirmed)
            {
                StatusMessage = "Deployment canceled.";
                return;
            }

            var deployProgress = new Progress<int>(pct =>
            {
                // Map 0-100 to 95-100
                var mapped = 95 + (int)(pct * 0.05);
                OverallProgress = Math.Min(100, mapped);
                StatusMessage = $"Deploying... {pct}%";
            });

            var applyRequest = new ApplyRequest
            {
                WimPath = wimPath,
                ImageIndex = DeployImageIndex,
                TargetDiskIndex = SelectedTargetDisk.Index,
                TargetDriveLetter = "",
                InjectDrivers = InjectDrivers,
                DriverFolderPath = DriverFolderPath
            };

            await _dismService.ApplyImageAsync(applyRequest, deployProgress, ct).ConfigureAwait(true);

            StatusMessage = "Clone Completed Successfully";
            OverallProgress = 100;
            await _dialogMessageService.ShowSuccessAsync("Clone Completed Successfully", ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Full clone workflow failed.");
            StatusMessage = ex.Message;
            await _dialogMessageService.ShowErrorAsync(ex.Message, ct).ConfigureAwait(true);
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void CancelRunning()
    {
        try
        {
            _cts?.Cancel();
        }
        catch
        {
            // ignored
        }
    }

    private static string BuildDefaultImageName()
    {
        var model = WindowsVersionHelper.GetComputerModel().Replace(" ", "_", StringComparison.Ordinal);
        var osVersion = WindowsVersionHelper.GetDisplayVersion();
        return $"{model}_{osVersion}_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HHmm}";
    }
}

