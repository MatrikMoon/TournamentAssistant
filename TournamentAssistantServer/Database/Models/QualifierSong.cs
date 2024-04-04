using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("QualifierSongs")]
    public class QualifierSong
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Guid")]
        public string Guid { get; set; }

        [Column("EventId")]
        public string EventId { get; set; }

        [Column("Name")]
        public string Name { get; set; }

        [Column("LevelId")]
        public string LevelId { get; set; }

        [Column("Characteristic")]
        public string Characteristic { get; set; }

        [Column("BeatmapDifficulty")]
        public int BeatmapDifficulty { get; set; }

        [Column("GameOptions")]
        public int GameOptions { get; set; }

        [Column("PlayerOptions")]
        public int PlayerOptions { get; set; }

        [Column("ShowScoreboard")]
        public bool ShowScoreboard { get; set; }

        [Column("Attempts")]
        public int Attempts { get; set; }

        [Column("DisablePause")]
        public bool DisablePause { get; set; }

        [Column("DisableFail")]
        public bool DisableFail{ get; set; }

        [Column("DisableScoresaberSubmission")]
        public bool DisableScoresaberSubmission { get; set; }

        [Column("DisableCustomNotesOnStream")]
        public bool DisableCustomNotesOnStream { get; set; }

        [Column("LeaderboardMessageId")]
        public string LeaderboardMessageId { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
