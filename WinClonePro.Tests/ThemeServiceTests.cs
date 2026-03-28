using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinClonePro.Core.Models;
using WinClonePro.UI.Models;
using WinClonePro.UI.Services;

namespace WinClonePro.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public async Task SetThemeModeAsync_PersistsSelectionForNextStartup()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinClonePro_ThemeService_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var settings = new AppSettings(
                appDataRootPath: Path.Combine(root, "AppData"),
                programDataRootPath: Path.Combine(root, "ProgramData"),
                windowsDirectoryPath: Path.Combine(root, "Windows"),
                adkRootPath: Path.Combine(root, "Adk"));

            using (var service = new ThemeService(settings))
            {
                service.Initialize();
                await service.SetThemeModeAsync(ThemeMode.Dark, CancellationToken.None);
            }

            using (var service = new ThemeService(settings))
            {
                service.Initialize();
                Assert.Equal(ThemeMode.Dark, service.CurrentMode);
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
