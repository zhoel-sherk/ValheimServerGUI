using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ValheimServerGUI.Properties;
using ValheimServerGUI.Tools.Http;

namespace ValheimServerGUI.Game.Mods
{
    public class BepInExPackRelease
    {
        public string Version { get; set; }

        public string DownloadUrl { get; set; }
    }

    public class ValheimPlusAsset
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("browser_download_url")]
        public string DownloadUrl { get; set; }
    }

    public class ValheimPlusRelease
    {
        [JsonProperty("tag_name")]
        public string Version { get; set; }

        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }

        [JsonProperty("assets")]
        public ValheimPlusAsset[] Assets { get; set; }

        /// <summary>
        /// The download URL of the Windows dedicated server package. Prefers the plain
        /// "WindowsServer.zip" asset; the "Renamed" variant is only meant for server hosts
        /// that overwrite ValheimPlus.dll, so it is used as a last resort.
        /// </summary>
        public string GetWindowsServerDownloadUrl()
        {
            if (Assets == null) return null;

            string FindAsset(Func<string, bool> predicate)
                => Assets.FirstOrDefault(a => a?.Name != null && predicate(a.Name))?.DownloadUrl;

            return FindAsset(n => n.Equals("WindowsServer.zip", StringComparison.OrdinalIgnoreCase))
                ?? FindAsset(n => n.EndsWith("WindowsServer.zip", StringComparison.OrdinalIgnoreCase) && !n.Contains("Renamed"))
                ?? FindAsset(n => n.Equals("ValheimPlus.dll", StringComparison.OrdinalIgnoreCase));
        }
    }

    public interface IModSourceClient
    {
        Task<BepInExPackRelease> GetLatestBepInExPackAsync();

        Task<ValheimPlusRelease> GetLatestValheimPlusAsync();

        Task DownloadFileAsync(string url, string destinationPath);
    }

    public class ModSourceClient : RestClient, IModSourceClient
    {
        private const string UserAgent = "ValheimServerGUI";
        private const int MaxDownloadAttempts = 3;

        public ModSourceClient(IRestClientContext context) : base(context)
        {
        }

        public async Task<BepInExPackRelease> GetLatestBepInExPackAsync()
        {
            var response = await Get(Resources.UrlBepInExPackApi)
                .WithHeader("User-Agent", UserAgent)
                .SendAsync<ThunderstorePackageResponse>();

            if (response?.Latest == null || string.IsNullOrWhiteSpace(response.Latest.DownloadUrl))
            {
                throw new Exception("Unable to reach Thunderstore.");
            }

            return new BepInExPackRelease
            {
                Version = response.Latest.VersionNumber,
                DownloadUrl = response.Latest.DownloadUrl,
            };
        }

        public async Task<ValheimPlusRelease> GetLatestValheimPlusAsync()
        {
            var release = await Get(Resources.UrlValheimPlusApi)
                .WithHeader("User-Agent", UserAgent)
                .SendAsync<ValheimPlusRelease>();

            if (release == null || string.IsNullOrWhiteSpace(release.Version))
            {
                throw new Exception("Unable to reach GitHub.");
            }

            return release;
        }

        public async Task DownloadFileAsync(string url, string destinationPath)
        {
            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory)) Directory.CreateDirectory(destinationDirectory);

            var candidates = await GetDownloadCandidatesAsync(url);
            Exception lastException = null;

            foreach (var candidate in candidates)
            {
                for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
                {
                    try
                    {
                        await DownloadOnceAsync(candidate, destinationPath);
                        return;
                    }
                    catch (Exception e)
                    {
                        lastException = e;
                        Context.Logger.Warning(
                            "Download attempt {attempt}/{max} failed for {url}: {message}",
                            attempt, MaxDownloadAttempts, candidate, e.Message);

                        if (attempt < MaxDownloadAttempts)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt));
                        }
                    }
                }
            }

            throw new Exception(
                $"Download failed after {MaxDownloadAttempts} attempts ({url}): {lastException?.Message}",
                lastException);
        }

        private async Task DownloadOnceAsync(string url, string destinationPath)
        {
            using var client = CreateDownloadClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", UserAgent);

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var sourceStream = await response.Content.ReadAsStreamAsync();
            await using var destinationStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destinationStream);
        }

        /// <summary>
        /// Returns the URLs to try, preferring the direct download link when the original URL
        /// is a redirect (e.g. Thunderstore redirects to its G-Core CDN). This avoids relying
        /// on a single host/redirect chain.
        /// </summary>
        private async Task<List<string>> GetDownloadCandidatesAsync(string url)
        {
            var candidates = new List<string> { url };

            try
            {
                using var client = CreateDownloadClient(followRedirects: false);
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", UserAgent);

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location != null)
                {
                    var location = response.Headers.Location;
                    var direct = location.IsAbsoluteUri
                        ? location.ToString()
                        : new Uri(new Uri(url), location).ToString();

                    if (!candidates.Contains(direct))
                    {
                        candidates.Insert(0, direct);
                    }
                }
            }
            catch (Exception e)
            {
                Context.Logger.Warning("Unable to resolve the download redirect for {url}: {message}", url, e.Message);
            }

            return candidates;
        }

        private static HttpClient CreateDownloadClient(bool followRedirects = true)
        {
            var handler = new SocketsHttpHandler
            {
                // Fail quickly on unreachable routes (e.g. a dead IPv6 path) so retries can succeed elsewhere
                ConnectTimeout = TimeSpan.FromSeconds(20),
                AllowAutoRedirect = followRedirects,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            };

            return new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5),
            };
        }

        private class ThunderstorePackageResponse
        {
            [JsonProperty("latest")]
            public ThunderstoreVersion Latest { get; set; }
        }

        private class ThunderstoreVersion
        {
            [JsonProperty("version_number")]
            public string VersionNumber { get; set; }

            [JsonProperty("download_url")]
            public string DownloadUrl { get; set; }
        }
    }
}
