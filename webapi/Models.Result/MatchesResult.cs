using System;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;
using webapi.Models.Db;

namespace webapi.Models.Result
{
    public class MatchesResult
    {
        public IEnumerable<Match> Matches { get; set; }
        public IEnumerable<Team> Teams { get; set; }
    }
}
