using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System;
using ValheimServerGUI.Core.Logging;
using ValheimServerGUI.Game;
using ValheimServerGUI.Tools.Logging.Components;

namespace ValheimServerGUI.Tools.Logging
{
    public interface IApplicationLogger : IBaseLogger
    {
    }

public class ApplicationLogger : BaseLogger, IApplicationLogger, IApplicationLog
{
    private readonly IServiceProvider ServiceProvider;
    private IUserPreferencesProvider UserPrefsProvider;

    public ApplicationLogger(IServiceProvider services)
    {
        // Dependencies are injected late to avoid creating a circular dependency
        ServiceProvider = services;

        AddRule(LogLevelTransformer.Default);
        AddRule(TimestampTransformer.Default);
    }

    #region IApplicationLog implementation

    public void Information(string messageTemplate, params object[] propertyValues)
    {
        ((ILogger)this).Write(Serilog.Events.LogEventLevel.Information, messageTemplate, propertyValues);
    }

    public void Warning(string messageTemplate, params object[] propertyValues)
    {
        ((ILogger)this).Write(Serilog.Events.LogEventLevel.Warning, messageTemplate, propertyValues);
    }

    public void Error(string messageTemplate, params object[] propertyValues)
    {
        ((ILogger)this).Write(Serilog.Events.LogEventLevel.Error, messageTemplate, propertyValues);
    }

    public void Error(Exception exception, string messageTemplate, params object[] propertyValues)
    {
        ((ILogger)this).Write(Serilog.Events.LogEventLevel.Error, exception, messageTemplate, propertyValues);
    }

    #endregion

        private void OnUserPreferencesSaved(object sender, UserPreferences prefs)
        {
            RebuildLogger();
        }

        #region BaseLogger overrides

        protected override void ConfigureLogger(LoggerConfiguration config)
        {
            if (UserPrefsProvider == null)
            {
                UserPrefsProvider = ServiceProvider.GetRequiredService<IUserPreferencesProvider>();
                UserPrefsProvider.PreferencesSaved += OnUserPreferencesSaved;
            }

            var prefs = UserPrefsProvider.LoadPreferences();
            if (prefs.WriteApplicationLogsToFile) AddFileLogging(config, "ApplicationLogs");
        }

        #endregion
    }
}
