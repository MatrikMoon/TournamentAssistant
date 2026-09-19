using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("GlobalConfiguration")]
    public class GlobalConfiguration
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("FullAccessDiscordIds")]
        public string FullAccessDiscordIds { get; set; }

        [Column("EndpointManagerDiscordIds")]
        public string EndpointManagerDiscordIds { get; set; }
    }
}
