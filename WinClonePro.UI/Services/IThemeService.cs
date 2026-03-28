using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.UI.Models;

namespace WinClonePro.UI.Services;

public interface IThemeService
{
    IReadOnlyList<ThemeMode> AvailableModes { get; }
    ThemeMode CurrentMode { get; }
    void Initialize();
    Task SetThemeModeAsync(ThemeMode mode, CancellationToken ct);
}
