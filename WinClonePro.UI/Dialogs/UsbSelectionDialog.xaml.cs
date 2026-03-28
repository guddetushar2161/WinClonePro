using System.Windows;
using WinClonePro.Core.Models;

namespace WinClonePro.UI.Dialogs;

public partial class UsbSelectionDialog : Window
{
    public DiskInfo? SelectedDisk => (DataContext as UsbSelectionDialogViewModel)?.SelectedDisk;

    public UsbSelectionDialog(UsbSelectionDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += confirmed =>
        {
            DialogResult = confirmed;
            Close();
        };
    }
}

