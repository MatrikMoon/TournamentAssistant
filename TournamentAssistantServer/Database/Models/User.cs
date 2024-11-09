using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("Users")]
    public class User
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Guid")]
        public string Guid { get; set; }

        [Column("Name")]
        public string Name { get; set; }

        [Column("OwnerDiscordId")]
        public string OwnerDiscordId { get; set; }

        [Column("Token")]
        public string Token { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
