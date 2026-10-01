namespace ValheimServerGUI.Game
{
    /// <summary>
    /// A single line shown in the Logs tab. Carries the severity alongside the text so the UI can
    /// colour it without re-parsing the line on every redraw.
    /// </summary>
    public sealed class LogEntry
    {
        public LogEntry(string text, LogSeverity severity)
        {
            Text = text ?? string.Empty;
            Severity = severity;
        }

        public string Text { get; }

        public LogSeverity Severity { get; }

        /// <summary>Renders back to the raw line, so saving the log produces a plain text file.</summary>
        public override string ToString() => Text;
    }
}