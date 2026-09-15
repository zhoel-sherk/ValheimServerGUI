#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ValheimServerGUI.Core.Platform
{
    /// <summary>
    /// UI-facing interaction contract: error dialogs, confirmation prompts and file/folder
    /// pickers. The core must never call MessageBox; the host (WinForms or Avalonia) provides
    /// an implementation. Commands report failures through this service.
    /// </summary>
    public interface IUserInteraction
    {
        /// <summary>
        /// Shows an error message to the user.
        /// </summary>
        void ShowError(string title, string message);

        /// <summary>
        /// Shows an informational message to the user.
        /// </summary>
        void ShowInfo(string title, string message);

        /// <summary>
        /// Shows a confirmation prompt. Returns true when the user confirms.
        /// </summary>
        Task<bool> ConfirmAsync(string title, string message);

        /// <summary>
        /// Shows a prompt with the given choices and returns the chosen option, or null when the
        /// user dismisses/cancels the prompt.
        /// </summary>
        Task<string?> ChooseAsync(string title, string message, IReadOnlyList<string> options, string? defaultOption = null);

        /// <summary>
        /// Shows an open-file dialog and returns the selected path, or null when cancelled.
        /// </summary>
        Task<string?> PickFileAsync(string title, string filterName, IReadOnlyList<string> extensions);

        /// <summary>
        /// Shows a save-file dialog and writes <paramref name="content"/> to the chosen path.
        /// Returns the written path, or null when cancelled/failed.
        /// </summary>
        Task<string?> SaveTextFileAsync(string title, string suggestedFileName, string content);
    }
}