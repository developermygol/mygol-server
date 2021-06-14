using contracts;
using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using Dapper.Contrib.Extensions;

namespace webapi.Models.Db
{
   
    public class MatchPlayer
    {
        [ExplicitKey] public long IdMatch { get; set; }
        [ExplicitKey] public long IdPlayer { get; set; }
        [ExplicitKey] public long IdTeam { get; set; }
        public long IdUser { get; set; }
        public long IdDay { get; set; }
        public int ApparelNumber { get; set; }
        public int Status { get; set; }         // MatchPlayerStatus
        public bool Titular { get; set; }
        public bool Captain { get; set; }

        [Write(false)] public Player Player { get; set; }
    }

    public enum MatchPlayerStatus
    {
        Called = 1,
        AcceptedCall = 2,
        DeclinedCall = 3,
        AcceptedCallAndPresent = 4,
        AcceptedCallAndNotPresent = 5,

        Played = 11,
        Injured = 12,
        Sanctioned = 13
    }
}
