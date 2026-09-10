using System.Diagnostics;
using System.IO;
using System.Text;

namespace ValheimServerGUI.Tools.Processes
{
    public static class ProcessExtensions
    {
        // todo: Validate that taskkill exists on the system and that the user can access it
        private const string KillCommand = "taskkill";

        public static Process AddBackgroundProcess(this IProcessProvider provider, string key, string command, string args)
        {
            var process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo =
                {
                    FileName = command,
                    Arguments = args,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,

                    // The Valheim server writes its console output in UTF-8,
                    // which is required for non-ASCII character names
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                },
            };

            // Run the process from its own directory so that anything reading the
            // current directory (mods, Doorstop) resolves the correct paths.
            var workingDirectory = Path.GetDirectoryName(command);
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                process.StartInfo.WorkingDirectory = workingDirectory;
            }

            provider.AddProcess(key, process);

            return provider.GetProcess(key);
        }

        /// <summary>
        /// Starts a secondary process to safely kill the provided process.
        /// Returns the provided process.
        /// </summary>
        public static Process SafelyKillProcess(this IProcessProvider provider, Process process)
        {
            if (process != null)
            {
                var killProcess = provider.AddBackgroundProcess($"{KillCommand}-{process.Id}", KillCommand, $"/pid {process.Id}");

                // todo: Send output to application logs
                provider.StartIO(killProcess);
            }

            return process;
        }

        /// <summary>
        /// Starts a secondary process to safely kill the process with the specified key.
        /// Returns the process with the specified key, which you can then wait for to exit.
        /// </summary>
        public static Process SafelyKillProcess(this IProcessProvider provider, string key)
        {
            return provider.SafelyKillProcess(provider.GetProcess(key));
        }

        public static bool IsProcessRunning(this IProcessProvider provider, string key)
        {
            var process = provider.GetProcess(key);

            if (process == null) return false;

            return !process.HasExited;
        }
    }
}
