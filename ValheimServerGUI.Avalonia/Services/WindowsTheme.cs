using Avalonia.Controls;
using System;
using System.Runtime.InteropServices;

namespace ValheimServerGUI.Avalonia.Services
{
    /// <summary>
    /// Small Windows-only helpers for the native window chrome. No-ops elsewhere.
    /// </summary>
    internal static class WindowsTheme
    {
        // DWMWA_USE_IMMERSIVE_DARK_MODE is 20 on Windows 10 build 18985+/11, 19 on older builds.
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeLegacy = 19;

        public static void ApplyDarkTitleBar(Window window)
        {
            if (!OperatingSystem.IsWindows()) return;

            try
            {
                var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (handle == IntPtr.Zero) return;

                var enabled = 1;
                if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeLegacy, ref enabled, sizeof(int));
                }
            }
            catch
            {
                // Cosmetic only; never let it affect the app
            }
        }

        [DllImport("dwmapi.dll", SetLastError = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
