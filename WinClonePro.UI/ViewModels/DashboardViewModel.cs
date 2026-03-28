using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;
using WinClonePro.UI.Models;
using WinClonePro.UI.Services;

namespace WinClonePro.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IDiskService _diskService;
    private readonly IThemeService _themeService;
    private bool _isApplyingTheme;

    public ObservableCollection<DiskInfo> Disks { get; } = new();
    public ObservableCollection<ThemeMode> ThemeModes { get; } = new();

    [ObservableProperty]
    private DiskInfo? selectedDisk;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = "";

    [ObservableProperty]
    private ThemeMode selectedThemeMode;

    public IRelayCommand RefreshDisksCommand { get; }

    public DashboardViewModel(IDiskService diskService, IThemeService themeService)
    {
        _diskService = diskService ?? throw new ArgumentNullException(nameof(diskService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));

        foreach (var themeMode in _themeService.AvailableModes)
        {
            ThemeModes.Add(themeMode);
        }

        _isApplyingTheme = true;
        SelectedThemeMode = _themeService.CurrentMode;
        _isApplyingTheme = false;

        RefreshDisksCommand = new AsyncRelayCommand(RefreshDisksAsync);

        _ = RefreshDisksAsync();
    }

    partial void OnSelectedThemeModeChanged(ThemeMode value)
    {
        if (_isApplyingTheme)
        {
            return;
        }

        _ = ApplyThemeModeAsync(value);
    }

    private async Task RefreshDisksAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = "Loading disks...";

        try
        {
            var disks = await _diskService.GetAllDisksAsync(CancellationToken.None).ConfigureAwait(true);

            Disks.Clear();
            foreach (var d in disks)
            {
                Disks.Add(d);
            }

            SelectedDisk = Disks.Count > 0 ? Disks[0] : null;

            var bootCount = 0;
            foreach (var d in Disks)
            {
                if (d.IsBootDisk)
                {
                    bootCount++;
                }
            }

            StatusMessage = bootCount > 0
                ? "Loaded disks. Boot disk detected — be careful with destructive operations."
                : "Loaded disks.";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh disks.");
            StatusMessage = "Failed to load disks.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ApplyThemeModeAsync(ThemeMode value)
    {
        try
        {
            _isApplyingTheme = true;
            await _themeService.SetThemeModeAsync(value, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply theme mode {ThemeMode}.", value);
        }
        finally
        {
            _isApplyingTheme = false;
        }
    }
}

