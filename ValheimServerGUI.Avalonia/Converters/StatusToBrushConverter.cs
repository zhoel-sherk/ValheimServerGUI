using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ValheimServerGUI.Game;

namespace ValheimServerGUI.Avalonia.Converters
{
    /// <summary>
    /// Maps a server status name to the Valheim "Mistlands Tech" palette: cyan for Running,
    /// amber for transitional states, muted grey for Stopped.
    /// </summary>
    public class StatusToBrushConverter : IValueConverter
    {
        private static readonly IBrush Success = new SolidColorBrush(Color.Parse("#06B6D4"));
        private static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#F59E0B"));
        private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#94A3B8"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                nameof(ServerStatus.Running) => Success,
                nameof(ServerStatus.Starting) => Warning,
                nameof(ServerStatus.Stopping) => Warning,
                _ => Muted,
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
