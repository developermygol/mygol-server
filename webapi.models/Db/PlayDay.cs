using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace webapi.Models.Db
{
    public class PlayDay: BaseObject
    {
        public string Name { get; set; }
        public long IdTournament { get; set; }
        public long IdStage { get; set; }
        public long IdGroup { get; set; }       // Not used now. Playdays are associated to stages (and tournaments)
        public string Dates { get; set; }
        public int SequenceOrder { get; set; }


        [Write(false)] public IList<DateTime> DatesList { get; private set; } = new List<DateTime>();
        [Write(false)] public IList<Match> Matches { get; set; }
        [Write(false)] public IList<TeamDayResult> TeamDayResults { get; set; }
        [Write(false)] public IList<PlayerDayResult> PlayerDayResults { get; set; }


        public void SetDatesFromDatesList()
        {
            Dates = JsonConvert.SerializeObject(DatesList);
        }

        public void SetDatesListFromDates()
        {
            DatesList = JsonConvert.DeserializeObject<List<DateTime>>(Dates);
        }
    }
}
