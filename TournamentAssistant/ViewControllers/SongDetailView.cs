using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.song-detail-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\song-detail-view.bsml")]
    internal class SongDetailView : BSMLAutomaticViewController
    {
        
    }
}