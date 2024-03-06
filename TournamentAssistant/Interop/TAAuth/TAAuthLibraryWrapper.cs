﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;

namespace TournamentAssistant.Interop
{
    internal class TAAuthLibraryWrapper
    {
        public static void EnsureAuthLibraryExists()
        {
            var executingAssembly = Assembly.GetExecutingAssembly();
            var executingAssemblyDirectory = Path.GetDirectoryName(executingAssembly.Location);

            var destinationName = "TAAuth.dll";
            var destinationPath = $"{executingAssemblyDirectory}/../Libs/{destinationName}";
            var destinationFolder = Path.GetDirectoryName(destinationPath);

            //No need to check if it's here if it's already loaded
            var currentDomain = AppDomain.CurrentDomain;
            var loadedAssemblies = currentDomain.GetAssemblies();

            if (loadedAssemblies.Any(x => x.GetName().Name == "TAAuth"))
            {
                Console.WriteLine("Assembly already loaded...");
                return;
            }

            //No need to download if it's already there
            if (File.Exists(destinationPath))
            {
                var assembly = Assembly.LoadFrom(destinationPath);

                //If the assembly is valid and up to date
                if (assembly != null && assembly.GetName().Version.ToString() == "0.0.1.0")
                {
                    Console.WriteLine($"Assembly already exists, skipping download...");
                    return;
                }
            }

            //Create the Libs folder if it doesn't exist (for debugging, get off my case xD)
            Directory.CreateDirectory(destinationFolder);

            //Extract the library from resources and write it to the disk
            using var client = new WebClient();
            client.DownloadFile("http://tournamentassistant.net/TAAuth.dll", destinationPath);

            Assembly.LoadFrom(destinationPath);
        }

        public static string GetToken(string username, string platformId)
        {
            EnsureAuthLibraryExists();
            return TAAuthInterop.GetToken(username, platformId);
        }
    }
}
