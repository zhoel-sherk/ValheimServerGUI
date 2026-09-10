using Avalonia.Controls;
using Avalonia.Interactivity;
using ValheimServerGUI.Core.Platform;
using ValheimServerGUI.Tools.Logging;

namespace ValheimServerGUI.Avalonia.Views
{
    public partial class AboutWindow : Window
    {
        private readonly IPlatformIntegration PlatformIntegration;
        private readonly IApplicationLogger Logger;

        public AboutWindow(IPlatformIntegration platformIntegration, IApplicationLogger logger)
        {
            InitializeComponent();
            PlatformIntegration = platformIntegration;
            Logger = logger;
        }

        private void OnReportIssueClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is ViewModels.AboutViewModel about)
                {
                    PlatformIntegration.OpenWebAddress(about.IssueUrl);
                }
            }
            catch (System.Exception exception)
            {
                Logger.Error(exception, "Failed to open the issues page");
            }
        }
    }
}