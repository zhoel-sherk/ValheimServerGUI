using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Threading.Tasks;
using ValheimServerGUI.Core.Platform;

namespace ValheimServerGUI.Avalonia.Services
{
    /// <summary>
    /// Avalonia implementation of <see cref="IUserInteraction"/>: error and confirmation
    /// dialogs hosted on the main window.
    /// </summary>
    public class AvaloniaUserInteraction : IUserInteraction
    {
        public void ShowError(string title, string message)
        {
            _ = ShowDialogAsync(title, message, isConfirm: false);
        }

        public Task<bool> ConfirmAsync(string title, string message)
        {
            return ShowDialogAsync(title, message, isConfirm: true);
        }

        private static async Task<bool> ShowDialogAsync(string title, string message, bool isConfirm)
        {
            var owner = GetMainWindow();
            if (owner == null) return false;

            var result = false;

            var dialog = new Window
            {
                Title = title,
                Width = 420,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
            };

            var yesButton = new Button { Content = "Yes", Width = 90, IsDefault = true };
            yesButton.Click += (_, _) => { result = true; dialog.Close(); };

            var noButton = new Button { Content = "No", Width = 90, IsCancel = true };
            noButton.Click += (_, _) => dialog.Close();

            var okButton = new Button { Content = "OK", Width = 90, IsDefault = true };
            okButton.Click += (_, _) => { result = true; dialog.Close(); };

            var confirmButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Children = { yesButton, noButton },
            };

            var okButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
                Children = { okButton },
            };

            dialog.Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    isConfirm ? confirmButtons : okButtons,
                },
            };

            await dialog.ShowDialog(owner);
            return result;
        }

        private static Window? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }

            return null;
        }
    }
}