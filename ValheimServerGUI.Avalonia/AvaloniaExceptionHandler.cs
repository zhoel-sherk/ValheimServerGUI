using System;
using ValheimServerGUI.Infrastructure.Diagnostics;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia
{
    /// <summary>
    /// Exception handler for the Avalonia client: logs the exception and any pending log buffer.
    /// Crash-report prompting is not implemented yet (Phase 2, dialogs).
    /// </summary>
    public class AvaloniaExceptionHandler : IExceptionHandler
    {
        private readonly IApplicationLogger Logger;

        public AvaloniaExceptionHandler(IApplicationLogger logger)
        {
            Logger = logger;
        }

        public event EventHandler? ExceptionHandled;

        public void HandleException(Exception e, string? contextMessage = null)
        {
            if (e == null) return;

            contextMessage ??= "Unknown Exception";
            Logger.Error(e, contextMessage);

            ExceptionHandled?.Invoke(this, EventArgs.Empty);
        }
    }
}