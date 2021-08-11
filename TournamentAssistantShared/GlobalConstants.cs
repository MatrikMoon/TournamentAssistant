using System;

namespace TournamentAssistantShared
{
    public static class GlobalConstants
    {
        public static string BeatsaverCDN => "https://cdn.beatsaver.com/"; 
        public static string BeatsaverAPI => "https://api.beatsaver.com/";
        public static string MapInfoByID => "https://api.beatsaver.com/maps/id/";
        public static string MapInfoByHash => "https://api.beatsaver.com/maps/hash/";
        public static string SystemPath => $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\TournamentAssistant";
        public static string Temp => $"{SystemPath}\\temp";
        public static string Cache => $"{SystemPath}\\cache";
        public static string SongData => $"{SystemPath}\\SongData";
        public static char[] IllegalCharacters => _illegalCharacters;


        
        private static char[] _illegalCharacters = { '>', '<', ':', '/', '\\', '\"', '|', '?', '*', ' ' };
    }
}
