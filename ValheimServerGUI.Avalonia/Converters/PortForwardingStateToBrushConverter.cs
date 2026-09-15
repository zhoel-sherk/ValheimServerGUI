using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ValheimServerGUI.Core.Network;

namespace ValheimServerGUI.Avalonia.Converters
{
    /// <summary>
    /// Maps an overall <see cref="PortForwardingState"/> to the palette: green when every port is
    /// forwarded, amber when some or none are, red when no gateway was found, grey when unknown.
    /// </summary>
    public class PortForwardingStateToBrushConverter : IValueConverter
    {
        private static readonly IBrush Ok = new SolidColorBrush(Color.Parse("#22C55E"));
        private static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#F59E0B"));
        private static readonly IBrush Danger = new SolidColorBrush(Color.Parse("#EF4444"));
        private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#94A3B8"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value switch
            {
                PortForwardingState.AllMapped => Ok,
                PortForwardingState.PartiallyMapped => Warning,
                PortForwardingState.NoneMapped => Warning,
                PortForwardingState.NoGateway => Danger,
                _ => Muted,
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
