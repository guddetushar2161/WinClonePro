using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace WinClonePro.UI.Views;

public partial class BootstrapWindow : Window
{
    public BootstrapWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(string message, int progress)
    {
        Dispatcher.InvokeAsync(() =>
        {
            StatusText.Text = message;
            StartupProgress.Value = progress;
        });
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });

        e.Handled = true;
    }
}

