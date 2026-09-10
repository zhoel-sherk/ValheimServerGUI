using Avalonia.Controls;
using ValheimServerGUI.Avalonia.ViewModels;

namespace ValheimServerGUI.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow(ShellViewModel shellViewModel)
        {
            InitializeComponent();
            DataContext = shellViewModel;
        }
    }
}