using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using HMUI;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.splash-screen-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\splash-screen-view.bsml")]
    internal class SplashScreenView : BSMLAutomaticViewController
    {
        [UIComponent("splash-background")]
        protected readonly ImageView _splashBackground = null!;

        [UIComponent("status-text")]
        protected readonly CurvedTextMeshPro _statusText = null!;

        private string _status = "No splash status has been set!";
        [UIValue("status")]
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                NotifyPropertyChanged();
            }
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);

            if (firstActivation)
            {
                _splashBackground.color = _splashBackground.color.ColorWithAlpha(0.5f);
            }
        }
    }
}