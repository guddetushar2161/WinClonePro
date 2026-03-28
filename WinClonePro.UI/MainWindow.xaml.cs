using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WinClonePro.UI.Views;
using MessageBox = System.Windows.MessageBox;

namespace WinClonePro.UI;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HashSet<int> _loadedTabs = [];
    private bool _navigationReady;

    public MainWindow(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        InitializeComponent();
    }

    public void CompleteStartup()
    {
        if (_navigationReady)
        {
            return;
        }

        _navigationReady = true;
        NavigationTabs.IsEnabled = true;
        LoadSelectedTabContent();
    }

    private void OnNavigationTabsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_navigationReady || !IsLoaded)
        {
            return;
        }

        LoadSelectedTabContent();
    }

    private void LoadSelectedTabContent()
    {
        if (NavigationTabs.SelectedIndex < 0 || _loadedTabs.Contains(NavigationTabs.SelectedIndex))
        {
            return;
        }

        if (NavigationTabs.SelectedItem is not TabItem selectedTab)
        {
            return;
        }

        try
        {
            selectedTab.Content = CreateTabContent(NavigationTabs.SelectedIndex);
            _loadedTabs.Add(NavigationTabs.SelectedIndex);

            Log.Information(
                "Loaded main tab {TabIndex} ({TabHeader}).",
                NavigationTabs.SelectedIndex,
                selectedTab.Header?.ToString() ?? "Unknown");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load tab {TabIndex}.", NavigationTabs.SelectedIndex);
            MessageBox.Show(
                "WinClone Pro could not load this screen. Review the log for details.",
                "Screen failed to load",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private System.Windows.Controls.UserControl CreateTabContent(int tabIndex)
    {
        return tabIndex switch
        {
            0 => new DashboardView(_serviceProvider.GetRequiredService<ViewModels.DashboardViewModel>()),
            1 => new CaptureView(_serviceProvider.GetRequiredService<ViewModels.CaptureViewModel>()),
            2 => new DeployView(_serviceProvider.GetRequiredService<ViewModels.DeployViewModel>()),
            3 => new WorkflowView(_serviceProvider.GetRequiredService<ViewModels.WorkflowViewModel>()),
            _ => throw new ArgumentOutOfRangeException(nameof(tabIndex), tabIndex, "Unknown tab index")
        };
    }
}
