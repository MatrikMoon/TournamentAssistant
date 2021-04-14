using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantUI.Misc;

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

        struct MockPlayer {
            public string Name { get; set; }
            public string UserId { get; set; }
        }

        /*List<Player> availableIds = new List<Player>(new Player[] {
                new Player()
                {
                    Name = "Astrella",
                    UserId = "2538637699496776"
                },
                new Player()
                {
                    Name = "AtomicX",
                    UserId = "76561198070511128"
                },
                new Player()
                {
                    Name = "Garsh",
                    UserId = "76561198187936410"
                },
                new Player()
                {
                    Name = "LSToast",
                    UserId = "76561198167393974"
                },
                new Player()
                {
                    Name = "CoolingCloset",
                    UserId = "76561198180044686"
                },
                new Player()
                {
                    Name = "miitchel",
                    UserId = "76561198301082541"
                },
                new Player()
                {
                    Name = "Shadow Ai",
                    UserId = "76561198117675143"
                },
                new Player()
                {
                    Name = "Silverhaze",
                    UserId = "76561198033166451"
                },
            });*/

        private MockPlayer GetRandomPlayer()
        {
            /*var ret = availableIds.ElementAt(0);
            availableIds.RemoveAt(0);
            return ret;*/
            return new MockPlayer()
            {
                Name = GenerateName(),
                UserId = $"{r.Next(int.MaxValue)}"
            };
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

        private void QRButton_Click(object sender, RoutedEventArgs e)
        {
            var navigationService = NavigationService.GetNavigationService(this);
            navigationService.Navigate(new QRPage());
        }

        private void Scoreboard_Connect_Click(object sender, RoutedEventArgs e)
        {
            var scoreboardClient = new ScoreboardClient("beatsaber.networkauditor.org", 10156);
            scoreboardClient.Start();

            scoreboardClient.PlayerInfoUpdated += Connection_PlayerInfoUpdated;
            scoreboardClient.PlaySongSent += MockPage_PlaySongSent;
        }

        private void MockPage_PlaySongSent()
        {
            Dispatcher.Invoke(() => ResetLeaderboardClicked(null, null));
        }

        List<Player> seenPlayers = new List<Player>();
        private void Connection_PlayerInfoUpdated(Player player)
        {
            Task.Run(async () =>
            {
                if (player.StreamDelayMs > 10) await Task.Delay((int)player.StreamDelayMs);

                lock (seenPlayers)
                {
                    if (!seenPlayers.Contains(player)) seenPlayers.Add(player);
                    else
                    {
                        var playerInList = seenPlayers.Find(x => x == player);
                        playerInList.Score = player.Score;
                        playerInList.Accuracy = player.Accuracy;
                    }

                    ScoreboardListBox.Dispatcher.Invoke(() =>
                    {
                        seenPlayers = seenPlayers.OrderByDescending(x => x.Accuracy).ToList();
                        ScoreboardListBox.Items.Clear();
                        for (var i = 0; i < 20 && i < seenPlayers.Count; i++) ScoreboardListBox.Items.Add($"{i + 1}: {seenPlayers[i].Name} \t {seenPlayers[i].Score} \t {seenPlayers[i].Accuracy.ToString("P", CultureInfo.InvariantCulture)}");
                    });


                    FlopListBox.Dispatcher.Invoke(() =>
                    {
                        seenPlayers = seenPlayers.OrderBy(x => x.Accuracy).ToList();
                        FlopListBox.Items.Clear();
                        var tempList = new List<Player>();
                        for (var i = 0; i < 20 && i < seenPlayers.Count; i++) tempList.Add(seenPlayers[i]);
                        tempList.Reverse();
                        for (var i = 0; i < 20 && i < tempList.Count; i++) FlopListBox.Items.Add($"{Math.Max(seenPlayers.Count - 20, 0) + (i + 1)}: {tempList[i].Name} \t {tempList[i].Score} \t {tempList[i].Accuracy.ToString("P", CultureInfo.InvariantCulture)}");
                    });
                }
            });
        }

        private void ResetLeaderboardClicked(object sender, RoutedEventArgs e)
        {
            seenPlayers.Clear();
            ScoreboardListBox.Items.Clear();
        }

        private async void QualsScoreButton_Clicked(object sender, RoutedEventArgs e)
        {
            var scores = ((await HostScraper.RequestResponse(new CoreServer
            {
                Address = "beatsaber.networkauditor.org",
                Port = 10156,
                Name = "Default Server"
            }, new Packet(new SubmitScore
            {
                Score = new Score
                {
                    EventId = Guid.Parse("333aa572-672c-4bf8-ae46-593faccb64da"),
                    Parameters = new GameplayParameters
                    {
                        Beatmap = new Beatmap
                        {
                            Characteristic = new Characteristic
                            {
                                SerializedName = "Standard"
                            },
                            Difficulty = SharedConstructs.BeatmapDifficulty.Easy,
                            LevelId = "custom_level_0B85BFB7912ADB4D6C42393AE350A6EAEF8E6AFC"
                        },
                        GameplayModifiers = new GameplayModifiers
                        {
                            Options = GameplayModifiers.GameOptions.NoFail
                        },
                        PlayerSettings = new PlayerSpecificSettings()
                    },
                    UserId = 76561198063268251,
                    Username = "Moon",
                    FullCombo = true,
                    _Score = int.Parse(ScoreBox.Text),
                    Color = "#ffffff"
                }
            }), typeof(ScoreRequestResponse), "Moon", 76561198063268251)).SpecificPacket as ScoreRequestResponse).Scores;

            ScoreboardListBox.Dispatcher.Invoke(() =>
            {
                var index = 0;
                ScoreboardListBox.Items.Clear();
                foreach (var score in scores) ScoreboardListBox.Items.Add($"{++index}: {score.Username} \t {score._Score}");
            });
        }
    }
}
