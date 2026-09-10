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
        #region Mods & Backups

        private void BuildModsTab()
        {
            TabMods = new TabPage
            {
                Text = "Mods",
                UseVisualStyleBackColor = true,
                Padding = new Padding(3),
                Location = new Point(4, 24),
                Size = new Size(452, 252),
            };

            // BepInEx
            var bepInExGroup = new GroupBox { Text = "BepInEx", Location = new Point(3, 3), Size = new Size(443, 84) };
            BepInExStatusField = CreateModStatusField("BepInEx");
            ButtonInstallBepInEx = CreateModButton("Install / Update", new Point(6, 44), 100);
            ButtonBepInExFromFile = CreateModButton("Install from file...", new Point(110, 44), 112);
            ButtonCheckBepInExUpdate = CreateModButton("Check for updates", new Point(226, 44), 112);
            bepInExGroup.Controls.AddRange(new Control[] { BepInExStatusField, ButtonInstallBepInEx, ButtonBepInExFromFile, ButtonCheckBepInExUpdate });

            // Valheim Plus
            var valheimPlusGroup = new GroupBox { Text = "Valheim Plus", Location = new Point(3, 93), Size = new Size(443, 84) };
            ValheimPlusStatusField = CreateModStatusField("Valheim Plus");
            ButtonInstallValheimPlus = CreateModButton("Install / Update", new Point(6, 44), 100);
            ButtonValheimPlusFromFile = CreateModButton("Install from file...", new Point(110, 44), 112);
            ButtonOpenValheimPlusConfig = CreateModButton("Open config", new Point(226, 44), 112);
            valheimPlusGroup.Controls.AddRange(new Control[] { ValheimPlusStatusField, ButtonInstallValheimPlus, ButtonValheimPlusFromFile, ButtonOpenValheimPlusConfig });

            var infoLabel = new Label
            {
                Location = new Point(6, 186),
                Size = new Size(440, 60),
                Text = "Mods are installed into the folder that contains valheim_server.exe.\r\n" +
                       "The server must be stopped while installing or updating mods.\r\n" +
                       "BepInEx is required before Valheim Plus can be installed.",
            };

            ButtonInstallBepInEx.Click += ButtonInstallBepInEx_Click;
            ButtonBepInExFromFile.Click += ButtonBepInExFromFile_Click;
            ButtonCheckBepInExUpdate.Click += ButtonCheckBepInExUpdate_Click;
            ButtonInstallValheimPlus.Click += ButtonInstallValheimPlus_Click;
            ButtonValheimPlusFromFile.Click += ButtonValheimPlusFromFile_Click;
            ButtonOpenValheimPlusConfig.Click += ButtonOpenValheimPlusConfig_Click;
            TabMods.VisibleChanged += TabMods_VisibleChanged;

            TabMods.Controls.AddRange(new Control[] { bepInExGroup, valheimPlusGroup, infoLabel });
            Tabs.Controls.Add(TabMods);
        }

        private static LabelField CreateModStatusField(string label)
        {
            return new LabelField
            {
                LabelText = label,
                LabelSplitRatio = 0.25,
                Location = new Point(6, 18),
                Size = new Size(431, 20),
                Value = "Checking...",
            };
        }

        private static Button CreateModButton(string text, Point location, int width)
        {
            return new Button
            {
                Text = text,
                Location = location,
                Size = new Size(width, 23),
                UseVisualStyleBackColor = true,
            };
        }

        private void RefreshModsTab()
        {
            try
            {
                var serverFolder = GetServerFolder();
                var installsAllowed = Server.IsAnyStatus(ServerStatus.Stopped) && !ModOperationRunning;

                if (string.IsNullOrWhiteSpace(serverFolder))
                {
                    BepInExStatusField.Value = "Server executable is not configured";
                    ValheimPlusStatusField.Value = "Server executable is not configured";
                }
                else
                {
                    BepInExStatusField.Value = FormatModStatus(BepInExManager.GetStatus(serverFolder));
                    ValheimPlusStatusField.Value = FormatModStatus(ValheimPlusManager.GetStatus(serverFolder));
                }

                ButtonInstallBepInEx.Enabled = installsAllowed;
                ButtonBepInExFromFile.Enabled = installsAllowed;
                ButtonCheckBepInExUpdate.Enabled = !ModOperationRunning && !string.IsNullOrWhiteSpace(serverFolder);
                ButtonInstallValheimPlus.Enabled = installsAllowed;
                ButtonValheimPlusFromFile.Enabled = installsAllowed;
                ButtonOpenValheimPlusConfig.Enabled = !string.IsNullOrWhiteSpace(serverFolder);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to refresh the mods tab");
            }
        }

        private async Task InstallBepInExAsync(string archivePath = null)
        {
            var serverFolder = GetServerFolder();
            if (!ValidateModInstallTarget(serverFolder, "BepInEx")) return;

            await RunModOperationAsync(
                BepInExStatusField,
                progress => archivePath == null
                    ? BepInExManager.InstallAsync(serverFolder, progress)
                    : BepInExManager.InstallFromFileAsync(serverFolder, archivePath, progress),
                "BepInEx");
        }

        private async Task InstallValheimPlusAsync(string archivePath = null)
        {
            var serverFolder = GetServerFolder();
            if (!ValidateModInstallTarget(serverFolder, "Valheim Plus")) return;

            await RunModOperationAsync(
                ValheimPlusStatusField,
                progress => archivePath == null
                    ? ValheimPlusManager.InstallAsync(serverFolder, progress)
                    : ValheimPlusManager.InstallFromFileAsync(serverFolder, archivePath, progress),
                "Valheim Plus");
        }

        private async Task RunModOperationAsync(
            LabelField statusField,
            Func<Action<string>, Task<ModStatus>> operation,
            string name)
        {
            if (ModOperationRunning) return;

            ModOperationRunning = true;
            RefreshModsTab();

            try
            {
                var status = await operation(message => statusField.Value = message);
                statusField.Value = FormatModStatus(status);
                Logger.Information("Installed {name}: {version}", name, status.Version ?? "unknown");
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed while installing {name}", name);
                MessageBox.Show(
                    $"{exception.Message}{Environment.NewLine}{Environment.NewLine}" +
                    "If this keeps failing, download the archive manually and use \"Install from file...\".",
                    $"Install {name} failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                ModOperationRunning = false;
                RefreshModsTab();
            }
        }

        private async Task CheckModUpdateAsync(LabelField statusField, Func<Task<ModStatus>> operation, string name)
        {
            if (ModOperationRunning) return;

            var serverFolder = GetServerFolder();
            if (string.IsNullOrWhiteSpace(serverFolder)) return;

            ModOperationRunning = true;
            RefreshModsTab();

            try
            {
                var status = await operation();
                statusField.Value = FormatModStatus(status);

                MessageBox.Show(
                    status.UpdateAvailable
                        ? $"A newer {name} version is available: {status.LatestVersion}"
                        : $"{name} is up to date.",
                    $"{name} update check",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to check {name} for updates", name);
                MessageBox.Show(
                    exception.Message,
                    $"{name} update check failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                ModOperationRunning = false;
                RefreshModsTab();
            }
        }

        private bool ValidateModInstallTarget(string serverFolder, string name)
        {
            if (string.IsNullOrWhiteSpace(serverFolder) || !Directory.Exists(serverFolder))
            {
                MessageBox.Show(
                    "Please configure a valid Valheim server executable first.",
                    $"Install {name}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            if (!Server.IsAnyStatus(ServerStatus.Stopped))
            {
                MessageBox.Show(
                    "Stop the server before installing or updating mods.",
                    $"Install {name}",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }

            return true;
        }

        private static string FormatModStatus(ModStatus status)
        {
            if (status == null || !status.IsInstalled)
            {
                return "Not installed";
            }

            var text = !string.IsNullOrWhiteSpace(status.Version)
                ? $"Installed: {status.Version}"
                : "Installed";

            if (!string.IsNullOrWhiteSpace(status.PackageVersion))
            {
                text += $" (pack {status.PackageVersion})";
            }

            if (!string.IsNullOrWhiteSpace(status.LatestVersion))
            {
                text += status.UpdateAvailable
                    ? $" - update available: {status.LatestVersion}"
                    : " - up to date";
            }

            return text;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            var unit = 0;

            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:0.#} {units[unit]}";
        }

        private string GetServerFolder()
        {
            var exePath = ServerExePathField?.Value;
            if (string.IsNullOrWhiteSpace(exePath)) return null;

            var folder = Path.GetDirectoryName(Environment.ExpandEnvironmentVariables(exePath));
            return string.IsNullOrWhiteSpace(folder) ? null : folder;
        }

        private DirectoryInfo GetSaveDataFolder()
        {
            try
            {
                var path = GetServerOptionsFromFormState().SaveDataFolderPath;
                if (string.IsNullOrWhiteSpace(path)) return null;

                return new DirectoryInfo(Environment.ExpandEnvironmentVariables(path));
            }
            catch
            {
                return null;
            }
        }

        private string PromptForArchive(string title)
        {
            using var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "Archives (*.zip;*.dll)|*.zip;*.dll|All files (*.*)|*.*",
                CheckFileExists = true,
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
        }

        private async void ButtonInstallBepInEx_Click(object sender, EventArgs e)
            => await InstallBepInExAsync();

        private async void ButtonBepInExFromFile_Click(object sender, EventArgs e)
        {
            var archivePath = PromptForArchive("Select a BepInEx archive");
            if (archivePath == null) return;

            await InstallBepInExAsync(archivePath);
        }

        private async void ButtonCheckBepInExUpdate_Click(object sender, EventArgs e)
        {
            var serverFolder = GetServerFolder();
            if (!string.IsNullOrWhiteSpace(serverFolder))
            {
                await CheckModUpdateAsync(BepInExStatusField, () => BepInExManager.CheckForUpdateAsync(serverFolder), "BepInEx");
            }
        }

        private async void ButtonInstallValheimPlus_Click(object sender, EventArgs e)
            => await InstallValheimPlusAsync();

        private async void ButtonValheimPlusFromFile_Click(object sender, EventArgs e)
        {
            var archivePath = PromptForArchive("Select a Valheim Plus archive");
            if (archivePath == null) return;

            await InstallValheimPlusAsync(archivePath);
        }

        private void ButtonOpenValheimPlusConfig_Click(object sender, EventArgs e)
        {
            var serverFolder = GetServerFolder();
            if (string.IsNullOrWhiteSpace(serverFolder)) return;

            OpenHelper.OpenDirectory(Path.Join(serverFolder, "BepInEx", "config"));
        }

        private void TabMods_VisibleChanged(object sender, EventArgs e)
        {
            if (TabMods.Visible) RefreshModsTab();
        }

        #endregion
    }
}
