using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    [Table("awards")]
    public class Award: BaseObject
    {
        public long IdPlayer { get; set; }
        public long IdDay { get; set; }
        public long IdTeam { get; set; }
        public long IdTournament { get; set; }
        public long IdStage { get; set; }
        public long IdGroup { get; set; }
        public int Type { get; set; }

        [Write(false)] public Player Player { get; set; }
        [Write(false)] public PlayDay Day { get; set; }
        [Write(false)] public Tournament Tournament { get; set; }
        [Write(false)] public Team Team { get; set; }
    }

    public enum AwardType
    {
        DreamTeam = 0, 
        MVP = 1, 
        MaxScorer = 2,
        TopScorer = 10,
        TopGoalKeeper = 20,
        TopAssistances = 30,
        TopMVP = 40,        
        BestFairPlay = 50
    }
}
