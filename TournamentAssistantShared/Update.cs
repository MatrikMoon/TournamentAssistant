using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared.SimpleJSON;

/**
 * Update checker created by Moon, merged with Ari's AutoUpdater on 10/14/2021
 * Checks for and downloads new updates when they are available
 */

namespace TournamentAssistantShared
{
    public class Update
    {
        public static string OSType = Convert.ToString(Environment.OSVersion);

        //For easy switching if those ever changed
        //Moon's note: while the repo url is unlikely to change, the filenames are free game. I type and upload those manually, after all
        private static readonly string repoURL = "https://github.com/MatrikMoon/TournamentAssistant/releases/latest";
        private static readonly string repoAPI = "https://api.github.com/repos/MatrikMoon/TournamentAssistant/releases/latest";
        private static readonly string linuxFilename = "TournamentAssistantServer";
        private static readonly string WindowsFilename = "TournamentAssistantServer.exe";
        public static async Task<bool> AttemptAutoUpdate()
        {
            string currentFilename;
            if (OSType.Contains("Unix"))
            {
                currentFilename = linuxFilename;
            }
            else if (OSType.Contains("Windows"))
            {
                currentFilename = WindowsFilename;
            }
            else
            {
                Logger.Error($"Update does not support your operating system. Detected Operating system is: {OSType}. Supported are: Unix, Windows");
                return false;
            }

            var uri = await GetExecutableURI(currentFilename);
            if (uri == null)
            {
                Logger.Error($"AutoUpdate resource not found. Please update manually from: {repoURL}");
                return false;
            }

            //Delete any .old executables, if there are any.
            File.Delete($"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{currentFilename}.old");

            //Rename current executable to .old
            File.Move($"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{currentFilename}", $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{currentFilename}.old");

            //Download new executable
            Logger.Info("Downloading new version...");
            await GetExecutableFromURI(uri, currentFilename);
            Logger.Success("New version downloaded sucessfully!");

            //Restart as the new version
            Logger.Info("Attempting to start new version");
            if (OSType.Contains("Unix")) Process.Start("chmod", $"+x {Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{currentFilename}"); //This is pretty hacky, but oh well.... -Ari
            try
            {
                using Process newVersion = new Process();
                newVersion.StartInfo.UseShellExecute = true;
                newVersion.StartInfo.CreateNoWindow = OSType.Contains("Unix"); //In linux shell there are no windows - causes an exception
                newVersion.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                newVersion.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{currentFilename}";
                newVersion.Start();
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
                Logger.Error($"Failed to start, please start new version manually from shell - downloaded version is saved at {Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{currentFilename}");
                return false;
            }
            Logger.Success("Application updated succesfully!");
            return true;
        }

        public static async Task GetExecutableFromURI(Uri uri, string filename)
        {
            using var client = new WebClient();
            client.DownloadProgressChanged += DownloadProgress;
            await client.DownloadFileTaskAsync(uri, filename);
        }

        private static void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.WriteLine($"Downloaded {e.BytesReceived} / {e.TotalBytesToReceive} bytes. {e.ProgressPercentage} % complete...");
        }

        public static async Task<Uri> GetExecutableURI(string versionType)
        {
            using var httpClientHandler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            using var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("user-agent", $"{Constants.NAME}");

            var response = await client.GetAsync(repoAPI);
            var result = JSON.Parse(await response.Content.ReadAsStringAsync());

            for (int i = 0; i < result["assets"].Count; i++)
            {
                if (result["assets"][i]["browser_download_url"].ToString().Contains(versionType))
                {
                    //Adding this check since on linux the filename has been changed and there is a possibility of a mismatch. Moon you are making it hard :/
                    //Moon's note: Nothing is sacred. Especially things I do manually. Prepare for such possibilities
                    if (versionType == linuxFilename && result["assets"][i]["browser_download_url"].ToString().Contains(".exe"))
                    {
                        Logger.Debug($"Web update resource found: {result["assets"][i]["browser_download_url"]}");
                        Uri.TryCreate(result["assets"][i]["browser_download_url"].ToString().Replace('"', ' ').Trim(), 0, out var resultUri);
                        return resultUri;
                    }
                }
            }
            return null;
        }

        public static void PollForUpdates(Action doAfterUpdate, CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (Version.Parse(Constants.VERSION) < await GetLatestRelease())
                    {
                        bool updateSuccess = await AttemptAutoUpdate();
                        if (!updateSuccess)
                        {
                            Logger.Error("AutoUpdate Failed, The server will now shut down. Please update to continue.");
                            doAfterUpdate();
                        }
                        else
                        {
                            Logger.Warning("Update Successful, exiting...");
                            doAfterUpdate();
                        }
                    }
                    await Task.Delay(1000 * 60 * 10, cancellationToken);
                }
            });
        }

        public static async Task<Version> GetLatestRelease()
        {
            using var httpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            using var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("user-agent", $"{Constants.NAME}");

            var response = await client.GetAsync(repoAPI);
            var result = JSON.Parse(await response.Content.ReadAsStringAsync());

            return Version.Parse(result["tag_name"]);
        }
    }
}