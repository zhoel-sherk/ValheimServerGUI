using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using System.Threading.Tasks;
using ValheimServerGUI.Avalonia.Services;
using ValheimServerGUI.Avalonia.Views;
using ValheimServerGUI.Core.Logging;
using ValheimServerGUI.Core.Network;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Core.Processes;
using ValheimServerGUI.Game;
using ValheimServerGUI.Game.Mods;
using ValheimServerGUI.Infrastructure.Diagnostics;
using ValheimServerGUI.Infrastructure.DependencyInjection;
using ValheimServerGUI.Infrastructure.Network;
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
                try
                {
                    _serviceProvider = AppServices.BuildServiceProvider();

                    RegisterUnhandledExceptionHandlers();

                    // Instantiate the Discord notification service so it starts listening to server events.
                    _serviceProvider.GetRequiredService<Services.DiscordStatusService>();

                    var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                    desktop.MainWindow = mainWindow;

                    InitializeTray(desktop, mainWindow);

                    // Kick off the initial async load (IP addresses, player cache) once the window is shown.
                    mainWindow.Opened += (_, _) =>
                        _ = _serviceProvider.GetRequiredService<ViewModels.ShellViewModel>().LoadCommand.ExecuteAsync(null);
                }
                catch (Exception e)
                {
                    _serviceProvider?.GetService<IExceptionHandler>()?.HandleException(e, "Fatal startup error");
                    throw;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        /// <summary>
        /// Adds a system tray icon so closing the window hides it instead of stopping a running
        /// server. Best-effort: if the platform has no tray, the window simply closes normally.
        /// </summary>
        private void InitializeTray(IClassicDesktopStyleApplicationLifetime desktop, MainWindow mainWindow)
        {
            try
            {
                var shell = _serviceProvider!.GetRequiredService<ViewModels.ShellViewModel>();
                var server = _serviceProvider!.GetRequiredService<ValheimServer>();

                var trayIcon = new TrayIcon
                {
                    ToolTipText = "Valheim Server GUI",
                    Icon = LoadTrayIcon(),
                    IsVisible = true,
                };

                var showItem = new NativeMenuItem("Show window");
                showItem.Click += (_, _) => ShowMainWindow(mainWindow);

                var startItem = new NativeMenuItem("Start server");
                startItem.Click += (_, _) => shell.ServerControls.StartCommand.Execute(null);

                var stopItem = new NativeMenuItem("Stop server");
                stopItem.Click += (_, _) => shell.ServerControls.StopCommand.Execute(null);

                var exitItem = new NativeMenuItem("Exit");
                exitItem.Click += (_, _) =>
                {
                    try
                    {
                        // Stop the server before exiting so the world is saved.
                        if (server.IsAnyStatus(ServerStatus.Running, ServerStatus.Starting)) server.Stop();
                    }
                    finally
                    {
                        mainWindow.AllowClose = true;
                        desktop.Shutdown();
                    }
                };

                trayIcon.Menu = new NativeMenu
                {
                    Items = { showItem, startItem, stopItem, exitItem },
                };

                trayIcon.Clicked += (_, _) => ShowMainWindow(mainWindow);

                TrayIcon.SetIcons(this, new TrayIcons { trayIcon });

                mainWindow.TrayEnabled = true;
            }
            catch (Exception e)
            {
                _serviceProvider?.GetService<IExceptionHandler>()?.HandleException(e, "Failed to create the tray icon");
            }
        }

        private static WindowIcon? LoadTrayIcon()
        {
            try
            {
                using var stream = AssetLoader.Open(new Uri("avares://ValheimServerGUI.Avalonia/Assets/ApplicationIcon.ico"));
                return new WindowIcon(stream);
            }
            catch
            {
                return null;
            }
        }

        private static void ShowMainWindow(MainWindow mainWindow)
        {
            try
            {
                mainWindow.Show();
                if (mainWindow.WindowState == WindowState.Minimized) mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            }
            catch
            {
                // Bringing the window back is best-effort
            }
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
    /// Composition root for the Avalonia client: the shared platform-neutral services plus the
    /// Avalonia-specific UI/platform implementations and views.
    /// </summary>
    public static class AppServices
    {
        public static ServiceProvider BuildServiceProvider(string[]? args = null)
        {
            var services = new ServiceCollection();

            // Shared platform-neutral services (Infrastructure + Tools + Game)
            services.AddValheimServerServices(args);

            // Avalonia-specific
            services
                .AddSingleton<IExceptionHandler, AvaloniaExceptionHandler>()
                .AddSingleton<IUserInteraction, AvaloniaUserInteraction>()
                .AddSingleton<Services.DiscordStatusService>();

            // Views / ViewModels
            services
                .AddSingleton<ViewModels.ServerControlsViewModel>()
                .AddSingleton<ViewModels.PlayersViewModel>()
                .AddSingleton<ViewModels.LogsViewModel>()
                .AddSingleton<ViewModels.ModsViewModel>()
                .AddSingleton<ViewModels.PreferencesViewModel>()
                .AddSingleton<ViewModels.AboutViewModel>()
                .AddTransient<ViewModels.DiscordSettingsViewModel>()
                .AddTransient<ViewModels.PortForwardingViewModel>()
                .AddTransient<ViewModels.WorldSettingsViewModel>()
                .AddSingleton<ViewModels.ShellViewModel>()
                .AddSingleton<MainWindow>()
                .AddTransient<PreferencesWindow>()
                .AddTransient<AboutWindow>()
                .AddTransient<DiscordSettingsWindow>()
                .AddTransient<PortForwardingWindow>()
                .AddTransient<WorldSettingsWindow>();

            return services.BuildServiceProvider();
        }
    }
}