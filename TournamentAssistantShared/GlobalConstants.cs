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
        public static string AppDataPath => $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}{Path.DirectorySeparatorChar}TournamentAssistant{Path.DirectorySeparatorChar}";
        public static string AppDataTemp => $"{AppDataPath}temp{Path.DirectorySeparatorChar}";
        public static string AppDataLogs => $"{AppDataPath}logs{Path.DirectorySeparatorChar}";
        public static string AppDataCache => $"{AppDataPath}cache{Path.DirectorySeparatorChar}";
        public static string AppDataSongDataPath => $"{AppDataPath}SongData{Path.DirectorySeparatorChar}";
        public static string ServerDataPath => $"{Environment.CurrentDirectory}";
        public static string ServerDataTemp => $"{ServerDataPath}temp{Path.DirectorySeparatorChar}";
        public static string ServerDataLogs => $"{ServerDataPath}logs{Path.DirectorySeparatorChar}";
        public static string ServerDataCache => $"{ServerDataPath}cache{Path.DirectorySeparatorChar}";
        public static string ServerDataSongDataPath => $"{ServerDataPath}SongData{Path.DirectorySeparatorChar}";
        public static char[] IllegalPathCharacters => _illegalPathCharacters;
        public static char[] TrimJSON => _trimJSON;
        public static bool LogAllToFile { get; set; } = true;
        public static bool IsPlugin { get; set; } = false;
        public static bool IsServer { get; set; } = false;

        private static char[] _trimJSON = { '\"', '\\', ' ' };
        private static char[] _illegalPathCharacters = { '>', '<', ':', '/', '\\', '\"', '|', '?', '*', ' ' };
    }
}
