using System;
using ValheimServerGUI.Tools;

namespace ValheimServerGUI.Avalonia.ViewModels
{
    /// <summary>
    /// About dialog (Phase 2 step 8): version, build date and links.
    /// </summary>
    public class AboutViewModel
    {
        public string VersionText { get; }

        public string BuildDateText { get; }

        public string IssueUrl { get; } = "https://github.com/zhoel-sherk/ValheimServerGUI/issues";

        public AboutViewModel()
        {
            try
            {
                VersionText = $"Version: {AssemblyHelper.GetApplicationVersion()}";
                BuildDateText = $"Build Date: {AssemblyHelper.GetApplicationBuildDate().ToUniversalTime().ToDisplayISOFormat()}";
            }
            catch
            {
                VersionText = "Version: unknown";
                BuildDateText = string.Empty;
            }
        }
    }
}