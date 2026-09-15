using System;
using Avalonia.Controls;
using ValheimServerGUI.Avalonia.ViewModels;

namespace ValheimServerGUI.Avalonia.Views
{
    public partial class PortForwardingWindow : Window
    {
        public PortForwardingWindow()
        {
            InitializeComponent();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            // Run a check as soon as the dialog opens so the state is never shown as
            // "not mapped" before the gateway has actually been read.
            if (DataContext is PortForwardingViewModel vm)
            {
                _ = vm.CheckAsync();
            }
        }
    }
}
