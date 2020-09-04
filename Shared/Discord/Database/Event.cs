using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantShared.Discord.Database
{
    [Table("Events")]
    public class Event
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Name")]
        public string Name { get; set; }
        
        [Column("EventId")]
        public ulong EventId { get; set; }

        [Column("GuildId")]
        public ulong GuildId { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
