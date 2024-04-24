// Define the path to the existing executable
using System.Diagnostics;

if (args.Length <= 0)
{
    Console.WriteLine("You shouldn't be seeing this, but anyway, you must provide a parameter to this program which is the path of the calling executable");
    return;
}

Console.WriteLine(args[0]);

var existingPath = args[0];

// Define the URL to download the new executable
var url = "http://tournamentassistant.net/taui.exe";

// Define the path where the new file will be saved
var newPath = $"{args[0]}_update";

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
                Console.WriteLine("Existing file deleted.");
            }
            else
            {
                fileDeleted = true; // File does not exist, no need to delete
                Console.WriteLine("No existing file found to delete.");
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
        Console.WriteLine("Failed to delete the existing file after 5 seconds.");
        return; // Exit if the file cannot be deleted
    }


    // Download the new executable using HttpClient
    using (var client = new HttpClient())
    using (var fileStream = new FileStream(newPath, FileMode.CreateNew))
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var contentStream = await response.Content.ReadAsStreamAsync();
        await contentStream.CopyToAsync(fileStream);
        Console.WriteLine("New file downloaded.");
    }

    // Rename and execute the downloaded file
    File.Move(newPath, existingPath);
    Process.Start(existingPath);
    Console.WriteLine("New file executed.");
}
catch (Exception ex)
{
    // Handle any errors that might have occurred
    Console.WriteLine("An error occurred: " + ex.Message);
}