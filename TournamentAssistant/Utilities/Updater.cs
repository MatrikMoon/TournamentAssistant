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

        public static async Task Update()
        {
            var updaterUrl = "http://tournamentassistant.net/downloads/TAUpdater.exe";

            Logger.Success(Environment.CommandLine);

            var executingAssembly = Assembly.GetExecutingAssembly();
            var executingAssemblyDirectory = Path.GetDirectoryName(executingAssembly.Location);

            var beatSaberDirectory = Path.GetFullPath($"{executingAssemblyDirectory}/../");
            var destinationPath = $"{beatSaberDirectory}TAUpdater.exe";

            Logger.Info($"Downloading TA updater");

            try
            {
                // Download TournamentAssistant.dll from tournamentassistant.net
                using (var client = new HttpClient())
                using (var fileStream = new FileStream(destinationPath, FileMode.Create))
                {
                    var response = await client.GetAsync(updaterUrl).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    using var contentStream = await response.Content.ReadAsStreamAsync();
                    await contentStream.CopyToAsync(fileStream);
                    Logger.Success("Successfully downloaded TA updater");

                    fileStream.Dispose();
                }

                var arguments = $"/K \"\"{destinationPath}\" -plugin \"{beatSaberDirectory}\" -commandLine {Environment.CommandLine}\"";
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
    }
}