using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using webapi.Models.Db;
using Microsoft.Extensions.Options;
using Dapper;
using Dapper.Contrib.Extensions;
using contracts;
using Utils;
using Microsoft.AspNetCore.Http;
using Serilog;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace webapi.Controllers
{
    public class MatchesController : CrudController<Match>
    {
        public MatchesController(IOptions<PostgresqlConfig> dbOptions, NotificationManager notifier, IOptions<Config> config) : base(config)
        {
            mNotifier = notifier;
        }


        // __ Read actions ____________________________________________________


        [HttpGet("{idMatch}")]
        public override IActionResult Get(long idMatch)
        {
            return DbOperation(c =>
            {
                // Auth: already authorized
                // Have to fill players, referees, events, field and day

                var canHaveIdPhoto = IsOrganizationAdmin() || IsReferee();

                var match = c.Get<Match>(idMatch);
                if (match == null) throw new Exception("Error.NotFound");

                match.HomePlayers = GetTeamPlayersForMatch(c, match.Id, match.IdHomeTeam, match.IdDay, canHaveIdPhoto);
                match.VisitorPlayers = GetTeamPlayersForMatch(c, match.Id, match.IdVisitorTeam, match.IdDay, canHaveIdPhoto);
                match.Referees = GetReferees(c, match.Id);
                match.Events = GetEvents(c, match.Id);
                match.HomeTeam = GetTeam(c, match.IdHomeTeam);
                match.VisitorTeam = GetTeam(c, match.IdVisitorTeam);
                match.Field = GetField(c, match.IdField);
                match.Day = GetPlayDay(c, match.IdDay);
                match.SanctionsMatch = GetSanctionsMatch(c, match.Id);

                return match;
            });
        }

        public override IActionResult Update([FromBody] Match value)
        {
            return DbTransaction((c, t) =>
            {
                if (value == null) throw new NoDataException();

                Audit.Information(this, "{0}: Match.Update: {1}", GetUserId(), value.Id);

                CheckAuthLevel(UserLevel.OrgAdmin);

                if (!ValidateEdit(value, c, t)) throw new Exception(ValidationError);

                var dbMatch = c.Get<Match>(value.Id);
                if (dbMatch == null) throw new Exception("Error.NotFound");

                var isTimeChanged = !dbMatch.StartTime.Equals(value.StartTime);

                // Make sure the score is not altered by an API call other than events. 
                value.HomeScore = dbMatch.HomeScore;
                value.VisitorScore = dbMatch.VisitorScore;

                var result = c.Update(value, t);

                // Notify users of the time change if near enough
                if (isTimeChanged && dbMatch.Status < (int)MatchStatus.Playing && IsNearMatch(value.StartTime))
                {
                    // TODO: Retrieve team names to fill the notification
                    // ... 

                    //var text = "El horario del partido xxx vs yyy ha cambiado a las zzzz";
                    //if (value.IdHomeTeam > 0) NotificationsController.NotifyTeam(c, t, value.IdHomeTeam, "Horario de tu partido actualizado", text);
                    //if (value.IdVisitorTeam > 0) NotificationsController.NotifyTeam(c, t, value.IdVisitorTeam, "Horario de tu partido actualizado", text);
                }

                return true;
            });
        }
        public override IActionResult Delete([FromBody] Match value)
        {
            return DbTransaction((c, t) =>
            {
                if (value == null) throw new NoDataException();

                Audit.Information(this, "{0}: Match.Delete: {1}", GetUserId(), value.Id);

                CheckAuthLevel(UserLevel.OrgAdmin);

                if (!ValidateDelete(value, c, t)) throw new Exception(ValidationError);

                var dbMatch = c.Get<Match>(value.Id);
                if (dbMatch == null) throw new Exception("Error.NotFound");

                var result = c.Delete(value, t);

                return true;
            });
        }

        [HttpGet("forgroup/{idGroup}")]
        public IActionResult MatchesForGroup(long idGroup)
        {
            // Auth: it's all public data
            return DbOperation(c =>
            {
                var par = new { idGroup = idGroup };

                var days = c.Query<PlayDay>(
                    $"SELECT p.* FROM playdays p JOIN stageGroups g ON g.idStage = p.idStage WHERE g.id = @idGroup ORDER BY id", par);

                var matches = GetMatchesWithField(c, null, "idGroup = @idGroup", par);

                return CreateDaysTree(days, matches);
            });
        }

        [HttpGet("forstage/{idStage}")]
        public IActionResult MatchesForStage(long idStage)
        {
            return DbOperation(c =>
            {
                var par = new { idStage = idStage };

                var days = c.Query<PlayDay>(
                    $"SELECT p.* FROM playdays p WHERE p.idStage = @idStage ORDER BY idGroup, id", par);

                var matches = GetMatchesWithField(c, null, "idStage = @idStage", par);

                return CreateDaysTree(days, matches);
            });
        }

        [HttpGet("fortournament/{idTournament}")]
        public IActionResult MatchesForTournament(long idTournament)
        {
            // Auth: it's all public data
            return DbOperation(c =>
            {
                return GetMatchesFor(c, "idTournament = @idTournament", new { idTournament = idTournament });
            });
        }

        [HttpGet("forreferee")]
        public IActionResult MatchesForReferee()
        {
            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.Referee);

                var idUser = GetUserId();
                return GetMatchesForReferee(c, idUser);
            });
        }

        [HttpPost("dayswithmatches")]
        public IActionResult GetDaysWithMatchesByMonth([FromBody] DayMatchesFilters data)
        {
            if (data == null) throw new NoDataException();

            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.Referee);
                
                return GetDaysWithMatches(c, data);
            });
        }

        [HttpPost("filtermatches")]
        public IActionResult FilterMatches([FromBody] MatchFilters data)
        {
            if (data == null) throw new NoDataException();

            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.Referee);
                
                var idUser = GetUserId();
                return GetFilteredMatches(c, data);
            });
        }

        [HttpPost("export")]
        public IActionResult MatchesExport([FromBody] MatchFilters data)
        {
            if (data == null) throw new NoDataException();

            StringBuilder csv = null;

            DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.Referee);

                var idUser = GetUserId();
                // var filteredMatches = GetFilteredMatches(c, data);

                string finalQuery = @"SELECT m.id AS ""id"", m.startTime AS ""DateTime"", t1.name AS ""Local Team"", 
                    m.homeScore AS ""Local Score"", m.visitorScore AS ""Visitor Score"", t2.name AS ""Visitor Team"", 
                    t.name AS ""Competition"", m.idStage AS ""Play Day"", f.name AS ""Field"", u.name AS ""Referee""
                    FROM matches m 	                    
	                    LEFT JOIN teams t1 ON m.idHomeTeam = t1.id 
	                    LEFT JOIN teams t2 ON m.idVisitorTeam = t2.id 
	                    LEFT JOIN tournaments t ON m.idTournament = t.id
	                    LEFT JOIN playdays p ON m.idday = p.id
                        LEFT JOIN fields f ON f.id = m.idField 
                        LEFT JOIN matchreferees r ON r.idmatch = m.id
                        LEFT JOIN users u ON u.id = r.idUser
                        WHERE ( m.idHomeTeam != -1 AND m.idVisitorTeam != -1 )  ";

                if (data.IdSeason != null) finalQuery += $" AND t.idseason = {data.IdSeason} ";
                if (data.IdTournament != null) finalQuery += $" AND t.id = {data.IdTournament} ";
                if (data.IdTeam != null) finalQuery += $" AND ( t1.id = {data.IdTeam} OR t2.id = {data.IdTeam} ) ";
                if (data.Status != null) finalQuery += $" AND m.status = {data.Status} ";
                if (data.IdField != null) finalQuery += $" AND f.id = {data.IdField} ";
                if (data.IdReferee != null) finalQuery += $" AND r.iduser = {data.IdReferee} ";

                if (data.UseDates == true && data.StartDate != null)
                {
                    if (data.EndDate == null) finalQuery += $" AND m.starttime::date = '{data.StartDate.Value.ToString("s")}' ";
                    else finalQuery += $" AND m.starttime::date  BETWEEN '{data.StartDate.Value.ToString("s")}' AND '{data.EndDate.Value.ToString("s")}' ";
                }

                finalQuery += "ORDER BY m.starttime ASC";

                csv = ReportsController.GetCsvFromQuery(c, finalQuery,
                (i, value) =>
                {
                    var r = value.ToString();
                    // if (i == 8) return GetPositionForIndex(r);
                    // if (i == 11) return ProcessEnrollmentData(r);

                    return r;
                });

                return null;
            });

            // Convert to CSV file
            return ReportsController.GetFileContentResult(csv.ToString(), "insurance.csv");
        }

        public static object GetDaysWithMatches(IDbConnection c, DayMatchesFilters data ) 
        {
            var query = $"SELECT starttime FROM matches WHERE starttime::date between '{data.Start}' and '{data.End}'";
            return c.Query<Match>(query);
        }

        public static object GetFilteredMatches(IDbConnection c, MatchFilters data)
        {            
            string finalQuery = @"SELECT m.*, t1.id, t1.name, t1.KeyName, t1.logoImgUrl, 
                    t2.id, t2.name, t2.keyName, t2.logoImgUrl, t.id, t.name, p.id, p.name, p.sequenceOrder, f.id, f.name
                    FROM matches m 	                    
	                    LEFT JOIN teams t1 ON m.idHomeTeam = t1.id 
	                    LEFT JOIN teams t2 ON m.idVisitorTeam = t2.id 
	                    LEFT JOIN tournaments t ON m.idTournament = t.id
	                    LEFT JOIN playdays p ON m.idday = p.id
                        LEFT JOIN fields f ON f.id = m.idField 
                        LEFT JOIN matchreferees r ON r.idmatch = m.id
                        WHERE ( m.idHomeTeam != -1 AND m.idVisitorTeam != -1 )  ";
            
            if (data.IdSeason != null) finalQuery += $" AND t.idseason = {data.IdSeason} ";
            if (data.IdTournament != null) finalQuery += $" AND t.id = {data.IdTournament} ";
            if (data.IdTeam != null) finalQuery += $" AND ( t1.id = {data.IdTeam} OR t2.id = {data.IdTeam} ) ";
            if (data.Status != null) finalQuery += $" AND m.status = {data.Status} ";
            if (data.IdField != null) finalQuery += $" AND f.id = {data.IdField} ";
            if (data.IdReferee != null) finalQuery += $" AND r.iduser = {data.IdReferee} ";

            if(data.UseDates == true && data.StartDate != null)
            {
                if (data.EndDate == null) finalQuery += $" AND m.starttime::date = '{data.StartDate.Value.ToString("s")}' ";
                else finalQuery += $" AND m.starttime::date  BETWEEN '{data.StartDate.Value.ToString("s")}' AND '{data.EndDate.Value.ToString("s")}' ";                
            }

            finalQuery += "ORDER BY m.starttime ASC";


            var matches = c.Query<Match, Team, Team, Tournament, PlayDay, Field, Match>(finalQuery,
            (match, t1, t2, tournament, day, field) =>
            {
                match.HomeTeam = t1;
                match.VisitorTeam = t2;
                match.Tournament = tournament;
                match.Day = day;
                match.Field = field;
                return match;
            },
            // new { data.IdSeason },
            splitOn: "id");

            foreach (var match in matches)
            {
                match.Referees = GetReferees(c, match.Id);
            }

            return matches;
        }

        public static object GetMatchesForReferee(IDbConnection c, long idUser)
        {
            // TODO: we should start limiting to the current season
            return c.Query<Match, Team, Team, Tournament, PlayDay, Field, Match>(@"
                    SELECT m.*, t1.id, t1.name, t1.KeyName, t1.logoImgUrl, t2.id, t2.name, t2.keyName, t2.logoImgUrl, t.id, t.name, p.id, p.name, p.sequenceOrder, f.id, f.name
                    FROM matches m 
	                    JOIN matchreferees mr ON m.id = mr.idmatch
	                    LEFT JOIN teams t1 ON m.idHomeTeam = t1.id 
	                    LEFT JOIN teams t2 ON m.idVisitorTeam = t2.id 
	                    LEFT JOIN tournaments t ON m.idTournament = t.id
	                    LEFT JOIN playdays p ON m.idday = p.id
                        LEFT JOIN fields f ON f.id = m.idField
                    WHERE mr.iduser = @idUser
                    ORDER BY m.starttime ASC",
                                (match, t1, t2, tournament, day, field) =>
                                {
                                    match.HomeTeam = t1;
                                    match.VisitorTeam = t2;
                                    match.Tournament = tournament;
                                    match.Day = day;
                                    match.Field = field;
                                    return match;
                                },
                                new { idUser },
                                splitOn: "id");
        }

        public static IEnumerable<PlayDay> GetMatchesFor(IDbConnection c, string conditions, object parameters)
        {
            IEnumerable<PlayDay> days = c.Query<PlayDay>($"SELECT * FROM playdays WHERE {conditions} ORDER BY idStage, sequenceOrder, id", parameters);
            var matches = GetMatchesWithField(c, null, conditions, parameters);

            return CreateDaysTree(days, matches);
        }

        public static IEnumerable<PlayDay> CreateDaysTree(IEnumerable<PlayDay> days, IEnumerable<Match> matches)
        {
            var keyedDays = days.ToDictionary<PlayDay, long>(day => day.Id);

            foreach (var m in matches)
            {
                if (!keyedDays.TryGetValue(m.IdDay, out PlayDay day))
                {
                    day = new PlayDay { Id = m.IdDay, Name = "Jornada huérfana", IdTournament = m.IdTournament, IdStage = m.IdStage };
                    keyedDays[day.Id] = day;
                    var newDays = new List<PlayDay>(days);
                    newDays.Add(day);
                    days = newDays;
                }
                
                if (day.Matches == null) day.Matches = new List<Match>();

                day.Matches.Add(m);
            }

            return days;
        }

        [HttpGet("forday/{idDay}")]
        public IActionResult MatchesForDay(long idDay)
        {
            // Auth: it's all public data
            var condition = "m.idDay = @idDay";

            return DbOperation(c =>
            {
                return GetMatchesWithTeams(c, condition, new { idDay = idDay });
            });
        }

        [HttpGet("forteam/{idTeam}")]
        public IActionResult MatchesForTeam(long idTeam, [FromQuery(Name = "idTournament")] long idTournament)
        {
            // Auth: it's all public data
            var condition = "m.idHomeTeam = @idTeam OR m.idVisitorTeam = @idTeam";
            if (idTournament > 0) condition += " AND m.idTournament = @idTournament";

            return DbOperation(c =>
            {
                var tournament = c.Get<Tournament>(idTournament);
                if (tournament == null) throw new Exception("Error.NotFound");
                if (!tournament.Visible) throw new Exception("Error.TournamentNotVisible");

                var result = GetMatchesWithField(c, null, condition, new { idTeam = idTeam, idTournament = idTournament });

                return result;
            });
        }


        [HttpGet("forplayer/{idPlayer}")]
        public IActionResult MatchesForPlayer(long idPlayer, [FromQuery(Name = "idTournament")] long idTournament)
        {
            // Auth: it's all public data

            return DbOperation(c =>
            {
                var tournament = c.Get<Tournament>(idTournament);
                if (tournament == null) throw new Exception("Error.NotFound");
                if (!tournament.Visible) throw new Exception("Error.TournamentNotVisible");

                return GetMatchesForPlayer(c, idPlayer, idTournament);
            });
        }

        [HttpGet("forfield/{idField}")]
        public IActionResult MatchesForField(long idField, [FromQuery(Name = "idTournament")] long idTournament)
        {
            // Auth: it's all public data
            var condition = "m.idField = @idField";
            if (idTournament > 0) condition += " AND m.idTournament = @idTournament";

            return DbOperation(c =>
            {
                var tournament = c.Get<Tournament>(idTournament);
                if (tournament == null) throw new Exception("Error.NotFound");
                if (!tournament.Visible) throw new Exception("Error.TournamentNotVisible");

                return GetMatchesWithTeams(c, condition, new { idField = idField, idTournament = idTournament });
            });
        }


        // __ Write actions ___________________________________________________


        [HttpPost("createevent")]
        public IActionResult CreateMatchEvent([FromBody] MatchEvent matchEvent)
        {
            return DbTransaction((c, t) =>
            {
                if (matchEvent == null) throw new NoDataException();

                Audit.Information(this, "{0}: Match.CreateEvent {1} {2} {3} {4}", GetUserId(), matchEvent.Type, matchEvent.IdTeam, matchEvent.IdPlayer, matchEvent.IdMatch);

                // Auth: only orgadmin or referees for the match can do this action.
                if (!IsOrganizationAdmin() && !IsRefereeForMatch(c, t, matchEvent.IdMatch)) throw new UnauthorizedAccessException();

                if (!CanRefereeModifyMatch(c, t, matchEvent.IdMatch)) throw new Exception("Error.MatchIsClosed");

                // Create the event and update any team and player statistics
                matchEvent.IdCreator = GetUserId();

                var (match, me) = MatchEvent.Create(c, t, matchEvent);

                var tournament = c.Get<Tournament>(match.IdTournament);

                bool isRecordClosedEvent = me.Type == (int)MatchEventType.RecordClosed;
                bool isMVPAwardEvent = me.Type == (int)MatchEventType.MVP;

                if(!string.IsNullOrEmpty(tournament.NotificationFlags))
                {
                    try
                    {
                        JObject notificationFlags = JsonConvert.DeserializeObject<JObject>(tournament.NotificationFlags);

                        if (isMVPAwardEvent)
                        {
                            bool isNotifyAward = notificationFlags["notifyAward"] != null && (bool)notificationFlags["notifyAward"] == true;

                            // 🚧💥 Template or traductions
                            string title = "¡Logro conseguido!";
                            string message = "¡Eres el Jugador mMás Valorado de tu equipo!";

                            if (isNotifyAward) NotifyPlayer(c, t, me.IdPlayer, title, message);
                        }

                        if (isRecordClosedEvent && string.IsNullOrEmpty(tournament.NotificationFlags))
                        {
                            bool isNotificationMatchResult = notificationFlags["notifyMatchResult"] != null && (bool)notificationFlags["notifyMatchResult"] == true;
                            bool isNotifyMatchResultAllInGroup = notificationFlags["notifyMatchResultAllInGroup"] != null && (bool)notificationFlags["notifyMatchResultAllInGroup"] == true;

                            if (isNotificationMatchResult || isNotifyMatchResultAllInGroup)
                            {
                                match.HomeTeam = GetTeam(c, match.IdHomeTeam);
                                match.VisitorTeam = GetTeam(c, match.IdVisitorTeam);

                                // 🚧💥 Template or traductions
                                string title = "Acta de partido disponible";
                                string message = $"Ha finalizado {match.HomeTeam.Name} vs {match.VisitorTeam.Name} el dia {match.StartTime.Day} de {match.StartTime.Month} Resultado {match.HomeScore} - {match.VisitorScore}";

                                if (isNotifyMatchResultAllInGroup) NotifyGroupPlayers(c, t, match, title, message);
                                else if (isNotificationMatchResult) NotifyMatchPlayers(c, t, match, title, message);
                            }
                        }
                    }
                    catch (Exception) { }
                }                

                if (IsSanctionRelatedEvent(me))
                {
                    var newSanctions = CreateSanctionsForAutomaticSanctionRules(c, t, match, me, GetUserId());
                    var newEvents = CreateAutomaticSanctionEventsForEvent(c, t, match, me);

                    return new { Match = match, Event = me, newEvents, newSanctions };
                }
                return new { Match = match, Event = me };
            });
        }

        [HttpPost("deleteevent")]
        public IActionResult DeleteMatchEvent([FromBody] MatchEvent matchEvent)
        {
            return DbTransaction((c, t) =>
            {
                if (matchEvent == null) throw new NoDataException();

                Audit.Information(this, "{0}: Match.DeleteEvent {1} {2} {3} {4} {5}", GetUserId(), matchEvent.Id, matchEvent.Type, matchEvent.IdTeam, matchEvent.IdPlayer, matchEvent.IdMatch);

                // Auth: only orgadmin or referees for the match can do this action.
                if (!IsOrganizationAdmin() && !IsRefereeForMatch(c, t, matchEvent.IdMatch)) throw new UnauthorizedAccessException();

                if (!CanRefereeModifyMatch(c, t, matchEvent.IdMatch)) throw new Exception("Error.MatchIsClosed");

                // Remove the event and undo the player and team statistics
                var match = MatchEvent.Delete(c, t, matchEvent);

                return match;
            });
        }


        [HttpPost("addday")]
        public IActionResult AddDayToTournament([FromBody] PlayDay day)
        {
            return DbTransaction((c, t) =>
            {
                if (day == null) throw new NoDataException();
                if (day.IdTournament <= 0 || day.IdStage <= 0 || day.Name == null) throw new Exception("Error.InvalidData");

                CheckAuthLevel(UserLevel.OrgAdmin);

                var existingDay = c.QueryFirstOrDefault<PlayDay>("SELECT * FROM playdays WHERE name = @Name AND idStage = @IdStage AND idTournament = @IdTournament", day, t);
                if (existingDay != null) throw new Exception("Error.PlayDay.AlreadyExists");

                day.Dates = "";

                var newId = c.Insert(day, t);

                return newId;
            });
        }

        [HttpPost("deleteday")]
        public IActionResult RemoveDayFromTournament([FromBody] PlayDay day)
        {
            return DbTransaction((c, t) =>
            {
                if (day == null) throw new NoDataException();
                if (day.Id <= 0 || day.IdTournament <= 0 || day.IdStage <= 0 || day.Name == null) throw new Exception("Error.InvalidData");

                CheckAuthLevel(UserLevel.OrgAdmin);

                // Not allowed if there are matches in the day
                var numMatches = c.ExecuteScalar<int>("SELECT COUNT(id) FROM matches WHERE idDay = @idDay", new { idDay = day.Id }, t);
                if (numMatches > 0) throw new Exception("Error.PlayDayHasMatches");

                var result = c.Delete(day, t);

                return result;
            });
        }

        [HttpPost("linkplayer/{matchId}/{playerId}")]
        public IActionResult LinkPlayer(long matchId, long playerId)
        {
            // Auth: only orgadmin
            // Creates a matchPlayer record. 

            throw new NotImplementedException();
        }
        

        [HttpPost("linkreferee")]
        public IActionResult LinkReferee([FromBody] MatchReferee matchReferee)
        {
            return DbOperation(c =>
            {
                // Auth: only orgadmin
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                // Creates a matchreferee record. 
                c.Insert(matchReferee);


                // TODO: Notify referee
                //mNotifier.SendCannedNotification(c, null, "RefereeMatch", GetUserId(), matchReferee.IdUser, matchReferee.IdMatch);
                //mNotifier.NotifyPush(c, null, TemplateKeys.PushRefereeLinkedToMatch, )

                return true;
            });
        }

        [HttpPost("unlinkreferee")]
        public IActionResult UnlinkReferee([FromBody] MatchReferee matchReferee)
        {
            return DbOperation(c =>
            {
                // Auth: only orgadmin
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                // Creates a matchreferee record. 
                var result = c.Delete(matchReferee);

                // TODO: Notify referee.

                if (!result) throw new Exception("Error.NotFound");

                return true;
            });
        }

        [HttpPost("setplayermatchapparelnumber")]
        public IActionResult SetPlayerMatchApparelNumber([FromBody] PlayerAttendance playerAttendance) 
        {
            // REFACTOR merge with setplayerattendance
            return DbTransaction((c, t) =>
            {
                if (!IsOrganizationAdmin() && !IsRefereeForMatch(c, t, playerAttendance.IdMatch)) throw new UnauthorizedAccessException();

                if (!CanRefereeModifyMatch(c, t, playerAttendance.IdMatch)) throw new Exception("Error.MatchIsClosed");

                var mr = c.QueryMultiple(@"
                        SELECT apparelNumber FROM teamPlayers WHERE idplayer = @idPlayer AND idTeam = @idTeam;
                        SELECT id, idUser FROM players WHERE id = @idPlayer;
                        SELECT * FROM matchplayers WHERE idMatch = @idMatch AND idPlayer = @idPlayer AND idTeam = @idTeam;
                        ", playerAttendance, t);

                var teamPlayer = mr.Read<TeamPlayer>().GetSingle();
                var player = mr.Read<Player>().GetSingle();

                var matchPlayer = mr.ReadFirstOrDefault<MatchPlayer>();
                if (matchPlayer == null)
                {
                    // not found, insert
                    matchPlayer = new MatchPlayer
                    {
                        IdTeam = playerAttendance.IdTeam,
                        IdPlayer = playerAttendance.IdPlayer,
                        IdUser = playerAttendance.IdUser,
                        IdDay = playerAttendance.IdDay,
                        IdMatch = playerAttendance.IdMatch,
                        Status = playerAttendance.Attended ? 1 : 0 ,
                        ApparelNumber = playerAttendance.ApparelNumber
                    };
                    c.Insert(matchPlayer, t);
                }
                else
                {
                    // already exists, update
                    matchPlayer.ApparelNumber = playerAttendance.ApparelNumber;
                    c.Update(matchPlayer, t);
                }

                return true;
            });
        }

        [HttpPost("setplayerattendance")]
        public IActionResult SetPlayerAttendance([FromBody] PlayerAttendance playerAttendance)
        {
            return DbTransaction((c, t) =>
            {
                if (!IsOrganizationAdmin() && !IsRefereeForMatch(c, t, playerAttendance.IdMatch)) throw new UnauthorizedAccessException();

                if (!CanRefereeModifyMatch(c, t, playerAttendance.IdMatch)) throw new Exception("Error.MatchIsClosed");

                if (playerAttendance.Attended)
                {
                    var mr = c.QueryMultiple(@"
                        SELECT apparelNumber FROM teamPlayers WHERE idplayer = @idPlayer AND idTeam = @idTeam;
                        SELECT id, idUser FROM players WHERE id = @idPlayer;
                        SELECT * FROM matchplayers WHERE idMatch = @idMatch AND idPlayer = @idPlayer AND idTeam = @idTeam;
                        ", playerAttendance, t);

                    var teamPlayer = mr.Read<TeamPlayer>().GetSingle();
                    var player = mr.Read<Player>().GetSingle();

                    var matchPlayer = mr.ReadFirstOrDefault<MatchPlayer>();                    
                    if (matchPlayer == null)
                    {
                        // not found, insert
                        matchPlayer = new MatchPlayer
                        {
                            IdTeam = playerAttendance.IdTeam, 
                            IdPlayer = playerAttendance.IdPlayer,
                            IdUser = playerAttendance.IdUser, 
                            IdDay = playerAttendance.IdDay, 
                            IdMatch = playerAttendance.IdMatch, 
                            Status = 1,
                            ApparelNumber = playerAttendance.ApparelNumber
                        };
                        c.Insert(matchPlayer, t);
                    }
                    else
                    {
                        // already exists, update
                        matchPlayer.Status = 1;
                        c.Update(matchPlayer, t);
                    }
                }
                else
                {
                    // Check if the player has events in the match. 
                    var numEvents = c.ExecuteScalar<int>("SELECT count(*) FROM matchevents WHERE idplayer = @idplayer AND idMatch = @idMatch", playerAttendance, t);
                    if (numEvents > 1) throw new Exception("Error.PlayerHasEventsInMatch");

                    // If no events, we can delete the attendance record
                    c.Execute("DELETE FROM matchplayers WHERE idMatch = @idMatch AND idPlayer = @idPlayer AND idTeam = @idTeam", playerAttendance, t);
                }

                return true;
            });
        }

        [HttpPost("updatecloserecord")]
        public IActionResult UpdateCloseRecord([FromBody] MatchRecordCloseData data)
        {
            return DbTransaction((c, t) =>
            {
                if (data == null) throw new NoDataException();
                if (!IsOrganizationAdmin() && !IsRefereeForMatch(c, t, data.IdMatch)) throw new UnauthorizedAccessException();

                // For now, we enforce that the refereee cannot make changes after the match is closed. This may change in the future,
                // allowing the referee to make changes if signed by the team admins with the pin. 
                if (!CanRefereeModifyMatch(c, t, data.IdMatch)) throw new Exception("Error.MatchIsClosed");

                // Check team pins
                // TBD

                // Update the text in the match
                var n = c.Execute("UPDATE matches SET comments = @comments WHERE id = @idMatch", data, t);
                if (n != 1) throw new Exception("Error.UpdatingMatchComments");

                // Create match record closed event
                var (match, me) = CreateRecordClosedEvent(c, t, data.IdMatch);

                // Add any new sanctions
                var newSanctions = CreateSanctionsForAutomaticSanctionRules(c, t, match, me, GetUserId());

                return new { Match = match, Event = me, newSanctions };
            });
        }


        // __ CRUD ____________________________________________________________


        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "Matches"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, Match target, IDbConnection conn)
        {
            if (reqType == RequestType.GetAll) return false;        // All matches: nobody should do that

            if (reqType == RequestType.GetSingle) return true;      // Get match details: is public data.

            // Write actions: only orgadmin
            return (IsOrganizationAdmin());
        }

        protected override bool ValidateDelete(Match value, IDbConnection c, IDbTransaction t)
        {
            var numEvents = c.ExecuteScalar<int>("SELECT COUNT(*) FROM matchEvents WHERE idMatch = @id", new { id = value.Id }, t);
            if (numEvents > 0) throw new Exception("Error.MatchHasEvents");

            // var numSanctions = c.ExecuteScalar<int>("SELECT COUNT(*) FROM matchSanctions WHERE idMatch = @id", new { id = value.Id }, t);
            // changet table name matchSanctions => sanctionmatches
            var numSanctions = c.ExecuteScalar<int>("SELECT COUNT(*) FROM sanctionmatches WHERE idMatch = @id", new { id = value.Id }, t);

            if (numSanctions > 0) throw new Exception("Error.MatchHasSanctions");

            return true;
        }

        protected override bool ValidateEdit(Match value, IDbConnection c, IDbTransaction t)
        {
            // Check if field is available on that hour.
            isFieldAvailable(c, value);

            // Teams cannot be changed if they have events associated
            var mr = c.QueryMultiple(@"
                SELECT * FROM matches WHERE id = @idMatch; 
                SELECT COUNT(*) FROM matchEvents WHERE idMatch = @idMatch;
                ", new { idMatch = value.Id } );

            var dbMatch = mr.Read<Match>().GetSingle();
            var numEvents = mr.ReadSingle<long>();
            if (numEvents == 0) return true;

            // We have events, check that things are not being changed beyond reasonable

            if (dbMatch.IdDay != value.IdDay) throw new Exception("Error.MatchEdit.Day");
            if (dbMatch.IdHomeTeam != value.IdHomeTeam) throw new Exception("Error.MatchEdit.Team");
            if (dbMatch.IdVisitorTeam != value.IdVisitorTeam) throw new Exception("Error.MatchEdit.Team");

            if (dbMatch.IdField != value.IdField && dbMatch.Status < (int)MatchStatus.Playing) NotifyMatchPeople(Request, c, t, dbMatch, "match.fieldupdated.push", new MatchNotificationData { Match = dbMatch });

            return true;
        }

        protected override object AfterNew(Match value, IDbConnection c, IDbTransaction t)
        {
            // When match is created, make sure the stageGroup is marcked as having a calendar
            SetGroupHasCalendarFlag(c, t, value.IdGroup);

            return base.AfterNew(value, c, t);
        }

        protected override bool ValidateNew(Match value, IDbConnection c, IDbTransaction t)
        {
            // Check if field is available on that hour.
            isFieldAvailable(c, value);

            return true;
        }


        // __ Helpers _________________________________________________________
        private bool isFieldAvailable(IDbConnection c, Match match)
        {
            long idField = match.IdField;
            long currentMatchId = match.Id;

            if (idField != 0)
            {
                DateTime startTime = match.StartTime;

                // Ranges inside starTime hour
                string initialDateTimeString = new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0).ToString("yyyy-MM-dd HH:mm:ss.fff");
                string finalDateTimeString = new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 59, 59).ToString("yyyy-MM-dd HH:mm:ss.fff");

                var validRangeDate = c.Query($"SELECT id FROM matches WHERE id != {currentMatchId} AND idfield = {idField} AND starttime BETWEEN '{initialDateTimeString}' AND '{finalDateTimeString}';");

                if (validRangeDate.Count() > 0) throw new Exception("Error: Field is allready in use");
            }

            return true;
        }

        private (Match, MatchEvent) CreateRecordClosedEvent(IDbConnection c, IDbTransaction t, long idMatch)
        {
            return MatchEvent.Create(c, t, new MatchEvent
            {
                IdMatch = idMatch,
                Type = (int)MatchEventType.RecordClosed,
                IdCreator = GetUserId(),
                MatchMinute = 200,
            });
        }


        private void SetGroupHasCalendarFlag(IDbConnection c, IDbTransaction t, long idGroup)
        {
            // When match is created, make sure the stageGroup is marcked as having a calendar
            var dbGroup = c.Get<StageGroup>(idGroup, t);
            if (dbGroup == null) throw new Exception("Error.MatchHasNoGroup");

            if ((dbGroup.Flags & (int)StageGroupFlags.HasGeneratedCalendar) == 0)
            {
                dbGroup.Flags |= (int)StageGroupFlags.HasGeneratedCalendar;
                c.Update(dbGroup, t);
            }
        }

        private bool CanRefereeModifyMatch(IDbConnection c, IDbTransaction t, long idMatch)
        {
            if (IsOrganizationAdmin()) return true;

            var recordClosed = c.Query<MatchEvent>("SELECT id FROM matchEvents WHERE idMatch = @idMatch AND type = @type", new { idMatch, type = (int)MatchEventType.RecordClosed }, t);
            return (recordClosed == null || recordClosed.Count() == 0);
        }

        private bool IsRefereeForMatch(IDbConnection c, IDbTransaction t, long idMatch)
        {
            // This is a great candidate for caching
            var currentUserId = GetUserId();

            foreach (var r in MatchReferee.GetForMatch(c, t, idMatch))
            {
                if (currentUserId == r.IdUser) return true;
            }

            return false;
        }


        private static IEnumerable<Player> GetTeamPlayersForMatch(IDbConnection c, long idMatch, long idTeam, long idDay, bool canHaveIdPhoto)
        {   
            var sql = @"
                SELECT 
                    p.id, p.name, p.surname, p.idUser, p.birthdate, p.idPhotoImgUrl, sm.idsanction, s.idTeam AS idSanctionTeam,
                    tp.idteam, tp.apparelnumber, tp.fieldPosition,
                    mp.*,
                    u.id, u.avatarImgUrl,
                    pdr.* 
                FROM teamplayers tp 
                     JOIN players p ON p.id = tp.idPlayer AND tp.status & 256 = 256
                     JOIN users u ON p.idUser = u.id
                     LEFT JOIN matchplayers mp ON mp.idPlayer = tp.idPlayer AND mp.idMatch = @idMatch
                     LEFT JOIN playerdayresults pdr ON p.id = pdr.idPlayer AND pdr.idDay = @idDay AND pdr.idteam = @idTeam
                     LEFT JOIN sanctionmatches sm ON sm.idMatch = @idMatch AND sm.idPlayer = p.id
                     LEFT JOIN sanctions s ON s.id = sm.idSanction
                WHERE 
                      tp.idTeam = @idTeam
                ORDER BY 
                      tp.apparelNumber
                ";

            var result = c.Query<Player, TeamPlayer, MatchPlayer, User, PlayerDayResult, Player>(sql, 
                (player, teamPlayer, matchPlayer, user, playerDayResult) =>
                {
                    if (!canHaveIdPhoto) player.IdPhotoImgUrl = null;

                    player.MatchData = matchPlayer;
                    player.TeamData = teamPlayer;
                    player.UserData = user;
                    player.DayResultSummary = playerDayResult;
                    return player;
                },
                new { idMatch = idMatch, idTeam = idTeam, idDay = idDay },
                splitOn: "idTeam, idMatch, id, idPlayer");

            // Remove duplicate sanctioned players (same player id, different idsanction)
            result = result.GroupBy(player => player.Id).Select(y => y.First());

            return result;
        }

        private static IEnumerable<MatchReferee> GetReferees(IDbConnection c, long idMatch)
        {
            return c.Query<MatchReferee, User, MatchReferee>("SELECT mr.*, u.name, u.avatarImgUrl FROM matchreferees mr JOIN users u ON mr.iduser = u.id WHERE mr.idmatch = @idMatch", 
                (mr, user) =>
                {
                    mr.Referee = user;
                    return mr;
                },
                new { idMatch = idMatch },
                splitOn: "name");
        }

        private static IEnumerable<MatchEvent> GetEvents(IDbConnection c, long idMatch)
        {
            return c.Query<MatchEvent>("SELECT * FROM matchevents WHERE idMatch = @idMatch ORDER BY matchMinute DESC, timeStamp DESC", new { idMatch = idMatch });
        }

        // 🚧🚧🚧
        private static IEnumerable<Sanction> GetSanctionsMatch(IDbConnection c, long idMatch)
        {
            return c.Query<Sanction>("SELECT * FROM sanctions WHERE idMatch = @idMatch", new { idMatch = idMatch });
        }

        private static Team GetTeam(IDbConnection c, long idTeam)
        {
            return c.Get<Team>(idTeam);
        }

        private static Field GetField(IDbConnection c, long idField)
        {
            return c.Get<Field>(idField);
        }

        private static PlayDay GetPlayDay(IDbConnection c, long idDay)
        {
            return c.Get<PlayDay>(idDay);
        }


        private static IEnumerable<Match> GetMatchesForPlayer(IDbConnection c, long idPlayer, long idTournament)
        {
            var condition = "";
            if (idTournament > 0) condition += " WHERE m.idTournament = @idTournament";

            //var sql = @"
            //    SELECT m.*, t1.name, t1.KeyName, t1.logoImgUrl, t2.name, t2.keyName, t2.logoImgUrl 
            //    FROM matches m JOIN matchplayers mp ON mp.idMatch = m.id AND mp.idPlayer = @idPlayer JOIN teams t1 ON m.idHomeTeam = t1.id JOIN teams t2 ON m.idVisitorTeam = t2.id"
            //    + condition +
            //    " ORDER BY m.id";

            var sql = @"SELECT m.*, t1.id, t1.name, t1.KeyName, t1.logoImgUrl, t2.id, t2.name, t2.keyName, t2.logoImgUrl
                        FROM matches m JOIN teams t1 ON m.idHomeTeam = t1.id JOIN teams t2 ON m.idVisitorTeam = t2.id 
                            JOIN teamplayers tp ON tp.idPlayer = @idPlayer AND (t1.id = tp.idteam OR t2.id = tp.idteam) "
                    + condition +
                    " ORDER BY m.id";

            return GetMatches(c, sql, new { idPlayer = idPlayer, idTournament = idTournament });
        }


        public static IEnumerable<Match> GetMatchesWithField(IDbConnection c, IDbTransaction t, string condition, object param = null)
        {
            var sql = 
                "SELECT m.*, f.id, f.name FROM matches m LEFT JOIN fields f ON m.idfield = f.id " + 
                "WHERE " + condition + " ORDER BY m.idStage, m.idGroup, startTime, m.id";

            var result = c.Query<Match, Field, Match>(sql, 
                (match, field) =>
                {
                    match.Field = field;
                    return match;
                },
                param, t,
                splitOn: "id");

            return result;
        }

        private static IEnumerable<Match> GetMatchesWithTeams(IDbConnection c, string condition, object param = null)
        {
            var sql = @"SELECT m.*, t1.id, t1.name, t1.KeyName, t1.logoImgUrl, t2.id, t2.name, t2.keyName, t2.logoImgUrl 
                FROM matches m LEFT JOIN teams t1 ON m.idHomeTeam = t1.id LEFT JOIN teams t2 ON m.idVisitorTeam = t2.id 
                WHERE " + condition + " ORDER BY m.idStage, m.idGroup, m.id";

            return GetMatches(c, sql, param);
        }

        private static IEnumerable<Match> GetMatches(IDbConnection c, string query, object param)
        {
            var result = c.Query<Match, Team, Team, Match>(query,
                 (match, t1, t2) =>
                 {
                     match.HomeTeam = t1;
                     match.VisitorTeam = t2;
                     return match;
                 },
                 param);

            return result;
        }

        private static bool IsSanctionRelatedEvent(MatchEvent me)
        {
            return ( me.Type == (int)MatchEventType.RecordClosed 
                 || (me.Type >= (int)MatchEventType.Card1 && me.Type <= (int)MatchEventType.Card5)
                   );
        }

        private static IEnumerable<Sanction> CreateSanctionsForAutomaticSanctionRules(IDbConnection c, IDbTransaction t, Match match, MatchEvent me, long idUser)
        {
            if (match == null || me == null || me.Type != (int)MatchEventType.RecordClosed) return null;

            var sanctions = AutoSanctionDispatcher.GetSanctionsForMatch(c, t, match);

            var result = ApplyMatchSanctions(c, t, idUser, sanctions);

            return result;
        }

        public static IEnumerable<Sanction> ApplyMatchSanctions(IDbConnection c, IDbTransaction t, long idUser, IEnumerable<Sanction> sanctions)
        {
            if (sanctions == null || sanctions.Count() == 0) return null;

            var result = new List<Sanction>();

            foreach (var sanction in sanctions)
            {
                result.Add(SanctionsController.CreateSanction(c, t, sanction, idUser));
            }

            return result;
        }

        private static IEnumerable<MatchEvent> CreateAutomaticSanctionEventsForEvent(IDbConnection c, IDbTransaction t, Match match, MatchEvent me)
        {
            if (me.Type < (int)MatchEventType.Card1 && me.Type > (int)MatchEventType.Card5) return null;

            var events = AutoSanctionDispatcher.GetNewCardEventsAfterCard(c, t, me, match.IdTournament);

            var result = ApplyMatchEvents(c, t, events);

            return result;
        }

        public static IEnumerable<MatchEvent> ApplyMatchEvents(IDbConnection c, IDbTransaction t, IEnumerable<MatchEvent> events)
        {
            if (events == null || events.Count() == 0) return null;

            var result = new List<MatchEvent>();

            foreach (var ev in events)
            {
                var (dbMatch, dbMatchEvent) = MatchEvent.Create(c, t, ev);
                result.Add(dbMatchEvent);
            }

            return result;
        }


        // ____________________________________________________________________
        private void NotifyMatchPlayers(IDbConnection c, IDbTransaction t, Match match, string title, string message)
        {
            Audit.Information(this, "{0}: Notifications.NotifyMatch.RecordClosed: {1} | {2}", GetUserId(), title, message); // LOG

            // 🔎 Multiple devices
            int notificationsNumber = NotificationsController.NotifyMatch(c, null, match, title, message);

            var teamPlayers = c.Query<Player>($"SELECT tp.idplayer, p.id, p.iduser FROM teamplayers tp JOIN players p ON tp.idPlayer = p.id WHERE idTeam IN({match.HomeTeam.Id}, {match.VisitorTeam.Id})");

            foreach (var player in teamPlayers)
            {
                c.Insert(new Notification { IdCreator = GetUserId(), IdRcptUser = player.IdUser, Status = (int)NotificationStatus.Unread, Text = message, Text2 = title, TimeStamp = DateTime.Now });
            }
        }

        private void NotifyGroupPlayers(IDbConnection c, IDbTransaction t, Match match, string title, string message)
        {
            Audit.Information(this, "{0}: Notifications.NotifyMatch.RecordClosed: {1} | {2}", GetUserId(), title, message); // LOG

            // 🔎 Multiple devices
            int notificationsNumber = NotificationsController.NotifyTeamGroup(c, null, match.IdGroup, title, message);

            var teamPlayers = c.Query<Player>($"SELECT tp.idplayer, p.id, p.iduser FROM teamplayers tp JOIN teamgroups tg ON tg.idteam = tp.idteam JOIN players p ON tp.idPlayer = p.id WHERE tg.idgroup = {match.IdGroup}");

            foreach (var player in teamPlayers)
            {
                c.Insert(new Notification { IdCreator = GetUserId(), IdRcptUser = player.IdUser, Status = (int)NotificationStatus.Unread, Text = message, Text2 = title, TimeStamp = DateTime.Now });
            }
        }

        private void NotifyPlayer(IDbConnection c, IDbTransaction t, long playerId, string title, string message)
        {
            Audit.Information(this, "{0}: Notifications.NotifyPlayer: {1} | {2}", GetUserId(), title, message); // LOG

            var player = c.QueryFirst<Player>($"SELECT * FROM palyers WHERE id = {playerId}");

            // 🔎 Multiple devices
            int notificationsNumber = NotificationsController.NotifyUser(c, null, player.IdUser, title, message);

            c.Insert(new Notification { IdCreator = GetUserId(), IdRcptUser = player.IdUser, Status = (int)NotificationStatus.Unread, Text = message, Text2 = title, TimeStamp = DateTime.Now });
        }

        private void NotifyMatchPeople(HttpRequest req, IDbConnection c, IDbTransaction t, Match match, string template, BaseTemplateData data)
        {
            // Notify: Go through all players and referees and send canned notification
        }

        public static bool IsNearMatch(DateTime startTime)
        {
            // Defined as less than 3 days
            var interval = startTime - DateTime.Now;
            return (interval.TotalDays < 3);
        }


        private readonly NotificationManager mNotifier;
    }


    public class PlayerAttendance
    {
        public long IdMatch { get; set; }
        public long IdTeam { get; set; }
        public long IdPlayer { get; set; }
        public long IdUser { get; set; }
        public long IdDay { get; set; }
        public int ApparelNumber { get; set; }
        public bool Attended { get; set; }
    }

    public class MatchRecordCloseData
    {
        public long IdMatch { get; set; }
        public string Comments { get; set; }

        public string HomeTeamPin { get; set; }
        public string VisitorTeamPin { get; set; }
        public string RefereePin { get; set; }
    }

    public class MatchFilters
    {        
        public Nullable<long> IdSeason { get; set; }
        public Nullable<long> IdTournament { get; set; }
        public Nullable<long> IdTeam { get; set; }
        public Nullable<long> Status { get; set; }
        public Nullable<long> IdField { get; set; }
        public Nullable<long> IdReferee { get; set; }
        public bool UseDates { get; set; }
        public Nullable<DateTime> StartDate { get; set; }
        public Nullable<DateTime> EndDate { get; set; }
    }

    public class DayMatchesFilters
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }

}