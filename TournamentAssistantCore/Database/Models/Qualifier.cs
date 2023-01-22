﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TournamentAssistantCore.Database.Models
{
    [Table("Qualifiers")]
    public class Qualifier
    {
        [Column("ID")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Key]
        public ulong ID { get; set; }

        [Column("Name")]
        public string Name { get; set; }

        [Column("Guid")]
        public string Guid { get; set; }

        [Column("TournamentId")]
        public string TournamentId { get; set; }

        [Column("GuildId")]
        public ulong GuildId { get; set; }

        [Column("GuildName")]
        public string GuildName { get; set; }

        [Column("InfoChannelId")]
        public ulong InfoChannelId { get; set; }

        [Column("InfoChannelName")]
        public string InfoChannelName { get; set; }

        [Column("LeaderboardMessageId")]
        public ulong LeaderboardMessageId { get; set; }

        [Column("Flags")]
        public int Flags { get; set; }

        [Column("Old")]
        public bool Old { get; set; }
    }
}
