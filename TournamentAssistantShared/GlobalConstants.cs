using System;
using System.IO;

namespace TournamentAssistantShared
{
    public static class GlobalConstants
    {
        //Rate limit is 10 requests / second, this constant defines how many times we can request in a second in miliseconds
        //Yes technically it defines how many download tasks we can start in a second by defining waiting time between those tasks
        //Not the most elegant soulution, but its good for now. Will revisit later to reflect actual amount of reuqests / second and calculate from that
        public static int BeatsaverRateLimit => 100;


        public static string ScoreSaberAPI => "https://new.scoresaber.com/api/";
        public static string BeatsaverCDN => "https://cdn.beatsaver.com/"; 
        public static string BeatsaverAPI => "https://api.beatsaver.com/";
        public static string MapInfoByID => "https://api.beatsaver.com/maps/id/";
        public static string MapInfoByHash => "https://api.beatsaver.com/maps/hash/";
        public static string SystemPath => $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}{Path.DirectorySeparatorChar}TournamentAssistant{Path.DirectorySeparatorChar}";
        public static string Temp => $"{SystemPath}temp{Path.DirectorySeparatorChar}";
        public static string Cache => $"{SystemPath}cache{Path.DirectorySeparatorChar}";
        public static string SongData => $"{SystemPath}SongData{Path.DirectorySeparatorChar}";
        public static char[] IllegalPathCharacters => _illegalPathCharacters;
        public static char[] TrimJSON => _trimJSON;



        private static char[] _trimJSON = { '\"', '\\', ' ' };
        private static char[] _illegalPathCharacters = { '>', '<', ':', '/', '\\', '\"', '|', '?', '*', ' ' };
    }
}
