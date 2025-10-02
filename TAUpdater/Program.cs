// Define the path to the existing executable
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

public class Program
{
    private static async Task UpdateTaui(string existingPath)
    {
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

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "Just reads assembly name, no worries")]
    private static async Task UpdatePlugin(string beatSaberDirectory, string beatSaberParameters, string beatSaberVersion)
    {
        var destinationFileName = "TournamentAssistant.dll";
        var pluginsDirectory = Path.GetFullPath($"{beatSaberDirectory}/Plugins/");
        var destinationDirectory = Path.GetFullPath($"{beatSaberDirectory}/IPA/Pending/Plugins/");
        var destinationPath = Path.Combine(destinationDirectory, destinationFileName);
        var beatSaberExecutable = Path.Combine(beatSaberDirectory, "Beat Saber.exe");

        // There's a chance the update downloaded so quickly that beat saber isn't closed yet, so
        // let's wait a bit just to be sure
        await Task.Delay(5000);

        // Delete any existing TA installations from plugins
        var assemblyFiles = Directory.GetFiles(pluginsDirectory, "*.dll");
        foreach (var assemblyFile in assemblyFiles)
        {
            try
            {
                // Load the assembly
                var assemblyName = AssemblyName.GetAssemblyName(assemblyFile);

                Console.WriteLine($"Checking Assembly: {Path.GetFileName(assemblyFile)} ({assemblyName.Name})");

                // Check if the assembly name matches the one to delete
                if (assemblyName.Name == "TournamentAssistant")
                {
                    Console.WriteLine("Found existing TA installation in the above file, deleting...");
                    File.Delete(assemblyFile);
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine($"Error loading types from assembly: {assemblyFile}");
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    Console.WriteLine($"  {loaderException?.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing assembly: {assemblyFile}, Error: {ex.Message}");
            }
        }

        // Create IPA/Pending/Plugins if it doesn't yet exist
        Directory.CreateDirectory(destinationDirectory);

        // Define the URL to download the new executable
        var url = $"http://tournamentassistant.net/downloads/TournamentAssistant_{beatSaberVersion}.dll";

        try
        {
            // Download the update
            await DownloadWithProgress(url, destinationPath);
            Console.WriteLine("Update downloaded");

            // Relaunch beat saber
            var startInfo = new ProcessStartInfo(beatSaberExecutable)
            {
                Arguments = beatSaberParameters,
                UseShellExecute = true,
                CreateNoWindow = false, // This should be redundant with UseShellExecute as true
                WindowStyle = ProcessWindowStyle.Normal,
                WorkingDirectory = beatSaberDirectory
            };

            Process.Start(startInfo);
            Console.WriteLine($"Relaunched Beat Saber as: Beat Saber.exe {beatSaberParameters}");
        }
        catch (Exception ex)
        {
            // Handle any errors that might have occurred
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }

    static void DrawWelcomeMessage()
    {
        Console.Clear(); // Clear the console window

        var message = " TA Updater ";
        var width = Console.WindowWidth / 2; // Use full console width for the gradient effect
        var leftOffset = Console.WindowWidth / 2 / 2;
        var topOffset = 0;

        if (width < message.Length * 2)
        {
            return;
        }

        // Draw the top border with gradient
        Console.SetCursorPosition(leftOffset, topOffset);
        DrawGradientBorder(width);

        // Prepare the message line
        Console.SetCursorPosition(leftOffset, topOffset + 1);
        Console.Write("▒"); // Padding for left border alignment
        Console.ForegroundColor = ConsoleColor.Red; // Set message color to red
        Console.SetCursorPosition((leftOffset + width / 2) - (message.Length / 2), topOffset + 1);
        Console.Write(message);
        Console.SetCursorPosition(leftOffset + width - 1, topOffset + 1);
        Console.WriteLine("▒"); // Move to the next line
        Console.ResetColor();

        // Draw the bottom border with gradient
        Console.SetCursorPosition(leftOffset, topOffset + 2);
        DrawGradientBorder(width);

        Console.ResetColor(); // Reset to default color
        Console.SetCursorPosition(0, topOffset + 4); // Move cursor below the box
    }

    static void DrawGradientBorder(int width)
    {
        // Define gradient steps - simulate from red, to white, back to red
        ConsoleColor[] gradientColors = new[] { ConsoleColor.Red, ConsoleColor.White, ConsoleColor.Red };

        int sectionWidth = width / gradientColors.Length; // Divide width by the number of colors for even sections

        for (int colorIndex = 0; colorIndex < gradientColors.Length; colorIndex++)
        {
            Console.ForegroundColor = gradientColors[colorIndex];
            for (int i = 0; i < sectionWidth; i++)
            {
                Console.Write("▒");
            }
        }

        // Fill in any remaining space with the last color
        Console.ForegroundColor = gradientColors[^1]; // ^1 is the last element
        int remainingWidth = width % gradientColors.Length;
        Console.Write(new string('▒', remainingWidth));
    }

    public static async Task Main(string[] args)
    {
        if (args.Length <= 0)
        {
            Console.WriteLine("You shouldn't be seeing this, but anyway, you must provide a parameter to this program which is the path of the calling executable");
            await Task.Delay(10000);
            return;
        }

        DrawWelcomeMessage();

        /*var index = 0;
        foreach (var arg in args)
        {
            Console.WriteLine($"{index++}: {arg}");
        }*/

        Console.WriteLine("Updating TA...");

        if (args[0] == "-taui") // TAUpdater.exe -taui [path to taui.exe]
        {
            var existingPath = args[1];
            await UpdateTaui(existingPath);
        }
        else if (args[0] == "-plugin" || args[0] == "-plugin134" || args[0] == "-plugin1391" || args[0] == "-plugin1408") // TAUpdater.exe -plugin [path to Beat Saber installation] [beat saber command line args, for relaunch]
        {
            var beatSaberDirectory = args[1];
            beatSaberDirectory = Path.GetFullPath(beatSaberDirectory);

            // -commandLine will always be the last parameter (sorry), so we start there and read the
            // rest of the args to get the original beat saber launch parameters
            var argsAsString = string.Join(" ", args);
            var beatSaberCommand = argsAsString.Substring(argsAsString.IndexOf("-commandLine") + "-commandLine ".Length);
            
            // Splitting this out because we can't trust beatSaberCommand to have the executable path escaped
            var beatSaberParameters = beatSaberCommand.Substring(beatSaberCommand.IndexOf("Beat Saber.exe ") + "Beat Saber.exe ".Length);

            // Get the version number...
            // Appended to file name when downloading update
            // (ex: http://tournamentassistant.net/downloads/TournamentAssistant_1.34.2.dll)
            var version = "1.29.1";
            if (args[0] == "-plugin134")
            {
                version = "1.34.2";
            }
            else if (args[0] == "-plugin1391")
            {
                version = "1.39.1";
            }
            else if (args[0] == "-plugin1408")
            {
                version = "1.40.8";
            }

            await UpdatePlugin(beatSaberDirectory, beatSaberParameters, version);
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


