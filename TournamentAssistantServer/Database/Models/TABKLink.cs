using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("TournamentLinks")]
    public class TABKLink
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("TournamentId")]
        public string TournamentId { get; set; }

        [Column("BeatKhanaTournamentGuid")]
        public string BeatKhanaTournamentGuid { get; set; }
    }
}
