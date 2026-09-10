using ValheimServerGUI.Core.Logging;
using ValheimServerGUI.Game;

namespace ValheimServerGUI.Tools.Logging
{
    /// <summary>
    /// Creates a <see cref="ValheimServerLogger"/> for a server run.
    /// </summary>
    public class ValheimServerLoggerFactory : IServerLoggerFactory
    {
        public IServerLogger Create(IValheimServerOptions options)
        {
            return new ValheimServerLogger(options);
        }
    }
}