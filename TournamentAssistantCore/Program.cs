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

            /*config.SaveTeams(new TournamentAssistantShared.Models.Team[]
            {
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "VOC"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Team Friction"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "HahaBall"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "The samich sled"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "5Head and Boomer"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Bandoot Fanclub"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Yallah Abfahrt"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Funny Fish :)"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "sjiep en sjedoo"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "peepoBrexit"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "hmmmm ummm maybe uhhhh"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Omedan"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Dazer"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Carl"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "The Slicers"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "wdg 12 yr olds"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Saber O's"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "NIGHT RAID SHADOW LEGENDS"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "doofenshmirtz incorporated"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "The Vagabonds"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Sabre Aerodynamica"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "boops boops"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Bacon & Beans (B&B)"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "The Skillgaps"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "zil's cronies"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "French Baguettes"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "the chosen ones"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Cube Slicing Task Force"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "SAUFEEEN"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Code_904"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "ToastGang"
                },
                new TournamentAssistantShared.Models.Team()
                {
                    Guid = Guid.NewGuid().ToString(),
                    Name = "Computericer"
                },
            });*/

            Console.ReadLine();
        }
    }
}
