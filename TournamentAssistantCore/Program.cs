using System;
using TournamentAssistantShared;

namespace TournamentAssistantCore
{
    class Program
    {
        public IConnection Connection { get; }

        static void Main(string[] args)
        {
            new SystemHost().StartHost();

            var config = new Config("serverConfig.json");
            var botToken = config.GetString("botToken");
            
            /*if (!string.IsNullOrEmpty(botToken))
            {
                var qualsBot = new QualifierBot(botToken);
                qualsBot.Start();
            }*/

            /*config.SaveTeams(new TournamentAssistantShared.Models.Team[]
            {
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Team One"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Team Green"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Team Spicy"
                },
            });*/

            Console.ReadLine();
        }
    }
}
