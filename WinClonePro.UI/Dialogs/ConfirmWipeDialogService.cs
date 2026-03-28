using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Models;

namespace WinClonePro.UI.Dialogs;

public sealed class ConfirmWipeDialogService : IConfirmWipeDialogService
{
    public Task<bool> ConfirmWipeAsync(ConfirmWipeDialogViewModel vm, DiskInfo disk, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var dialog = new ConfirmWipeDialog(
            vm,
            disk.Model,
            disk.FriendlySize,
            disk.Index);

        var result = dialog.ShowDialog();
        return Task.FromResult(result == true);
    }
}

