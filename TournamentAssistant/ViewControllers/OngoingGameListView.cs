using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.ongoing-game-list-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\ongoing-game-list-view.bsml")]
    internal class OngoingGameListView : BSMLAutomaticViewController
    {
        internal void ClearMatches()
        {

        }

        internal void AddMatches(Match[] matches)
        {

        }

        internal void AddMatch(Match match)
        {
            throw new NotImplementedException();
        }

        internal void RemoveMatch(Match match)
        {
            throw new NotImplementedException();
        }
    }
}