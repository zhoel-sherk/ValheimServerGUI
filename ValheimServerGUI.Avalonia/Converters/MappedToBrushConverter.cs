using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ValheimServerGUI.Avalonia.Converters
{
    /// <summary>
    /// Maps a boolean "is mapped" flag to the palette: cyan when true, muted grey when false.
    /// </summary>
    public class MappedToBrushConverter : IValueConverter
    {
        private static readonly IBrush Mapped = new SolidColorBrush(Color.Parse("#06B6D4"));
        private static readonly IBrush NotMapped = new SolidColorBrush(Color.Parse("#94A3B8"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? Mapped : NotMapped;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
