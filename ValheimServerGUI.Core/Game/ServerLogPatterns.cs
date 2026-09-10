namespace ValheimServerGUI.Game
{
    /// <summary>
    /// Regex patterns used to parse Valheim server stdout. The patterns are matched
    /// against a timestamp-prefixed message, so they must not anchor with '^' unless
    /// they account for the prefix.
    /// </summary>
    public static class ServerLogPatterns
    {
        // NOTE: Newer server builds also log "Game server connected failed" on
        // startup errors, so this pattern must not match that message.
        public const string ServerConnected = @"Game server connected\s*$";

        // NOTE: Older server builds log "World saved (x ms)", newer builds (1.0.x+) log
        // "World save (5/5) done. Total time [x ms]" - the time may include group separators.
        public const string WorldSavedLegacy = @"World saved \(\s*?([[\d\.]+?)\s*?ms\s*?\)\s*?$";
        public const string WorldSaved = @"World save \(5/5\) done\. Total time \[([\d\.,\s]+?)ms\]";

        public const string CrossplayJoinCodeActive = @"Session "".*?"" with join code (.*?) ";
        public const string CrossplayJoinCodeRegistered = @"Session "".*?"" registered with join code (\S+?)\s*$";

        public const string PlayerConnecting = @"Got connection SteamID (\d+?)\D*?$";
        public const string PlayerConnectingCrossplay = @"PlayFab socket with remote ID .*? received local Platform ID (\w+?)_(\d+?)$";

        // NOTE: ZDOID can be a negative number, account for that w/ regex!
        public const string PlayerConnected = @"Got character ZDOID from (.+?) : ([\d-]+?)\D*?:(\d+?)\D*?$";

        public const string PlayerDisconnectingWrongPassword = @"Peer (\d+?) has wrong password";
        public const string PlayerDisconnectingIncompatibleVersion = @"Peer (\d+?) has incompatible version";

        // This is technically "disconnecting" but it's the best terminator I can find.
        public const string PlayerDisconnected = @"Closing socket (\d+?)\D*?$";
        public const string PlayerDisconnectedCrossplay = @"Destroying abandoned non persistent zdo ([\d-]+?):.*$";
    }
}