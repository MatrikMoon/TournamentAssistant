using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.player-list-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\player-list-view.bsml")]
    internal class PlayerListView : BSMLAutomaticViewController
    {

    }
}