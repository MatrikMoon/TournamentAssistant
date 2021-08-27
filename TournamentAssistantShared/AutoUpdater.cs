using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TournamentAssistantShared.SimpleJSON;

namespace TournamentAssistantShared
{
    public class AutoUpdater
    {
        public static string osType = Convert.ToString(Environment.OSVersion);

        //For easy switching if those ever changed
        private static readonly string repoURL = "https://github.com/MatrikMoon/TournamentAssistant/releases/latest";
        private static readonly string repoAPI = "https://api.github.com/repos/MatrikMoon/TournamentAssistant/releases/latest";
        private static readonly string linuxFilename = "TournamentAssistantCore";
        private static readonly string WindowsFilename = "TournamentAssistantCore.exe";
        public static async Task<bool> AttemptAutoUpdate()
        {
            string CurrentFilename;
            if (osType.Contains("Unix")) CurrentFilename = linuxFilename;
            else if (osType.Contains("Windows")) CurrentFilename = WindowsFilename;
            else
            {
                Logger.Error($"AutoUpdater does not support your operating system. Detected Operating system is: {osType}. Supported are: Unix, Windows");
                return false;
            }

            Uri URI = await GetExecutableURI(CurrentFilename);

            if (URI == null)
            {
                Logger.Error($"AutoUpdate resource not found. Please update manually from: {repoURL}");
                return false;
            }

            //Delete any .old executables, if there are any.
            File.Delete($"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{CurrentFilename}.old");

            //Rename current executable to .old
            File.Move($"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{CurrentFilename}", $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{CurrentFilename}.old");

            //Download new executable
            Logger.Info("Downloading new version...");
            await GetExecutableFromURI(URI, CurrentFilename);
            Logger.Success("New version downloaded sucessfully!");

            //Restart as the new version
            Logger.Info("Attempting to start new version");
            if (osType.Contains("Unix")) Process.Start("chmod", $"+x {Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{CurrentFilename}"); //This is pretty hacky, but oh well....
            try
            {
                using Process newVersion = new Process();
                newVersion.StartInfo.UseShellExecute = true;
                newVersion.StartInfo.CreateNoWindow = osType.Contains("Unix"); //In linux shell there are no windows - causes an exception
                newVersion.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                newVersion.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{CurrentFilename}";
                newVersion.Start();
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
                Logger.Error($"Failed to start, please start new version manually from shell - downloaded version is saved at {Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}{CurrentFilename}");
                return false;
            }
            Logger.Success("Application updated succesfully!!");
            return true;
        }

        public static async Task GetExecutableFromURI(Uri URI, string filename)
        {
            WebClient Client = new WebClient();
            Client.DownloadProgressChanged += DownloadProgress;
            await Client.DownloadFileTaskAsync(URI, filename);
            Console.WriteLine();
            return;            
        }

        private static void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Write($"\rDownloaded {e.BytesReceived} / {e.TotalBytesToReceive} bytes. {e.ProgressPercentage} % complete...");
        }

        public static async Task<Uri> GetExecutableURI(string versionType)
        {
            HttpClientHandler httpClientHandler = new HttpClientHandler()
            {
                AllowAutoRedirect = false
            };
            using var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("user-agent", $"{SharedConstructs.Name}");

            var response = await client.GetAsync(repoAPI);

            var result = JSON.Parse(await response.Content.ReadAsStringAsync());

            for (int i = 0; i < result["assets"].Count; i++)
            {
                if (result["assets"][i]["browser_download_url"].ToString().Contains(versionType))
                {
                    //Adding this check since on linux the filename has been changed and there is a possibility of a mismatch. Moon you are making it hard :/
                    if (versionType == linuxFilename && result["assets"][i]["browser_download_url"].ToString().Contains(".exe")) continue;

                    Logger.Debug($"Web update resource found: {result["assets"][i]["browser_download_url"]}");
                    Uri.TryCreate(result["assets"][i]["browser_download_url"].ToString().Replace('"', ' ').Trim(), 0, out Uri resultUri);
                    return resultUri;
                }
            }
            return null;
        }
    }
}
