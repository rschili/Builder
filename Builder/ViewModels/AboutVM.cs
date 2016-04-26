using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Newtonsoft.Json.Linq;
using RSCoreLib.WPF;

namespace Builder
    {
    public class AboutVM : ViewModelBase
        {
        private static readonly ILog log = LogManager.GetLogger(typeof(AboutVM));

        private readonly Version _version = typeof(AboutVM).Assembly.GetName().Version;
        public AboutVM ()
            {
            OpenLatestReleaseCommand.Handler = OpenLatestRelease;
            Task.Run(CheckForUpdates);
            }

        private async Task CheckForUpdates ()
            {
            try
                {
                using (HttpClient client = new HttpClient())
                    {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
                    client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("rschili_update_check", "1.0"));

                    string json = null;
                    using (var response = await client.GetAsync("https://api.github.com/repos/rschili/Builder/releases/latest"))
                        {
                        if (!response.IsSuccessStatusCode)
                            {
                            UpdateText = "Could not check for updates";
                            return;
                            }

                        json = await response.Content.ReadAsStringAsync();
                        }

                    var jobj = await Task.Run(() => JObject.Parse(json));
                    string url = (string)jobj.GetValue("url", StringComparison.InvariantCultureIgnoreCase);
                    string name = (string)jobj.GetValue("name", StringComparison.InvariantCultureIgnoreCase);
                    DateTime published_at = (DateTime)jobj.GetValue("published_at", StringComparison.InvariantCultureIgnoreCase);
                    if (string.IsNullOrEmpty(url) ||
                        string.IsNullOrEmpty(name))
                        {
                        UpdateText = "Could not check for updates";
                        return;
                        }

                    var match = Regex.Match(name, @"^v(?<version>\d+\.\d+)-\w*$");
                    Version version;
                    if (!match.Success || !Version.TryParse(match.Groups["version"].Value, out version))
                        {
                        UpdateText = "Could not check for updates";
                        return;
                        }

                    if((version.Major == _version.Major && version.Minor >= _version.Minor) || (version.Major > _version.Major))
                        {
                        UpdateText = "Using the latest version.";
                        return;
                        }

                    UpdateImage = "../Images/vs/info16.png";
                    UpdateText = $"New Version available: {version.ToString(2)} built {published_at.ToShortDateString()}";
                    DownloadUrl = url;
                    }
                }
            catch (Exception e)
                {
                log.Error($"Error checking for updates. {e.Message}", e);
                UpdateText = "Could not check for updates";
                }
            finally
                {
                OnPropertyChanged(nameof(UpdateText));
                OnPropertyChanged(nameof(UpdateImage));
                OnPropertyChanged(nameof(DownloadUrl));
                }
            }

        public string Info => $"Version {_version.ToString(2)}, built {App.LinkerTime.ToShortDateString()}";

        public string UpdateImage { get; set; } = null;
        public string UpdateText { get; set; } = "Checking for Updates...";
        public string DownloadUrl { get; set; } = null;

        public SimpleCommand OpenLatestReleaseCommand { get; } = new SimpleCommand(true);

        private void OpenLatestRelease (object obj)
            {

            }

        }
    }
