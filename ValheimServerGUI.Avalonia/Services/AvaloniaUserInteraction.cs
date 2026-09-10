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
            Dispatcher.UIThread.Post(() => ShowDialog(title, message, isConfirm: false));
        }

        public Task<bool> ConfirmAsync(string title, string message)
        {
            var tcs = new TaskCompletionSource<bool>();
            Dispatcher.UIThread.Post(() =>
            {
                var result = ShowDialog(title, message, isConfirm: true);
                tcs.SetResult(result);
            });
            return tcs.Task;
        }

        private static bool ShowDialog(string title, string message, bool isConfirm)
        {
            var owner = GetMainWindow();
            if (owner == null) return false;

            var dialog = new Window
            {
                Title = title,
                Width = 420,
                Height = 220,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
            };

            var result = false;
            var okButton = new Button { Content = "OK", Width = 90, IsDefault = true };
            okButton.Click += (_, _) => { result = true; dialog.Close(); };

            if (isConfirm)
            {
                var yesButton = new Button { Content = "Yes", Width = 90, IsDefault = true };
                yesButton.Click += (_, _) => { result = true; dialog.Close(); };
                var noButton = new Button { Content = "No", Width = 90, IsCancel = true };
                noButton.Click += (_, _) => dialog.Close();
                okButton = yesButton;

                var buttons = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { yesButton, noButton },
                };

                dialog.Content = new StackPanel
                {
                    Margin = new Thickness(16),
                    Spacing = 16,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                        buttons,
                    },
                };
            }
            else
            {
                var buttons = new StackPanel
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
                        buttons,
                    },
                };
            }

            dialog.ShowDialog(owner);
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