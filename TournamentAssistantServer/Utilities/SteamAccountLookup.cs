using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

/**
 * Created by Moon on 3/15/2025
 * Quick helper for reading steam profile XML
 */


namespace TournamentAssistantServer.Utilities
{
    public class SteamAccountLookup
    {
        public class SteamProfile
        {
            public string SteamID64 { get; set; }
            public string SteamID { get; set; }
            public string AvatarIcon { get; set; }
            public string AvatarMedium { get; set; }
            public string AvatarFull { get; set; }
            public string CustomURL { get; set; }
            public List<SteamGame> MostPlayedGames { get; set; }
        }

        public class SteamGame
        {
            public string GameName { get; set; }
            public string GameLink { get; set; }
            public string GameIcon { get; set; }
            public string GameLogo { get; set; }
            public string HoursPlayed { get; set; }
            public string HoursOnRecord { get; set; }
        }

        private static readonly HttpClient client = new HttpClient();
        private static readonly ConcurrentDictionary<string, SteamProfile> cache = new ConcurrentDictionary<string, SteamProfile>();

        public static async Task<SteamProfile> GetProfileFromSteamId64Async(string steamId64)
        {
            if (cache.TryGetValue(steamId64, out var cachedProfile))
            {
                return cachedProfile;
            }

            var url = $"https://steamcommunity.com/profiles/{steamId64}?xml=1";
            var xml = await client.GetStringAsync(url);
            var profile = ParseProfile(xml);

            cache[steamId64] = profile;
            return profile;
        }

        private static SteamProfile ParseProfile(string xml)
        {
            XDocument doc = XDocument.Parse(xml);

            SteamProfile profile = new SteamProfile
            {
                SteamID64 = doc.Root.Element("steamID64")?.Value,
                SteamID = doc.Root.Element("steamID")?.Value.Trim(),
                AvatarIcon = doc.Root.Element("avatarIcon")?.Value.Trim(),
                AvatarMedium = doc.Root.Element("avatarMedium")?.Value.Trim(),
                AvatarFull = doc.Root.Element("avatarFull")?.Value.Trim(),
                CustomURL = doc.Root.Element("customURL")?.Value.Trim(),
                MostPlayedGames = doc.Root
                    .Element("mostPlayedGames")?
                    .Elements("mostPlayedGame")
                    .Select(game => new SteamGame
                    {
                        GameName = game.Element("gameName")?.Value.Trim(),
                        GameLink = game.Element("gameLink")?.Value.Trim(),
                        GameIcon = game.Element("gameIcon")?.Value.Trim(),
                        GameLogo = game.Element("gameLogo")?.Value.Trim(),
                        HoursPlayed = game.Element("hoursPlayed")?.Value.Trim(),
                        HoursOnRecord = game.Element("hoursOnRecord")?.Value.Trim()
                    })
                    .ToList()
            };

            return profile;
        }
    }
}
