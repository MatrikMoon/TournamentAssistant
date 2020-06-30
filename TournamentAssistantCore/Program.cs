using System;
using TournamentAssistantShared;
using TournamentAssistantShared.Discord;

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

            //If there wasn't a token in the config, check the arguments
            if (string.IsNullOrEmpty(botToken) && args.Length > 0) botToken = args[0];

            //If we have a token, start a qualifier bot
            if (!string.IsNullOrEmpty(botToken))
            {
                var qualsBot = new QualifierBot(botToken: botToken);
                qualsBot.Start();
            }

            //config.SaveBannedMods(new string[] { "IntroSkip", "AutoPauseStealth", "NoteSliceVisualizer", "SongChartVisualizer", "Custom Notes" });
            /*config.SaveTeams(new TournamentAssistantShared.Models.Team[]
            {
                new TournamentAssistantShared.Models.Team()
                {
                    Id = Guid.NewGuid(),
                    Name = "Team One"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Id = Guid.NewGuid(),
                    Name = "Team Green"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Id = Guid.NewGuid(),
                    Name = "Team Spicy"
                },
            });*/

            Console.ReadLine();
        }
    }
}
