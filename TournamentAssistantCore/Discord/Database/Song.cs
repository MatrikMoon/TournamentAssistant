using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantCore.Discord.Database
{
    [Table("Songs")]
    public class Song
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Name")]
        public string Name { get; set; }
        
        [Column("EventId")]
        public string EventId { get; set; }

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

        [Column("Old")]
        public bool Old { get; set; }
    }
}
