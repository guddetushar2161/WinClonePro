using System.Threading;
using System.Threading.Tasks;

namespace WinClonePro.UI.Dialogs;

public interface IDialogMessageService
{
    Task ShowSuccessAsync(string message, CancellationToken ct);
    Task ShowErrorAsync(string message, CancellationToken ct);
}

