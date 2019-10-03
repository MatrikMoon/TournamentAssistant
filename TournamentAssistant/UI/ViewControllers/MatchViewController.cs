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

        public void SetData(IBeatmapLevel level)
        {
            _selectedLevel = level;
        }
    }
}
