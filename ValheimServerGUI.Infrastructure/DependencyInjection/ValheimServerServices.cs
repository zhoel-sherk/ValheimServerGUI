using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using ValheimServerGUI.Core.Logging;
using ValheimServerGUI.Core.Network;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Core.Processes;
using ValheimServerGUI.Game;
using ValheimServerGUI.Game.Mods;
using ValheimServerGUI.Infrastructure.Network;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Data;
using ValheimServerGUI.Tools.Http;
using ValheimServerGUI.Tools.Logging;
using ValheimServerGUI.Tools.Processes;

namespace ValheimServerGUI.Infrastructure.DependencyInjection
{
    /// <summary>
    /// Shared composition root for the platform-neutral services used by every client
    /// (Avalonia, tests). Hosts register their own UI/platform implementations
    /// (<c>IUserInteraction</c>, <c>IExceptionHandler</c>, views) on top of this.
    /// </summary>
    public static class ValheimServerServices
    {
        public static IServiceCollection AddValheimServerServices(this IServiceCollection services, string[] args = null)
        {
            // Tools / infrastructure
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
                .AddSingleton<IPlatformIntegration, WindowsPlatformIntegration>()
                .AddSingleton<ISteamCloudWorldProvider, SteamCloudWorldProvider>()
                .AddSingleton<IPortForwarder, UpnpPortForwarder>()
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
                .AddSingleton<IStartupArgsProvider>(new StartupArgsProvider(args ?? Array.Empty<string>()))
                .AddSingleton<ValheimServer>();

            return services;
        }
    }
}
