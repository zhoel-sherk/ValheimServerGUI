using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ValheimServerGUI.Game.Mods
{
    public class BackupInfo
    {
        public string WorldName { get; set; }

        public string Name { get; set; }

        public string FullPath { get; set; }

        public DateTime Timestamp { get; set; }

        public long SizeBytes { get; set; }

        public bool IsDirectory { get; set; }
    }

    public class BackupsStatus
    {
        public List<BackupInfo> Backups { get; set; } = new();

        public BackupInfo Latest { get; set; }

        public bool HasBackups => Backups.Count > 0;

        public bool IsHealthy { get; set; }

        public string Summary { get; set; }

        public int ExpectedCount { get; set; }
    }

    public interface IBackupService
    {
        IReadOnlyList<BackupInfo> GetBackups(DirectoryInfo saveDataFolder);

        BackupsStatus GetStatus(DirectoryInfo saveDataFolder, int expectedCount, int shortIntervalSeconds);
    }

    /// <summary>
    /// Discovers the automatic world backups created by Valheim and reports their health.
    /// Modern builds (1.0.x+) store a backup as a folder named
    /// "&lt;world&gt;_backup_auto-&lt;yyyyMMdd-HHmmss&gt;"; legacy builds stored a pair of
    /// "&lt;world&gt;_backup_&lt;yyyyMMdd-HHmmss&gt;.db/.fwl" files.
    /// </summary>
    public class BackupService : IBackupService
    {
        private static readonly Regex BackupNameRegex = new(
            @"^(?<world>.+?)_backup_(?:auto-)?(?<date>\d{8})-(?<time>\d{6})",
            RegexOptions.IgnoreCase);

        public IReadOnlyList<BackupInfo> GetBackups(DirectoryInfo saveDataFolder)
        {
            var backups = new List<BackupInfo>();
            if (saveDataFolder == null || !saveDataFolder.Exists) return backups;

            foreach (var worldsFolder in GetWorldsFolders(saveDataFolder))
            {
                if (!worldsFolder.Exists) continue;

                foreach (var entry in worldsFolder.EnumerateFileSystemInfos())
                {
                    var match = BackupNameRegex.Match(entry.Name);
                    if (!match.Success) continue;

                    var worldName = match.Groups["world"].Value;
                    var isDirectory = entry is DirectoryInfo;

                    DateTime.TryParseExact(
                        $"{match.Groups["date"].Value}{match.Groups["time"].Value}",
                        "yyyyMMddHHmmss",
                        null,
                        System.Globalization.DateTimeStyles.None,
                        out var timestamp);

                    var existing = backups.FirstOrDefault(b =>
                        string.Equals(b.Name, entry.Name, StringComparison.OrdinalIgnoreCase)
                        || (!isDirectory && b.WorldName == worldName && b.Timestamp == timestamp && !b.IsDirectory));

                    if (existing != null)
                    {
                        // Pair legacy .db/.fwl files for the same backup into one entry
                        existing.SizeBytes += TryGetSize(entry);
                        continue;
                    }

                    backups.Add(new BackupInfo
                    {
                        WorldName = worldName,
                        Name = entry.Name,
                        FullPath = entry.FullName,
                        Timestamp = timestamp,
                        SizeBytes = TryGetSize(entry),
                        IsDirectory = isDirectory,
                    });
                }
            }

            return backups
                .OrderByDescending(b => b.Timestamp)
                .ToList();
        }

        public BackupsStatus GetStatus(DirectoryInfo saveDataFolder, int expectedCount, int shortIntervalSeconds)
        {
            var backups = GetBackups(saveDataFolder).ToList();

            var status = new BackupsStatus
            {
                Backups = backups,
                Latest = backups.FirstOrDefault(),
                ExpectedCount = expectedCount,
            };

            if (!status.HasBackups)
            {
                status.IsHealthy = false;
                status.Summary = "No automatic backups found yet.";
                return status;
            }

            var age = DateTime.Now - status.Latest.Timestamp;
            var ageText = age.ToHumanReadable();

            // Backups are only created for sessions that ran long enough, and the shortest
            // interval is configurable, so allow for some slack before flagging as unhealthy
            var staleThreshold = TimeSpan.FromSeconds(Math.Max(shortIntervalSeconds, 60) * 2);
            status.IsHealthy = age <= staleThreshold;

            status.Summary = status.IsHealthy
                ? $"Healthy - latest backup {ageText} ago ({backups.Count} total)."
                : $"Warning - latest backup is {ageText} old ({backups.Count} total).";

            return status;
        }

        private static IEnumerable<DirectoryInfo> GetWorldsFolders(DirectoryInfo saveDataFolder)
        {
            yield return new DirectoryInfo(Path.Join(saveDataFolder.FullName, "worlds"));
            yield return new DirectoryInfo(Path.Join(saveDataFolder.FullName, "worlds_local"));
        }

        private static long TryGetSize(FileSystemInfo entry)
        {
            try
            {
                if (entry is FileInfo file) return file.Length;

                if (entry is DirectoryInfo directory)
                {
                    return directory.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                }
            }
            catch
            {
                // Ignore entries that cannot be read
            }

            return 0;
        }
    }

    internal static class BackupTimeExtensions
    {
        public static string ToHumanReadable(this TimeSpan span)
        {
            if (span.TotalSeconds < 0) return "just now";
            if (span.TotalMinutes < 1) return $"{(int)span.TotalSeconds}s";
            if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m";
            if (span.TotalDays < 1) return $"{(int)span.TotalHours}h {(int)span.Minutes}m";
            return $"{(int)span.TotalDays}d {(int)span.Hours}h";
        }
    }
}
