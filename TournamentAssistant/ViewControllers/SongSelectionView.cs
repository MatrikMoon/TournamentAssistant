using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.song-selection-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\song-selection-view.bsml")]
    internal class SongSelectionView : BSMLAutomaticViewController
    {

    }
}