using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinClonePro.Core.Models;

namespace WinClonePro.UI.Dialogs;

public sealed partial class UsbSelectionDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<DiskInfo> removableDisks = new();

    [ObservableProperty]
    private DiskInfo? selectedDisk;

    public bool IsBootDiskSelected => SelectedDisk?.IsBootDisk == true;

    public bool CanConfirm => SelectedDisk is not null && !IsBootDiskSelected;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        CloseRequested?.Invoke(true);
    }

    public event Action<bool>? CloseRequested;

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }
}

