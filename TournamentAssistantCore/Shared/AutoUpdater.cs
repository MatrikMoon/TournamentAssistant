using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.SimpleJSON;

namespace TournamentAssistantCore.Shared
{
    class AutoUpdater
    {
        public static string osType = Convert.ToString(Environment.OSVersion);

        //For easy switching if those ever changed
        private static readonly string linuxExtension = "Core-linux";
        private static readonly string WindowsExtension = "Core.exe";
        //private static readonly string UIExtension = "UI.exe";
        public static async Task<bool> AttemptAutoUpdate()
        {
            string CurrentExtension;
            if (osType.Contains("Unix")) CurrentExtension = linuxExtension;
            else if (osType.Contains("Windows")) CurrentExtension = WindowsExtension;
            else
            {
                Logger.Error($"AutoUpdater does not support your operating system. Detected Operating system is: {osType}. Supported are: Unix, Windows");
                return false;
            }

            Uri URI = await GetExecutableURI(CurrentExtension);

            if (URI == null)
            {
                Logger.Error($"AutoUpdate resource not found. Please update manually from: https://github.com/MatrikMoon/TournamentAssistant/releases/latest");
                return false;
            }

            //Delete any .old executables, if there are any.
            File.Delete($"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}TournamentAssistant{CurrentExtension}.old");

            //Rename current executable to .old
            File.Move($"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}TournamentAssistant{CurrentExtension}", $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}TournamentAssistant{CurrentExtension}.old");

            //Download new executable
            Logger.Info("Downloading new version...");
            await GetExecutableFromURI(URI, CurrentExtension);
            Logger.Success("New version downloaded sucessfully!");

            //Restart as the new version
            Logger.Info("Attempting to start new version");
            if (CurrentExtension.Contains("linux")) Process.Start("chmod", $"+x {Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}TournamentAssistant{CurrentExtension}");
            using (Process newVersion = new Process())
            {
                newVersion.StartInfo.UseShellExecute = true;
                newVersion.StartInfo.CreateNoWindow = false;
                newVersion.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                newVersion.StartInfo.FileName = $"{Directory.GetCurrentDirectory()}{Path.DirectorySeparatorChar}TournamentAssistant{CurrentExtension}";
                newVersion.Start();
            }
            Logger.Success("Application updated succesfully!!");
            return true;
        }

        public static async Task GetExecutableFromURI(Uri URI, string extension)
        {
            WebClient Client = new WebClient();
            Client.DownloadProgressChanged += DownloadProgress;
            await Client.DownloadFileTaskAsync(URI, $"TournamentAssistant{extension}");
            Console.WriteLine("\n\n");
            return;            
        }

        private static void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            Console.Write($"\rDownloaded {e.BytesReceived} / {e.TotalBytesToReceive} bytes. {e.ProgressPercentage} % complete...");
        }

        public static async Task<Uri> GetExecutableURI(string versionType)
        {
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;

            using (var client = new HttpClient(httpClientHandler))
            {
                client.DefaultRequestHeaders.Add("user-agent", $"{SharedConstructs.Name}");

                var response = client.GetAsync($"https://api.github.com/repos/MatrikMoon/TournamentAssistant/releases/latest");
                response.Wait();

                var result = JSON.Parse(await response.Result.Content.ReadAsStringAsync());

                for (int i = 0; i < result["assets"].Count; i++)
                {
                    if (result["assets"][i]["browser_download_url"].ToString().Contains(versionType))
                    {
                        Logger.Debug($"Web update resource found: {result["assets"][i]["browser_download_url"]}");
                        Uri.TryCreate(result["assets"][i]["browser_download_url"].ToString().Replace('"', ' ').Trim(), 0, out Uri resultUri);
                        return resultUri;
                    }
                }
                return null;
            }
        }
    }
}
