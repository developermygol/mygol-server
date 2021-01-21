using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    [DebuggerDisplay("{Name} ({Id})")]
    public class Tournament : BaseObject
    {
        public string Name { get; set; }
        public int Type { get; set; }
        public int Status { get; set; }

        public long IdSeason { get; set; }
        public long IdTournamentMode { get; set; }
        public long IdCategory { get; set; }

        public string LogoImgUrl { get; set; }
        public bool Visible { get; set; }

        public string SponsorData { get; set; }
        public string AppearanceData { get; set; }
        public string NotificationFlags { get; set; }
        public long SequenceOrder { get; set; }

        [Write(false)] public IEnumerable<Team> Teams { get; set; }
        [Write(false)] public IEnumerable<PlayDay> Days { get; set; }

        [Write(false)] public IEnumerable<TournamentStage> Stages { get; set; }
        [Write(false)] public IEnumerable<StageGroup> Groups { get; set; }
        [Write(false)] public IEnumerable<TeamGroup> TeamGroups { get; set; }

        [Write(false)] public Season Season { get; set; }
        [Write(false)] public TournamentMode Mode { get; set; }

        public override string Print()
        {
            return $"Tournament: id:{Id} name:'{Name}' visible:{Visible} season:{Season?.Name}";
        }
    }

    public enum TournamentStatus
    {
        InscriptionsOpen        = 1,
        InscriptionsClosed      = 2,
        Playing                 = 3,
        Finished                = 4
    }


    public class TournamentStage : BaseObject
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public long IdTournament { get; set; }
        public int Type { get; set; }               // CalendarType
        public int Status { get; set; }
        public int SequenceOrder { get; set; }
        public string ClassificationCriteria { get; set; }
        public string ColorConfig { get; set; }

        [Write(false)] public IEnumerable<TeamDayResult> LeagueClassification { get; set; }
        [Write(false)] public IEnumerable<PlayDay> KnockoutClassification { get; set; }
    }

    public class StageGroup : BaseObject
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public long IdTournament { get; set; }
        public long IdStage { get; set; }
        public int NumTeams { get; set; }
        public int NumRounds { get; set; }
        public int Flags { get; set; }
        public int SequenceOrder { get; set; }
        public string ColorConfig { get; set; }
    }

    [Flags]
    public enum StageGroupFlags
    {
        HasGeneratedCalendar = 1,
    }
}
