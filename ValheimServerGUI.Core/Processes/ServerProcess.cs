using System;
using System.Collections.Generic;

namespace ValheimServerGUI.Core.Processes
{
    /// <summary>
    /// A validated command specification used to start a server process. This is a
    /// platform-neutral description: both the local Windows runner and a future SSH
    /// implementation can consume it without knowing about <see cref="System.Diagnostics"/>.
    /// </summary>
    public class ServerProcessSpec
    {
        public string ExecutablePath { get; set; }

        public string Arguments { get; set; }

        public string WorkingDirectory { get; set; }

        public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Starts a process from a <see cref="ServerProcessSpec"/>. The returned instance
    /// owns the process lifecycle for a single run.
    /// </summary>
    public interface IServerProcessFactory
    {
        IServerProcess Create(ServerProcessSpec spec);
    }

    /// <summary>
    /// A running server process with an event/stream abstraction. This hides the concrete
    /// process API so the same contract can be implemented by a local process or a remote
    /// SSH session.
    /// </summary>
    public interface IServerProcess : IDisposable
    {
        event Action<string> OutputDataReceived;

        event Action<string> ErrorDataReceived;

        event Action Exited;

        void Start();

        void Stop();
    }
}