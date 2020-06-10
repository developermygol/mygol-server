using Dapper;
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

namespace webapi.Controllers
{

    public class SanctionsController : CrudController<Sanction>
    {
        public SanctionsController(IOptions<Config> config) : base(config)
        {
        }

        public override IActionResult Get(long id)
        {
            return DbOperation(c =>
            {
                if (!IsAuthorized(RequestType.GetAll, null, c)) throw new UnauthorizedAccessException();

                var condition = IsLoggedIn() ? "" : "AND sa.visible = 't'";   // Public site can only see visible allegations.

                var mr = c.QueryMultiple(@"
                    SELECT * FROM sanctions s LEFT JOIN players p ON s.idplayer = p.id LEFT JOIN teams t ON t.id = s.idteam JOIN tournaments tnmt ON s.idtournament = tnmt.id WHERE s.id = @id;
                    SELECT sa.*, u.id, u.name, u.level FROM sanctionallegations sa LEFT JOIN users u ON sa.idUser = u.id WHERE idSanction = @id " + condition + @" ORDER BY sa.date ASC;
                    ", new { id = id });

                var sanctions = mr.Read<Sanction, Player, Team, Tournament, Sanction>((sanction, player, team, tournament) =>
                {
                    sanction.Player = player;
                    sanction.Team = team;
                    sanction.Tournament = tournament;
                    return sanction;
                });

                var result = sanctions.GetSingle();

                result.Allegations = mr.Read<SanctionAllegation, User, SanctionAllegation>((allegation, user) =>
                {
                    allegation.User = user;
                    return allegation;
                });

                result.SanctionMatches = GetFullSanctionMatches(c, null, id);

                return result;
            });
        }

        [HttpGet("all")]
        public IActionResult GetAllSanctions()
        {
            return DbOperation(c =>
            {
                return GetSanctionsFor(c, null, "s.id = s.id", null, IsOrganizationAdmin());
            });
        }

        [HttpGet("fortournament/{idTournament:long}")]
        public IActionResult GetSanctionsForTournament(long idTournament)
        {
            return DbOperation(c =>
            {
                return GetSanctionsFor(c, null, "s.idTournament = @idTournament", new { idTournament }, IsOrganizationAdmin());
            });
        }

        [HttpGet("summaryfortournament/{idTournament:long}")]
        public IActionResult GetSummarySanctionsForTournament(long idTournament)
        {
            return DbOperation(c =>
            {
                return GetSanctionsFor(c, null, "s.idTournament = @idTournament AND (s.status = 1 OR s.status = 2)", new { idTournament }, IsOrganizationAdmin());
            });
        }

        [HttpGet("forteam/{idTeam:long}/{idTournament:long}")]
        public IActionResult GetSanctionsForTeam(long idTeam, long idTournament)
        {
            return DbOperation(c =>
            {
                return GetSanctionsFor(c, null, "s.idTeam = @idTeam AND s.idTournament = @idTournament ", new { idTeam, idTournament }, IsOrganizationAdmin());
            });
        }

        [HttpGet("forplayer/{idPlayer:long}")]
        public IActionResult GetSanctionsForPlayer(long idPlayer)
        {
            return DbOperation(c =>
            {
                return GetSanctionsForPlayer(c, null, idPlayer, IsOrganizationAdmin());
            });
        }

        [HttpGet("formatch/{idMatch:long}")]
        public IActionResult GetSanctionsForMatch(long idMatch)
        {
            return DbOperation(c =>
            {
                return GetSanctionsFor(c, null, "s.idMatch = @idMatch", new { idMatch }, IsOrganizationAdmin());
            });
        }

        [HttpPost("recalculatematches")]
        public IActionResult RecalculateSanctionMatches([FromBody] Sanction sanction)
        {
            return DbTransaction((c, t) =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                if (sanction == null) throw new NoDataException();

                UpdatePlayerSanctionMatches(c, t, sanction);

                return true;
            });
        }

        internal static IEnumerable<Sanction> GetSanctionsForPlayer(IDbConnection c, IDbTransaction t, long idPlayer, bool isOrgAdmin)
        {
            return GetSanctionsFor(c, t, "s.idPlayer = @idPlayer", new { idPlayer }, isOrgAdmin);
        }


        private static IEnumerable<Sanction> GetSanctionsFor(IDbConnection c, IDbTransaction t, string condition, object args, bool isOrgAdmin, int limit = 0)
        {
            var otherPlayerFields = isOrgAdmin ? "p.idPhotoImgUrl, " : "";
            var limitClause = (limit > 0) ? " LIMIT " + limit : ""; 
            var sql = $"SELECT s.*, p.id, p.name, p.surname, {otherPlayerFields} t.id, t.name FROM sanctions s LEFT JOIN players p ON p.id = s.idPlayer LEFT JOIN teams t ON t.id = s.idteam WHERE {condition} ORDER BY s.startDate DESC, s.id DESC {limitClause}";

            var result = c.Query<Sanction, Player, Team, Sanction>(sql,
                (sanction, player, team) =>
                {
                    sanction.Player = player;
                    sanction.Team = team;
                    return sanction;
                }, args);

            return result;
        }


        // __ CRUD impl _______________________________________________________


        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "sanctions"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, Sanction target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(Sanction value, IDbConnection c, IDbTransaction t)
        {
            // check if there are payments associated to this sanction
            var sanction = c.Get<Sanction>(value.Id, t);
            if (sanction == null) throw new Exception("Error.NotFound");
            if (sanction.IdPayment > 0) throw new Exception("Error.SanctionHasPayment");

            return true;
        }

        protected override bool ValidateEdit(Sanction value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateNew(Sanction value, IDbConnection c, IDbTransaction t)
        {
            value.IsAutomatic = false;

            return true;
        }


        public static Sanction CreateSanction(IDbConnection c, IDbTransaction t, Sanction sanction, long idUser)
        {
            var id = c.Insert(sanction, t);
            sanction.Id = id;

            AfterCreateSanction(c, t, sanction, idUser);

            return sanction;
        }

        private static object AfterCreateSanction(IDbConnection c, IDbTransaction t, Sanction value, long idCreator)
        {
            // Insert the first allegation
            var newAllegation = new SanctionAllegation
            {
                Content = value.InitialContent,
                IdSanction = value.Id,
                IdUser = idCreator,
                Date = DateTime.Now,
                Status = (int)SanctionAllegationStatus.Created,
                Title = Localization.Get("Resolución comité de competición", null),
                Visible = true
            };

            c.Insert(newAllegation, t);

            if (value.Type == (int)SanctionType.Team)
            {
                // Apply penalty for team: tournament points
                CreateTeamSanctionPenalty(c, t, value);
                FillSanctionTeam(c, t, value);
            }
            else
            {
                // Create sanction matches for this sanction
                CreatePlayerSanctionMatches(c, t, value);
                FillSanctionTeamAndPlayer(c, t, value);

                // Notify push to player
                var playerIdUser = c.ExecuteScalar<long>("SELECT idUser FROM players WHERE id = @id", new { id = value.IdPlayer });

                NotificationsController.TryNotifyUser(c, t, playerIdUser, Localization.Get("Nueva sanción", null), Localization.Get("Se te ha sancionado con {0} partidos.", null, value.NumMatches));
            }


            return value;
        }

        private static void FillSanctionTeam(IDbConnection c, IDbTransaction t, Sanction value)
        {
            value.Team = c.Query<Team>("SELECT id, name FROM teams WHERE id = @idTeam", new { idTeam = value.IdTeam }, t).FirstOrDefault();
        }

        private static void FillSanctionTeamAndPlayer(IDbConnection c, IDbTransaction t, Sanction value)
        {
            var mr = c.QueryMultiple(@"
                SELECT id, name FROM teams WHERE id = @idTeam;
                SELECT id, name, surname FROM players WHERE id = @idPlayer;
            ", new { idTeam = value.IdTeam, idPlayer = value.IdPlayer }, t);

            value.Team = mr.ReadFirstOrDefault<Team>();
            value.Player = mr.ReadFirstOrDefault<Player>();
        }

        protected override object AfterNew(Sanction value, IDbConnection c, IDbTransaction t)
        {
            return AfterCreateSanction(c, t, value, GetUserId());
        }

        protected override object AfterEdit(Sanction value, IDbConnection c, IDbTransaction t)
        {
            if (value.Type == (int)SanctionType.Team)
            {
                UpdateTeamSanctionPenalty(c, t, value);
            }
            else
            {
                UpdatePlayerSanctionMatches(c, t, value);
                FillSanctionMatches(c, t, value);
            }

            return value;
        }


        protected override object AfterDelete(Sanction value, IDbConnection c, IDbTransaction t)
        {
            if (value.Type == (int)SanctionType.Team)
            {
                DeleteTeamSanctionPenalty(c, t, value);
            }
            else
            {
                DeletePlayerSanctionMatches(c, t, value.Id);
            }

            // Cascade delete associated allegations
            c.Execute("DELETE FROM sanctionallegations WHERE idSanction = @idSanction", new { idSanction = value.Id }, t);

            return true;
        }


        // __ Sanction matches ________________________________________________


        public static IEnumerable<Match> GetFullSanctionMatches(IDbConnection c, IDbTransaction t, long idSanction)
        {
            var select = @"SELECT m.*, t1.id, t1.name, t1.KeyName, t1.logoImgUrl, t2.id, t2.name, t2.keyName, t2.logoImgUrl
                FROM sanctionmatches sm JOIN matches m ON m.id = sm.idmatch LEFT JOIN teams t1 ON m.idHomeTeam = t1.id LEFT JOIN teams t2 ON m.idVisitorTeam = t2.id
            ";

            var sql = select + " WHERE idSanction = @idSanction ORDER BY m.starttime, m.id";
            var result = c.Query<Match, Team, Team, Match>(sql, (match, home, visitor) =>
            {
                match.HomeTeam = home;
                match.VisitorTeam = visitor;
                return match;
            },
            new { idSanction }, t);

            return result;
        }


        public static IEnumerable<Match> GetSanctionMatchIds(IDbConnection c, IDbTransaction t, Sanction sanction)
        {
            var selectIds = @"SELECT m.id FROM matches m ";

            var where = @"
                        WHERE 
	                        m.idTournament = @idTournament
	                        AND (m.idHomeTeam = @idTeam OR m.idVisitorTeam = @idTeam)
	                        AND m.idHomeTeam <> -1
	                        AND m.idVisitorTeam <> -1
	                        AND m.starttime > @startDate
            ";

            var sql2 = $@"{selectIds} {where} ORDER BY m.starttime LIMIT @numMatches";
            var args = new { idTournament = sanction.IdTournament, idTeam = sanction.IdTeam, startDate = sanction.StartDate, numMatches = sanction.NumMatches, idMatch = sanction.IdMatch };

            var result = c.Query<Match>(sql2, args, t);

            return result;
        }

        //private static DateTime GetSanctionStartDate(DateTime? matchDate)
        //{
        //    if (!matchDate.HasValue) matchDate = DateTime.Now;

        //    return matchDate.Value.AddDays(1);
        //}

        public static IList<SanctionMatch> CreatePlayerSanctionMatches(IDbConnection c, IDbTransaction t, Sanction sanction)
        {
            var matches = GetSanctionMatchIds(c, t, sanction);
            if (matches == null) throw new Exception("Error.Sanction.NoMatchesAvailable");

            var count = matches.Count();
            // Enforce that there are enough matches available in the tournament. DISABLED as requested by Jesús. 
            //if (count < sanction.NumMatches) throw new Exception("Error.Sanction.NotEnoughMatchesInCalendar");

            var result = new List<SanctionMatch>();

            var i = 0;

            foreach (var m in matches)
            {
                var sanctionMatch = new SanctionMatch
                {
                    IdMatch = m.Id,
                    IdTournament = sanction.IdTournament,
                    IdPlayer = sanction.IdPlayer,
                    IdSanction = sanction.Id
                };

                if (i == count - 1) sanctionMatch.IsLast = true;

                // TODO: Would be nice to batch these inserts.
                sanctionMatch.Id = c.Insert(sanctionMatch, t);

                result.Add(sanctionMatch);

                i++;
            }

            

            return result;
        }


        public static void DeletePlayerSanctionMatches(IDbConnection c, IDbTransaction t, long idSanction)
        {
            c.Execute("DELETE FROM sanctionmatches WHERE idSanction = @idSanction", new { idSanction }, t);
        }

        public static void UpdatePlayerSanctionMatches(IDbConnection c, IDbTransaction t, Sanction sanction)
        {
            DeletePlayerSanctionMatches(c, t, sanction.Id);
            CreatePlayerSanctionMatches(c, t, sanction);
        }

        public static void FillSanctionMatches(IDbConnection c, IDbTransaction t, Sanction sanction)
        {
            sanction.SanctionMatches = GetFullSanctionMatches(c, t, sanction.Id);
        }


        // __ Team sanctions __________________________________________________


        public static void CreateTeamSanctionPenalty(IDbConnection c, IDbTransaction t, Sanction sanction)
        {
            var eventsToCreate = new List<MatchEvent>();

            // lost match penalty
            if (sanction.LostMatchPenalty > 0)
            {
                // retrieve the match, if the team is winner, add match events for losing
                var match = c.Get<Match>(sanction.IdMatch);
                if (match == null) return;

                var lostMatchEvents = GetLostMatchEvents(match, sanction.IdTeam);
                if (lostMatchEvents != null) eventsToCreate.AddRange(lostMatchEvents);
            }

            // tournament points penalty
            if (sanction.TournamentPointsPenalty > 0)
            {
                eventsToCreate.Add(new MatchEvent
                {
                    Type = (int)MatchEventType.ChangeTeamStats,
                    IntData1 = -sanction.TournamentPointsPenalty,
                    IdMatch = sanction.IdMatch,
                    IdTeam = sanction.IdTeam,
                    MatchMinute = 200,
                    IsAutomatic = true
                });
            }

            var createdEventIds = CreateMatchEvents(c, t, eventsToCreate);

            UpdateSanctionMatchEvents(c, t, sanction, createdEventIds);
        }

        public static void UpdateSanctionMatchEvents(IDbConnection c, IDbTransaction t, Sanction sanction, IEnumerable<long> eventIds)
        {
            sanction.SanctionMatchEvents = JsonConvert.SerializeObject(eventIds);
            c.Update(sanction, t);
        }

        public static IEnumerable<long> CreateMatchEvents(IDbConnection c, IDbTransaction t, IEnumerable<MatchEvent> events)
        {
            var result = new List<long>();

            if (events != null)
            {
                foreach (var ev in events)
                {
                    var (_, me) = MatchEvent.Create(c, t, ev);
                    result.Add(me.Id);
                }
            }

            return result;
        }

        public static void UpdateTeamSanctionPenalty(IDbConnection c, IDbTransaction t, Sanction sanction)
        {
            DeleteTeamSanctionPenalty(c, t, sanction);
            CreateTeamSanctionPenalty(c, t, sanction);
        }

        public static void DeleteTeamSanctionPenalty(IDbConnection c, IDbTransaction t, Sanction sanction)
        {
            if (sanction.SanctionMatchEvents == null) return;

            // Delete any events associated with the team sanctions

            var ids = JsonConvert.DeserializeObject<long[]>(sanction.SanctionMatchEvents);
            if (ids == null || ids.Length == 0) return;

            var matchEvents = c.Query<MatchEvent>($"SELECT * FROM matchevents WHERE id IN ({Utils.GetJoined(ids)})", t);

            foreach (var me in matchEvents)
            {
                MatchEvent.Delete(c, t, me);
            }
        }

        public static IEnumerable<MatchEvent> GetLostMatchEvents(Match m, long idTeam)
        {
            var isHomeTeam = (idTeam == m.IdHomeTeam);
            var isDraw = m.HomeScore == m.VisitorScore;
            var homeWon = m.HomeScore > m.VisitorScore;
            var homeLost = !homeWon;

            const int win = MatchEvent.TournamentPointsForWinning;
            const int draw = MatchEvent.TournamentPointsForDraw;

            if (isHomeTeam)
            {
                // sanctioned team is home
                if (homeWon)
                {
                    // sanctioned team won the match, create events to compensate victory
                    return CreateCompensationEvents(m, m.IdHomeTeam, m.IdVisitorTeam, -win, win, -1, 1, 0, 0, 1, -1);
                }
                else if (isDraw)
                {
                    // remove 1 point from home, add 2 to visitor
                    return CreateCompensationEvents(m, m.IdHomeTeam, m.IdVisitorTeam, -draw, win - draw, 0, 1, -1, -1, 1, 0);
                }
            }
            else
            {
                // sanctioned team is visitor
                if (isDraw)
                {
                    // remove 1 point from visitor, add 2 to home
                    return CreateCompensationEvents(m, m.IdHomeTeam, m.IdVisitorTeam, win - draw, -draw, 1, 0, -1, -1, 0, 1);
                }
                else if (!homeWon)
                {
                    // sanctioned team (visitor) won the match, create events to compensate victory
                    return CreateCompensationEvents(m, m.IdHomeTeam, m.IdVisitorTeam, win, -win, 1, -1, 0, 0, -1, 1);
                }
            }

            return null;
        }

        private static IEnumerable<MatchEvent> CreateCompensationEvents(Match m, long idTeam1, long idTeam2, int points1, int points2, int gamesWon1, int gamesWon2, int gamesDraw1, int gamesDraw2, int gamesLost1, int gamesLost2)
        {
            var result = new List<MatchEvent>();

            result.Add(new MatchEvent
            {
                Type = (int)MatchEventType.ChangeTeamStats,
                IntData1 = points1,
                IntData2 = gamesWon1,
                IntData3 = gamesDraw1, 
                IntData4 = gamesLost1,
                IdMatch = m.Id,
                IdTeam = idTeam1,
                MatchMinute = 200,
                IsAutomatic = true
            });

            result.Add(new MatchEvent
            {
                Type = (int)MatchEventType.ChangeTeamStats,
                IntData1 = points2,
                IntData2 = gamesWon2,
                IntData3 = gamesDraw2,
                IntData4 = gamesLost2,
                IdMatch = m.Id,
                IdTeam = idTeam2,
                MatchMinute = 200,
                IsAutomatic = true
            });

            return result;
        }
    }
}
