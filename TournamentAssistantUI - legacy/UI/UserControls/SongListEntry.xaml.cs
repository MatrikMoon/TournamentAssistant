using TournamentAssistantShared.BeatSaver;
using System.Windows.Controls;

namespace TournamentAssistantUI.UI.UserControls
{
    /// <summary>
    /// Interaction logic for SongEntry.xaml
    /// </summary>
    public partial class SongListEntry : UserControl
    {
        public DownloadedSong Song { get; set; }

        public SongListEntry()
        {
            InitializeComponent();
        }
    }
}
