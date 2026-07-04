using System;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using vmsOpenAcars.Services.Http;

namespace vmsOpenAcars
{
    public class UpdateInfo
    {
        public Version Version { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseNotes { get; set; }
    }

    public static class UpdateChecker
    {
        private const string GitHubApiUrl =
            "https://api.github.com/repos/fhprietor/vmsOpenAcars/releases/latest";

        public static async Task<UpdateInfo> CheckGitHub()
        {
            string json = await HttpClientProvider.General.GetStringAsync(GitHubApiUrl);
            var obj = JObject.Parse(json);

            string tagName = obj["tag_name"]?.ToString();
            string notes = obj["body"]?.ToString();
            string zipUrl = obj["assets"]?[0]?["browser_download_url"]?.ToString();

            // Limpia "v0.2.3" o "v0.2.3-beta" → "0.2.3"
            string versionStr = tagName?
                .TrimStart('v')
                .Split('-')[0];

            return new UpdateInfo
            {
                Version = Version.Parse(versionStr),
                DownloadUrl = zipUrl,
                ReleaseNotes = notes
            };
        }

        public static Version GetLocalVersion()
            => Assembly.GetExecutingAssembly().GetName().Version;

        public static bool IsNewer(UpdateInfo info)
        {
            var remote = new Version(info.Version.Major, info.Version.Minor, info.Version.Build);
            var local = GetLocalVersion();
            var localN = new Version(local.Major, local.Minor, local.Build);
            return remote > localN;
        }
    }
}