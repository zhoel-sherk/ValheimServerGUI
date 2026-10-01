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

        private void OnApplyClick(object? sender, RoutedEventArgs e)
        {
            // The ViewModel commits the settings in ApplyCommand. The close result tells the caller
            // the user accepted (Cancel and Esc close without a result).
            Close(true);
        }
    }
}
