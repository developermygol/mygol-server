using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace webapi.Models.Db
{
    [Table("matchplayersnotices")]
    public class MatchPlayerNotice
    {
        [ExplicitKey] public long IdNotice { get; set; }
        [ExplicitKey] public long IdMatch { get; set; }
        [ExplicitKey] public long IdPlayer { get; set; }
        [ExplicitKey] public long IdTeam { get; set; }
        public long IdUser { get; set; }
        public long IdDay { get; set; }
        public bool Accepted { get; set; }
        public DateTime AcceptedDate { get; set; } // UtcNow

        [Write(false)] public Player Player { get; set; }
        [Write(false)] public Notice Notice { get; set; }
    }
}
