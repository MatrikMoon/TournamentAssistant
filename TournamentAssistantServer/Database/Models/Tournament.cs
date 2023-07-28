using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("Tournaments")]
    public class Tournament
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Guid")]
        public string Guid { get; set; }

        [Column("HashedPassword")]
        public string HashedPassword { get; set; }

        [Column("Name")]
        public string Name { get; set; }

        [Column("Image")]
        public string Image { get; set; }

        [Column("EnableTeams")]
        public bool EnableTeams { get; set; }

        [Column("ScoreUpdateFrequency")]
        public int ScoreUpdateFrequency { get; set; }

        [Column("BannedMods")]
        public string BannedMods { get; set; }

        [Column("ServerAddress")]
        public string ServerAddress { get; set; }

        [Column("ServerName")]
        public string ServerName { get; set; }

        [Column("ServerPort")]
        public string ServerPort { get; set; }

        [Column("ServerWebsocketPort")]
        public string ServerWebsocketPort { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
