using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("Webhooks")]
    public class Webhook
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Guid")]
        public string Guid { get; set; }

        [Column("TournamentId")]
        public string TournamentId { get; set; }

        [Column("Url")]
        public string Url { get; set; }

        [Column("Triggers")]
        public long Triggers { get; set; }

        [Column("SigningSecret")]
        public string SigningSecret { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
