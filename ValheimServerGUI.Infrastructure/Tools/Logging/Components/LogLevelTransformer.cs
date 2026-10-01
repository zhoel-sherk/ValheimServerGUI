using Serilog.Events;
using System.Collections.Generic;

namespace ValheimServerGUI.Tools.Logging.Components
{
    public class LogLevelTransformer : LogTransformer
    {
        // Public so anything that needs to recognise a rendered line (e.g. the log severity
        // classifier) reads the same literals instead of duplicating them.
        public const string VerbosePrefix = "[VER] ";
        public const string DebugPrefix = "[DBG] ";
        public const string InformationPrefix = "";
        public const string WarningPrefix = "[WRN] ";
        public const string ErrorPrefix = "[ERR] ";
        public const string FatalPrefix = "[FAT] ";

        private static readonly Dictionary<LogEventLevel, string> LevelPrefixes = new()
        {
            { LogEventLevel.Verbose, VerbosePrefix },
            { LogEventLevel.Debug, DebugPrefix },
            { LogEventLevel.Information, InformationPrefix },
            { LogEventLevel.Warning, WarningPrefix },
            { LogEventLevel.Error, ErrorPrefix },
            { LogEventLevel.Fatal, FatalPrefix },
        };

        public static readonly LogLevelTransformer Default = new();

        public override string Transform(LogEvent logEvent, string renderedMessage)
        {
            return $"{LevelPrefixes[logEvent.Level]}{renderedMessage}";
        }
    }
}
