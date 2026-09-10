using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Threading.Tasks;
using ValheimServerGUI.Avalonia.Services;
using ValheimServerGUI.Avalonia.Views;
using ValheimServerGUI.Core.Logging;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Core.Processes;
using ValheimServerGUI.Game;
using ValheimServerGUI.Game.Mods;
using ValheimServerGUI.Infrastructure.Diagnostics;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Data;
using ValheimServerGUI.Tools.Http;
using ValheimServerGUI.Tools.Logging;
using ValheimServerGUI.Tools.Processes;

namespace ValheimServerGUI.Avalonia
{
    public partial class App : Application
    {
        private ServiceProvider? _serviceProvider;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _serviceProvider = AppServices.BuildServiceProvider();

                RegisterUnhandledExceptionHandlers();

                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                desktop.MainWindow = mainWindow;

                // Kick off the initial async load (IP addresses, player cache) once the window is shown.
                mainWindow.Opened += (_, _) =>
                    _ = _serviceProvider.GetRequiredService<ViewModels.ShellViewModel>().LoadCommand.ExecuteAsync(null);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void RegisterUnhandledExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                HandleException(e.ExceptionObject as Exception, "Unhandled AppDomain Exception");

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                HandleException(e.Exception, "Unobserved Task Exception");
                e.SetObserved();
            };

            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                HandleException(e.Exception, "Unhandled UI Thread Exception");
                e.Handled = true;
            };
        }

        private void HandleException(Exception? e, string context)
        {
            if (e == null) return;

            try
            {
                _serviceProvider?.GetService<IExceptionHandler>()?.HandleException(e, context);
            }
            catch
            {
                // Logging must never crash the app
            }
        }
    }

    /// <summary>
    /// Composition root for the Avalonia client. Mirrors the WinForms registrations minus the
    /// WinForms-only services (forms, IFormProvider).
    /// </summary>
    public static class AppServices
    {
        public static ServiceProvider BuildServiceProvider(string[]? args = null)
        {
            var startupArgsProvider = new StartupArgsProvider(args ?? Array.Empty<string>());

            var services = new ServiceCollection();

            // Tools / Infrastructure
            services
                .AddSingleton<IDataFileRepositoryContext, DataFileRepositoryContext>()
                .AddSingleton<IFileProvider, JsonFileProvider>()
                .AddSingleton<IServerProcessFactory, LocalServerProcessFactory>()
                .AddSingleton<ApplicationLogger>()
                .AddSingleton<ILogger>(sp => sp.GetRequiredService<ApplicationLogger>())
                .AddSingleton<IApplicationLogger>(sp => sp.GetRequiredService<ApplicationLogger>())
                .AddSingleton<IApplicationLog>(sp => sp.GetRequiredService<ApplicationLogger>())
                .AddSingleton<IServerLoggerFactory, ValheimServerLoggerFactory>()
                .AddSingleton<IHttpClientProvider, HttpClientProvider>()
                .AddSingleton<IRestClientContext, RestClientContext>()
                .AddSingleton<IIpAddressProvider, IpAddressProvider>()
                .AddSingleton<IGitHubClient, GitHubClient>()
                .AddSingleton<ISoftwareUpdateProvider, SoftwareUpdateProvider>()
                .AddSingleton<IExceptionHandler, AvaloniaExceptionHandler>()
                .AddSingleton<IUserInteraction, AvaloniaUserInteraction>()
                .AddSingleton<IPlatformIntegration, WindowsPlatformIntegration>()
                .AddSingleton<IRuneberryApiClient, RuneberryApiClient>();

            // Mods & backups
            services
                .AddSingleton<IModSourceClient, ModSourceClient>()
                .AddSingleton<IBepInExManager, BepInExManager>()
                .AddSingleton<IValheimPlusManager, ValheimPlusManager>()
                .AddSingleton<IBackupService, BackupService>();

            // Game & server data
            services
                .AddSingleton<IPlayerDataRepository, PlayerDataRepository>()
                .AddSingleton<IUserPreferencesProvider, UserPreferencesProvider>()
                .AddSingleton<IServerPreferencesProvider, ServerPreferencesProvider>()
                .AddSingleton<IWorldPreferencesProvider, WorldPreferencesProvider>()
                .AddSingleton<IStartupArgsProvider>(startupArgsProvider)
                .AddSingleton<ValheimServer>();

            // Views / ViewModels
            services
                .AddSingleton<ViewModels.ServerControlsViewModel>()
                .AddSingleton<ViewModels.PlayersViewModel>()
                .AddSingleton<ViewModels.LogsViewModel>()
                .AddSingleton<ViewModels.ModsViewModel>()
                .AddSingleton<ViewModels.PreferencesViewModel>()
                .AddSingleton<ViewModels.AboutViewModel>()
                .AddSingleton<ViewModels.ShellViewModel>()
                .AddSingleton<MainWindow>()
                .AddTransient<PreferencesWindow>()
                .AddTransient<AboutWindow>();

            return services.BuildServiceProvider();
        }
    }
}