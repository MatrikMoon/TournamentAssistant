using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TournamentAssistantShared;
using TournamentAssistantShared.SimpleJSON;

namespace TournamentAssistantCore
{
    class SystemHost
    {
        public static IConnection Connection;
        public static AutoResetEvent MainThreadStop = new(false);


        static async Task Main(string[] args)
        {
            Logger logger = new(Logger.InstanceType.Server);

            var result = await GetContributorsAsync();
            string contributors = result[0];
            result.RemoveAt(0);

            foreach (var item in result)
                contributors += $", {item}";

            Console.WriteLine($"-----------------------------------------------------------------------------------------------");
            Console.WriteLine($"{SharedConstructs.Name}\n");
            Console.WriteLine($"Version: {SharedConstructs.Version}");
            Console.WriteLine($"Contributors: {contributors}");
            Console.WriteLine($"-----------------------------------------------------------------------------------------------\n");

            Connection = new SystemServer(args.Length > 0 ? args[0] : null);
            (Connection as SystemServer).Start();

            _ = MainThreadStop.WaitOne();

            logger.LoggerThread.Abort(); //Close the logger properly
        }

        //Another very sloppy implementation xd
        public static async Task<List<string>> GetContributorsAsync()
        {
            HttpClientHandler httpClientHandler = new HttpClientHandler();
            httpClientHandler.AllowAutoRedirect = false;

            using var client = new HttpClient(httpClientHandler);
            client.DefaultRequestHeaders.Add("user-agent", $"{SharedConstructs.Name}-{SharedConstructs.Version}");

            var response = await client.GetAsync($"https://api.github.com/repos/MatrikMoon/TournamentAssistant/contributors");
            var result = JSON.Parse(await response.Content.ReadAsStringAsync());

            List<string> contributors = new();

            foreach (var item in result)
                contributors.Add(item.Value["login"]);

            return contributors;
        }
    }
}
