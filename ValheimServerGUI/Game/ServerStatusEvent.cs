using System;

namespace ValheimServerGUI.Game
{
    public class ServerStatusEvent
    {
        public ServerStatusEvent(string serverName, ServerStatus status, DateTime? timestamp = null)
        {
            ServerName = serverName;
            ServerStatus = status;
            Timestamp = timestamp ?? DateTime.UtcNow;
        }

        public string ServerName { get; set; }

        public ServerStatus ServerStatus { get; set; }

        public DateTime Timestamp { get; set; }
    }
}
