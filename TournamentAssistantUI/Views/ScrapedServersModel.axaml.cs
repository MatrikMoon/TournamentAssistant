using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace TournamentAssistantUI.Views
{
    public class ScrapedServersModel : UserControl
    {
        public ScrapedServersModel()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
