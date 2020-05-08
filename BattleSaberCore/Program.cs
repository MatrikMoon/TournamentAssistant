using System;
using BattleSaberShared.Discord;
using BattleSaberShared;

namespace BattleSaberCore
{
    class Program
    {
        public IConnection Connection { get; }

        static void Main(string[] args)
        {
            new BattleSaberHost().StartHost();

            var config = new Config("serverConfig.json");
            var botToken = config.GetString("botToken");
            
            if (!string.IsNullOrEmpty(botToken))
            {
                var qualsBot = new QualifierBot(botToken);
                qualsBot.Start();
            }

            /*config.SaveTeams(new BattleSaberShared.Models.Team[]
            {
                new BattleSaberShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Team One"
                },
                new BattleSaberShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Team Green"
                },
                new BattleSaberShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Team Spicy"
                },
            });*/

            Console.ReadLine();
        }
    }
}
