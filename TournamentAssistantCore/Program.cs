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
            config.SaveBannedMods(new string[] { "IntroSkip", "AutoPauseStealth", "NoteSliceVisualizer", "SongChartVisualizer", "Custom Notes" });
            //var botToken = config.GetString("botToken");

            /*if (!string.IsNullOrEmpty(botToken))
            {
                var qualsBot = new QualifierBot(botToken);
                qualsBot.Start();
            }*/

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
