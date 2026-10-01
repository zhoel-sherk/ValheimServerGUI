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

            // A cancelled operation is benign: it reaches the global handler on teardown or on a
            // request timeout, not from a real fault. Reporting it as an error (with a stack trace)
            // is misleading, so log it plainly and stop there.
            if (e is OperationCanceledException)
            {
                TryLog(() => Logger.Information("Operation cancelled ({context}): {message}", contextMessage, e.Message));
                return;
            }

            TryLog(() => Logger.Error(e, contextMessage));

            ExceptionHandled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Logging must never throw on the exception path - a failure here would replace the real
        /// exception with a confusing one.
        /// </summary>
        private static void TryLog(Action log)
        {
            try
            {
                log();
            }
            catch
            {
                // Intentionally swallowed: we are already handling a fault.
            }
        }
    }
}