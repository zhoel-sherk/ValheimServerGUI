namespace ValheimServerGUI.Game
{
    /// <summary>
    /// How important a log line is, used to highlight it in the UI. Assigned by the log
    /// classifier from the line contents; not a Serilog level (server stdout has none).
    /// </summary>
    public enum LogSeverity
    {
        /// <summary>Routine chatter: chunk loading, ZDO bookkeeping, connection stats.</summary>
        Normal = 0,

        /// <summary>Worth noticing but not a player event: world saved, join code issued.</summary>
        Info = 1,

        /// <summary>Something good happened: server connected, a player joined.</summary>
        Success = 2,

        /// <summary>Something went wrong for a player: death, failed connection attempt.</summary>
        Warning = 3,

        /// <summary>A connection was refused: wrong password, incompatible version, app error.</summary>
        Error = 4,
    }
}