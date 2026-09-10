using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ValheimServerGUI.Controls;
using ValheimServerGUI.Game;
using ValheimServerGUI.Game.Mods;
using ValheimServerGUI.Properties;
using ValheimServerGUI.Tools;
using ValheimServerGUI.Tools.Logging;
using ValheimServerGUI.Tools.Models;

namespace ValheimServerGUI.Forms
{
    public partial class MainWindow
    {
        #region Menu Items

        private void MenuItemFileNewWindow_Click(object sender, EventArgs e)
        {
            LaunchNewWindow();
        }

        private void MenuItemFileNewProfile_Click(object sender, EventArgs e)
        {
            var profileName = PromptForProfileName();
            if (profileName == null) return;

            var prefs = new ServerPreferences { ProfileName = profileName, Name = profileName };
            ServerPrefsProvider.SavePreferences(prefs);

            SetFormStateFromPrefs(prefs);
        }

        private void MenuItemFileSaveProfile_Click(object sender, EventArgs e)
        {
            var prefs = GetPrefsFromFormState();
            ServerPrefsProvider.SavePreferences(prefs);
        }

        private void MenuItemFileSaveProfileAs_Click(object sender, EventArgs e)
        {
            var profileName = PromptForProfileName($"Copy of {CurrentProfile}");
            if (profileName == null) return;

            var prefs = GetPrefsFromFormState();
            prefs.ProfileName = profileName;
            ServerPrefsProvider.SavePreferences(prefs);
            CurrentProfile = profileName;
        }

        private void MenuItemFileLoadProfileItem_Click(object sender, EventArgs e)
        {
            if (sender is not ToolStripItem item) return;

            SetFormStateFromPrefs(item.Text);
        }

        private void MenuItemFileRemoveProfileItem_Click(object sender, EventArgs e)
        {
            if (sender is not ToolStripItem item) return;

            var profileName = item.Text;
            var result = MessageBox.Show(
                $"Remove server profile '{profileName}'?",
                "Remove Profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                ServerPrefsProvider.RemovePreferences(profileName);
            }
        }

        private void MenuItemFilePreferences_Click(object sender, EventArgs e)
        {
            ShowPreferencesForm();
        }

        private void MenuItemFileDirectories_Click(object sender, EventArgs e)
        {
            ShowDirectoriesForm();
        }

        private void MenuItemFileOpenSettings_Click(object sender, EventArgs e)
        {
            var prefsDir = Path.GetDirectoryName(Resources.UserPrefsFilePathV2);
            OpenHelper.OpenDirectory(prefsDir);
        }

        private void MenuItemFileClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void MenuItemHelpManual_Click(object sender, EventArgs e)
        {
            OpenHelper.OpenWebAddress(Resources.UrlHelp);
        }

        private void MenuItemHelpPortForwarding_Click(object sender, EventArgs e)
        {
            OpenHelper.OpenWebAddress(Resources.UrlHelpPortForwarding);
        }

        private void MenuItemHelpIssues_Click(object sender, EventArgs e)
        {
            OpenHelper.OpenWebAddress(Resources.UrlIssues);
        }

        private void MenuItemHelpUpdates_Click(object sender, EventArgs e)
        {
            CheckForUpdates(true);
        }

        private void MenuItemHelpAbout_Click(object sender, EventArgs e)
        {
            var aboutForm = FormProvider.GetForm<AboutForm>();
            aboutForm.ShowDialog();
        }

        #endregion
    }
}
