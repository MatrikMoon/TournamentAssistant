using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TournamentAssistantUI.ViewModels
{
    public class MainMenuViewModel : ReactiveObject, IRoutableViewModel
    {
        // Reference to IScreen that owns the routable view model.
        public IScreen HostScreen { get; }
        // Unique identifier for the routable view model.
        public string UrlPathSegment { get; } = Guid.NewGuid().ToString().Substring(0, 5);

        public ICommand ConnectAsCoordinator { get; }
        public ICommand ConnectAsOverlay { get; }
        public ICommand OpenSettings { get; }
        public ICommand QuitApp { get; }

        public MainMenuViewModel(IScreen screen)
        {
            HostScreen = screen;
            ConnectAsCoordinator = ReactiveCommand.Create(() =>
            {
                
            });

            ConnectAsOverlay = ReactiveCommand.Create(() =>
            {

            });

            OpenSettings = ReactiveCommand.Create(() =>
            {

            });

            QuitApp = ReactiveCommand.Create(() =>
            {
                Environment.Exit(0);
            });
        }
    }
}
