using System.Windows.Controls;
using WinClonePro.UI.ViewModels;

namespace WinClonePro.UI.Views;

public partial class DashboardView : System.Windows.Controls.UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    public DashboardView(DashboardViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}

