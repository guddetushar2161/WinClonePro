using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinClonePro.Core.Models;

namespace WinClonePro.UI.Dialogs;

public sealed partial class SystemRequirementsDialogViewModel : ObservableObject
{
    public sealed class RequirementRow
    {
        public string Label { get; init; } = "";
        public bool IsPassed { get; init; }
        public string StatusText => IsPassed ? "✔" : "✖";
        public System.Windows.Media.Brush StatusBrush =>
            IsPassed ? System.Windows.Media.Brushes.ForestGreen : System.Windows.Media.Brushes.IndianRed;
    }

    public ObservableCollection<RequirementRow> RequirementRows { get; } = new();

    private readonly SystemCheckResult _result;
    public ObservableCollection<string> Errors { get; } = new();

    public bool ShowInstallAdkButton => !_result.AdkInstalled;

    public string DialogTitle => "System Requirements Not Met";

    private const string AdkInstallUrl =
        "https://learn.microsoft.com/en-us/windows-hardware/get-started/adk-install";

    public SystemRequirementsDialogViewModel(SystemCheckResult result)
    {
        _result = result ?? throw new ArgumentNullException(nameof(result));

        RequirementRows.Add(new RequirementRow { Label = "Administrator", IsPassed = result.IsAdministrator });
        RequirementRows.Add(new RequirementRow { Label = "DISM available", IsPassed = result.DismAvailable });
        RequirementRows.Add(new RequirementRow { Label = "DiskPart available", IsPassed = result.DiskPartAvailable });
        RequirementRows.Add(new RequirementRow { Label = "BCDBoot available", IsPassed = result.BcdBootAvailable });
        RequirementRows.Add(new RequirementRow { Label = "Windows ADK installed", IsPassed = result.AdkInstalled });
        RequirementRows.Add(new RequirementRow { Label = "WinPE media tools available", IsPassed = result.WinPeToolsAvailable });

        foreach (var e in result.Errors)
        {
            Errors.Add(e);
        }

        foreach (var warning in result.Warnings)
        {
            Errors.Add(warning);
        }
    }

    [RelayCommand]
    private void InstallAdk()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AdkInstallUrl) { UseShellExecute = true });
        }
        catch
        {
            // No-op: user can navigate manually.
        }
    }
}

