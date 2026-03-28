using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Models;

namespace WinClonePro.UI.Dialogs;

public interface IConfirmWipeDialogService
{
    Task<bool> ConfirmWipeAsync(ConfirmWipeDialogViewModel vm, DiskInfo disk, CancellationToken ct);
}

