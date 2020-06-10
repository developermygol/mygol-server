using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi.Controllers
{
    public class SearchController: DbController
    {
        public SearchController(IOptions<Config> config) : base(config)
        {
            
        }

        [HttpGet()]
        public IActionResult Get([FromQuery(Name = "query")] string query, [FromQuery(Name = "type")] string type = null)
        {
            if (query == null || query.Length < 3) return new EmptyResult();

            return DbOperation(c =>
            {
                //if (!IsLoggedIn()) throw new UnauthorizedAccessException();
                var args = new { query = $"%{query}%" };

                switch (type)
                {
                    case "p":   // players
                        return c.Query<Player, User, Player>(@"
                                SELECT p.id, p.name, p.surname, u.name, u.avatarImgUrl, u.id
                                FROM players p JOIN users u ON p.idUser = u.id
                                WHERE u.name ilike @query",
                            (player, user) =>
                            {
                                player.UserData = user;
                                return player;
                            },
                            args, 
                            splitOn: "name");

                    case "t":       // Teams
                        return c.Query<Team>(@"
                                SELECT t.logoImgUrl, t.id, t.name, t.keyname
                                FROM teams t
                                WHERE t.name ilike @query OR t.keyname ilike @query", 
                                args);

                    case "doc":         // Documents in the uploads area
                        break;
                    case "news":        // text in the content area
                        break;
                    default:            // Search everywhere
                        return SearchEverywhere(c, query);
                }

                return null;

            });
        }

        [HttpGet("players")]
        public IActionResult GetPlayers([FromQuery(Name = "query")] string query)
        {
            // Result includes name, team, apparelNumber and tournament.

            return null;
        }


        public static SearchResult SearchEverywhere(IDbConnection c, string query, int limit = 20)
        {
            string sql = null;
            var likeQuery = '%' + query + '%';

            if (long.TryParse(query, out long queryAsId) && queryAsId < 100000)        // 100000 to support mobile phone search as string
            {
                sql = @"
                    SELECT p.*, u.* FROM players p JOIN users u ON p.iduser = u.id WHERE p.id = @id OR u.id = @id LIMIT @limit;
                    SELECT * FROM teams t LEFT JOIN tournamentTeams tt ON tt.idTeam = t.id LEFT JOIN tournaments tn ON tn.id = tt.idTournament WHERE t.id = @id LIMIT @limit;
                    SELECT * FROM tournaments t JOIN seasons s ON t.idSeason = s.id WHERE t.id = @id LIMIT @limit;
                    SELECT * FROM fields WHERE id = @id LIMIT @limit;
                    SELECT * FROM users WHERE level = 2 AND id = @id LIMIT @limit;
                ";
            }
            else
            {
                sql = @"
                    SELECT p.*, u.* FROM players p JOIN users u ON p.iduser = u.id WHERE u.name ilike @query OR email ilike @query OR u.mobile ilike @query OR idCardNumber ilike @query LIMIT @limit;
                    SELECT t.*, tn.* FROM teams t LEFT JOIN tournamentTeams tt ON tt.idTeam = t.id LEFT JOIN tournaments tn ON tn.id = tt.idTournament WHERE t.name ilike @query OR t.keyName ilike @query LIMIT @limit;
                    SELECT * FROM tournaments t JOIN seasons s ON t.idSeason = s.id WHERE t.name ilike @query LIMIT @limit;
                    SELECT * FROM fields WHERE name ilike @query LIMIT @limit;
                    SELECT * FROM users WHERE level = 2 AND (name ilike @query OR mobile ilike @query) LIMIT @limit;
                ";
            }

            var mr = c.QueryMultiple(sql, new { query = likeQuery, id = queryAsId, limit });

            var result = new SearchResult { Query = query };

            var players = mr.Read<Player, User, Player>((player, user) => { player.UserData = user; return player; }, splitOn: "id");
            var teams = mr.Read<Team, Tournament, Team>((team, tnmt) => { team.Tournaments = new[] { tnmt }; return team; }, splitOn: "id");
            var tournaments = mr.Read<Tournament, Season, Tournament>((t, s) => { t.Season = s; return t; }, splitOn: "id");
            var fields = mr.Read<Field>();
            var referees = mr.Read<User>();

            result.Players = players;
            result.Teams = teams;
            result.Tournaments = tournaments;
            result.Fields = fields;
            result.Referees = referees;

            return result;
        }
    }

    public class SearchResult
    {
        public string Query { get; set; }
        public IEnumerable<Player> Players { get; set; }
        public IEnumerable<Team> Teams { get; set; }
        public IEnumerable<Tournament> Tournaments { get; set; }
        public IEnumerable<Field> Fields { get; set; }
        public IEnumerable<User> Referees { get; set; }

        public IEnumerable<BaseObject> GetAll()
        {
            if (Players != null) foreach (var p in Players) yield return p;
            if (Teams != null) foreach (var t in Teams) yield return t;
            if (Tournaments != null) foreach (var t in Tournaments) yield return t;
            if (Fields != null) foreach (var f in Fields) yield return f;
            if (Referees != null) foreach (var r in Referees) yield return r;
        }
    }
}
