using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace ValheimServerGUI.Avalonia.Converters
{
    /// <summary>
    /// Inverts a boolean value for bindings.
    /// </summary>
    public class NegateBoolConverter : IValueConverter
    {
        public static readonly NegateBoolConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b ? !b : value;

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b ? !b : value;
    }
}