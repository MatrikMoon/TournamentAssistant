using IPA;
using TournamentAssistantShared;

/**
 * Created by Moon on 8/5/2019
 * Base plugin class for the TournamentAssistant plugin
 * Intended to be the player-facing UI for tournaments, where
 * players' games can be handled by their match coordinators
 */

namespace TournamentAssistant
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        public string Name => SharedConstructs.Name;
        public string Version => SharedConstructs.Version;

        [OnEnable]
        public void OnEnable()
        {
            Config config = new();
            // TODO: Add to container
        }

        [OnDisable]
        public void OnDisable()
        {

        }
    }
}