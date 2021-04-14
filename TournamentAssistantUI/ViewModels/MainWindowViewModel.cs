using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;

namespace TournamentAssistantUI.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ICommand ConnectAsCoordinatorButtonPressed { get; }
        //public ICommand ConnectAsStreamPlayerButtonPressed { get; } //Not implemented yet
        public ICommand DirectConnectButtonPressed { get; }
        public ICommand ConnectToScrapedButtonPressed { get; }
        private bool _ConnectScraped = true;
        private bool _DirectConnect = false;
        private bool _IsConnecting = false;
        private bool _IsScrapingHosts = true;
        private bool _ScrapeProgressUnknown = true;
        private bool _ConnectScrapedButtonsActive = false;
        private bool _FinishedScraping = false;
        private int _ScrapeProgress = 0;
        private string _LoadingText = "Scraping Master Host...";
        private string? _DirectConnectIPDomainText;
        private string? _DirectConnectPortText;
        private ScrapedServersViewModel? _SelectedHost;
        public Dictionary<CoreServer, State>? ScrapedInfo { get; set; }
        public ObservableCollection<ScrapedServersViewModel> ScrapedHosts { get; } = new();
        Config config = new($"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\TournamentAssistantUIConfig.json"); //Lets stash this away, the user doesn't need to interact with it
        public bool ConnectScrapedButtonsActive
        {
            get => _ConnectScrapedButtonsActive;
            set => this.RaiseAndSetIfChanged(ref _ConnectScrapedButtonsActive, value);
        }
        public bool ConnectScraped
        {
            get => _ConnectScraped;
            set => this.RaiseAndSetIfChanged(ref _ConnectScraped, value);
        }
        public bool DirectConnect
        {
            get => _DirectConnect;
            set => this.RaiseAndSetIfChanged(ref _DirectConnect, value);
        }
        public bool IsScrapingHosts
        {
            get => _IsScrapingHosts;
            set => this.RaiseAndSetIfChanged(ref _IsScrapingHosts, value);
        }
        public bool ScrapeProgressUnknown
        {
            get => _ScrapeProgressUnknown;
            set => this.RaiseAndSetIfChanged(ref _ScrapeProgressUnknown, value);
        }
        public bool FinishedScraping
        {
            get => _FinishedScraping;
            set => this.RaiseAndSetIfChanged(ref _FinishedScraping, value);
        }
        public bool IsConnecting
        {
            get => _IsConnecting;
            set => this.RaiseAndSetIfChanged(ref _IsConnecting, value);
        }
        public int ScrapeProgress
        {
            get => _ScrapeProgress;
            set => this.RaiseAndSetIfChanged(ref _ScrapeProgress, value);
        }
        public string LoadingText
        {
            get => _LoadingText;
            set => this.RaiseAndSetIfChanged(ref _LoadingText, value);
        }
        public string? DirectConnectIPDomainText
        {
            get => _DirectConnectIPDomainText;
            set => this.RaiseAndSetIfChanged(ref _DirectConnectIPDomainText, value);
        }
        public string? DirectConnectPortText
        {
            get => _DirectConnectPortText;
            set => this.RaiseAndSetIfChanged(ref _DirectConnectPortText, value);
        }
        public ScrapedServersViewModel? SelectedHost
        {
            get => _SelectedHost;
            set => this.RaiseAndSetIfChanged(ref _SelectedHost, value);
        }
        public MainWindowViewModel()
        {
            ConnectAsCoordinatorButtonPressed = ReactiveCommand.Create(() =>
            {
                //If no selection is made and we are connecting a scraped server, return. I'll actually make a dialog for this later, since this is more likely to happen, so a dialog is needed.
                if (SelectedHost == null && ConnectScraped) return;

                //If not text is entered and we are connection to IP/Port directly, return. I'll also make a dialog later.
                if (DirectConnectIPDomainText == null && DirectConnect) return;
                if (DirectConnectPortText == null && DirectConnect) return;

                
            });

            /*ConnectAsStreamPlayerButtonPressed = ReactiveCommand.Create(() =>
            {
                //Not implemented yet
            });*/

            DirectConnectButtonPressed = ReactiveCommand.Create(() =>
            {
                ConnectScraped = false;
                DirectConnect = true;
            });

            ConnectToScrapedButtonPressed = ReactiveCommand.Create(() =>
            {
                ConnectScraped = true;
                DirectConnect = false;
            });


            Task.Run(ScrapeHostsAsync);
        }

        public void ConnectToServer(CoreServer server, string username, string password)
        {
            //Show our connecting dialog
            IsConnecting = true;
            ConnectScraped = false;
            DirectConnect = false;
            LoadingText = $"Connecting to {server.Name}";
            SystemClient Client = new(server.Address, server.Port, username, Connect.ConnectTypes.Coordinator, "0", password);
            Client.ConnectedToServer += Client_ConnectedToServer;
            Client.FailedToConnectToServer += Client_FailedToConnectToServer;
            Client.ServerDisconnected += Client_ServerDisconnected;
            Client.Start();
        }

        private void Client_ServerDisconnected()
        {
            throw new NotImplementedException();
        }

        private void Client_FailedToConnectToServer(ConnectResponse obj)
        {
            throw new NotImplementedException();
        }

        private void Client_ConnectedToServer(ConnectResponse obj)
        {
            throw new NotImplementedException();
        }

        public async Task ScrapeHostsAsync()
        {
            IsScrapingHosts = true;
            FinishedScraping = false;

            //Commented out is the code that makes this operate as a mesh network
            /*ScrapedInfo = (await HostScraper.ScrapeHosts(config.GetHosts(), "Scraper", 10, onInstanceComplete: OnIndividualInfoScraped))
                .Where(x => x.Value != null)
                .ToDictionary(s => s.Key, s => s.Value);*/

            //Clear the saved hosts so we don't have stale ones clogging us up
            config.SaveHosts(Array.Empty<CoreServer>());

            //This code will make the network operate as a hub and spoke network, since networkauditor.org is the domain of the master server
            //In essense, we just scrape the master server now.
            LoadingText = "Scraping Master Host...";
            ScrapedInfo = (await HostScraper.ScrapeHosts(config.GetHosts().Where(x => x.Address.Contains("networkauditor")).ToArray(), "Coordinator Panel", 10, onInstanceComplete: OnIndividualInfoScraped))
                .Where(x => x.Value != null)
                .ToDictionary(s => s.Key, s => s.Value);

            //Since we're scraping... Let's save the data we learned about the hosts while we're at it
            var scrapedHosts = config.GetHosts().Union(ScrapedInfo.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts)).ToList();
            config.SaveHosts(scrapedHosts.ToArray());

            //I need the state of each server to check if they are password protected or not. Also works as a check if they are online, master server sometimes (most times) has old entries.
            LoadingText = "Scraping Hosts...";
            ScrapeProgressUnknown = true; //If we are starting a new scrape we don't know how many hosts we are actually scraping, so lets make the progress bar reflect that
            ScrapedInfo = (await HostScraper.ScrapeHosts(scrapedHosts.ToArray(), "Coordinator Panel", 10, onInstanceComplete: OnIndividualInfoScraped))
                .Where(x => x.Value != null)
                .ToDictionary(s => s.Key, s => s.Value);

            OnInfoScraped();
        }

        private void OnInfoScraped()
        {
            //Lets actually show the list with the selection, and activate our buttons
            IsScrapingHosts = false;
            FinishedScraping = true;
            ConnectScrapedButtonsActive = true;

            //Iritating null reference, so lets just return when that happens (Let me tell you a secret, it won't happen, well unless our dear user is offline, but thats not my problem to fix)
            //And yes I could make a dialog box, I just don't wanna make one, feel free to make one if you want :)
            if (ScrapedInfo == null) return;

            //Fill the selection with hosts that we managed to make a connection to
            foreach (var scrapedItem in ScrapedInfo)
            {
                ScrapedServersViewModel Host = new();
                Host.Name = scrapedItem.Key.Name;
                Host.Address = $"{scrapedItem.Key.Address}:{scrapedItem.Key.Port}";
                Host.IsPasswordProtected = scrapedItem.Value.ServerSettings.Password != string.Empty || scrapedItem.Value.ServerSettings.Password != "";
                ScrapedHosts.Add(Host);
            }
        }

        //Copy-pasta from moon's plugin code
        private void OnIndividualInfoScraped(CoreServer host, State state, int count, int total) => UpdateScrapeCount(count, total);

        private void UpdateScrapeCount(int count, int total)
        {
            //If this method is called then we know where in the scrape progress we are, so lets make the progress bar reflect that
            if (ScrapeProgressUnknown) ScrapeProgressUnknown = false;
            ScrapeProgress = Decimal.ToInt32(Decimal.Multiply(Decimal.Divide(count, total), 100));
        }
    }
}
