/**
 * Created by Moon on 8/5/2019
 * This houses various structures to be used by both plugin and panel
 */

namespace TournamentAssistantShared
{
    public static class SharedConstructs
    {
        public const string Name = "TournamentAssistant";
        public const string Version = "0.3.2";
        public const int VersionCode = 032;
        public static string Changelog =
            "0.0.1: Begin assembling UI for coordinator panels\n" +
            "0.1.1: Implemented versioning system\n" +
            "0.1.2: Fixed song download bug\n" +
            "0.1.3: Recreated song detail view, refactored tournament flowcoordinator into room flowcoordinator, added match destroying / player leaving to back button on TournamentAssistant side\n" +
            "0.1.4: Added Teams\n" +
            "0.1.5: Reorganized workflow, baby-proofed server disconnections\n" +
            "0.1.6: Updated for QR Sync\n" +
            "0.1.7: Bugfixes\n" +
            "0.1.8: Bugfixes\n" +
            "0.1.9: Re-added no-arrows\n" +
            "0.2.0: Prevent players from pausing, added mod list grabber\n" +
            "0.2.1: BeatKhana charity event version\n" +
            "0.2.2: Bugfixes, added DisableFail\n" +
            "0.2.5: Added Banned Mods checking, bugfixes\n" +
            "0.2.8: Fixed server config overwriting, added IPV6 support, behind-the-scenes work on Qualifiers\n" +
            "0.3.0: Finished implementing qualifiers and decentralized network\n" +
            "0.3.1: (skipped due to github issue)\n" +
            "0.3.2: Added downloading from custom hosts, qualifier event settings\n";
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
