using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("Roles")]
    public class Role
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Guid")]
        public string Guid { get; set; }

        [Column("Name")]
        public string Name { get; set; }

        [Column("RoleId")]
        public string RoleId { get; set; }

        [Column("TournamentId")]
        public string TournamentId { get; set; }

        [Column("Permissions")]
        public string Permissions { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
