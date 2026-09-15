using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ValheimServerGUI.Avalonia.Views
{
    public partial class DiscordSettingsWindow : Window
    {
        public DiscordSettingsWindow()
        {
            InitializeComponent();
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
