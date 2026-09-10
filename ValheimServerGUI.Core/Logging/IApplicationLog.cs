using System;

namespace ValheimServerGUI.Core.Logging
{
    /// <summary>
    /// Minimal application-level logging contract used by the domain layer. Mirrors the subset
    /// of Serilog <c>ILogger</c> used by the server, without depending on Serilog itself.
    /// </summary>
    public interface IApplicationLog
    {
        void Information(string messageTemplate, params object[] propertyValues);

        void Warning(string messageTemplate, params object[] propertyValues);

        void Error(string messageTemplate, params object[] propertyValues);

        void Error(Exception exception, string messageTemplate, params object[] propertyValues);
    }
}