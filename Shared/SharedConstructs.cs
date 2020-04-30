/**
 * Created by Moon on 8/5/2019
 * This houses various structures to be used by both plugin and panel
 */

namespace BattleSaberShared
{
    public static class SharedConstructs
    {
        public const string Name = "BattleSaber";
        public const string Version = "0.1.1";
        public const int VersionCode = 011;
        public static string Changelog =
            "0.0.1: Begin assembling UI for coordinator panels\n" +
            //Whoops
            "0.1.1: Implemented versioning system\n";

        public enum BeatmapDifficulty
        {
            Easy,
            Normal,
            Hard,
            Expert,
            ExpertPlus
        }
    }
}
