using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Logger = TournamentAssistantShared.Logger;

/**
 * Update checker created by Moon, merged with Ari's AutoUpdater on 10/14/2021
 * Checks for and downloads new updates when they are available
 */

namespace TournamentAssistant.Utilities
{
    public static class Updater
    {
        public static void DeleteUpdater()
        {
            if (File.Exists("TAUpdater.exe"))
            {
                File.Delete("TAUpdater.exe");
            }
        }

        public static async Task Update(Action<double> downloadProgressChanged)
        {
            var updaterUrl = "http://tournamentassistant.net/downloads/TAUpdater.exe";

            var executingAssembly = Assembly.GetExecutingAssembly();
            var executingAssemblyDirectory = Path.GetDirectoryName(executingAssembly.Location);

            var beatSaberDirectory = Path.GetFullPath($"{executingAssemblyDirectory}/../");
            var destinationPath = Path.Combine(beatSaberDirectory, "TAUpdater.exe");

            Logger.Info($"Downloading TA updater");

            try
            {
                // Download TournamentAssistant.dll from tournamentassistant.net
                await DownloadWithProgress(updaterUrl, destinationPath, downloadProgressChanged);
                Logger.Success("Successfully downloaded TA updater");

                var arguments = $"/K \"\"{destinationPath}\" -plugin1370 \"{beatSaberDirectory}\" -commandLine {Environment.CommandLine}\"";
                arguments = arguments.Replace("\\", "\\\\");

                var startInfo = new ProcessStartInfo("cmd.exe")
                {
                    Arguments = arguments,
                    UseShellExecute = true,
                    CreateNoWindow = false, // This should be redundant with UseShellExecute as true
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Logger.Warning($"Starting updater with: {startInfo.Arguments}");
                Process.Start(startInfo);

                Application.Quit();
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
            }
        }

        private static async Task DownloadWithProgress(string url, string destinationPath, Action<double> downloadProgressChanged)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress = totalBytes != -1;

                    // Define the path where the file will be saved
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var totalRead = 0L;
                        var buffer = new byte[8192]; // Adjust buffer size as needed
                        var isMoreToRead = true;

                        while (isMoreToRead)
                        {
                            var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                            if (read == 0)
                            {
                                isMoreToRead = false;
                            }
                            else
                            {
                                // Write the data to the file
                                await fileStream.WriteAsync(buffer, 0, read);

                                totalRead += read;
                                if (canReportProgress)
                                {
                                    downloadProgressChanged?.Invoke(Math.Round((double)totalRead / totalBytes * 100, 2));
                                    Console.WriteLine($"Downloaded {totalRead} of {totalBytes} bytes. {Math.Round((double)totalRead / totalBytes * 100, 2)}% complete");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}