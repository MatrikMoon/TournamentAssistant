using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.SimpleJSON;

/**
 * Update checker created by Moon, merged with Ari's AutoUpdater on 10/14/2021
 * Checks for and downloads new updates when they are available
 */

namespace TournamentAssistantServer
{
    public static class Updater
    {
        private static readonly string _osType = Convert.ToString(Environment.OSVersion);
        private static readonly CancellationTokenSource _updateCheckerCts = new();

        //For easy switching if those ever changed
        //Moon's note: while the repo url is unlikely to change, the filenames are free game. I type and upload those manually, after all
        private static readonly string _repoURL = "https://github.com/MatrikMoon/TournamentAssistant/releases/latest";
        private static readonly string _repoAPI = "https://api.github.com/repos/MatrikMoon/TournamentAssistant/releases/latest";
        private static readonly string _linuxFilename = "TournamentAssistantServer";
        private static readonly string _windowsFilename = "TournamentAssistantServer.exe";

        public static async void StartUpdateChecker(TAServer server)
        {
            Logger.Info($"Running on {_osType}");
            Logger.Info("Checking for updates...");

            try
            {
                var newVersion = await GetLatestRelease();

                if (Version.Parse(Constants.VERSION) < newVersion)
                {
                    Logger.Error($"Update required! You are on \'{Constants.VERSION}\', new version is \'{newVersion}\'");
                    Logger.Info("Attempting automatic update...");

                    var updateSuccess = await AttemptAutoUpdate();
                    if (!updateSuccess)
                    {
                        Logger.Error($"Automatic update failed. Please update manually at {_repoURL}\n" +
                            $"Shutting down...");
                    }
                    else
                    {
                        Logger.Warning("Update was successful, exiting...");
                    }

                    Environment.Exit(0);
                }
                else
                {
                    Logger.Success($"You are on the most recent version! ({Constants.VERSION})");

                    //Start periodic update checker
                    await PollForUpdates(() =>
                    {
                        server.Shutdown();
                        Environment.Exit(0);
                    }, _updateCheckerCts.Token);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to check for updates. Reason: {ex.Message}");
            }
        }

        private static async Task<bool> AttemptAutoUpdate()
        {
            string currentFilename;
            if (_osType.Contains("Unix"))
            {
                currentFilename = _linuxFilename;
            }
            else if (_osType.Contains("Windows"))
            {
                currentFilename = _windowsFilename;
            }
            else
            {
                Logger.Error($"Update does not support your operating system. Detected Operating system is: {_osType}. Supported are: Unix, Windows");
                return false;
            }

            var uri = await GetExecutableURI(currentFilename);
            if (uri == null)
            {
                Logger.Error($"Update resource not found. Please update manually from: {_repoURL}");
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
            if (_osType.Contains("Unix"))
            {
                Process.Start("chmod", $"+x {Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{currentFilename}"); //This is pretty hacky, but oh well.... -Ari
            }

            try
            {
                using Process newVersion = new Process();
                newVersion.StartInfo.UseShellExecute = true;
                newVersion.StartInfo.CreateNoWindow = _osType.Contains("Unix"); //In linux shell there are no windows - causes an exception
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

        private static async Task GetExecutableFromURI(Uri uri, string filename)
        {
            using var client = new WebClient();
            client.DownloadProgressChanged += DownloadProgress;
            await client.DownloadFileTaskAsync(uri, filename);
        }

        private static void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.WriteLine($"Downloaded {e.BytesReceived} / {e.TotalBytesToReceive} bytes. {e.ProgressPercentage} % complete...");
        }

        private static async Task<Uri> GetExecutableURI(string versionType)
        {
            using var httpClientHandler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };

            using var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("user-agent", $"{Constants.NAME}");

            var response = await client.GetAsync(_repoAPI);
            var result = JSON.Parse(await response.Content.ReadAsStringAsync());

            for (int i = 0; i < result["assets"].Count; i++)
            {
                if (result["assets"][i]["browser_download_url"].ToString().Contains(versionType))
                {
                    //Adding this check since on linux the filename has been changed and there is a possibility of a mismatch. Moon you are making it hard :/
                    //Moon's note: Nothing is sacred. Especially things I do manually. Prepare for such possibilities
                    if (versionType == _linuxFilename && result["assets"][i]["browser_download_url"].ToString().Contains(".exe"))
                    {
                        Logger.Debug($"Web update resource found: {result["assets"][i]["browser_download_url"]}");
                        Uri.TryCreate(result["assets"][i]["browser_download_url"].ToString().Replace('"', ' ').Trim(), 0, out var resultUri);
                        return resultUri;
                    }
                }
            }
            return null;
        }

        private static async Task PollForUpdates(Action doAfterUpdate, CancellationToken cancellationToken)
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
        }

        private static async Task<Version> GetLatestRelease()
        {
            using var httpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            using var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("user-agent", $"{Constants.NAME}");

            var response = await client.GetAsync(_repoAPI);
            var result = JSON.Parse(await response.Content.ReadAsStringAsync());

            return Version.Parse(result["tag_name"]);
        }
    }
}