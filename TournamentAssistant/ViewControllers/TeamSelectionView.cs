using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using System;
using System.Collections.Generic;
using TournamentAssistantShared.Models;

namespace TournamentAssistant.ViewControllers
{
    [ViewDefinition("TournamentAssistant.Views.team-selection-view.bsml")]
    [HotReload(RelativePathToLayout = @"..\Views\team-selection-view.bsml")]
    internal class TeamSelectionView : BSMLAutomaticViewController
    {
        public event Action<Team>? TeamSelected;

        internal void SetTeams(List<Team> teams)
        {
            throw new NotImplementedException();
        }
    }
}