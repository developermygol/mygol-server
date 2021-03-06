﻿using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;
using webapi.Models.Result;

namespace webapi.Controllers
{
    
    public class TournamentsController: CrudController<Tournament>
    {
        public TournamentsController(IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "tournaments"
            };
        }

        public override IActionResult Get()
        {
            return DbOperation(c =>
            {
                var query = (IsOrganizationAdmin()) ?
                    "SELECT * FROM tournaments" : "SELECT * FROM tournaments WHERE visible='t'";

                return c.Query<Tournament>(query);
            });
        }


        public override IActionResult Get(long id)
        {
            return DbOperation(c =>
            {
                if (!IsAuthorized(RequestType.GetSingle, null, c)) throw new UnauthorizedAccessException();

                var role = GetUserRole();
                switch (role)
                {
                    case (int)UserLevel.OrgAdmin:
                    case (int)UserLevel.MasterAdmin:
                        return GetOrgAdminTournamentData(c, id);
                    default: return GetPublicTournamentData(c, id);
                }

            });
        }

        [HttpPost("{id:long}/recalculatestats")]
        public async Task<IActionResult> RecalculateStats(long id)
        {
            return await DbTransactionAsync(async (c, t) =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                await ResetStats(c, t, id);

                return true;
            });
        }

        public async Task ResetStats(IDbConnection c, IDbTransaction t, long id)
        {
            await MatchEvent.ResetTournamentStats(c, t, id);
            await MatchEvent.ApplyTournamentStats(c, t, id);
        }

        [HttpPost("{id:long}/clearautomaticsanctions")]
        public IActionResult ClearAutomaticSanctions(long id)
        {
            return DbTransaction((c, t) =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                AutoSanctionDispatcher.ClearAllAutomaticSanctions(c, t, id);

                ResetStats(c, t, id).Wait();

                return true;
            });
        }

        [HttpPost("{id:long}/recalculateautomaticsanctions")]
        public IActionResult RecalculateAutomaticSanctions(long id)
        {
            return DbTransaction((c, t) =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                AutoSanctionDispatcher.ClearAllAutomaticSanctions(c, t, id);

                ResetStats(c, t, id).Wait();

                AutoSanctionDispatcher.ApplyAllAutomaticSanctions(c, t, id, GetUserId());

                return true;
            });
        }

        [HttpPut("sponsordata")]
        public IActionResult SetSponsorData([FromBody] UpdateTournamnetSponsorDataRequest data)
        {
            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                c.Execute($"UPDATE tournaments SET sponsordata = '{data.SectionsJson}' WHERE id = {data.IdTournament};");
                return true;
            });
        }

        [HttpPut("appearance")]
        public IActionResult SetAppearanceData([FromBody] UpdateTournamnetAppearanceDataRequest data)
        {
            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                c.Execute($"UPDATE tournaments SET appearancedata = '{data.AppearanceJsonString}' WHERE id = {data.IdTournament};");
                return true;
            });
        }

        [HttpPut("dreamteam")]
        public IActionResult SetDreamTeamData([FromBody] UpdateTournamnetDreamTeamDataRequest data)
        {
            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                c.Execute($"UPDATE tournaments SET dreamteam = '{data.DreamTeamJsonString}' WHERE id = {data.IdTournament};");
                return true;
            });
        }

        [HttpPut("saveorder")]
        public IActionResult UpdateTournamentsSequenceOrder([FromBody] UpdateTournamentsSequenceOrder data)
        {
            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                IEnumerable<TournamentSequence> sequences = data.TournamentsSequence;

                foreach (var sequence in sequences)
                {
                    c.Execute($"UPDATE tournaments SET sequenceorder = '{sequence.SequenceOrder}' WHERE id = {sequence.Id};");
                }

                return c.Query<Tournament>("SELECT * FROM tournaments"); ;
            });
        }

        [HttpGet("stageclassification/{idStage}")]
        public IActionResult ClassificationForStage(long idStage)
        {
            return DbOperation(c =>
            {
                var stage = c.Get<TournamentStage>(idStage);
                if (stage == null) throw new Exception("Error.NotFound");

                var tournament = c.Get<Tournament>(stage.IdTournament);
                if (tournament == null) throw new Exception("Error.NotFound");
                if (!tournament.Visible) throw new Exception("Error.TournamentNotVisible");


                switch ((CalendarType)stage.Type)
                {
                    case CalendarType.League:
                        stage.LeagueClassification = GetLeagueClassification(c, null, stage, 999);
                        break;
                    case CalendarType.Knockout:
                        stage.KnockoutClassification = GetKnockoutClassification(c, null, idStage);
                        break;
                    default:
                        throw new Exception("Error.UnknownStageType");
                }

                return stage;
            });
        }

        [HttpGet("{idTournament}/ranking/scorers/{type}/{limit:long?}")]
        public IActionResult GetScorerRanking(long idTournament, long type, long limit = -1)
        {
            if (limit == -1) limit = RankingDefaultNumberOfResults;
            if (limit == 0) limit = RankingMaxNumberOfResults;

            return DbOperation(c =>
            {
                string query = null;

                // Type is how the data is grouped: 1 tournament, 2 stages, 3 groups
                switch (type)
                {
                    case 1: query = @"
                        SELECT p.name as playerName, p.surname as playerSurname, p.idUser, tp.idTeam as idTeam, r.*
                        FROM (
	                        SELECT idPlayer, idTeam, SUM(gamesplayed) AS gamesplayed, SUM(points) AS points
	                        FROM playerDayResults 
	                        WHERE idTournament = @idTournament
	                        GROUP BY idPlayer, idTeam
                        ) AS r 
                        JOIN teamplayers tp ON tp.idPlayer = r.idPlayer AND tp.idteam = r.idteam
                        JOIN tournamentTeams tt ON tp.idteam = tt.idteam AND idtournament = @idTournament
                        JOIN players p ON r.idPlayer = p.id
                        WHERE r.points > 0
                        ORDER BY r.points DESC NULLS LAST
                        LIMIT @limit";
                        break;
                    case 2: query = @"
                        SELECT p.name as playerName, p.surname as playerSurname, p.idUser, tp.idTeam as idTeam, r.*
                        FROM (
	                        SELECT idPlayer, idTeam, idStage, SUM(gamesplayed) AS gamesplayed, SUM(points) AS points
	                        FROM playerDayResults 
	                        WHERE idTournament = @idTournament
	                        GROUP BY idPlayer, idTeam, idStage
                        ) AS r 
                        JOIN teamplayers tp ON tp.idPlayer = r.idPlayer AND tp.idteam = r.idteam
                        JOIN tournamentTeams tt ON tp.idteam = tt.idteam AND idtournament = @idTournament                        
                        JOIN players p ON r.idPlayer = p.id
                        WHERE r.points > 0
                        ORDER BY idStage, points DESC NULLS LAST
                        LIMIT @limit";
                        break;
                    case 3:
                        query = @"
                        SELECT p.name as playerName, p.surname as playerSurname, p.idUser, tp.idTeam as idTeam, r.*
                        FROM (
	                        SELECT idPlayer, idTeam, idGroup, SUM(gamesplayed) AS gamesplayed, SUM(points) AS points
	                        FROM playerDayResults 
	                        WHERE idTournament = @idTournament
	                        GROUP BY idPlayer, idTeam, idGroup
                        ) AS r 
                        JOIN teamplayers tp ON tp.idPlayer = r.idPlayer AND tp.idteam = r.idteam
                        JOIN tournamentTeams tt ON tp.idteam = tt.idteam AND idtournament = @idTournament
                        JOIN players p ON r.idPlayer = p.id
                        WHERE r.points > 0
                        ORDER BY idGroup, points DESC NULLS LAST
                        LIMIT @limit";
                        break;
                    default:
                        throw new Exception("Error.InvalidRankType");
                }

                var result = c.Query<PlayerDayResult>(query, new { idTournament = idTournament, limit = limit });

                return result;
            });
        }

        [HttpGet("{idTournament}/ranking/goalkeepers/{type}/{limit:long?}")]
        public IActionResult GetGoalKeeperRanking(long idTournament, long type, long limit = -1)
        {
            if (limit == -1) limit = RankingDefaultNumberOfResults;

            return DbOperation(c =>
            {
                string query = null;

                // Type is how the data is grouped: 1 tournament, 2 stages, 3 groups
                switch (type)
                {
                    case 1:
                        query = @"
                        SELECT p.id AS idPlayer, p.name AS playerName, p.surname AS playerSurname, p.idUser, t2.id AS idTeam, t.* 
                        FROM (
	                        SELECT idteam, SUM(gamesplayed) AS gamesplayed, SUM(pointsagainst) AS pointsagainst
	                        FROM teamdayresults
	                        WHERE idTournament = @idTournament
	                        GROUP BY idTeam
                        ) t 
                        JOIN teams t2 ON t2.id = t.idteam
                        JOIN players p ON t2.idgoalkeeper = p.id
                        JOIN tournamentTeams tt ON t2.id = tt.idteam AND idtournament = @idTournament
                        ORDER BY pointsagainst ASC
                        LIMIT @limit";
                        break;
                    case 2:
                        query = @"
                        SELECT p.id AS idPlayer, p.name AS playerName, p.surname AS playerSurname, p.idUser, t2.id AS idTeam, t.* 
                        FROM (
	                        SELECT idteam, idStage, SUM(gamesplayed) AS gamesplayed, SUM(pointsagainst) AS pointsagainst
	                        FROM teamdayresults
	                        WHERE idTournament = @idTournament
	                        GROUP BY idTeam, idStage
                        ) t 
                        JOIN teams t2 ON t2.id = t.idteam
                        JOIN players p ON t2.idgoalkeeper = p.id
                        JOIN tournamentTeams tt ON t2.id = tt.idteam AND idtournament = @idTournament
                        ORDER BY pointsagainst ASC
                        LIMIT @limit";
                        break;
                    case 3:
                        query = @"
                        SELECT p.id AS idPlayer, p.name AS playerName, p.surname AS playerSurname, p.idUser, t2.id AS idTeam, t.* 
                        FROM (
	                        SELECT idteam, idStage, idGroup, SUM(gamesplayed) AS gamesplayed, SUM(pointsagainst) AS pointsagainst
	                        FROM teamdayresults
	                        WHERE idTournament = @idTournament
	                        GROUP BY idTeam, idStage, idGroup
                        ) t 
                        JOIN teams t2 ON t2.id = t.idteam
                        JOIN players p ON t2.idgoalkeeper = p.id
                        JOIN tournamentTeams tt ON t2.id = tt.idteam AND idtournament = @idTournament
                        ORDER BY pointsagainst ASC
                        LIMIT @limit";
                        break;
                    default:
                        throw new Exception("Error.InvalidRankType");
                }

                var result = c.Query<PlayerDayResult>(query, new { idTournament = idTournament, limit = limit });

                return result;
            });
        }

        [HttpGet("{idTournament}/ranking/assistances/{type}/{limit:long?}")]
        public IActionResult GetAssistancesRanking(long idTournament, long type, long limit = -1)
        {
            if (limit == -1) limit = RankingDefaultNumberOfResults;

            return DbOperation(c =>
            {
                string query = null;

                // Type is how the data is grouped: 1 tournament, 2 stages, 3 groups
                switch (type)
                {
                    case 1:
                        query = @"
                        SELECT p.name as playerName, p.surname as playerSurname, p.idUser, tp.idTeam as idTeam, r.*
                        FROM (
                            SELECT idPlayer, idTeam, SUM(gamesplayed) AS gamesplayed, SUM(assistances) AS assistances
                            FROM playerDayResults 
                            WHERE idTournament = @idTournament
                            GROUP BY idPlayer, idTeam
                        ) AS r 
                        JOIN teamplayers tp ON tp.idPlayer = r.idPlayer AND tp.idteam = r.idteam
                        JOIN tournamentTeams tt ON tp.idteam = tt.idteam AND idtournament = @idTournament
                        JOIN players p ON r.idPlayer = p.id
                        WHERE r.assistances > 0
                        ORDER BY assistances DESC
                        LIMIT @limit";
                        break;
                    case 2:
                        query = @"
                        SELECT p.name as playerName, p.surname as playerSurname, p.idUser, tp.idTeam as idTeam, r.*
                        FROM (
                            SELECT idPlayer, idTeam, idStage, SUM(gamesplayed) AS gamesplayed, SUM(assistances) AS assistances
                            FROM playerDayResults 
                            WHERE idTournament = @idTournament
                            GROUP BY idPlayer, idTeam, idStage
                        ) AS r 
                        JOIN teamplayers tp ON tp.idPlayer = r.idPlayer AND tp.idteam = r.idteam
                        JOIN tournamentTeams tt ON tp.idteam = tt.idteam AND idtournament = @idTournament
                        JOIN players p ON r.idPlayer = p.id
                        WHERE r.assistances > 0
                        ORDER BY assistances DESC
                        LIMIT @limit";
                        break;
                    case 3:
                        query = @"
                        SELECT p.name as playerName, p.surname as playerSurname, p.idUser, tp.idTeam as idTeam, r.*
                        FROM (
                            SELECT idPlayer, idTeam, idStage, idGroup, SUM(gamesplayed) AS gamesplayed, SUM(assistances) AS assistances
                            FROM playerDayResults 
                            WHERE idTournament = @idTournament
                            GROUP BY idPlayer, idTeam, idStage, idGroup
                        ) AS r 
                        JOIN teamplayers tp ON tp.idPlayer = r.idPlayer AND tp.idteam = r.idteam
                        JOIN tournamentTeams tt ON tp.idteam = tt.idteam AND idtournament = @idTournament
                        JOIN players p ON r.idPlayer = p.id
                        WHERE r.assistances > 0
                        ORDER BY assistances DESC
                        LIMIT @limit";
                        break;
                    default:
                        throw new Exception("Error.InvalidRankType");
                }

                var result = c.Query<PlayerDayResult>(query, new { idTournament = idTournament, limit = limit });

                return result;
            });
        }

        [HttpGet("{idTournament}/ranking/mvps/{type}/{limit:long?}")]
        public IActionResult GetMVPsRanking(long idTournament, long type, long limit = -1)
        {
            if (limit == -1) limit = RankingDefaultNumberOfResults;

            return DbOperation(c =>
            {
                string query = null;

                // Type is how the data is grouped: 1 tournament, 2 stages, 3 groups
                switch (type)
                {
                    case 1:
                        query = @"
                        SELECT p.name as playerName, p.surname as playerSurname, p.idUser, tp.idTeam as idTeam, r.*
                        FROM (
                            SELECT idPlayer, idTeam, SUM(gamesplayed) AS gamesplayed, SUM(mvppoints) AS mvppoints
                            FROM playerDayResults 
                            WHERE idTournament = @idTournament
                            GROUP BY idPlayer, idTeam
                        ) AS r 
                        JOIN teamplayers tp ON tp.idPlayer = r.idPlayer AND tp.idteam = r.idteam
                        JOIN tournamentTeams tt ON tp.idteam = tt.idteam AND idtournament = @idTournament
                        JOIN players p ON r.idPlayer = p.id
                        WHERE r.mvppoints > 0
                        ORDER BY mvppoints DESC
                        LIMIT @limit";
                        break;
                    case 2:
                        query = @"
                        SELECT p.name as playerName, p.surname as playerSurname, p.idUser, tp.idTeam as idTeam, r.*
                        FROM (
                            SELECT idPlayer, idTeam, idStage, SUM(gamesplayed) AS gamesplayed, SUM(mvppoints) AS mvppoints
                            FROM playerDayResults 
                            WHERE idTournament = @idTournament
                            GROUP BY idPlayer, idTeam, idStage
                        ) AS r 
                        JOIN teamplayers tp ON tp.idPlayer = r.idPlayer AND tp.idteam = r.idteam
                        JOIN tournamentTeams tt ON tp.idteam = tt.idteam AND idtournament = @idTournament
                        JOIN players p ON r.idPlayer = p.id
                        WHERE r.mvppoints > 0
                        ORDER BY mvppoints DESC
                        LIMIT @limit";
                        break;
                    case 3:
                        query = @"
                        SELECT p.name as playerName, p.surname as playerSurname, p.idUser, tp.idTeam as idTeam, r.*
                        FROM (
                            SELECT idPlayer, idTeam, idStage, idGroup, SUM(gamesplayed) AS gamesplayed, SUM(mvppoints) AS mvppoints
                            FROM playerDayResults 
                            WHERE idTournament = @idTournament
                            GROUP BY idPlayer, idTeam, idStage, idGroup
                        ) AS r 
                        JOIN teamplayers tp ON tp.idPlayer = r.idPlayer AND tp.idteam = r.idteam
                        JOIN tournamentTeams tt ON tp.idteam = tt.idteam AND idtournament = @idTournament
                        JOIN players p ON r.idPlayer = p.id
                        WHERE r.mvppoints > 0
                        ORDER BY mvppoints DESC
                        LIMIT @limit";
                        break;
                    default:
                        throw new Exception("Error.InvalidRankType");
                }

                var result = c.Query<PlayerDayResult>(query, new { idTournament = idTournament, limit = limit });

                return result;
            });
        }

        [HttpGet("dreamteamrankings/{idTournament:long}")]
        public IActionResult GetTorunamentDreamTeamRankings(long idTournament)
        {
            // 🔎 Web public use it.
            return DbOperation(c =>
            {                
                var playersDaysResults = c.Query<DreamTeamRanking>(@"
                    SELECT pdr.idPlayer, pdr.idTeam, SUM(pdr.gamesplayed) AS gamesPlayed, SUM(pdr.gameswon ) AS gamesWon, SUM(pdr.gamesdraw) AS gamesDraw, 
	                    SUM(pdr.gameslost) AS gamesLost, SUM(pdr.points) AS points, SUM(pdr.pointsagainst) AS pointsAgainst, 
	                    SUM(pdr.pointsinown) AS pointsInOwn, SUM(pdr.cardstype1) AS cardsType1, SUM(pdr.cardstype2) AS cardsType2, 
	                    SUM(pdr.assistances) AS assistances, sum (pdr.mvppoints) AS mvpPoints, SUM(pdr.penaltyPoints ) AS penaltyPoints,
	                    SUM(pdr.penaltyfailed) AS penaltyFailed, SUM(pdr.penaltystopped) AS penaltystopped, 
	                    SUM(pdr.dreamteampoints) AS dreamteamPoints, t.fieldPosition, tm.name AS teamName, p.name, p.surname, p.idPhotoImgUrl, u.avatarImgUrl
                    FROM playerdayresults pdr 
                    INNER JOIN players p ON p.id = pdr.idplayer
                    INNER JOIN users u ON u.id = p.iduser
                    INNER JOIN teams tm ON tm.id = pdr.idteam
                    INNER JOIN teamplayers t ON t.idteam = pdr.idteam
                    INNER JOIN playdays pd ON pd.id = pdr.idday 
                    WHERE pdr.idTournament = @idTournament
                    AND pd.lastupdatetimestamp IS NOT NULL
                    AND t.fieldposition != 0
                    GROUP BY pdr.idplayer, pdr.idteam, t.fieldposition, tm.name, p.name, p.surname, p.idPhotoImgUrl, u.avatarImgUrl;
                ", new { idTournament = idTournament });
                return playersDaysResults;
            });
        }

        private Tournament GetOrgAdminTournamentData(IDbConnection c, long id)
        {
            //var result = c.Get<Tournament>(id);
            //result.Teams = c.Query<Team>("SELECT teams.* FROM teams JOIN tournamentteams ON idteam = id WHERE idtournament = @id", new { id = id });

            var multi = c.QueryMultiple(@"
                SELECT * FROM tournaments WHERE id = @id;
                SELECT DISTINCT teams.* FROM teams JOIN tournamentteams ON idteam = id WHERE idtournament = @id;
                SELECT * FROM tournamentStages WHERE idTournament = @id ORDER BY sequenceOrder, id;
                SELECT * FROM stageGroups WHERE idTournament = @id ORDER BY idStage, sequenceOrder, id;
                SELECT * FROM teamGroups WHERE idTournament = @id ORDER BY idStage, idGroup, sequenceOrder;
            ", new { id = id });

            var result = multi.Read<Tournament>().GetSingle();
            result.Teams = multi.Read<Team>();
            result.Stages = multi.Read<TournamentStage>();
            result.Groups = multi.Read<StageGroup>();
            result.TeamGroups = multi.Read<TeamGroup>();

            // Fill dream team
            // Fill MVP ranking

            return result;
        }
              
        private Tournament GetPublicTournamentData(IDbConnection c, long idTournament)
        {
            var result = GetOrgAdminTournamentData(c, idTournament);

            if (!result.Visible) throw new Exception("Error.TournamentNotVisible");

            return result;
        }

        private IEnumerable<TeamDayResult> GetLeagueClassification(IDbConnection c, IDbTransaction t, TournamentStage stage, int daySequenceNumber)
        {
            var sql = @"
            SELECT tg.idGroup, t.id as idTeam, -1 as idDay,
                COALESCE(sum(gamesplayed), 0) as gamesplayed, 
                COALESCE(sum(gameswon), 0) as gameswon, 
                COALESCE(sum(gamesdraw), 0) as gamesdraw, 
                COALESCE(sum(gameslost), 0) as gameslost,
                COALESCE(sum(points), 0) as points, 
                COALESCE(sum(pointsAgainst), 0) as pointsAgainst, 
                COALESCE(sum(pointdiff), 0) as pointdiff, 
                COALESCE(sum(sanctions), 0) as sanctions, 
                COALESCE(sum(tournamentpoints), 0) as tournamentpoints
            FROM teams t 
            JOIN teamgroups tg ON tg.idteam = t.id AND tg.idstage = @idStage
            LEFT JOIN teamdayresults td ON td.idteam = t.id  AND td.idStage = @idStage
            GROUP BY tg.idGroup, t.id, t.name
            ORDER BY tournamentpoints DESC, pointdiff DESC, gameswon DESC;
            ";

            var result = c.Query<TeamDayResult>(sql, new { idStage = stage.Id }, t);

            if (!string.IsNullOrWhiteSpace(stage.ClassificationCriteria))
            {
                MatchFilter matchFilter = null;
                var criteria = JsonConvert.DeserializeObject<int[]>(stage.ClassificationCriteria);
                result = LeagueClassification.SortClassification(result, criteria, (teamA, teamB) =>
                {
                    if (matchFilter == null)
                    {
                        var matches = c.Query<Match>("SELECT idhometeam, idvisitorteam, homescore, visitorscore FROM matches WHERE idStage = @idStage", new { idStage = stage.Id }, t);
                        if (matches == null) throw new Exception("Error.NoMatches");
                        matchFilter = new MatchFilter(matches.ToList());
                    }

                    return matchFilter.GetMatchesForTeams(teamA, teamB);
                });
            }

            return result;
        }

        private IEnumerable<PlayDay> GetKnockoutClassification(IDbConnection c, IDbTransaction t, long idStage)
        {
            var par = new { idStage = idStage };

            var days = c.Query<PlayDay>(
                $"SELECT p.* FROM playdays p WHERE p.idStage = @idStage ORDER BY idGroup, id", par);

            var matches = MatchesController.GetMatchesWithField(c, t, "idStage = @idStage", par);
            var result = MatchesController.CreateDaysTree(days, matches);

            return result;
        }


        public static async Task CascadeDeleteTournament(IDbConnection c, IDbTransaction t, long idTournament)
        {
            await c.ExecuteAsync(@"
                DELETE FROM tournamentteams WHERE idTournament = @idTournament;
                
                DELETE FROM playerdayresults WHERE idTournament = @idTournament;
                DELETE FROM teamdayresults WHERE idTournament = @idTournament;
                DELETE FROM matches WHERE idTournament = @idTournament;
                
                DELETE FROM playdays WHERE idTournament = @idTournament;
                
                DELETE FROM contents WHERE idTournament = @idTournament;

                DELETE FROM teamgroups WHERE idTournament = @idTournament;
                DELETE FROM tournamentstages WHERE idTournament = @idTournament;
                DELETE FROM stagegroups WHERE idTournament = @idTournament;

                DELETE FROM tournaments WHERE id = @idTournament;
            ", new { idTournament = idTournament }, t);
        }
        
        protected override bool IsAuthorized(RequestType reqType, Tournament target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(Tournament value, IDbConnection c, IDbTransaction t)
        {
            // Check if there are teams or matches
            var numTeams = c.ExecuteScalar<int>($"SELECT count(idteam) FROM tournamentteams WHERE idtournament = @id", new { id = value.Id } );
            //var numMatches = c.ExecuteScalar<int>($"SELECT count(id) FROM matches WHERE idtournament = {value.Id}");
            //return (numTeams == 0 && numMatches == 0);

            if (numTeams > 0) throw new Exception("Error.TournamentNotEmpty");

            return true;
        }

        protected override bool ValidateEdit(Tournament value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }

        protected override bool ValidateNew(Tournament value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }

        protected override object AfterEdit(Tournament value, IDbConnection conn, IDbTransaction t)
        {
            string sql = $"UPDATE tournamentstages SET status = {value.Status} WHERE idtournament = {value.Id};";
            conn.Execute(sql, t);

            return true;
        }

        private const int RankingDefaultNumberOfResults = 15;
        private const int RankingMaxNumberOfResults = 10000;
    }

    public class UpdateTournamnetSponsorDataRequest
    {
        public long IdTournament { get; set; }
        public string SectionsJson { get; set; }
    }

    public class UpdateTournamnetAppearanceDataRequest
    {
        public long IdTournament { get; set; }
        public string AppearanceJsonString { get; set; }
    }

    public class UpdateTournamnetDreamTeamDataRequest
    {
        public long IdTournament { get; set; }
        public string DreamTeamJsonString { get; set; }
    }

    public class UpdateTournamentsSequenceOrder
    {
        public IEnumerable<TournamentSequence> TournamentsSequence { get; set; }
    }

    public class TournamentSequence
    {
        public long Id { get; set; }
        public long SequenceOrder { get; set; }
    }
        
    public class DreamTeamRanking
    {
        public long IdPlayer { get; set; }
        public long IdTeam { get; set; }
        
        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int GamesDraw { get; set; }
        public int GamesLost { get; set; }
        public int Points { get; set; }
        public int PointsAgainst { get; set; }
        public int PointsInOwn { get; set; }
        public int CardsType1 { get; set; }
        public int CardsType2 { get; set; }
        public int Assistances { get; set; }
        public int MVPPoints { get; set; }
        public int PenaltyPoints { get; set; }
        public int PenaltyFailed { get; set; }
        public int Penaltystopped { get; set; }
        public int DreamteamPoints { get; set; }    
        
        public int FieldPosition { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string TeamName { get; set; }
        public string IdPhotoImgUrl { get; set; }
        public string AvatarImgUrl { get; set; }
    }
}
