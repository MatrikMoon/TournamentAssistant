using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using ReactiveUI;
using TournamentAssistantUI.ViewModels;

namespace TournamentAssistantUI.Views
{
    public class MainMenu : ReactiveUserControl<MainMenuViewModel>
    {
        public MainMenu()
        {
            this.WhenActivated(disposables => { });
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
