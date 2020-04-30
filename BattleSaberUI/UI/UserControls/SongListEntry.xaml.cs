using System.Windows.Controls;
using BattleSaberUI.BeatSaver;

namespace BattleSaberUI.UI.UserControls
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
