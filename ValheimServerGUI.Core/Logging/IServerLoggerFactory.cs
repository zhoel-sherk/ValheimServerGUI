using ValheimServerGUI.Game;

namespace ValheimServerGUI.Core.Logging
{
    /// <summary>
    /// Creates a <see cref="IServerLogger"/> for a specific server run. The logger is created
    /// per-start because its configuration (file logging, filter) depends on the options.
    /// </summary>
    public interface IServerLoggerFactory
    {
        IServerLogger Create(IValheimServerOptions options);
    }
}