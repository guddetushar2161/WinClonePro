using System.Windows;

namespace WinClonePro.UI.Dialogs;

public partial class ConfirmWipeDialog : Window
{
    public string DiskModel { get; }
    public string DiskFriendlySize { get; }
    public int DiskIndex { get; }

    public ConfirmWipeDialog(ConfirmWipeDialogViewModel viewModel, string diskModel, string diskFriendlySize, int diskIndex)
    {
        InitializeComponent();
        DataContext = viewModel;

        DiskModel = diskModel ?? "";
        DiskFriendlySize = diskFriendlySize ?? "";
        DiskIndex = diskIndex;

        viewModel.CloseRequested += result =>
        {
            DialogResult = result;
            Close();
        };
    }
}

