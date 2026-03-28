using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using WinClonePro.Core.Helpers;
using WinClonePro.Core.Interfaces;
using WinClonePro.Core.Models;
using WinClonePro.Core.Services;
using WinClonePro.UI.Services;
using WinClonePro.UI.Dialogs;
using WinClonePro.UI.ViewModels;
using WinClonePro.UI.Views;
using MessageBox = System.Windows.MessageBox;
using WinFormsMessageBox = System.Windows.Forms.MessageBox;
using WinFormsMessageBoxButtons = System.Windows.Forms.MessageBoxButtons;
using WinFormsMessageBoxIcon = System.Windows.Forms.MessageBoxIcon;

namespace WinClonePro.UI;

public partial class App : System.Windows.Application
{
    private const string StartupFailureTitle = "WinClone Pro Startup Error";
    private static int _loggingInitialized;
    private ServiceProvider? _services;
    private AppSettings? _settings;
    private bool _fatalErrorShown;

    public App(AppSettings? startupSettings = null)
    {
        _settings = startupSettings;
        InitializeComponent();
    }

    [STAThread]
    public static void Main()
    {
        AppSettings? settings = null;

        try
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

            settings = new AppSettings();
            InitializeLogging(settings);
            Log.Information("Process entry reached before WPF application initialization.");

            var app = new App(settings);
            app.Run();
        }
        catch (Exception ex)
        {
            TryLogStartupException(ex, "Unhandled exception before application run.");
            TryShutdownPartialApplication();
            ShowStartupMessage(ex.Message, settings?.LogDirectoryPath);
            Log.CloseAndFlush();
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            _settings ??= new AppSettings();
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            InitializeLogging(_settings);

            RegisterGlobalExceptionHandlers();
            LogStartupContext(_settings, e);

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(_settings);
            serviceCollection.AddSingleton<IProcessRunner, ProcessRunner>();
            serviceCollection.AddSingleton<ISystemIo, SystemIo>();
            serviceCollection.AddSingleton<IWmiQueryRunner, WmiQueryRunner>();
            serviceCollection.AddSingleton<IToolResolver, ToolResolver>();
            serviceCollection.AddSingleton<IDigitalSignatureVerifier, DigitalSignatureVerifier>();
            serviceCollection.AddSingleton<IEmbeddedToolService, EmbeddedToolService>();
            serviceCollection.AddSingleton<IDependencyInstallerService, DependencyInstallerService>();
            serviceCollection.AddSingleton<IBootstrapService, BootstrapService>();
            serviceCollection.AddSingleton<IDiskService, DiskService>();
            serviceCollection.AddSingleton<DiskPartScriptBuilder>();
            serviceCollection.AddSingleton<IDismService, DismService>();
            serviceCollection.AddSingleton<IWinPeService, WinPeService>();
            serviceCollection.AddSingleton<ISystemCheckService, SystemCheckService>();
            serviceCollection.AddSingleton<BootstrapViewModel>();
            serviceCollection.AddSingleton<DashboardViewModel>();
            serviceCollection.AddSingleton<CaptureViewModel>();
            serviceCollection.AddSingleton<IConfirmWipeDialogService, ConfirmWipeDialogService>();
            serviceCollection.AddSingleton<IDialogMessageService, DialogMessageService>();
            serviceCollection.AddSingleton<DeployViewModel>();
            serviceCollection.AddSingleton<WorkflowViewModel>();
            serviceCollection.AddSingleton<IThemeService, ThemeService>();
            serviceCollection.AddSingleton<MainWindow>();

            _services = serviceCollection.BuildServiceProvider();
            _services.GetRequiredService<IThemeService>().Initialize();
            Log.Information("Application services built successfully.");

            var mainWindow = _services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();

            EventHandler? contentRenderedHandler = null;
            contentRenderedHandler = (_, _) =>
            {
                mainWindow.ContentRendered -= contentRenderedHandler;
                Log.Information("Main window loaded.");
                _ = RunPostStartupInitializationAsync(mainWindow);
            };
            mainWindow.ContentRendered += contentRenderedHandler;
        }
        catch (Exception ex)
        {
            HandleStartupFailure(ex);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _services?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error disposing service provider.");
        }
        finally
        {
            Log.CloseAndFlush();
        }

        base.OnExit(e);
    }

    private async Task RunPostStartupInitializationAsync(MainWindow mainWindow)
    {
        BootstrapWindow? bootstrapWindow = null;

        try
        {
            Log.Information("Starting post-render system preparation.");
            var serviceProvider = _services ?? throw new InvalidOperationException("Application services are unavailable.");

            var bootstrapViewModel = serviceProvider.GetRequiredService<BootstrapViewModel>();
            bootstrapViewModel.ApplyProgress(new BootstrapProgressState
            {
                Stage = BootstrapStage.CheckingSystem,
                ProgressPercentage = 0,
                CurrentStep = "Checking system",
                Detail = "Preparing WinClone Pro for first use."
            });

            bootstrapWindow = new BootstrapWindow
            {
                Owner = mainWindow,
                DataContext = bootstrapViewModel
            };

            bootstrapWindow.UpdateStatus("Checking system", 0);

            mainWindow.IsEnabled = false;
            bootstrapWindow.Show();

            var bootstrapService = serviceProvider.GetRequiredService<IBootstrapService>();
            var bootstrapProgress = new Progress<BootstrapProgressState>(state =>
            {
                bootstrapViewModel.ApplyProgress(state);
                bootstrapWindow.UpdateStatus(state.CurrentStep, state.ProgressPercentage);
            });
            var bootstrapResult = await bootstrapService
                .PrepareSystemAsync(bootstrapProgress, CancellationToken.None)
                .ConfigureAwait(true);

            if (!bootstrapResult.Success)
            {
                var errorText = bootstrapResult.SystemCheckResult.Errors.Count > 0
                    ? string.Join(Environment.NewLine, bootstrapResult.SystemCheckResult.Errors)
                    : bootstrapResult.FailureMessage;

                Log.Error("System preparation failed after UI load: {ErrorText}", errorText);
                ShowFatalError(
                    "WinClone Pro could not prepare this system." + Environment.NewLine + Environment.NewLine + errorText,
                    "System preparation failed");
                Shutdown(-1);
                return;
            }

            mainWindow.CompleteStartup();
            Log.Information("Post-render system preparation completed successfully.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Post-render system preparation failed unexpectedly.");
            ShowFatalError(
                "WinClone Pro failed while preparing required services after launch. Review the log for details.",
                "Startup failed");
            Shutdown(-1);
        }
        finally
        {
            if (bootstrapWindow is not null)
            {
                bootstrapWindow.Close();
            }

            if (mainWindow.IsLoaded && mainWindow.IsVisible && Current?.MainWindow is not null && Current.MainWindow.IsLoaded)
            {
                Current.MainWindow.IsEnabled = true;
            }
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled dispatcher exception.");
        ShowFatalError(
            "WinClone Pro encountered an unexpected UI error and needs to close. Review the log for details.",
            "Unexpected error");
        e.Handled = true;
        Shutdown(-1);
    }

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Error(ex, "Unhandled AppDomain exception. IsTerminating={IsTerminating}", e.IsTerminating);
        }
        else
        {
            Log.Error("Unhandled AppDomain exception object: {ExceptionObject}", e.ExceptionObject);
        }

        ShowFatalError(
            "WinClone Pro encountered a fatal background error and will close. Review the log for details.",
            "Fatal error");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception.");
        e.SetObserved();
        ShowFatalError(
            "WinClone Pro encountered an unexpected background task error. Review the log for details.",
            "Background task error");
    }

    private void HandleStartupFailure(Exception ex)
    {
        TryLogStartupException(ex, "Failed during application startup.");
        TryShutdownPartialApplication();
        ShowStartupMessage("WinClone Pro failed during startup.", _settings?.LogDirectoryPath);

        Shutdown(-1);
    }

    private void LogStartupContext(AppSettings settings, StartupEventArgs e)
    {
        Log.Information("App starting...");
        Log.Information(
            "Startup context: BaseDirectory={BaseDirectory}, CurrentDirectory={CurrentDirectory}, AppDataRoot={AppDataRoot}, ProgramDataRoot={ProgramDataRoot}, LogDirectory={LogDirectory}, OSVersion={OSVersion}, Framework={Framework}, Is64BitProcess={Is64BitProcess}, MachineName={MachineName}, UserName={UserName}, Arguments={Arguments}",
            AppDomain.CurrentDomain.BaseDirectory,
            Directory.GetCurrentDirectory(),
            settings.AppDataRootPath,
            settings.ProgramDataRootPath,
            settings.LogDirectoryPath,
            Environment.OSVersion.VersionString,
            Assembly.GetEntryAssembly()?.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?.FrameworkName ?? "unknown",
            Environment.Is64BitProcess,
            Environment.MachineName,
            Environment.UserName,
            string.Join(' ', e.Args));
    }

    private void ShowFatalError(string message, string title)
    {
        if (_fatalErrorShown)
        {
            return;
        }

        _fatalErrorShown = true;

        if (Current?.Dispatcher is { HasShutdownStarted: false })
        {
            Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });

            return;
        }

        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void InitializeLogging(AppSettings settings)
    {
        if (Interlocked.Exchange(ref _loggingInitialized, 1) == 1)
        {
            return;
        }

        Directory.CreateDirectory(settings.LogDirectoryPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(settings.LogDirectoryPath, "winclonepro-log-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();

        Log.Information("Serilog initialized. LogDirectory={LogDirectory}", settings.LogDirectoryPath);
    }

    private static void TryLogStartupException(Exception ex, string message)
    {
        try
        {
            Log.Error(ex, message);
        }
        catch
        {
            // Ignore logging failures while showing the startup fallback dialog.
        }
    }

    private static void ShowStartupMessage(string message, string? logDirectoryPath)
    {
        var logPath = string.IsNullOrWhiteSpace(logDirectoryPath) ? "the WinClone Pro log folder" : logDirectoryPath;
        WinFormsMessageBox.Show(
            $"{message}{Environment.NewLine}{Environment.NewLine}Log location: {logPath}",
            StartupFailureTitle,
            WinFormsMessageBoxButtons.OK,
            WinFormsMessageBoxIcon.Error);
    }

    private static void TryShutdownPartialApplication()
    {
        try
        {
            Current?.Dispatcher.InvokeShutdown();
        }
        catch
        {
            // Ignore failures while unwinding partial startup.
        }
    }
}
