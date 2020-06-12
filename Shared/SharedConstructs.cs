/**
 * Created by Moon on 8/5/2019
 * This houses various structures to be used by both plugin and panel
 */

namespace TournamentAssistantShared
{
    public static class SharedConstructs
    {
        public const string Name = "TournamentAssistant";
        public const string Version = "0.2.0";
        public const int VersionCode = 020;
        public static string Changelog =
            "0.0.1: Begin assembling UI for coordinator panels\n" +
            //Whoops
            "0.1.1: Implemented versioning system\n" + 
            "0.1.2: Fixed song download bug\n" +
            "0.1.3: Recreated song detail view, refactored tournament flowcoordinator into room flowcoordinator, added match destroying / player leaving to back button on TournamentAssistant side\n" +
            "0.1.4: Added Teams\n" +
            "0.1.5: Reorganized workflow, baby-proofed server disconnections\n" +
            "0.1.6: Updated for QR Sync\n" +
            "0.1.7: Bugfixes\n" +
            "0.1.8: Bugfixes\n" +
            "0.1.9: Re-added no-arrows\n" +
            "0.2.0: Prevent players from pausing, added mod list grabber";

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
