using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ValheimServerGUI.Avalonia.Views
{
    public partial class PlayerDetailsWindow : Window
    {
        public PlayerDetailsWindow()
        {
            InitializeComponent();
        }

        private void OnSaveClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
