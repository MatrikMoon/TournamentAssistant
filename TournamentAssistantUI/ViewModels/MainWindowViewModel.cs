using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantUI.Models;
using TournamentAssistantUI.Views;

namespace TournamentAssistantUI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        //Lets stash the config away, the user doesn't need to interact with it
        public static readonly string configPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\TournamentAssistantUI";
        public ICommand ConnectAsCoordinator { get; }
        public ICommand ConnectAsOverlay { get; }
        public ICommand OpenSettings { get; }
        public ICommand QuitApp { get; }
        internal Interaction<ConnectWindowViewModel, SystemClient> ConnectDialog { get; }
        public SystemClient Client;

        public MainWindowViewModel()
        {
            ConnectDialog = new Interaction<ConnectWindowViewModel, SystemClient>();

            ConnectAsCoordinator = ReactiveCommand.Create(async () =>
            {
                var ConnectWindow = new ConnectWindowViewModel();
                Client = await ConnectDialog.Handle(ConnectWindow);
            });

            ConnectAsOverlay = ReactiveCommand.Create(() =>
            {

            });

            OpenSettings = ReactiveCommand.Create(() =>
            {

            });

            QuitApp = ReactiveCommand.Create(() =>
            {

            });
        }
    }
}
