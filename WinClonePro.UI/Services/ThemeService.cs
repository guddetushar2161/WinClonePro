using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using Serilog;
using WinClonePro.Core.Models;
using WinClonePro.UI.Models;
using WpfApplication = System.Windows.Application;

namespace WinClonePro.UI.Services;

public sealed class ThemeService : IThemeService, IDisposable
{
    private static readonly IReadOnlyList<ThemeMode> ThemeModes =
    [
        ThemeMode.System,
        ThemeMode.Light,
        ThemeMode.Dark
    ];

    private readonly AppSettings _settings;
    private readonly PaletteHelper _paletteHelper = new();
    private readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };
    private bool _initialized;

    public ThemeService(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public IReadOnlyList<ThemeMode> AvailableModes => ThemeModes;

    public ThemeMode CurrentMode { get; private set; } = ThemeMode.System;

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        CurrentMode = LoadPreferences().Mode;
        ApplyCurrentTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public async Task SetThemeModeAsync(ThemeMode mode, CancellationToken ct)
    {
        CurrentMode = mode;
        ApplyCurrentTheme();
        await SavePreferencesAsync(new ThemePreferences { Mode = mode }, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }

    private string PreferencesPath => Path.Combine(_settings.AppDataRootPath, "ui-preferences.json");

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (CurrentMode != ThemeMode.System)
        {
            return;
        }

        if (WpfApplication.Current?.Dispatcher is { } dispatcher)
        {
            _ = dispatcher.InvokeAsync(ApplyCurrentTheme);
        }
    }

    private void ApplyCurrentTheme()
    {
        try
        {
            if (WpfApplication.Current is null)
            {
                return;
            }

            var theme = _paletteHelper.GetTheme();
            theme.SetBaseTheme(ResolveBaseTheme());
            _paletteHelper.SetTheme(theme);

            Log.Information("Applied theme mode {ThemeMode}.", CurrentMode);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed applying theme mode {ThemeMode}.", CurrentMode);
        }
    }

    private BaseTheme ResolveBaseTheme()
    {
        return CurrentMode switch
        {
            ThemeMode.Dark => BaseTheme.Dark,
            ThemeMode.Light => BaseTheme.Light,
            _ => ReadSystemTheme() ? BaseTheme.Dark : BaseTheme.Light
        };
    }

    private ThemePreferences LoadPreferences()
    {
        try
        {
            if (!File.Exists(PreferencesPath))
            {
                return new ThemePreferences();
            }

            var json = File.ReadAllText(PreferencesPath);
            return JsonSerializer.Deserialize<ThemePreferences>(json, _serializerOptions) ?? new ThemePreferences();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed loading theme preferences from {PreferencesPath}.", PreferencesPath);
            return new ThemePreferences();
        }
    }

    private async Task SavePreferencesAsync(ThemePreferences preferences, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(_settings.AppDataRootPath);

            await using var stream = File.Create(PreferencesPath);
            await JsonSerializer.SerializeAsync(stream, preferences, _serializerOptions, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed saving theme preferences to {PreferencesPath}.", PreferencesPath);
        }
    }

    private static bool ReadSystemTheme()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1);

            return value switch
            {
                int intValue => intValue == 0,
                byte byteValue => byteValue == 0,
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}
