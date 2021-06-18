using Dapper;
using Dapper.Contrib.Extensions;
using Ganss.XSS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using webapi.Models.Db;
using webapi.Models.Result;

namespace webapi.Controllers
{

    public class TeamsController : CrudController<Team>
    {
        public TeamsController(IOptions<Config> config, NotificationManager notif, AuthTokenManager authManager) : base(config)
        {
            mNotifications = notif;
            mAuthTokenManager = authManager;
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
        
        [HttpGet("export/{idTeam}/{idTournament}")]
        public IActionResult TeamExport (long idTeam, long idTournament)
        {
            string fileContent = "";

            DbTransaction((c,t)=>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                // Team data
                var result = c.Get<Team>(idTeam);
                if (result == null) throw new Exception("Error.NotFound");

                // Tournament specific data: 
                // - Team players
                result.Players = GetPlayerTotals(c, t, idTeam, idTournament);

                // Team Sponsors
                result.Sponsors = c.Query<Sponsor>("SELECT * FROM sponsors WHERE idTeam = @id", new { id = idTeam });

                fileContent = JObject.FromObject(result).ToString();

                return null;

            });

            return CreateJSONFileContentResult(fileContent);
        }

        [HttpGet("{idTeam}/details/{idTournament}")]
        public IActionResult Details(long idTeam, long idTournament)
        {
            return DbTransaction((c, t)=>
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
                    result.Players = GetPlayerTotals(c, t, idTeam, idTournament);

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

        [HttpPost("upload")]
        public async Task<IActionResult> TeamUploadAsync([FromForm] TeamFile file)
        {
            // JSON file => string
            var stringBuilder = new StringBuilder();
            using (var reader = new StreamReader(file.File.OpenReadStream()))
            {
                while (reader.Peek() >= 0)
                    stringBuilder.AppendLine(await reader.ReadLineAsync());
            }
            string JSONString = stringBuilder.ToString();

            // Set file content to Team Object
            JObject jObjectTeam = JObject.Parse(JSONString);
            Team team = jObjectTeam.ToObject<Team>();
            long idTournamnet = file.IdTournament;

            return DbOperation(conn =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);
                if (team == null) throw new NoDataException();
                long idTeam = team.Id;

                // var existTeamQuery = conn.Query($"SELECT * FROM teams WHERE id = {team.Id} OR name = '{team.Name}' OR keyname = '{team.KeyName}';");
                
                // 🔎 External Org team can overrrite team.id values so will make query more restrictive
                var existTeamQuery = conn.Query($"SELECT * FROM teams WHERE id = {team.Id} AND name = '{team.Name}' AND keyname = '{team.KeyName}';");

                bool teamExitst = existTeamQuery.Count() > 0;
                                
                if (!teamExitst)
                {
                    // Add team and add Team to Tournament
                    Audit.Information(this, "{0}: Teams.CreateInTournament {idTournament} -> {Name}", GetUserId(), team.Name, idTournamnet);
                    var teamId = conn.Insert(team);
                    idTeam = teamId;
                    conn.Insert(new TournamentTeam { IdTournament = idTournamnet, IdTeam = teamId });                    
                } else
                {
                    var existsTeamInTournamentQuery = conn.Query($"SELECT * FROM tournamentteams WHERE idtournament = {idTournamnet} AND idteam = '{idTeam}';");
                    bool existsTeamInTournament = existsTeamInTournamentQuery.Count() > 0;

                    if (!existsTeamInTournament)
                    {
                        // Add team just into tournamnet
                        Audit.Information(this, "{0}: Teams.AddToTournament {idTournament} -> {idTeam}", GetUserId(), idTournamnet, idTeam);

                        conn.Insert(new TournamentTeam { IdTournament = idTournamnet, IdTeam = idTeam });

                        // TODO: Notify team admin (if any) her team has been added to the competition. 
                    }
                    if (existsTeamInTournament) return new Exception("Error.Team_already_exitsts_in_tournament");
                }

                // 🚧🚧🚧 Add players to team
                foreach (var player in team.Players)
                {
                    if (player == null || player.UserData == null || player.TeamData == null) throw new Exception("Malformed request");
                    Audit.Information(this, "{0}: Players.CreatePlayer {Name} {Surname}", GetUserId(), player.Name, player.Surname);

                    // User email should exists 
                    bool userExists = PlayersController.CheckUserExistsInGlobalDirectory(Request, player.UserData.Id, player.UserData.Email);
                    if (!userExists) throw new Exception($"{player.UserData.Email} does not exists in the global directory.");

                    var invite = new InviteInput { IdPlayer = player.Id, IdTeam = idTeam, InviteText = "Hi, we want to invite you to join our team." }; // 🚧 Lang

                    DbTransaction((c, t) =>
                    {
                        Audit.Information(this, "{0}: Players.Invite1: {IdTeam} {IdPlayer}", GetUserId(), invite.IdTeam, invite.IdPlayer);

                        if (!IsOrganizationAdmin() && !IsTeamAdmin(invite.IdTeam, c)) throw new UnauthorizedAccessException();

                        // Validate player is not already on the team. 
                        var existingPlayer = c.ExecuteScalar<int>("SELECT COUNT(*) FROM teamplayers WHERE idteam = @idTeam AND idplayer = @idPlayer", invite, t);
                        if (existingPlayer > 0) throw new Exception("Error.PlayerAlreadyInTeam");

                        invite.InviteText = mSanitizer.Sanitize(invite.InviteText);

                        var userOrg = c.QueryFirstOrDefault<User>($"SELECT * FROM users WHERE id = {player.IdUser}");
                        var notifData = new PlayerNotificationData { };

                        if (userOrg == null)
                        {
                            // Insert player
                            var newPlayer = PlayersController.InsertPlayer(c, t, player, player.IdUser, GetUserId(), false, null, UserEventType.PlayerImported);
                            invite.IdPlayer = newPlayer.Id;

                            // Importing player that not exists in current org users
                            notifData = GetPlayerNotification(c, GetUserId(), invite.IdPlayer, invite.IdTeam, false, player.IdUser);
                        }
                        else
                        {
                            // Get current org player id becouse import Id can overlap
                            var playerOrg = c.QueryFirstOrDefault<Player>($"SELECT * FROM players WHERE iduser = {userOrg.Id}");
                            if(playerOrg == null) throw new Exception("Error.PlayerNotFound"); // Player should exist
                            invite.IdPlayer = playerOrg.Id;

                            notifData = GetPlayerNotification(c, GetUserId(), invite.IdPlayer, invite.IdTeam);
                        }

                        // Create the teamplayers record
                        var tp = new TeamPlayer
                        {
                            IdPlayer = invite.IdPlayer,
                            IdTeam = invite.IdTeam,
                            IsTeamAdmin = false,
                            Status = 1
                        };

                        c.Insert(tp, t);

                        // UserEvent

                        c.Insert(new UserEvent
                        {
                            IdCreator = GetUserId(),
                            IdUser = notifData.To.Id,
                            Type = (int)UserEventType.PlayerInvitedToTeam,
                            TimeStamp = DateTime.Now,
                            Description = notifData.Team.Name
                        }, t);

                        notifData.InviteMessage = invite.InviteText;

                        Audit.Information(this, "{0}: Players.Invite2: {1} {2}", GetUserId(), notifData.Team.Name, notifData.To.Name);

                        mNotifications.NotifyEmail(Request, c, t, TemplateKeys.EmailPlayerInviteHtml, notifData);

                        return true;
                    });
                }

                return true;
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
                
        private static FileContentResult CreateJSONFileContentResult(string content)
        {
            var contentType = System.Net.Mime.MediaTypeNames.Application.Json;
            var fileName = "team.json";

            // var contentType = System.Net.Mime.MediaTypeNames.Text.Plain;            
            // var fileName = Guid.NewGuid().ToString() + ".txt";
            var bytes = Encoding.GetEncoding(1252).GetBytes(content);
            var result = new FileContentResult(bytes, contentType)
            {
                FileDownloadName = fileName
            };

            return result;
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

        private PlayerNotificationData GetPlayerNotification(IDbConnection c, long idCreator, long idPlayer, long idTeam, bool wantsPin = false, long userId = -1)
        {
            var fromUser = UsersController.GetUserForId(c, idCreator);
            var toUser = new User { };

            if (userId == -1) // User should exitst in current org
            {
                toUser = c.QueryFirst<User>($"SELECT u.id, u.name, u.mobile FROM users u JOIN players p ON p.idUser = u.id AND p.id = {idPlayer};");
                var userToGlobal = UsersController.GetUserForId(c, toUser.Id);
                toUser.Email = userToGlobal.Email;
                toUser.EmailConfirmed = userToGlobal.EmailConfirmed;
            }
            else // Global User info
            {
                toUser = UsersController.GetUserForId(c, userId);
            }

            var mr = c.QueryMultiple(@"
                    SELECT id, name, logoImgUrl FROM organizations LIMIT 1;
                    SELECT id, name, logoImgUrl FROM teams WHERE id = @idTeam;
                ", new { idFrom = idCreator, idPlayer = idPlayer, idTeam = idTeam });

            // var fromUser = mr.ReadFirst<User>();
            // var toUser = mr.ReadFirst<User>();
            var org = mr.ReadFirst<PublicOrganization>();
            var team = mr.ReadFirst<Team>();
            if (fromUser == null && idCreator >= 10000000) fromUser = UsersController.GetGlobalAdminForId(idCreator);

            if (team == null) throw new Exception("Error.NotFound.Team");
            if (toUser == null) throw new Exception("Error.NotFound.ToUser");
            if (fromUser == null) throw new Exception("Error.NotFound.FromUser");

            var activationLink = toUser.EmailConfirmed && !wantsPin ? "" : PlayersController.GetActivationLink(Request, mAuthTokenManager, toUser);
            var activationPin = toUser.EmailConfirmed && !wantsPin ? "" : UsersController.GetActivationPin(mAuthTokenManager, toUser);

            return new PlayerNotificationData
            {
                To = toUser,
                From = fromUser,
                Team = team,
                Org = org,
                ActivationLink = activationLink,
                ActivationPin = activationPin,
                Images = new PlayerInviteImages
                {
                    OrgLogo = Utils.GetUploadUrl(Request, org.LogoImgUrl, org.Id, "org"),
                    TeamLogo = Utils.GetUploadUrl(Request, team.LogoImgUrl, team.Id, "team")
                }
            };
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
	                p.id, p.iduser, p.name, p.surname, p.idphotoimgurl, u.id, u.avatarImgUrl,
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

            // Add golbal users props
            for (int i = 0; i < result.Count(); i++)
            {
                var userGlobal = UsersController.GetUserForId(c, result.ElementAt(i).UserData.Id);

                result.ElementAt(i).UserData.Email = userGlobal.Email;
                result.ElementAt(i).UserData.EmailConfirmed = userGlobal.EmailConfirmed;
            }

            return result;
        }

        private static IEnumerable<Player> GetPlayerTotals(IDbConnection c, IDbTransaction t, long idTeam, long idTournament)
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

        private NotificationManager mNotifications;
        private AuthTokenManager mAuthTokenManager;
        private readonly HtmlSanitizer mSanitizer = new HtmlSanitizer();
    }

    public class TeamFile
    {
        public long IdTournament { get; set; }
        public IFormFile File { get; set; }
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
