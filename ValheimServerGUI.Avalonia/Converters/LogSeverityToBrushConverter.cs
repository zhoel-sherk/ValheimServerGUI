using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ValheimServerGUI.Game;

namespace ValheimServerGUI.Avalonia.Converters
{
    /// <summary>
    /// Colours a log line by how important it is: red for refused connections and application
    /// errors, amber for deaths, green for joins, cyan for noteworthy server events.
    /// </summary>
    public class LogSeverityToBrushConverter : IValueConverter
    {
        private static readonly IBrush Normal = new SolidColorBrush(Color.Parse("#E2E8F0"));
        private static readonly IBrush Info = new SolidColorBrush(Color.Parse("#06B6D4"));
        private static readonly IBrush Success = new SolidColorBrush(Color.Parse("#22C55E"));
        private static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#F59E0B"));
        private static readonly IBrush Error = new SolidColorBrush(Color.Parse("#EF4444"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                LogSeverity.Error => Error,
                LogSeverity.Warning => Warning,
                LogSeverity.Success => Success,
                LogSeverity.Info => Info,
                _ => Normal,
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}