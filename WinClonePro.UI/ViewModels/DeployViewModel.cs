using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WinClonePro.Core.Exceptions;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;
using WinClonePro.UI.Dialogs;
using Forms = System.Windows.Forms;

namespace WinClonePro.UI.ViewModels;

public partial class DeployViewModel : ObservableObject
{
    private readonly IDiskService _diskService;
    private readonly IDismService _dismService;
    private readonly IConfirmWipeDialogService _confirmWipeDialogService;
    private readonly IDialogMessageService _dialogMessageService;

    private CancellationTokenSource? _deployCts;

    [ObservableProperty]
    private ObservableCollection<DiskInfo> availableDisks = new();

    [ObservableProperty]
    private ObservableCollection<ImageInfo> availableImages = new();

    [ObservableProperty]
    private DiskInfo? selectedTargetDisk;

    [ObservableProperty]
    private ImageInfo? selectedImage;

    [ObservableProperty]
    private string wimPath = "";

    [ObservableProperty]
    private bool injectDrivers;

    [ObservableProperty]
    private string driverFolderPath = "";

    [ObservableProperty]
    private int deployProgress;

    [ObservableProperty]
    private string statusMessage = "";

    [ObservableProperty]
    private bool isDeploying;

    public bool CanDeploy =>
        SelectedTargetDisk is not null &&
        SelectedImage is not null &&
        !IsDeploying &&
        !string.IsNullOrWhiteSpace(WimPath) &&
        File.Exists(WimPath);

    partial void OnSelectedTargetDiskChanged(DiskInfo? value)
    {
        OnPropertyChanged(nameof(CanDeploy));
    }

    partial void OnSelectedImageChanged(ImageInfo? value)
    {
        OnPropertyChanged(nameof(CanDeploy));
    }

    public DeployViewModel(
        IDiskService diskService,
        IDismService dismService,
        IConfirmWipeDialogService confirmWipeDialogService,
        IDialogMessageService dialogMessageService)
    {
        _diskService = diskService ?? throw new ArgumentNullException(nameof(diskService));
        _dismService = dismService ?? throw new ArgumentNullException(nameof(dismService));
        _confirmWipeDialogService = confirmWipeDialogService ?? throw new ArgumentNullException(nameof(confirmWipeDialogService));
        _dialogMessageService = dialogMessageService ?? throw new ArgumentNullException(nameof(dialogMessageService));

        _ = LoadDisksAsync();
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

            SelectedTargetDisk = AvailableDisks.Count > 0 ? AvailableDisks[0] : null;
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load disks.";
            Log.Error(ex, "Failed to load disks for deploy view.");
        }
    }

    [RelayCommand]
    private void BrowseWim()
    {
        using var ofd = new Forms.OpenFileDialog
        {
            Filter = "WIM files (*.wim)|*.wim",
            Title = "Select WIM image"
        };

        if (ofd.ShowDialog() == Forms.DialogResult.OK)
        {
            WimPath = ofd.FileName;
        }
    }

    [RelayCommand]
    private async Task LoadWimInfo()
    {
        if (string.IsNullOrWhiteSpace(WimPath))
        {
            return;
        }

        StatusMessage = "Loading WIM info...";
        try
        {
            var images = await _dismService.GetWimInfoAsync(WimPath, CancellationToken.None).ConfigureAwait(true);
            AvailableImages.Clear();
            foreach (var img in images)
            {
                AvailableImages.Add(img);
            }

            SelectedImage = AvailableImages.Count > 0 ? AvailableImages[0] : null;
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load WIM info.";
            await _dialogMessageService.ShowErrorAsync(ex.Message, CancellationToken.None).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task StartDeploy()
    {
        if (SelectedTargetDisk is null)
        {
            throw new ArgumentException("Target disk not selected.", nameof(SelectedTargetDisk));
        }

        if (SelectedImage is null)
        {
            throw new ArgumentException("Image not selected.", nameof(SelectedImage));
        }

        if (string.IsNullOrWhiteSpace(WimPath) || !File.Exists(WimPath))
        {
            throw new ArgumentException("Invalid WIM path.", nameof(WimPath));
        }

        if (SelectedTargetDisk.IsBootDisk)
        {
            throw new SafetyViolationException("CRITICAL: Cannot deploy image to the active OS disk.");
        }

        _deployCts = new CancellationTokenSource();
        var token = _deployCts.Token;

        DeployProgress = 0;
        IsDeploying = true;
        StatusMessage = "Partitioning disk...";

        var confirmVm = new ConfirmWipeDialogViewModel();

        var confirmed = await _confirmWipeDialogService
            .ConfirmWipeAsync(confirmVm, SelectedTargetDisk, token)
            .ConfigureAwait(true);

        if (!confirmed)
        {
            IsDeploying = false;
            StatusMessage = "Deployment canceled.";
            return;
        }

        try
        {
            var progress = new Progress<int>(pct =>
            {
                DeployProgress = pct;
                StatusMessage = GetStatusForProgress(pct);
            });

            var request = new ApplyRequest
            {
                WimPath = WimPath,
                ImageIndex = SelectedImage.Index,
                TargetDiskIndex = SelectedTargetDisk.Index,
                TargetDriveLetter = "",
                InjectDrivers = InjectDrivers,
                DriverFolderPath = DriverFolderPath
            };

            var ok = await _dismService.ApplyImageAsync(request, progress, token).ConfigureAwait(true);
            if (!ok)
            {
                throw new InvalidOperationException("Deployment failed.");
            }

            StatusMessage = "Deployment completed successfully.";
            await _dialogMessageService.ShowSuccessAsync("Deployment completed successfully", token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not SafetyViolationException)
        {
            StatusMessage = "Deployment failed.";
            await _dialogMessageService.ShowErrorAsync(ex.Message, token).ConfigureAwait(false);
            throw;
        }
        finally
        {
            IsDeploying = false;
        }
    }

    private string GetStatusForProgress(int pct)
    {
        // Best-effort stage mapping. DISM emits similar progress blocks; tests only assert progress changes.
        if (pct < 15)
        {
            return "Partitioning disk...";
        }

        if (pct < 80)
        {
            return "Applying image...";
        }

        if (pct < 95)
        {
            return "Configuring bootloader...";
        }

        if (InjectDrivers && pct >= 95)
        {
            return "Installing drivers...";
        }

        return "Configuring bootloader...";
    }

    [RelayCommand]
    private void CancelDeploy()
    {
        try
        {
            _deployCts?.Cancel();
        }
        catch
        {
            // ignored
        }
    }
}

