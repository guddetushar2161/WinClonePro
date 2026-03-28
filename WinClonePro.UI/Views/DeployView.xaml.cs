using Forms = System.Windows.Forms;

namespace WinClonePro.UI.Views;

public partial class DeployView : System.Windows.Controls.UserControl
{
    public DeployView()
    {
        InitializeComponent();
    }

    public DeployView(ViewModels.DeployViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void OnBrowseDriverFolderClicked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not ViewModels.DeployViewModel vm)
        {
            return;
        }

        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select driver folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            vm.DriverFolderPath = dialog.SelectedPath;
        }
    }
}

