using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ValheimServerGUI.Avalonia.ViewModels;

namespace ValheimServerGUI.Avalonia.Converters
{
    /// <summary>
    /// Maps a <see cref="PortMappingRowState"/> to the palette: green when the port is forwarded,
    /// amber when it is known to be missing, muted grey when the gateway could not be read.
    /// </summary>
    public class PortStateToBrushConverter : IValueConverter
    {
        private static readonly IBrush Mapped = new SolidColorBrush(Color.Parse("#22C55E"));
        private static readonly IBrush NotMapped = new SolidColorBrush(Color.Parse("#F59E0B"));
        private static readonly IBrush Unknown = new SolidColorBrush(Color.Parse("#94A3B8"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                PortMappingRowState.Mapped => Mapped,
                PortMappingRowState.NotMapped => NotMapped,
                _ => Unknown,
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
