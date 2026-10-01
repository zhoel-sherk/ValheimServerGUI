using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ValheimServerGUI.Game;

namespace ValheimServerGUI.Avalonia.Converters
{
    /// <summary>
    /// Colours a player name by live status: green while online, amber while joining or leaving,
    /// muted for offline. Makes the currently connected players pop out of the cached list.
    /// </summary>
    public class PlayerStatusToBrushConverter : IValueConverter
    {
        private static readonly IBrush Online = new SolidColorBrush(Color.Parse("#22C55E"));
        private static readonly IBrush Transitional = new SolidColorBrush(Color.Parse("#F59E0B"));
        private static readonly IBrush Offline = new SolidColorBrush(Color.Parse("#64748B"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                PlayerStatus.Online => Online,
                PlayerStatus.Joining => Transitional,
                PlayerStatus.Leaving => Transitional,
                _ => Offline,
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}