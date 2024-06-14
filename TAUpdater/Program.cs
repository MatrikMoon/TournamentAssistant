// Define the path to the existing executable
using System.Diagnostics;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length <= 0)
        {
            Console.WriteLine("You shouldn't be seeing this, but anyway, you must provide a parameter to this program which is the path of the calling executable");
            await Task.Delay(10000);
            return;
        }

        var index = 0;
        foreach (var arg in args)
        {
            Console.WriteLine($"{index++}: {arg}");
        }

        Console.WriteLine("Updating TA...");

        if (args[0] == "-taui") // TAUpdater.exe -taui [path to taui.exe]
        {
            var existingPath = args[1];

            // Define the URL to download the new executable
            var url = "http://tournamentassistant.net/downloads/taui.exe";

            try
            {
                // Attempt to delete the existing file, waiting up to 5 seconds if it is still running
                var fileDeleted = false;
                var retryCount = 0;
                const int maxRetryCount = 5;

                while (!fileDeleted && retryCount < maxRetryCount)
                {
                    try
                    {
                        if (File.Exists(existingPath))
                        {
                            File.Delete(existingPath);
                            fileDeleted = true;
                            Console.WriteLine("Existing file deleted");
                        }
                        else
                        {
                            fileDeleted = true; // File does not exist, no need to delete
                            Console.WriteLine("No existing file found to delete");
                        }
                    }
                    catch (IOException)
                    {
                        // Wait for 1 second before trying again
                        await Task.Delay(1000);
                        retryCount++;
                    }
                }

                if (!fileDeleted)
                {
                    Console.WriteLine("Failed to delete the existing file after 5 seconds");
                    return; // Exit if the file cannot be deleted
                }

                // Download the update using HttpClient
                await DownloadWithProgress(url, existingPath);
                Console.WriteLine("Update downloaded");

                // Execute the downloaded file
                Process.Start(existingPath);
                Console.WriteLine("Launched new TAUI");
            }
            catch (Exception ex)
            {
                // Handle any errors that might have occurred
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }
        else if (args[0] == "-plugin" || args[0] == "-plugin134") // TAUpdater.exe -plugin [path to Beat Saber installation] [beat saber command line args, for relaunch]
        {
            var beatSaberDirectory = args[1];
            beatSaberDirectory = Path.GetFullPath(beatSaberDirectory);

            var destinationFileName = "TournamentAssistant.dll";
            var destinationDirectory = Path.GetFullPath($"{beatSaberDirectory}/IPA/Pending/Plugins/");
            var destinationPath = Path.Combine(destinationDirectory, destinationFileName);
            var beatSaberExecutable = Path.Combine(beatSaberDirectory, "Beat Saber.exe");

            // Create IPA/Pending/Plugins if it doesn't yet exist
            Directory.CreateDirectory(destinationDirectory);

            // Define the URL to download the new executable
            var url = "http://tournamentassistant.net/downloads/TournamentAssistant.dll";
            if (args[0] == "-plugin134")
            {
                url = "http://tournamentassistant.net/downloads/TournamentAssistant_1.34.dll";
            }

            try
            {
                // Download the update
                await DownloadWithProgress(url, destinationPath);
                Console.WriteLine("Update downloaded");

                // Relaunch beat saber
                var argsAsString = string.Join(" ", args);
                var beatSaberCommand = argsAsString.Substring(argsAsString.IndexOf("-commandLine") + "-commandLine ".Length);

                // Splitting this out because we can't trust beatSaberCommand to have the executable path escaped
                var beatSaberParameters = beatSaberCommand.Substring(beatSaberCommand.IndexOf("Beat Saber.exe ") + "Beat Saber.exe ".Length);

                var startInfo = new ProcessStartInfo(beatSaberExecutable)
                {
                    Arguments = beatSaberParameters,
                    UseShellExecute = true,
                    CreateNoWindow = false, // This should be redundant with UseShellExecute as true
                    WindowStyle = ProcessWindowStyle.Normal,
                    WorkingDirectory = beatSaberDirectory
                };

                // There's a chance the update downloaded so quickly that beat saber isn't closed yet, so
                // let's wait a bit just to be sure
                await Task.Delay(3000);

                Process.Start(startInfo);
                Console.WriteLine($"Relaunched Beat Saber as: {beatSaberCommand}");
            }
            catch (Exception ex)
            {
                // Handle any errors that might have occurred
                Console.WriteLine("An error occurred: " + ex.Message);
            }
        }

        await Task.Delay(10000);
    }

    private static async Task DownloadWithProgress(string url, string destinationPath)
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
                                Console.WriteLine($"Downloaded {totalRead} of {totalBytes} bytes. {Math.Round((double)totalRead / totalBytes * 100, 2)}% complete");
                            }
                        }
                    }
                }
            }
        }
    }
}


