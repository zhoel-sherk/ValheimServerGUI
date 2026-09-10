using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ValheimServerGUI.Core.Processes;

namespace ValheimServerGUI.Tools.Processes
{
    /// <summary>
    /// Local implementation of <see cref="IServerProcess"/> backed by
    /// <see cref="System.Diagnostics.Process"/>. stdout/stderr are read as UTF-8 and the
    /// process runs from its own directory so that mods and Doorstop resolve correct paths.
    /// </summary>
    public class LocalServerProcess : IServerProcess
    {
        private readonly Process Process;

        public LocalServerProcess(ServerProcessSpec spec)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = spec.ExecutablePath,
                Arguments = spec.Arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,

                // The Valheim server writes its console output in UTF-8,
                // which is required for non-ASCII character names
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            // Run the process from its own directory so that anything reading the
            // current directory (mods, Doorstop) resolves the correct paths.
            startInfo.WorkingDirectory = spec.WorkingDirectory ?? Path.GetDirectoryName(spec.ExecutablePath);

            foreach (var (key, value) in spec.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables[key] = value;
            }

            Process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = startInfo,
            };

            Process.OutputDataReceived += (_, e) => OutputDataReceived?.Invoke(e.Data);
            Process.ErrorDataReceived += (_, e) => ErrorDataReceived?.Invoke(e.Data);
            Process.Exited += (_, _) => Exited?.Invoke();
        }

        public event Action<string> OutputDataReceived;

        public event Action<string> ErrorDataReceived;

        public event Action Exited;

        public void Start()
        {
            Process.Start();
            Process.BeginOutputReadLine();
            Process.BeginErrorReadLine();
        }

        public void Stop()
        {
            // todo: Validate that taskkill exists on the system and that the user can access it
            if (Process.HasExited) return;

            var killProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/pid {Process.Id}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                },
            };

            killProcess.Start();
        }

        public void Dispose()
        {
            Process.Dispose();
        }
    }

    /// <summary>
    /// Local implementation of <see cref="IServerProcessFactory"/>.
    /// </summary>
    public class LocalServerProcessFactory : IServerProcessFactory
    {
        public IServerProcess Create(ServerProcessSpec spec)
        {
            return new LocalServerProcess(spec);
        }
    }
}