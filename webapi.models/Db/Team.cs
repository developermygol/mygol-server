using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    [DebuggerDisplay("{Name} ({Id})")]
    public class Team: BaseObject
    {
        public string Name { get; set; }
        public string KeyName { get; set; }
        public string LogoImgUrl { get; set; }
        public long IdField { get; set; }
        public long Status { get; set; }
        public long IdTactic { get; set; }
        public string LogoConfig { get; set; }
        public string ApparelConfig { get; set; }
        public string TeamImgUrl { get; set; }
        public string TeamImgUrl2 { get; set; }
        public string TeamImgUrl3 { get; set; }
        public DateTime? PrefTime { get; set; }
        public int IdGoalKeeper { get; set; }
        
        [Write(false)] public TeamPlayer TeamData { get; set; }     // Move this to Players.Teams. Only used in player details. TeamPlayer may have a Team field
        [Write(false)] public IEnumerable<Player> Players { get; set; } // Team players in the selected tournament
        [Write(false)] public IEnumerable<PlayDay> Days { get; set; }
        [Write(false)] public Tournament Tournament { get; set; }       // Tournament data for this particular query.
        [Write(false)] public IEnumerable<Tournament> Tournaments { get; set; }  // All tournaments the team is associatiated with.
        [Write(false)] public IEnumerable<Sponsor> Sponsors { get; set; }
        

        public override string Print()
        {
            return $"Team: id:{Id} keyName:'{KeyName}' name:'{Name}' idField:{IdField} ";
        }
    }

    public class TournamentTeam
    {
        [ExplicitKey] public long IdTeam { get; set; }
        [ExplicitKey] public long IdTournament { get; set; }
    }

    public class TeamGroup: BaseObject
    {
        public long IdTeam { get; set; }
        public long IdTournament { get; set; }
        public long IdStage { get; set; }
        public long IdGroup { get; set; }
        public int SequenceOrder { get; set; }
    }

    public enum TeamStatus
    {
        InscriptionRequested = 0,
        InscriptionDenied = 1,
        Inscribed = 2, 
        Disabled = 10
    }
}
