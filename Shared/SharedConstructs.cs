using System;

/**
 * Created by Moon on 8/5/2019
 * This houses various structures to be used by both plugin and panel
 */

namespace TournamentAssistantShared
{
    public static class SharedConstructs
    {
        public const string Name = "TournamentAssistant";
        public const string Version = "0.0.1";
        public const int VersionCode = 001;
        public static string Changelog =
            "0.0.1: Begin assembling UI for coordinator panels\n";

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
