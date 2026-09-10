using System;
using ValheimServerGUI.Core.Platform;

namespace ValheimServerGUI.Tools
{
    /// <summary>
    /// Convenience facade over <see cref="IPlatformIntegration"/> for legacy static call sites.
    /// </summary>
    public static class OpenHelper
    {
        private static readonly IPlatformIntegration PlatformIntegration = new WindowsPlatformIntegration();

        public static void OpenDirectory(string path)
        {
            PlatformIntegration.OpenDirectory(path);
        }

        public static void OpenWebAddress(string url)
        {
            PlatformIntegration.OpenWebAddress(url);
        }
    }
}