using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace webapi.Models.Db
{
    //[DebuggerDisplay("{Name} ({Id})")]
    public class Sanction: BaseObject
    {
        public long IdPlayer { get; set; }
        public long IdTeam { get; set; }
        public long IdMatch { get; set; }
        public long IdTournament { get; set; }
        public long IdDay { get; set; }
        public string Title { get; set; }
        public int Status { get; set; }
        
        public DateTime? StartDate { get; set; }
        public long IdPayment { get; set; }
        public bool IsAutomatic { get; set; }
        public long IdSanctionConfigRuleId { get; set; }
        public int Type { get; set; }           // Player (0) or Team (2)

        public int NumMatches { get; set; }
        public int LostMatchPenalty { get; set; }
        public int TournamentPointsPenalty { get; set; }

        public string SanctionMatchEvents { get; set; }

        [Write(false)] public string InitialContent { get; set; }

        [Write(false)] public Player Player { get; set; }
        [Write(false)] public Team Team { get; set; }
        [Write(false)] public Match Match { get; set; }
        [Write(false)] public Tournament Tournament { get; set; }
        [Write(false)] public IEnumerable<SanctionAllegation> Allegations { get; set; }
        [Write(false)] public IEnumerable<Match> SanctionMatches { get; set; }
        [Write(false)] [JsonIgnore] public AutoSanctionCardConfig SourceCardConfigRule { get; set; }
        [Write(false)] [JsonIgnore] public IEnumerable<MatchEvent> AutomanticSanctionMatchEvents { get; set; }
    }

    [Table("sanctionmatches")]
    public class SanctionMatch : BaseObject
    {
        public long IdSanction { get; set; }
        public long IdPlayer { get; set; }
        public long IdMatch { get; set; }
        public long IdTournament { get; set; }
        public bool IsLast { get; set; }

        [Write(false)] Match Match { get; set; }
    }


    public class SanctionAllegation : BaseObject
    {
        public long IdSanction { get; set; }
        public long IdUser { get; set; }
        public int Status { get; set; }
        public DateTime? Date { get; set; }
        public string Content { get; set; }
        public string Title { get; set; }
        public bool Visible { get; set; }

        [Write(false)] public User User { get; set; }
    }


    public enum SanctionType
    {
        Player = 0, 
        Team = 2
    }

    public enum SanctionStatus
    {
        AutomaticallyGenerated = 1,     // En vigor - alegaciones abierta
        Resolved = 2,                   // En vigor - alegaciones cerradas
        InProgress = 3,                 // En trámite - alegaciones abiertas
        Finished = 4                    // Cumplida
    }

    public enum SanctionAllegationStatus
    {
        Created = 1,
        HiddenByAdmin = 2
    }
}
