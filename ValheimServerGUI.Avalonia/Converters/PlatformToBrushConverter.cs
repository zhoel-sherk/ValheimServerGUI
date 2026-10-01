using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ValheimServerGUI.Avalonia.Converters
{
    /// <summary>
    /// Background colour for the platform badge in the Players list. Colours come from the theme
    /// (<c>VsgPlatform*Brush</c>) so the palette stays in one place. Platforms we don't recognise
    /// get a neutral colour, since crossplay can report any store.
    /// </summary>
    public class PlatformToBrushConverter : IValueConverter
    {
        private const string SteamResource = "VsgPlatformSteamBrush";
        private const string XboxResource = "VsgPlatformXboxBrush";
        private const string PlayStationResource = "VsgPlatformPlayStationBrush";
        private const string NintendoResource = "VsgPlatformNintendoBrush";
        private const string OtherResource = "VsgPlatformOtherBrush";

        // Fallbacks mirror App.axaml so a lookup failure during startup is not a hard error.
        private static readonly IBrush FallbackOther = new SolidColorBrush(Color.Parse("#3F3A56"));

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var key = value?.ToString()?.Trim().ToUpperInvariant() switch
            {
                "STEAM" => SteamResource,
                "XBOX" => XboxResource,
                "PLAYSTATION" => PlayStationResource,
                "NINTENDO" => NintendoResource,
                _ => OtherResource,
            };

            var app = Application.Current;
            if (app != null &&
                app.TryGetResource(key, app.RequestedThemeVariant, out var found) &&
                found is IBrush brush)
            {
                return brush;
            }

            return FallbackOther;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}