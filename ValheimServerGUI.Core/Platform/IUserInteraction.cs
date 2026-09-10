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
        /// Shows a confirmation prompt. Returns true when the user confirms.
        /// </summary>
        Task<bool> ConfirmAsync(string title, string message);
    }
}