using Microsoft.Win32;
using System;
using System.Security;
using ValheimServerGUI.Tools.Logging;

// Registry APIs are Windows-only; every entry point is guarded by OperatingSystem.IsWindows().
#pragma warning disable CA1416

namespace ValheimServerGUI.Tools
{
    public interface IStartupHelper
    {
        /// <summary>
        /// Enables or disables running the app on Windows startup. Returns true when the
        /// registry already matched the requested state or was updated successfully.
        /// </summary>
        bool ApplyStartupSetting(bool enabled, string appName, string executablePath);
    }

    /// <summary>
    /// Manages the Windows "Run" registry value so the app can start with Windows. Tries
    /// HKEY_LOCAL_MACHINE first (all users), falling back to HKEY_CURRENT_USER. No-ops on
    /// non-Windows platforms.
    /// </summary>
    public class StartupHelper : IStartupHelper
    {
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        private readonly IApplicationLogger Logger;

        public StartupHelper(IApplicationLogger logger)
        {
            Logger = logger;
        }

        public bool ApplyStartupSetting(bool enabled, string appName, string executablePath)
        {
            if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(appName)) return false;

            try
            {
                var currentPath = GetStartupPath(appName);

                if (enabled)
                {
                    if (string.Equals(currentPath, executablePath, StringComparison.OrdinalIgnoreCase)) return true;

                    SetStartupPath(appName, executablePath);
                    Logger.Information("{app} will now run on Windows startup", appName);
                    return true;
                }

                if (currentPath == null) return true;

                RemoveStartupPath(appName);
                Logger.Information("{app} will no longer run on Windows startup", appName);
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply the Windows startup setting");
                return false;
            }
        }

        private static string GetStartupPath(string appName)
        {
            return ReadValue(Registry.LocalMachine, appName) ?? ReadValue(Registry.CurrentUser, appName);
        }

        private static string ReadValue(RegistryKey hive, string appName)
        {
            try
            {
                using var key = hive.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(appName)?.ToString();
            }
            catch (SecurityException)
            {
                return null;
            }
        }

        private static void SetStartupPath(string appName, string executablePath)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(RunKeyPath, writable: true);
                key?.SetValue(appName, executablePath);
                if (key != null) return;
            }
            catch (SecurityException)
            {
                // Fall through to the current-user hive
            }

            using var currentUser = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            currentUser.SetValue(appName, executablePath);
        }

        private static void RemoveStartupPath(string appName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(RunKeyPath, writable: true);
                key?.DeleteValue(appName, throwOnMissingValue: false);
            }
            catch (SecurityException)
            {
                // Fall through to the current-user hive
            }

            using var currentUser = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            currentUser?.DeleteValue(appName, throwOnMissingValue: false);
        }
    }
}
