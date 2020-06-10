using contracts;
using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using Dapper.Contrib.Extensions;

namespace webapi.Models.Db
{
    public class MatchReferee
    {
        [ExplicitKey] public long IdMatch { get; set; }
        [ExplicitKey] public long IdUser { get; set; }    // user id --> referee
        public int Role { get; set; }

        [Write(false)] public User Referee { get; set; }


        public static IEnumerable<MatchReferee> GetForMatch(IDbConnection c, IDbTransaction t, long idMatch)
        {
            return c.Query<MatchReferee>("SELECT * FROM matchreferees WHERE idMatch = @idMatch", new { idMatch }, t);
        }
    }
}
