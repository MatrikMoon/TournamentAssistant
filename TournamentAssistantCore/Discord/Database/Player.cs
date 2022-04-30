using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantCore.Discord.Database
{
    [Table("Players")]
    public class Player
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("GuildId")]
        public ulong GuildId { get; set; }

        [Column("DiscordId")]
        public ulong DiscordId { get; set; }

        [Column("ScoresaberId")]
        public ulong ScoresaberId { get; set; }

        [Column("DiscordName")]
        public string DiscordName { get; set; }

        [Column("DiscordExtension")]
        public string DiscordExtension { get; set; }

        [Column("DiscordMention")]
        public string DiscordMention { get; set; }

        [Column("Country")]
        public string Country { get; set; }

        [Column("Rank")]
        public int Rank { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
