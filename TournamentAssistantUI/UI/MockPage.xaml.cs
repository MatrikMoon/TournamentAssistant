using TournamentAssistantUI.Misc;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

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

        private void Connect_Click(object sender, RoutedEventArgs e)
        {
            var clientCountValid = int.TryParse(ClientCountBox.Text, out var clientsToConnect);
            if (!clientCountValid) return;

            if (mockPlayers != null) mockPlayers.ForEach(x => x.Shutdown());
            mockPlayers = new List<MockClient>();

            var hostText = HostBox.Text.Split(':');

            for (int i = 0; i < clientsToConnect; i++) mockPlayers.Add(new MockClient(hostText[0], hostText.Length > 1 ? int.Parse(hostText[1]) : 10156, GenerateName()));

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
