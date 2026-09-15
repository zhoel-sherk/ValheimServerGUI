using Semver;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ValheimServerGUI.Tools
{
    public static class AssemblyHelper
    {
        private static string _appVersion;
        private static string AppVersion => _appVersion ??= GetInformationalVersion();
        private const string BuildPrefix = "+build";

        public static string GetApplicationVersion()
        {
            var appVersion = AppVersion;
            var index = appVersion.IndexOf(BuildPrefix);
            return index >= 0 ? appVersion[..index] : appVersion;
        }


        /// <remarks>
        /// Adapted from: https://rmauro.dev/add-build-time-to-your-csharp-assembly/
        /// Set as SourceRevisionId in csproj.
        /// </remarks>
        public static DateTime GetApplicationBuildDate()
        {
            var appVersion = AppVersion;
            var index = appVersion.IndexOf(BuildPrefix);
            if (index >= 0)
            {
                return DateTime.Parse(appVersion[(index + BuildPrefix.Length)..], CultureInfo.InvariantCulture);
            }

            // No +build suffix (e.g. when hosted from a library rather than the app exe): fall back to
            // the entry assembly's file timestamp so the About dialog still shows something meaningful.
            var entryAssembly = Assembly.GetEntryAssembly();
            var location = entryAssembly?.Location ?? Assembly.GetExecutingAssembly().Location;
            return !string.IsNullOrEmpty(location) ? File.GetLastWriteTimeUtc(location) : DateTime.MinValue;
        }

        /// <summary>
        /// Returns...
        ///   * 1 if the provided version is newer than...
        ///   * -1 if the provided version is older than...
        ///   * 0 if the provided version is the same as...
        /// ...the current application version.
        /// Returns -2 if either version could not be parsed.
        /// </summary>
        /// <param name="otherVersion"></param>
        /// <returns></returns>
        public static int CompareVersion(string version)
        {
            return CompareVersions(GetApplicationVersion(), version);
        }

        /// <summary>
        /// Pure comparison backing <see cref="CompareVersion(string)"/>. Returns...
        ///   * 1 if <paramref name="otherVersion"/> is newer than <paramref name="currentVersion"/>
        ///   * -1 if <paramref name="otherVersion"/> is older than <paramref name="currentVersion"/>
        ///   * 0 if the two are equal
        /// ...using semantic-version precedence, in which a stable release outranks its own
        /// pre-releases (e.g. 2.4.0 is newer than 2.4.0-rc.1). Returns -2 if either version could
        /// not be parsed. Both arguments accept an optional leading "v", as GitHub release tags carry.
        /// </summary>
        public static int CompareVersions(string currentVersion, string otherVersion)
        {
            try
            {
                var current = SemVersion.Parse(currentVersion, SemVersionStyles.Any);
                var other = SemVersion.Parse(otherVersion, SemVersionStyles.Any);
                return SemVersion.CompareSortOrder(other, current);
            }
            catch
            {
                return -2;
            }
        }

        public static Version GetDotnetRuntimeVersion()
        {
            return Environment.Version;
        }

        #region Helper methods

        private static string GetInformationalVersion()
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var attribute = assembly
                .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)
                .FirstOrDefault() as AssemblyInformationalVersionAttribute;

            return attribute?.InformationalVersion ?? "0.0.0";
        }

        #endregion
    }
}
