/**
 * Created by Moon on 8/5/2019
 * This houses various structures to be used by both plugin and panel
 */

namespace TournamentAssistantShared
{
    public static class Constants
    {
        public const string NAME = "TournamentAssistant";
        public const string PLUGIN_VERSION = "1.1.1";
        public const int PLUGIN_VERSION_CODE = 111;
        public const string WEBSOCKET_VERSION = "1.1.3";
        public const int WEBSOCKET_VERSION_CODE = 113;
        public const string TAUI_VERSION = "1.1.3";
        public const int TAUI_VERSION_CODE = 113;
        public const string SERVER_VERSION = "1.1.1";
        public const int SERVER_VERSION_CODE = 111;
        public const string MASTER_SERVER = "server.tournamentassistant.net";
        public const int MASTER_PORT = 8675;
        public const string Changelog =
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
            "0.4.9: Updated for new beatsaver api\n" +
            "0.5.0: Updated for Beat Saber 1.18.0, added bot messaging to server\n" +
            "0.5.1: Merge Danny's pull request\n" +
            "0.5.2: Revert score update method\n" +
            "0.5.3: Updated for 1.19.0, temporarily disabled Custom Notes integration as the plugin is not yet updated\n" +
            "0.5.4: Updated for 1.21.0\n" +
            "0.6.0: Major netcode rewrite/shift to protobuf\n" +
            "0.6.1: Merge Danny's ServerMessage changes for BSL, use BSMT for references, fix plugin update notification\n" +
            "0.6.2: Add new OSTs, support sending scores to non-player connections\n" +
            "0.6.3: Various fixes related to gathering server info, most noticeable when using an associated bot\n" +
            "0.6.4: Hotfix for two match deletion bugs and a version checking bug\n" +
            "0.6.5: Fix scraper implementation, sexify websocket server\n" +
            "0.6.6: Fix DLC loading, improve messaging regarding websocket server\n" +
            "0.6.7: New packet structure, score packets are now separate from player updates\n" +
            "0.6.8: Update for 1.25.0\n" +
            "0.6.9: Fixed Discord ids being stored as integers, add password entry prompt in-game\n" +
            "0.7.0: Some server synchronization fixes, for players and users that means more stability\n" +
            "0.7.3: Update for 1.29.1\n" +
            "0.7.4: Score update fix\n" +
            "0.7.5: Add ability for players to select Pro Mode";

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
