using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ValheimServerGUI.Game
{
    /// <summary>
    /// Parses Valheim server stdout lines and dispatches matches to registered handlers.
    /// The regex patterns are applied to the timestamp-prefixed message and must not
    /// anchor with '^' unless they account for the prefix.
    /// </summary>
    public class ServerLogParser
    {
        public delegate void LogMatchHandler(string[] captures);

        private readonly Dictionary<string, LogMatchHandler> Actions = new();

        public void AddAction(string pattern, LogMatchHandler handler)
        {
            Actions[pattern] = handler;
        }

        public void HandleMessage(string message, Action<Exception, string> onError = null)
        {
            foreach (var kvp in Actions)
            {
                var match = Regex.Match(message, kvp.Key, RegexOptions.IgnoreCase);
                if (!match.Success) continue;

                try
                {
                    // The first capture group is the whole string, so skip that
                    var captures = (match.Groups as IEnumerable<Group>).Skip(1).Select(g => g.ToString()).ToArray();
                    kvp.Value(captures);
                }
                catch (Exception e)
                {
                    onError?.Invoke(e, message);
                }
            }
        }
    }
}