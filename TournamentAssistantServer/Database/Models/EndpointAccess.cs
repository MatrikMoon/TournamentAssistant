using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("EndpointAccess")]
    public class EndpointAccess
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("EndpointId")]
        public string EndpointId { get; set; }

        [Column("WebsocketEnabled")]
        public bool WebsocketEnabled { get; set; } = true;

        [Column("RestEnabled")]
        public bool RestEnabled { get; set; } = true;

        [Column("PlayerEnabled")]
        public bool PlayerEnabled { get; set; } = true;
    }
}
