using System;
using System.Collections.Generic;
using System.Text;

namespace TournamentAssistantShared
{
    public static class GlobalConstants
    {
        public const string BeatsaverCDN = "https://cdn.beatsaver.com/";
        public const string BeatsaverAPI = "https://api.beatsaver.com/";
        public const string MapInfoByID = "https://api.beatsaver.com/maps/id/";
        public const string MapInfoByHash = "https://api.beatsaver.com/maps/hash/";
        public static string SystemPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\TournamentAssistantUI";
        public static string Temp = $"{SystemPath}\\temp";
        public static string cache = $"{SystemPath}\\cache";
        public static string SongData = $"{SystemPath}\\SongData";
        public static char[] IllegalCharacters = { '>', '<', ':', '/', '\\', '\"', '|', '?', '*', ' ' };
    }
}
