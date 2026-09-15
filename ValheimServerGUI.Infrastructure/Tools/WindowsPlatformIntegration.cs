using System;
using System.Diagnostics;
using ValheimServerGUI.Core.Platform;

namespace ValheimServerGUI.Tools
{
    /// <summary>
    /// Windows implementation of <see cref="IPlatformIntegration"/> using explorer.exe.
    /// Every method is best-effort: a missing file/folder, an invalid path or a missing shell
    /// handler must never crash the app.
    /// </summary>
    public class WindowsPlatformIntegration : IPlatformIntegration
    {
        public void OpenDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                path = Environment.ExpandEnvironmentVariables(path);
            }
            catch
            {
                return;
            }

            try
            {
                // If this is a path to a valid file, open that file's directory
                path = PathExtensions.GetFileInfo(path).Directory.FullName;
            }
            catch
            {
                try
                {
                    // Otherwise, check if this is a path to a valid directory
                    path = PathExtensions.GetDirectoryInfo(path).FullName;
                }
                catch
                {
                    // If neither, do nothing
                    return;
                }
            }

            TryStart("explorer.exe", path);
        }

        public void OpenWebAddress(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            TryStart("explorer.exe", url);
        }

        public void OpenFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                path = Environment.ExpandEnvironmentVariables(path);
            }
            catch
            {
                return;
            }

            // Use the OS shell so the file opens with its registered default application.
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch
            {
                // A missing file or no registered handler should never crash the app
            }
        }

        private static void TryStart(string fileName, string arguments)
        {
            try
            {
                Process.Start(fileName, arguments);
            }
            catch
            {
                // A missing shell handler should never crash the app
            }
        }
    }
}
