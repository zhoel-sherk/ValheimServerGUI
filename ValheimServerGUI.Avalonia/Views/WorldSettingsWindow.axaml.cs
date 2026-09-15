using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ValheimServerGUI.Avalonia.Views
{
    public partial class WorldSettingsWindow : Window
    {
        public WorldSettingsWindow()
        {
            InitializeComponent();
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
