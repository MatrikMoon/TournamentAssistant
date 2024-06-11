using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("AuthorizedUsers")]
    public class AuthorizedUser
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Guid")]
        public string Guid { get; set; }

        [Column("TournamentId")]
        public string TournamentId { get; set; }

        [Column("DiscordId")]
        public string DiscordId { get; set; }

        [Column("PermissionFlags")]
        public int PermissionFlags { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
