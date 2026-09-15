namespace ValheimServerGUI.Core.Platform
{
    /// <summary>
    /// Platform integration contract: opening folders/URLs with the desktop shell and
    /// registering the app to start with the OS. The local Windows implementation uses
    /// <c>explorer.exe</c>/registry; an Avalonia or Linux implementation will provide its own.
    /// </summary>
    public interface IPlatformIntegration
    {
        /// <summary>
        /// Opens a directory (or the containing folder of a file) in the OS file explorer.
        /// </summary>
        void OpenDirectory(string path);

        /// <summary>
        /// Opens a file with the OS default application (e.g. a mod config file in its editor).
        /// </summary>
        void OpenFile(string path);

        /// <summary>
        /// Opens a URL in the default web browser.
        /// </summary>
        void OpenWebAddress(string url);
    }
}