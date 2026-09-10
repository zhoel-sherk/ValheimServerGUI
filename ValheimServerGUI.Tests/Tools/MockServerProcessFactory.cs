using System;
using System.Collections.Generic;
using System.Threading;
using ValheimServerGUI.Core.Processes;

namespace ValheimServerGUI.Tests.Tools
{
    public class MockServerProcess : IServerProcess
    {
        public ServerProcessSpec Spec { get; }

        public bool IsStarted { get; private set; }

        public bool IsStopped { get; private set; }

        public List<string> OutputLines { get; } = new();

        public List<string> ErrorLines { get; } = new();

        public bool IsDisposed { get; private set; }

        public MockServerProcess(ServerProcessSpec spec)
        {
            Spec = spec;
        }

        public event Action<string> OutputDataReceived;

        public event Action<string> ErrorDataReceived;

        public event Action Exited;

        public void Start()
        {
            IsStarted = true;
        }

        public void Stop()
        {
            IsStopped = true;
        }

        public void SimulateOutput(string data)
        {
            OutputLines.Add(data);
            OutputDataReceived?.Invoke(data);
        }

        public void SimulateError(string data)
        {
            ErrorLines.Add(data);
            ErrorDataReceived?.Invoke(data);
        }

        public void SimulateExit()
        {
            Exited?.Invoke();
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public class MockServerProcessFactory : IServerProcessFactory
    {
        public List<MockServerProcess> CreatedProcesses { get; } = new();

        public IServerProcess Create(ServerProcessSpec spec)
        {
            var process = new MockServerProcess(spec);
            CreatedProcesses.Add(process);
            return process;
        }
    }
}