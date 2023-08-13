using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

/* 
 * We have some extra fields here which could be considered unnecessary, but they're left
 * here intentionally. For example: Characteristic, BeatmapDifficulty, GameOptions, PlayerOptions
 * Technically, we could grab these by looking up the map id, but they're left here as a
 * second sanity check for score vailidity. For example, if a score is submitted with different
 * modifiers, it should show up here... Maybe they'll get removed eventually, but for now they stay.
 */

namespace TournamentAssistantServer.Database.Models
{
    [Table("Scores")]
    public class Score
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("MapId")]
        public string MapId { get; set; }

        [Column("EventId")]
        public string EventId { get; set; }

        [Column("LevelId")]
        public string LevelId { get; set; }

        [Column("PlatformId")]
        public string PlatformId { get; set; }

        [Column("Username")]
        public string Username { get; set; }

        [Column("Score")]
        public int _Score { get; set; }

        [Column("FullCombo")]
        public bool FullCombo { get; set; }

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
