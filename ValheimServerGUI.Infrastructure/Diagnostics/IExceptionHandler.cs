using System;

namespace ValheimServerGUI.Infrastructure.Diagnostics
{
    /// <summary>
    /// Contract for handling unexpected exceptions. The WinForms client implements this with a
    /// MessageBox-backed crash-report prompt; the Avalonia client will provide a dialog-based
    /// implementation.
    /// </summary>
    public interface IExceptionHandler
    {
        event EventHandler ExceptionHandled;

        void HandleException(Exception e, string contextMessage = null);
    }
}