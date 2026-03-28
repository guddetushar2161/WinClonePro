using System.Windows.Controls;
using WinClonePro.UI.ViewModels;

namespace WinClonePro.UI.Views;

public partial class CaptureView : System.Windows.Controls.UserControl
{
    public CaptureView()
    {
        InitializeComponent();
    }

    public CaptureView(CaptureViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}

