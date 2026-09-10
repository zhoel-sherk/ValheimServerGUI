using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ValheimServerGUI.Controls;
using ValheimServerGUI.Game.Mods;
using ValheimServerGUI.Tools;

namespace ValheimServerGUI.Forms
{
    public partial class MainWindow
    {
        #region Backups

        private void BuildBackupsTab()
        {
            TabBackups = new TabPage
            {
                Text = "Backups",
                UseVisualStyleBackColor = true,
                Padding = new Padding(3),
                Location = new Point(4, 24),
                Size = new Size(452, 252),
            };

            BackupsStatusField = CreateModStatusField("Status");
            BackupsStatusField.Location = new Point(6, 10);
            BackupsStatusField.Size = new Size(440, 20);

            BackupsListView = new ListView
            {
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Location = new Point(6, 36),
                Size = new Size(443, 180),
            };
            BackupsListView.Columns.Add("Backup", 250);
            BackupsListView.Columns.Add("Date", 130);
            BackupsListView.Columns.Add("Size", 60);

            ButtonRefreshBackups = CreateModButton("Refresh", new Point(6, 222), 100);
            ButtonOpenBackupsFolder = CreateModButton("Open folder", new Point(112, 222), 110);

            ButtonRefreshBackups.Click += ButtonRefreshBackups_Click;
            ButtonOpenBackupsFolder.Click += ButtonOpenBackupsFolder_Click;
            BackupsListView.DoubleClick += BackupsListView_DoubleClick;
            TabBackups.VisibleChanged += TabBackups_VisibleChanged;

            TabBackups.Controls.AddRange(new Control[] { BackupsStatusField, BackupsListView, ButtonRefreshBackups, ButtonOpenBackupsFolder });
            Tabs.Controls.Add(TabBackups);
        }

        private void RefreshBackups()
        {
            try
            {
                var saveDataFolder = GetSaveDataFolder();
                var status = BackupService.GetStatus(
                    saveDataFolder,
                    ServerBackupsField.Value,
                    ServerShortBackupIntervalField.Value);

                BackupsStatusField.Value = status.Summary;
                ButtonRefreshBackups.Enabled = !ModOperationRunning;
                ButtonOpenBackupsFolder.Enabled = !string.IsNullOrWhiteSpace(saveDataFolder?.FullName);

                BackupsListView.BeginUpdate();
                BackupsListView.Items.Clear();

                foreach (var backup in status.Backups)
                {
                    var item = new ListViewItem(backup.Name)
                    {
                        Tag = backup,
                    };
                    item.SubItems.Add(backup.Timestamp.ToString("g"));
                    item.SubItems.Add(FormatFileSize(backup.SizeBytes));
                    BackupsListView.Items.Add(item);
                }

                BackupsListView.EndUpdate();
            }
            catch (Exception exception)
            {
                Logger.Error(exception, "Failed to refresh backups");
            }
        }

        private void ButtonRefreshBackups_Click(object sender, EventArgs e) => RefreshBackups();

        private void ButtonOpenBackupsFolder_Click(object sender, EventArgs e)
            => OpenHelper.OpenDirectory(GetSaveDataFolder()?.FullName);

        private void BackupsListView_DoubleClick(object sender, EventArgs e)
        {
            if (BackupsListView.SelectedItems.Count == 0) return;

            if (BackupsListView.SelectedItems[0].Tag is BackupInfo backup)
            {
                OpenHelper.OpenDirectory(backup.FullPath);
            }
        }

        private void TabBackups_VisibleChanged(object sender, EventArgs e)
        {
            if (TabBackups.Visible) RefreshBackups();
        }

        #endregion
    }
}
