using contracts;
using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using Dapper.Contrib.Extensions;

namespace webapi.Models.Db
{
    [Table("matches")]
    public class Match: BaseObject
    {
        public long IdField { get; set; }
        public long IdTournament { get; set; }
        public long IdStage { get; set; }
        public long IdGroup { get; set; }
        public long IdHomeTeam { get; set; }
        public long IdVisitorTeam { get; set; }
        public long IdDay { get; set; }
        public DateTime StartTime { get; set; }
        public int Duration { get; set; }
        public int Status { get; set; }         // MatchStatus
        public int HomeScore { get; set; }
        public int VisitorScore { get; set; }

        public string HomeTeamDescription { get; set; }         // To be displayed if no home team is defined
        public string VisitorTeamDescription { get; set; }      // To be displayed if no visitor team is defined

        public string Comments { get; set; }
        public string VideoUrl { get; set; }

        [Write(false)] public Team HomeTeam { get; set; }
        [Write(false)] public Team VisitorTeam { get; set; }
        [Write(false)] public Field Field { get; set; }
        [Write(false)] public PlayDay Day { get; set; }
        [Write(false)] public IEnumerable<Player> HomePlayers { get; set; }
        [Write(false)] public IEnumerable<Player> VisitorPlayers { get; set; }
        [Write(false)] public IEnumerable<MatchReferee> Referees { get; set; }
        [Write(false)] public IEnumerable<MatchEvent> Events { get; set; }
        [Write(false)] public Tournament Tournament { get; set; }
        [Write(false)] public IEnumerable<Sanction> SanctionsMatch { get; set; } // 🚧🚧🚧

        public bool IsScheduled()
        {
            return Status >= (int)MatchStatus.Scheduled;
        }
    }

    public enum MatchStatus
    {
        Created     = 1,
        Scheduled   = 2,
        Playing     = 3,
        Finished    = 4,
        Signed      = 5,
        Posponed    = 6,
        Cancelled   = 8,

        Skip        = 10        // Descansa
    }
}
