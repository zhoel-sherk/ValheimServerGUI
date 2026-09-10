using System;
using System.Collections.Generic;

namespace ValheimServerGUI.Core.Logging
{
    /// <summary>
    /// Minimal logging contract for the Valheim server process. Used by the domain layer so
    /// it does not depend on a specific logging implementation.
    /// </summary>
    public interface IServerLogger
    {
        event Action<string> LogReceived;

        IEnumerable<string> LogBuffer { get; }

        void Information(string message);

        void Error(string message);
    }
}