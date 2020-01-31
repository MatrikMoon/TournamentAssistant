using System.Windows.Controls;
using TournamentAssistantUI.BeatSaver;

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for SongEntry.xaml
    /// </summary>
    public partial class SongListEntry : UserControl
    {
        public Song Song { get; set; }

        public SongListEntry()
        {
            InitializeComponent();
        }
    }
}
