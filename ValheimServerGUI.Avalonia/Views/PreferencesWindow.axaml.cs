using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ValheimServerGUI.Avalonia.Views
{
    public partial class PreferencesWindow : Window
    {
        public PreferencesWindow()
        {
            InitializeComponent();
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}