using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ValheimServerGUI.Tools.Logging
{
    /// <summary>
    /// In-memory stream of the filtered Valheim server stdout, so any client can display it.
    /// The server passes its <c>LogMessageHandler</c> to <see cref="Add"/>; the UI subscribes to
    /// <see cref="LogReceived"/> and replays <see cref="LogBuffer"/>.
    /// </summary>
    public interface IServerLogStream
    {
        event Action<string> LogReceived;

        IEnumerable<string> LogBuffer { get; }

        void Add(string message);

        void Clear();
    }

    public class ServerLogStream : IServerLogStream
    {
        private const int MaxEntries = 2000;

        private readonly ConcurrentQueue<string> Buffer = new();

        public event Action<string> LogReceived;

        public IEnumerable<string> LogBuffer => Buffer.ToArray();

        public void Add(string message)
        {
            if (message == null) return;

            Buffer.Enqueue(message);
            while (Buffer.Count > MaxEntries && Buffer.TryDequeue(out _)) { }

            LogReceived?.Invoke(message);
        }

        public void Clear()
        {
            while (Buffer.TryDequeue(out _)) { }
        }
    }
}
