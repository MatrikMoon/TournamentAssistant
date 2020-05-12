using HMUI;
using System;

namespace TournamentAssistant.UI.FlowCoordinators
{
    abstract class FinishableFlowCoordinator : FlowCoordinator
    {
        public abstract event Action DidFinishEvent;
    }
}
