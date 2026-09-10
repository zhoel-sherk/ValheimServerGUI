using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ValheimServerGUI.Game.Mods
{
    public static class ZipHelper
    {
        /// <summary>
        /// Extracts a zip archive into a destination folder.
        /// </summary>
        /// <param name="zipPath">The zip file to extract.</param>
        /// <param name="destinationFolder">The folder to extract into.</param>
        /// <param name="stripRootPrefix">
        /// When set, only entries under this prefix are extracted, and the prefix is removed
        /// from the target path (e.g. Thunderstore packages wrap everything in a root folder).
        /// </param>
        /// <param name="overwriteIfExists">
        /// Optional predicate that receives the relative target path and returns whether an
        /// existing file should be overwritten. When null, existing files are overwritten.
        /// </param>
        /// <returns>The number of files extracted.</returns>
        public static int ExtractZip(
            string zipPath,
            string destinationFolder,
            string stripRootPrefix = null,
            Func<string, bool> overwriteIfExists = null)
        {
            var destinationRoot = Path.GetFullPath(destinationFolder);
            Directory.CreateDirectory(destinationRoot);

            var extractedCount = 0;

            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                // Directory entries (no file name) are created implicitly
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var relativePath = entry.FullName.Replace('\\', '/');

                if (!string.IsNullOrEmpty(stripRootPrefix))
                {
                    if (!relativePath.StartsWith(stripRootPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    relativePath = relativePath.Substring(stripRootPrefix.Length);
                }

                if (string.IsNullOrWhiteSpace(relativePath)) continue;

                var targetPath = Path.GetFullPath(Path.Join(destinationRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

                // Guard against zip-slip (entries pointing outside of the destination)
                if (!targetPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase)) continue;

                if (File.Exists(targetPath) && overwriteIfExists != null && !overwriteIfExists(relativePath)) continue;

                var targetDirectory = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(targetDirectory)) Directory.CreateDirectory(targetDirectory);

                entry.ExtractToFile(targetPath, overwrite: true);
                extractedCount++;
            }

            return extractedCount;
        }

        /// <summary>
        /// Returns the common top-level folder prefix (including a trailing slash) when every
        /// file in the archive lives under a single root folder, otherwise returns null.
        /// </summary>
        public static string DetectSingleRootPrefix(string zipPath)
        {
            using var archive = ZipFile.OpenRead(zipPath);

            var rootSegments = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .Select(e => e.FullName.Replace('\\', '/').Split('/')[0])
                .Distinct()
                .ToList();

            if (rootSegments.Count != 1) return null;

            var hasFilesInsideRoot = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .All(e => e.FullName.Replace('\\', '/').Contains('/'));

            return hasFilesInsideRoot ? $"{rootSegments[0]}/" : null;
        }

        /// <summary>
        /// Returns true when the archive contains the given file (by relative path, using '/'
        /// separators and case-insensitive comparison).
        /// </summary>
        public static bool ContainsEntry(string zipPath, string relativePath)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            return archive.Entries.Any(e =>
                string.Equals(e.FullName.Replace('\\', '/'), relativePath, StringComparison.OrdinalIgnoreCase));
        }
    }
}
