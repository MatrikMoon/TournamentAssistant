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

        [Column("TAGuid")]
        public string TAGuid { get; set; }

        [Column("DiscordId")]
        public string DiscordId { get; set; }

        [Column("DiscordName")]
        public string DiscordName { get; set; }

        [Column("DiscordExtension")]
        public string DiscordExtension { get; set; }

        [Column("DiscordMention")]
        public string DiscordMention { get; set; }

        [Column("DiscordAvatar")]
        public string DiscordAvatar { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
