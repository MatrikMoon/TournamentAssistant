﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantServer.Database.Models
{
    [Table("Qualifiers")]
    public class Qualifier
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Guid")]
        public string Guid { get; set; }

        [Column("Name")]
        public string Name { get; set; }

        [Column("Image")]
        public string Image { get; set; }

        [Column("TournamentId")]
        public string TournamentId { get; set; }

        [Column("InfoChannelId")]
        public string InfoChannelId { get; set; }

        [Column("InfoChannelName")]
        public string InfoChannelName { get; set; }

        [Column("Flags")]
        public int Flags { get; set; }

        [Column("Sort")]
        public int Sort { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
