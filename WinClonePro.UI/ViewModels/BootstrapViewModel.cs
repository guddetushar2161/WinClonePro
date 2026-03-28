using CommunityToolkit.Mvvm.ComponentModel;
using WinClonePro.Core.Models;

namespace WinClonePro.UI.ViewModels;

public partial class BootstrapViewModel : ObservableObject
{
    [ObservableProperty]
    private int progress;

    [ObservableProperty]
    private string currentStep = "Preparing system";

    [ObservableProperty]
    private string detail = "Checking your environment and preparing required components.";

    [ObservableProperty]
    private string extractingToolsStatus = "Pending";

    [ObservableProperty]
    private string checkingSystemStatus = "Pending";

    [ObservableProperty]
    private string installingComponentsStatus = "Pending";

    [ObservableProperty]
    private string finalizingStatus = "Pending";

    public void ApplyProgress(BootstrapProgressState state)
    {
        Progress = state.ProgressPercentage;
        CurrentStep = state.CurrentStep;
        Detail = state.Detail;

        switch (state.Stage)
        {
            case BootstrapStage.ExtractingTools:
                ExtractingToolsStatus = "In progress";
                break;

            case BootstrapStage.CheckingSystem:
                ExtractingToolsStatus = NormalizeFinishedState(ExtractingToolsStatus);
                CheckingSystemStatus = "In progress";
                break;

            case BootstrapStage.InstallingComponents:
                ExtractingToolsStatus = NormalizeFinishedState(ExtractingToolsStatus);
                CheckingSystemStatus = NormalizeFinishedState(CheckingSystemStatus);
                InstallingComponentsStatus = "In progress";
                break;

            case BootstrapStage.Finalizing:
                ExtractingToolsStatus = NormalizeFinishedState(ExtractingToolsStatus);
                CheckingSystemStatus = NormalizeFinishedState(CheckingSystemStatus);
                if (InstallingComponentsStatus == "Pending")
                {
                    InstallingComponentsStatus = "Skipped";
                }
                else
                {
                    InstallingComponentsStatus = NormalizeFinishedState(InstallingComponentsStatus);
                }

                FinalizingStatus = "In progress";
                break;

            case BootstrapStage.Ready:
                ExtractingToolsStatus = NormalizeFinishedState(ExtractingToolsStatus);
                CheckingSystemStatus = NormalizeFinishedState(CheckingSystemStatus);
                if (InstallingComponentsStatus == "Pending")
                {
                    InstallingComponentsStatus = "Skipped";
                }
                else
                {
                    InstallingComponentsStatus = NormalizeFinishedState(InstallingComponentsStatus);
                }

                FinalizingStatus = "Complete";
                break;

            case BootstrapStage.Failed:
                FinalizingStatus = "Blocked";
                break;
        }
    }

    private static string NormalizeFinishedState(string value)
    {
        return value == "Pending" ? "Complete" : value == "In progress" ? "Complete" : value;
    }
}
