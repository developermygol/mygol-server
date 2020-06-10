using System;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;
using webapi.Models.Db;

namespace webapi.Models.Result
{
    public class TournamentResult
    {
        public Tournament Tournament { get; set; }
        public IEnumerable<Team> Teams { get; set; }
        //public IEnumerable<Player> Players { get; set; }
    }
}
