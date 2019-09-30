using CustomUI.BeatSaber;

namespace TournamentAssistant.UI.ViewControllers
{
    class MatchViewController : CustomViewController
    {
        private IPreviewBeatmapLevel _selectedLevel;

        protected override void DidActivate(bool firstActivation, ActivationType activationType)
        {
            base.DidActivate(firstActivation, activationType);
            if (firstActivation && activationType == ActivationType.AddedToHierarchy)
            {
                
            }
        }

        //This is here just in case the user quits the game after having connected to the server
        public void OnApplicationQuit()
        {
            Plugin.client.Shutdown();
        }

        public void SetData(IBeatmapLevel level)
        {
            _selectedLevel = level;
        }
    }
}
