using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi.Controllers
{

    public class TeamsController : CrudController<Team>
    {
        public TeamsController(IOptions<Config> config) : base(config)
        {
        }

        [HttpGet("/api/tournaments/{idTournament}/teams")]
        public IActionResult GetTeamsForTournament(long idTournament)
        {
            return DbOperation(conn =>
            {
                CheckAuthLevel(UserLevel.All);

                return conn.Query<Team>("SELECT teams.* FROM tournamentteams JOIN teams ON idTeam = id WHERE idTournament = @tournament", new { tournament = idTournament });
            });
        }

        [HttpPost("/api/tournaments/teamfilterbytournaments")]
        public IActionResult GetTeamsForTournament([FromBody] FilterTeamsByTorunaments data)
        {
            return DbOperation(conn =>
            {
                CheckAuthLevel(UserLevel.All);

                return conn.Query<Team>($"SELECT teams.* FROM tournamentteams JOIN teams ON idTeam = id WHERE idTournament IN({string.Join(",", data.TournamnetsIds)})");
            });
        }


        [HttpPost("/api/tournaments/{idTournament}/teams")]
        public IActionResult CreateTeamInTournament([FromBody] Team team, long idTournament)
        {
            return DbOperation(conn =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                if (team == null) throw new NoDataException();

                Audit.Information(this, "{0}: Teams.CreateInTournament {idTournament} -> {Name}", GetUserId(), team.Name, idTournament);

                var teamId = conn.Insert(team);
                conn.Insert(new TournamentTeam { IdTournament = idTournament, IdTeam = teamId });

                return teamId;
            });
        }

        [HttpPost("/api/tournaments/{idTournament}/addteam/{idTeam}")]
        public IActionResult AddTeamToTournament(long idTournament, long idTeam)
        {
            return DbOperation(conn =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                Audit.Information(this, "{0}: Teams.AddToTournament {idTournament} -> {idTeam}", GetUserId(), idTournament, idTeam);

                conn.Insert(new TournamentTeam { IdTournament = idTournament, IdTeam = idTeam });

                // TODO: Notify team admin (if any) her team has been added to the competition. 

                return true;
            });
        }


        [HttpPost("/api/tournaments/{idTournament}/team/delete")]
        public IActionResult RemoveTeamFromTournament([FromBody] Team team, long idTournament)
        {
            return DbTransaction((c, t) =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                if (team == null) throw new NoDataException();

                Audit.Information(this, "{0}: Teams.UnlinkFromTournament {idTournament} -> {idTeam}", GetUserId(), idTournament, team.Id);

                // Check that the team has no matches in the tournament
                var numMatches = c.ExecuteScalar<int>("SELECT COUNT(*) FROM matches WHERE idTournament = @idTournament AND (idHomeTeam = @idTeam OR idVisitorTeam = @idTeam)", new { idTournament, idTeam = team.Id }, t);
                if (numMatches > 0) throw new Exception("Error.UnlinkTeam.TeamHasMatches");

                // Delete the link
                c.Execute("DELETE FROM tournamentteams WHERE idTeam = @idteam AND idtournament = @idtournament", new { idteam = team.Id, idtournament = idTournament }, t);

                // Check if the team is in other competitions. If not, remove it completely.
                // For now, it's best to just leave it there. Just in case. 
                //var otherCompetitions = conn.ExecuteScalar<int>("SELECT count(idtournament) FROM tournamentteams WHERE idteam = @idteam", new { idteam = team.Id });
                //if (otherCompetitions == 0) conn.Delete(team);

                // TODO: Notify team admin their team has been removed from the competition. 

                return true;
            });
        }


        [HttpPost("obliterate")]
        public IActionResult Obliterate([FromBody] Team value)
        {
            // Forces deletion of the team.

            return DbTransaction((c, t) =>
            {
                if (value == null) throw new NoDataException();

                CheckAuthLevel(UserLevel.OrgAdmin);

                // Matches
                // TournamentTeams
                // TeamPlayers
                // Teams

                var sql = @"
                    UPDATE matches SET idHomeTeam = -1 WHERE idHomeTeam = @idTeam;
                    UPDATE matches SET idVisitorTeam = -1 WHERE idVisitorTeam = @idTeam;
                    
                    DELETE FROM matchevents WHERE idTeam = @idTeam;
                    DELETE FROM matchPlayers WHERE idTeam = @idTeam;
                    DELETE FROM teamdayresults WHERE idTeam = @idTeam;
                    DELETE FROM contents WHERE idTeam = @idTeam;
                    DELETE FROM paymentConfigs WHERE idTeam = @idTeam;
                    DELETE FROM sponsors WHERE idTeam = @idTeam;
                    DELETE FROM awards WHERE idTeam = @idTeam;

                    DELETE FROM tournamentTeams WHERE idTeam = @idTeam;
                    DELETE FROM teamPlayers WHERE idTeam = @idTeam;
                    DELETE FROM teamGroups WHERE idTeam = @idTeam;

                    DELETE FROM teams WHERE id = @idTeam;
                ";

                c.Execute(sql, new { idTeam = value.Id }, t);

                return true;
            });
        }

        [HttpGet("{idTeam}/details/{idTournament}")]
        public IActionResult Details(long idTeam, long idTournament)
        {
            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.All);

                // Team data
                var result = c.Get<Team>(idTeam);
                if (result == null) throw new Exception("Error.NotFound");

                // List of all tournaments where it participates
                result.Tournaments = GetTournamentsForTeam(c, idTeam);

                if (idTournament > -1)
                {
                    // Tournament specific data: 
                    // - Team players
                    result.Players = GetPlayerTotals(c, idTeam, idTournament);

                    // - Team playDays with: 
                    //   - Matches
                    result.Days = GetDaysForTeam(c, idTeam, idTournament);

                    //   - TeamDayResults
                    FillTeamDayResults(c, idTeam, idTournament, result.Days);
                }

                // Team Sponsors
                result.Sponsors = c.Query<Sponsor>("SELECT * FROM sponsors WHERE idTeam = @id", new { id = idTeam });

                return result;
            });
        }

        [HttpGet("/api/tournaments/{idTournament}/teamsready")]
        public IActionResult GetTeamsReadyForTournament(long idTournament)
        {
            // Used by the calendar
            return DbOperation(conn =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                return conn.Query<Team>("SELECT teams.* FROM tournamentteams JOIN teams ON idTeam = id WHERE idTournament = @tournament AND status = 2", new { tournament = idTournament });
            });
        }

        [HttpPost("updateplayerstatus")]
        public IActionResult UpdatePlayerStatus([FromBody] TeamPlayerStatusInput data)
        {
            return DbTransaction((c, t) =>
            {
                if (data == null) throw new NoDataException();

                CheckAuthLevel(UserLevel.OrgAdmin);

                Audit.Information(this, "{0}: Teams.UpdatePlayerStatus: {1} {2} {3}", GetUserId(), data.IdPlayer, data.IdTeam, data.Status);

                var teamPlayer = c.Query<TeamPlayer>("SELECT * FROM teamplayers WHERE idTeam = @idTeam AND idPlayer = @idPlayer", data, t).GetSingle();

                teamPlayer.Status = data.Status;

                c.Execute("UPDATE teamplayers SET status = @status WHERE idPlayer = @idPlayer AND idTeam = @idTeam", data);

                return true;
            });
        }



        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "Teams"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, Team target, IDbConnection conn)
        {
            if (mConfig.RequireLogin && !IsLoggedIn()) return false;

            if (reqType == RequestType.GetAll || reqType == RequestType.GetSingle) return true;   // Allow open read acccess

            // All options available to org admins
            if (IsOrganizationAdmin()) return true;

            // A team admin should be able to list her own team and edit it.
            if (reqType == RequestType.GetSingle || reqType == RequestType.Put)
            {
                return (IsTeamAdmin(target.Id, conn));
            }

            return false;
        }

        protected override bool ValidateDelete(Team value, IDbConnection c, IDbTransaction t)
        {
            // Para borrar un equipo hay que comprobar que no hay ningún jugador asociado, o partidos ( o borrar en cascada, o marcar como borrado y no mostrar en ese caso.)
            // Sólo se puede borrar si no está en otras competiciones

            // Check that the team has no matches in the tournament
            var numMatches = c.ExecuteScalar<int>("SELECT COUNT(*) FROM matches WHERE idHomeTeam = @idTeam OR idVisitorTeam = @idTeam", new { idTeam = value.Id }, t);
            if (numMatches > 0) throw new Exception("Error.DeleteTeam.TeamHasMatches");

            return true;
        }

        protected override bool ValidateEdit(Team value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length >= 1;
        }

        protected override bool ValidateNew(Team value, IDbConnection c, IDbTransaction t)
        {
            // Here we could disallow creating teams on the default POST route (i.e. without a the join to a tournament). 
            return value.Name != null && value.Name.Length >= 1;
        }


        private static void FillTeamDayResults(IDbConnection c, long idTeam, long idTournament, IEnumerable<PlayDay> days)
        {
            // Load the tournament days inside each day here

            var tdrs = c.Query<TeamDayResult>("SELECT * FROM teamdayresults WHERE idTournament = @idTournament AND idTeam = @idTeam ORDER BY idDay, idTeam", new { idTournament = idTournament, idTeam = idTeam });
            var keyedDays = days.ToDictionary<PlayDay, long>(day => day.Id);

            foreach (var tdr in tdrs)
            {
                var day = keyedDays[tdr.IdDay];
                if (day.TeamDayResults == null) day.TeamDayResults = new List<TeamDayResult>();
                day.TeamDayResults.Add(tdr);
            }
        }


        private IEnumerable<PlayDay> GetDaysForTeam(IDbConnection c, long idTeam, long idTournament)
        {
            var sql = @"
                SELECT 
                    d.id, d.name, d.dates, d.idStage, d.idGroup, d.idTournament,
                    m.id, m.idhometeam, m.idvisitorteam, m.idfield, m.starttime, m.homescore, m.visitorscore, m.status, m.idTournament, m.idStage, m.idGroup, m.idDay, 
                    t1.name, t1.id, t1.logoImgUrl,
                    t2.name, t2.id, t2.logoImgUrl, 
                    f.id, f.name
                FROM playdays d 
                    JOIN matches m ON m.idday = d.id
                    JOIN teams t1 ON m.idhometeam = t1.id
                    JOIN teams t2 ON m.idvisitorteam = t2.id
                    LEFT JOIN fields f ON m.idFIeld = f.id
                WHERE 
                    m.idTournament = @idTournament
                    AND (m.idhometeam = @idteam OR m.idvisitorteam = @idteam)
                ORDER BY 
                    d.sequenceorder
            ";

            var result = c.Query<PlayDay, Match, Team, Team, Field, PlayDay>(sql,
                (day, match, home, visitor, field) =>
                {
                    match.HomeTeam = home;
                    match.VisitorTeam = visitor;
                    match.Field = field;
                    day.Matches = new List<Match> { match };
                    return day;
                },
                new { idTournament = idTournament, idTeam = idTeam },
                splitOn: "id, name, name, id");

            result = GroupMatchesInDays(result);

            return result;
        }

        private static IEnumerable<PlayDay> GroupMatchesInDays(IEnumerable<PlayDay> daysAndMatches)
        {
            // Group matches of the same day in the same record
            // This is to handle the case where a team plays more than one match in the same day (yes, apparently it happens). 

            var result = new List<PlayDay>();
            PlayDay lastDay = null;

            foreach (var day in daysAndMatches)
            {
                if (lastDay == null)
                {
                    lastDay = day;
                    result.Add(day);
                    continue;
                }

                if (day.Id == lastDay.Id)
                {
                    foreach (var m in day.Matches) lastDay.Matches.Add(m);  // Probably an array expansion on each, but it should be very few (how many matches does a team play on the same day?)
                }
                else
                {
                    result.Add(day);
                }
            }

            return result;
        }

        public static IEnumerable<Player> GetPlayerStatistics(IDbConnection c, string condition, object parameters)
        {
            var sql = $@"
                SELECT 
	                p.id, p.name, p.surname, p.idphotoimgurl, u.id, u.avatarImgUrl,
                    tp.apparelNumber, tp.idTacticPosition, tp.fieldPosition,
	                coalesce(sum(points), 0) as points,
                    coalesce(sum(gamesPlayed), 0) as gamesPlayed, 
                    coalesce(sum(gamesWon), 0) as gamesWon, 
                    coalesce(sum(gamesDraw), 0) as gamesDraw,
                    coalesce(sum(gamesLost), 0) as gamesLost,
                    coalesce(sum(pointsAgainst), 0) as pointsAgainst,
	                coalesce(sum(pointsInOwn), 0) as pointsInOwn,
	                coalesce(sum(cardsType1), 0) as cardsType1,
	                coalesce(sum(cardsType2), 0) as cardsType2,
	                coalesce(sum(cardsType3), 0) as cardsType3,
	                coalesce(sum(cardsType4), 0) as cardsType4,
	                coalesce(sum(cardsType5), 0) as cardsType5,
	                coalesce(sum(data1), 0) as data1,
	                coalesce(sum(data2), 0) as data2,
	                coalesce(sum(data3), 0) as data3,
	                coalesce(sum(data4), 0) as data4,
	                coalesce(sum(data5), 0) as data5
                FROM 
	                players p 
	                JOIN users u ON p.idUser = u.id
	                JOIN teamplayers tp ON tp.idPlayer = p.id AND (tp.enrollmentStep >= 100 OR (tp.status & 256) = 256) AND tp.idTeam = @idTeam 
	                LEFT JOIN playerdayresults pdr ON pdr.idPlayer = p.id AND pdr.idTeam = @idTeam AND pdr.idTournament = @idTournament
                WHERE 
	                {condition}
                GROUP BY 
	                p.id, p.name, p.surname, u.id, u.avatarImgUrl, tp.apparelNumber, tp.idTacticPosition, tp.fieldPosition
                ORDER BY 
                    tp.apparelNumber
            ";

            var result = c.Query<Player, User, TeamPlayer, PlayerDayResult, Player>(sql,
                (player, user, teamPlayer, pdr) =>
                {
                    player.UserData = user;
                    player.DayResultSummary = pdr;
                    player.TeamData = teamPlayer;
                    return player;
                },
                parameters,
                splitOn: "id,apparelNumber,points");

            return result;
        }

        private static IEnumerable<Player> GetPlayerTotals(IDbConnection c, long idTeam, long idTournament)
        {
            return GetPlayerStatistics(c, "tp.idTeam = @idTeam ", new { idTournament = idTournament, idTeam = idTeam });

        }

        public static IEnumerable<Tournament> GetTournamentsForTeam(IDbConnection c, long idTeam)
        {
            return c.Query<Tournament, Season, Tournament>(
                "SELECT t.*, s.* FROM tournamentTeams tt JOIN tournaments t ON t.id = tt.idTournament AND tt.idTeam = @idTeam JOIN seasons s ON t.idSeason = s.id", 
                (tournament, season) =>
                {
                    tournament.Season = season;
                    return tournament;
                },
                new { idTeam = idTeam }, 
                splitOn: "id");
        }
    }


    public class TeamPlayerStatusInput
    {
        public long IdPlayer { get; set; }
        public long IdTeam { get; set; }
        public long IdTournament { get; set; }
        public int Status { get; set; }
    }

    public class FilterTeamsByTorunaments
    {
        public int[] TournamnetsIds { get; set; }
    }
}
