using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using TournamentAssistantShared;
using TournamentAssistantShared.Models;
using TournamentAssistantShared.Models.Packets;
using TournamentAssistantUI.Models;
using TournamentAssistantUI.Views;


namespace TournamentAssistantUI.ViewModels
{
    public class ConnectWindowViewModel : MainWindowViewModel
    {
        public ICommand ConnectButtonPressed { get; }
        public ICommand BackButtonPressed { get; }
        private bool _IsScrapingHosts = true;
        private bool _ScrapeProgressUnknown = true;
        private bool _ConnectScrapedButtonsActive = false;
        private bool _FinishedScraping = false;
        private bool _IsPasswordProtected;
        private int _ScrapeProgress = 0;
        private string _LoadingText = "Scraping Master Host...";
        private string? _IPDomainText;
        private string? _PortText;
        private ScrapedServersModel? _SelectedHost;
        public Dictionary<CoreServer, State>? ScrapedInfo { get; set; }
        public ObservableCollection<ScrapedServersModel> ScrapedHosts { get; } = new();

        private Config hostConfig = new($"{configPath}\\HostConfig.json");
        internal Interaction<UsernamePasswordDialogViewModel, UsernamePasswordModel> UsernamePasswdDialog { get; }
        public LoadingDialog LoadingDialog = new();
        public SystemClient Client;

        public bool ConnectScrapedButtonsActive
        {
            get => _ConnectScrapedButtonsActive;
            set => this.RaiseAndSetIfChanged(ref _ConnectScrapedButtonsActive, value);
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
        public bool IsPasswordProtected
        {
            get => _IsPasswordProtected;
            set => this.RaiseAndSetIfChanged(ref _IsPasswordProtected, value);
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
        public string? IPDomainText
        {
            get => _IPDomainText;
            set => this.RaiseAndSetIfChanged(ref _IPDomainText, value);
        }
        public string? PortText
        {
            get => _PortText;
            set => this.RaiseAndSetIfChanged(ref _PortText, value);
        }
        public ScrapedServersModel? SelectedHost
        {
            get => _SelectedHost;
            set 
            {
                this.RaiseAndSetIfChanged(ref _SelectedHost, value);
                IPDomainText = value.ServerObjectReference.Address;
                PortText = value.ServerObjectReference.Port.ToString();
            }
        }
        public ConnectWindowViewModel()
        {
            UsernamePasswdDialog = new Interaction<UsernamePasswordDialogViewModel, UsernamePasswordModel>();
            LoadingDialog.ViewModel = new LoadingDialogViewModel();

            ConnectButtonPressed = ReactiveCommand.Create(() =>
            {
                if (SelectedHost != null && SelectedHost.Address == $"{IPDomainText}:{PortText}") ConnectScraped();
                else ConnectIP();
            });

            BackButtonPressed = ReactiveCommand.Create(() =>
            {
                
            });

            //Apparently config hadler breaks if the full path doesn't exist. Moon you should have a check in the constructor of the config for that. I'll add it later when I finish this
            if (!Directory.Exists(configPath)) Directory.CreateDirectory(configPath);

            Task.Run(ScrapeHostsAsync);
        }
        public async void ConnectScraped()
        {
            var credentialDialog = new UsernamePasswordDialogViewModel();
            CoreServer server = SelectedHost.ServerObjectReference;

            LoadingDialog.Show();
            LoadingDialog.ViewModel.LoadingText = $"Connecting to {server.Name}";
            LoadingDialog.ViewModel.Indeterminate = true;

            credentialDialog.IsPasswordProtected = IsPasswordProtected;
            var credentials = await UsernamePasswdDialog.Handle(credentialDialog);

            //If our dear user closes the dialog - Shouldn't be able to but task manager and alt + f4 are a thing so you never know - Users are dumb
            if (credentials is null) credentials = new UsernamePasswordModel();

            ConnectClient(server, credentials.Username, credentials.Password);
        }
        public async void ConnectIP()
        {
            var credentialDialog = new UsernamePasswordDialogViewModel();
            CoreServer server = new()
            {
                Address = IPDomainText.Trim(),
                Port = Int32.Parse(PortText.Trim()), //Assuming our user is not dumb, i'll probably add a check later if it is numeric
                Name = "Custom server"
            };

            //Show our connecting dialog
            LoadingDialog.Show();
            LoadingDialog.ViewModel.LoadingText = $"Connecting to {server.Name}";
            LoadingDialog.ViewModel.Indeterminate = true;

            var scrapedData = await ScrapeHostAsync(server);
            if (scrapedData.Value == null)
            {
                LoadingDialog.ViewModel.LoadingText = $"Failed to get {server.Name} info. Server might be offline";
                LoadingDialog.ViewModel.Indeterminate = false;
                for (int i = 0; i < 100; i++)
                {
                    LoadingDialog.ViewModel.Progress = i;
                    await Task.Delay(10);
                }
                return;
            }

            //We now know the servers actual name, so why not just change it
            server.Name = scrapedData.Value.ServerSettings.ServerName;
            LoadingDialog.ViewModel.LoadingText = $"Connecting to {server.Name}";

            //Lets ask for the username and password if needed
            IsPasswordProtected = scrapedData.Value.ServerSettings.Password != string.Empty;
            credentialDialog.IsPasswordProtected = IsPasswordProtected;
            var credentials = await UsernamePasswdDialog.Handle(credentialDialog);

            //If our dear user closes the dialog - Shouldn't be able to but task manager and alt + f4 is a thing so you never know - Users are dumb
            if (credentials is null) credentials = new UsernamePasswordModel();

            ConnectClient(server, credentials.Username, credentials.Password);
        }
        public void ConnectClient(CoreServer server, string username, string password)
        {
            if (password is null) password = string.Empty;
            Client = new(server.Address, server.Port, username, Connect.ConnectTypes.Coordinator, "0", password);
            Client.ConnectedToServer += Client_ConnectedToServer;
            //Client.FailedToConnectToServer += Client_FailedToConnectToServer;
            Client.Start();
        }
        public void Client_ConnectedToServer(ConnectResponse response)
        {
            LoadingDialog.Hide();
            //OnConnect(response);
        }
        public static async Task<KeyValuePair<CoreServer, State>> ScrapeHostAsync(CoreServer server)
        {
            CoreServer[] scrapeServer = { server };

            var ScrapedData = await HostScraper.ScrapeHosts(scrapeServer, "Coordinator Panel", 10);

            //We can assume theres only a single KeyValuePair in the array
            return ScrapedData.ToArray()[0];
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
            hostConfig.SaveHosts(Array.Empty<CoreServer>());

            //This code will make the network operate as a hub and spoke network, since networkauditor.org is the domain of the master server
            //In essense, we just scrape the master server now.
            LoadingText = "Scraping Master Host...";
            ScrapedInfo = (await HostScraper.ScrapeHosts(hostConfig.GetHosts().Where(x => x.Address.Contains("networkauditor")).ToArray(), "Coordinator Panel", 10, onInstanceComplete: OnIndividualInfoScraped))
                .Where(x => x.Value != null)
                .ToDictionary(s => s.Key, s => s.Value);

            //Since we're scraping... Let's save the data we learned about the hosts while we're at it
            var scrapedHosts = hostConfig.GetHosts().Union(ScrapedInfo.Values.Where(x => x.KnownHosts != null).SelectMany(x => x.KnownHosts)).ToList();
            hostConfig.SaveHosts(scrapedHosts.ToArray());

            //I need the state of each server to check if they are password protected or not. Also works as a check if they are online, master server sometimes (most times) has old entries.
            LoadingText = "Scraping Hosts...";
            ScrapeProgressUnknown = true;
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
            //I also know that this WILL break the dialog if unhandled, but again, not my problem to fix when our dear user is offline.
            if (ScrapedInfo == null) return;

            //Fill the selection with hosts that we managed to make a connection to (Sorted alphabetically)
            foreach (var scrapedItem in ScrapedInfo.ToList().OrderBy(x => x.Key.Name).ToList())
            {
                if (scrapedItem.Value == null) continue;
                ScrapedServersModel Host = new();
                Host.Name = scrapedItem.Key.Name;
                Host.Address = $"{scrapedItem.Key.Address}:{scrapedItem.Key.Port}";
                Host.IsPasswordProtected = scrapedItem.Value.ServerSettings.Password != string.Empty;
                Host.ServerObjectReference = scrapedItem.Key;
                ScrapedHosts.Add(Host);
            }
        }

        //Copy-pasta from moon's plugin code
        private void OnIndividualInfoScraped(CoreServer host, State state, int count, int total) => UpdateScrapeCount(count, total);

        private void UpdateScrapeCount(int count, int total)
        {
            //If this method is called then we know where in the scrape progress we are, so lets make the progress bar reflect that
            if (ScrapeProgressUnknown) ScrapeProgressUnknown = false;
            ScrapeProgress = decimal.ToInt32(decimal.Multiply(decimal.Divide(count, total), 100));
        }
    }
}
