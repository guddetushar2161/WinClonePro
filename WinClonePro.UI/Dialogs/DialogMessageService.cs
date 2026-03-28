using System.Threading;
using System.Threading.Tasks;

namespace WinClonePro.UI.Dialogs;

public sealed class DialogMessageService : IDialogMessageService
{
    public Task ShowSuccessAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        System.Windows.MessageBox.Show(message, "WinClone Pro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        return Task.CompletedTask;
    }

    public Task ShowErrorAsync(string message, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        System.Windows.MessageBox.Show(message, "WinClone Pro", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        return Task.CompletedTask;
    }
}

