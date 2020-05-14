using TournamentAssistantUI.Misc;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Linq;

namespace TournamentAssistantUI.UI
{
    /// <summary>
    /// Interaction logic for MockClient.xaml
    /// </summary>
    public partial class MockPage : Page
    {
        private static Random r = new Random();

        private List<MockClient> mockPlayers;

        public MockPage()
        {
            InitializeComponent();
        }

        struct Player {
            public string Name { get; set; }
            public ulong UserId { get; set; }
        }

        List<Player> availableIds = new List<Player>(new Player[] {
                new Player()
                {
                    Name = "Astrella",
                    UserId = 2538637699496776
                },
                new Player()
                {
                    Name = "AtomicX",
                    UserId = 76561198070511128
                },
                new Player()
                {
                    Name = "Garsh",
                    UserId = 76561198187936410
                },
                new Player()
                {
                    Name = "LSToast",
                    UserId = 76561198167393974
                },
                new Player()
                {
                    Name = "CoolingCloset",
                    UserId = 76561198180044686
                },
                new Player()
                {
                    Name = "miitchel",
                    UserId = 76561198301082541
                },
                new Player()
                {
                    Name = "Shadow Ai",
                    UserId = 76561198117675143
                },
                new Player()
                {
                    Name = "Silverhaze",
                    UserId = 76561198033166451
                },
            });

        private Player GetRandomPlayer()
        {
            var ret = availableIds.ElementAt(0);
            availableIds.RemoveAt(0);
            return ret;
        }

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var clientCountValid = int.TryParse(ClientCountBox.Text, out var clientsToConnect);
            if (!clientCountValid) return;

            if (mockPlayers != null) mockPlayers.ForEach(x => x.Shutdown());
            mockPlayers = new List<MockClient>();

            var hostText = HostBox.Text.Split(':');

            for (int i = 0; i < clientsToConnect; i++)
            {
                var player = GetRandomPlayer();
                mockPlayers.Add(new MockClient(hostText[0], hostText.Length > 1 ? int.Parse(hostText[1]) : 10156, player.Name, player.UserId));
            }

            mockPlayers.ForEach(x => x.Start());
        }

        private void Disconnect_Click(object sender, RoutedEventArgs e)
        {
            mockPlayers.ForEach(x => x.Shutdown());
        }

        private static string GenerateName(int desiredLength = -1)
        {
            string[] consonants = { "b", "c", "d", "f", "g", "h", "j", "k", "l", "m", "l", "n", "p", "q", "r", "s", "sh", "zh", "t", "v", "w", "x" };
            string[] vowels = { "a", "e", "i", "o", "u", "ae", "y" };

            if (desiredLength < 0) desiredLength = r.Next(6, 20);

            string name = string.Empty;

            for (int i = 0; i < desiredLength; i++)
            {
                name += i % 2 == 0 ? consonants[r.Next(consonants.Length)] : vowels[r.Next(vowels.Length)];
                if (i == 0) name = name.ToUpper();
            }

            return name;
        }
    }
}
