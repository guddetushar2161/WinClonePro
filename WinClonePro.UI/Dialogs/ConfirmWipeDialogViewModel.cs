using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinClonePro.UI.Dialogs;

public partial class ConfirmWipeDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string confirmationText = "";

    public bool CanConfirm => string.Equals(ConfirmationText, "WIPE", StringComparison.OrdinalIgnoreCase);

    public event Action<bool>? CloseRequested;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm()
    {
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(false);
    }
}

