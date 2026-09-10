using System;
using System.Diagnostics;
using ValheimServerGUI.Core.Platform;

namespace ValheimServerGUI.Tools
{
    /// <summary>
    /// Windows implementation of <see cref="IPlatformIntegration"/> using explorer.exe.
    /// </summary>
    public class WindowsPlatformIntegration : IPlatformIntegration
    {
        public void OpenDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            path = Environment.ExpandEnvironmentVariables(path);

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

            Process.Start("explorer.exe", path);
        }

        public void OpenWebAddress(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            Process.Start("explorer.exe", url);
        }
    }
}