using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public void ShowInfo(string title, string message)
        {
            _ = ShowDialogAsync(title, message, isConfirm: false);
        }

        public Task<bool> ConfirmAsync(string title, string message)
        {
            return ShowDialogAsync(title, message, isConfirm: true);
        }

        public Task<string?> ChooseAsync(string title, string message, IReadOnlyList<string> options, string? defaultOption = null)
        {
            return ShowChoiceDialogAsync(title, message, options, defaultOption);
        }

        public async Task<string?> PickFileAsync(string title, string filterName, IReadOnlyList<string> extensions)
        {
            var owner = GetMainWindow();
            if (owner == null) return null;

            try
            {
                var types = new List<FilePickerFileType>
                {
                    new(filterName) { Patterns = extensions.Select(e => "*" + e).ToList() },
                };

                var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = title,
                    AllowMultiple = false,
                    FileTypeFilter = types,
                });

                return files.Count > 0 ? files[0].TryGetLocalPath() : null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> SaveTextFileAsync(string title, string suggestedFileName, string content)
        {
            var owner = GetMainWindow();
            if (owner == null) return null;

            try
            {
                var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = title,
                    SuggestedFileName = suggestedFileName,
                    DefaultExtension = "txt",
                    FileTypeChoices = new[] { new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } } },
                });

                var path = file?.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(path)) return null;

                await File.WriteAllTextAsync(path, content);
                return path;
            }
            catch
            {
                return null;
            }
        }

        public async Task CopyToClipboardAsync(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            var owner = GetMainWindow();
            if (owner?.Clipboard == null) return;

            try
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.CreateText(text));
                await owner.Clipboard.SetDataAsync(data);
            }
            catch
            {
                // Clipboard access is best-effort
            }
        }

        private static async Task<string?> ShowChoiceDialogAsync(string title, string message, IReadOnlyList<string> options, string? defaultOption)
        {
            var owner = GetMainWindow();
            if (owner == null) return null;

            string? result = null;

            var dialog = new Window
            {
                Title = title,
                Width = 460,
                Height = 260,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false,
                ShowInTaskbar = false,
            };

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 8,
            };

            foreach (var option in options)
            {
                var captured = option;
                var button = new Button
                {
                    Content = option,
                    Width = 90,
                    IsDefault = option == defaultOption,
                };
                button.Click += (_, _) => { result = captured; dialog.Close(); };
                buttons.Children.Add(button);
            }

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

            await dialog.ShowDialog(owner);
            return result;
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