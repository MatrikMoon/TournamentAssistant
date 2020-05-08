using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BattleSaberCore.Shared.Discord.Database
{
    [Table("Players")]
    public class Player
    {
        [Column("ID", TypeName = "BIGINT")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public long ID { get; set; }

        [Column("GuildId")]
        public ulong GuildId { get; set; }

        [Column("UserId")]
        public ulong UserId { get; set; }

        [Column("DiscordName")]
        public string DiscordName { get; set; }

        [Column("DiscordExtension")]
        public string DiscordExtension { get; set; }

        [Column("DiscordMention")]
        public string DiscordMention { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
