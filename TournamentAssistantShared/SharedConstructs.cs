/**
 * Created by Moon on 8/5/2019
 * This houses various structures to be used by both plugin and panel
 */

namespace TournamentAssistantShared
{
    public static class SharedConstructs
    {
        public const string Name = "TournamentAssistant";
        public const string Version = "0.4.9";
        public const int VersionCode = 049;
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
            "0.3.2: Added downloading from custom hosts, qualifier event settings\n" +
            "0.3.3: Version bump for 0.12.2\n" +
            "0.3.4: Fixed QR codes, BattleSaber,\"Gathering Data\" bug\n" +
            "0.3.5: Fixed Oculus bug\n" +
            "0.3.6: Changed to hub and spoke style network, bump TAUI version, fix accuracy for overlay\n" +
            "0.3.7: Added password support and disabled score submission when nofail is on\n" +
            "0.3.8: Fixed qualifier flow coordinator lock-in, partially fixed custom leaderboards\n" +
            "0.4.0: Version bump to 1.13.2, merged websocket server, fixed quals leaderboard, re-mesh-networked for event scraping purposes\n" +
            "0.4.1: Fixed a few quals ui bugs, merged player settings page\n" +
            "0.4.2: Bump version number, stream sync fix, alpha bot-notification readded\n" +
            "0.4.3: Version bump to 1.13.4\n" +
            "0.4.4: Fixed modifier bug\n" +
            "0.4.5: Merged Arimodu changes: Added direct connect, changelog, inspirational quotes, server auto-updater. Bot updates: added automatically updating leaderboard message for qualifiers. Plugin updates: Re-added anti-fail, clients no longer save the server list. Coordinator/Plugin updates: Re-enable custom-notes-on-stream toggle\n" +
            "0.4.6: Fixed Qualifiers not showing up in list, added \"delayed start\" option for players concerned about not being able to use AutoPause, fixed qualifier event creation / song add bugs\n" +
            "0.4.7: Fixed Qualifier song add bug\n" +
            "0.4.8-beta: Updated plugin for Beat Saber 1.16.1\n" +
            "0.4.9: Updated for new beatsaver api";

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
