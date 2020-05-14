using HMUI;
using System;

namespace TournamentAssistant.UI.FlowCoordinators
{
    interface IFinishableFlowCoordinator
    {
        event Action DidFinishEvent;
    }
}
