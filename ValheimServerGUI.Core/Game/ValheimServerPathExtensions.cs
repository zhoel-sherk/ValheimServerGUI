using System;
using System.IO;

namespace ValheimServerGUI.Game
{
    /// <summary>
    /// Validates and resolves the server executable and save-data paths described by
    /// <see cref="IValheimServerOptions"/>. Kept in Core so that option validation is
    /// portable and does not depend on the WinForms app.
    /// </summary>
    public static class ValheimServerPathExtensions
    {
        private static readonly string NL = Environment.NewLine;

        public static FileInfo GetValidatedServerExe(this IValheimServerOptions options)
        {
            var path = Environment.ExpandEnvironmentVariables(options.ServerExePath);

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Cannot open file, path is not defined.");
            }

            if (!Path.HasExtension(path))
            {
                throw new ArgumentException($"Cannot open file, must point to a valid .exe file:{NL}{path}");
            }

            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"File not found at path:{NL}{path}");
            }

            return new FileInfo(path);
        }

        public static DirectoryInfo GetValidatedSaveDataFolder(this IValheimServerOptions options)
        {
            var path = Environment.ExpandEnvironmentVariables(options.SaveDataFolderPath);

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Cannot open directory, path is not defined.");
            }

            if (!Directory.Exists(path))
            {
                throw new DirectoryNotFoundException($"Directory not found at path:{NL}{path}");
            }

            return new DirectoryInfo(path);
        }
    }
}