using WinClonePro.UI.ViewModels;

namespace WinClonePro.UI.Views;

public partial class WorkflowView : System.Windows.Controls.UserControl
{
    public WorkflowView()
    {
        InitializeComponent();
    }

    public WorkflowView(WorkflowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}

