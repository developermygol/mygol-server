using Dapper;
using Dapper.Contrib.Extensions;
using Ganss.XSS;
using HandlebarsDotNet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using SelectPdf;
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

    public class PlayersController : DbController
    {
        public static readonly string[] PublicReadablePlayerFields = new[] {
                    "Id", "Name", "Surname", "BirthDate", "UserData", "TeamData", "LargeImgUrl",
                    "SignatureImgUrl", "Motto", "Height", "Weight", "FacebookKey", "TwitterKey", "InstagramKey" };

        public static readonly string[] TeamAdminReadablePlayerFields = new[] {
                    "Id", "Name", "Surname", "BirthDate", "UserData", "TeamData", "LargeImgUrl", "IdPhotoImgUrl",
                    "SignatureImgUrl", "Motto", "Height", "Weight", "FacebookKey", "TwitterKey", "InstagramKey" };



        private static readonly string[] TeamAdminReadableUserFields = new[] { "Name", "Email", "Mobile", "AvatarImgUrl" };
        public static readonly string[] PublicReadableUserFields = new[] { "Name", "AvatarImgUrl" };

        private static readonly string[] OrgAdminWritableUserFields = new[] { "Email", "Mobile", "AvatarImgUrl" };
        private static readonly string[] PlayerWritableUserFields = new[] { "Mobile", "AvatarImgUrl" };

        public static readonly string[] PublicReadableTeamDataFields = new[] { "FieldPosition", "ApparelNumber", "FieldSide", "IdTeam", "IdPlayer", "Status", "IsTeamAdmin" };

        private static readonly string[] OrgAdminWritableTeamPlayerFields = new[] { "ApparelNumber", "FieldSide", "FieldPosition", "Status", "IsTeamAdmin", "EnrollmentData", "EnrollmentStep" };
        private static readonly string[] TeamAdminWritableTeamPlayerFields = new[] { "ApparelNumber", "FieldSide", "FieldPosition" };
        private static readonly string[] PlayerWritableTeamPlayerFields = new[] { "FieldSide", "FieldPosition", "Mobile" };



        public PlayersController(IOptions<Config> config, NotificationManager notif, AuthTokenManager authManager) : base(config)
        {
            mNotifications = notif;
            mAuthTokenManager = authManager;
        }

        [HttpPost]
        public IActionResult CreatePlayer([FromBody] Player player)
        {
            // Auth: Only org admin or team admin for the team can create the player. 

            if (player == null || player.UserData == null || player.TeamData == null) throw new Exception("Malformed request");

            Audit.Information(this, "{0}: Players.CreatePlayer {Name} {Surname}", GetUserId(), player.Name, player.Surname);


            return DbTransaction((c, t) =>
           {
               bool isOrgAdmin = IsOrganizationAdmin();
               bool isTeamAdmin = IsTeamAdmin(player.TeamData.IdTeam, c);

               if (!(isOrgAdmin || isTeamAdmin)) throw new UnauthorizedAccessException();

               var idCreator = GetUserId();
               var newPlayer = InsertPlayer(c, t, player, idCreator, isOrgAdmin);

               var notifData = GetPlayerNotification(c, t, idCreator, newPlayer.Id, player.TeamData.IdTeam);
               mNotifications.NotifyEmail(Request, c, t, TemplateKeys.EmailPlayerInviteHtml, notifData);

               AddUserToGlobalDirectory(Request, newPlayer.UserData.Id, newPlayer.UserData.Email);

               return newPlayer;
           });
        }

        [HttpPost("upload")]
        public async Task<IActionResult> PlayerUploadAsync([FromForm] PlayerFile file)
        {
            // JSON file => string
            var stringBuilder = new StringBuilder();
            using (var reader = new StreamReader(file.File.OpenReadStream()))
            {
                while (reader.Peek() >= 0)
                    stringBuilder.AppendLine(await reader.ReadLineAsync());
            }
            string JSONString = stringBuilder.ToString();


            // Set file content to Player Object
            JObject jObjectPlayer = JObject.Parse(JSONString);
            Player player = jObjectPlayer.ToObject<Player>();
            long idTournamnet = file.IdTournament;
            long idTeam = file.IdTeam;

            if (player == null || player.UserData == null || player.TeamData == null) throw new Exception("Malformed request");
            Audit.Information(this, "{0}: Players.CreatePlayer {Name} {Surname}", GetUserId(), player.Name, player.Surname);

            // User email should exists 
            bool userExists = CheckUserExistsInGlobalDirectory(Request, player.UserData.Id, player.UserData.Email);
            if (!userExists) throw new Exception($"{player.UserData.Email} does not exists in the global directory.");

            var invite = new InviteInput { IdPlayer = player.Id, IdTeam = idTeam, InviteText = "Hi, we want to invite you to join our team." }; // 🚧 Lang

            return DbTransaction((c, t) =>
            {
                Audit.Information(this, "{0}: Players.Invite1: {IdTeam} {IdPlayer}", GetUserId(), invite.IdTeam, invite.IdPlayer);

                if (!IsOrganizationAdmin() && !IsTeamAdmin(invite.IdTeam, c)) throw new UnauthorizedAccessException();

                // Validate player is not already on the team. 
                var existingPlayer = c.ExecuteScalar<int>("SELECT COUNT(*) FROM teamplayers WHERE idteam = @idTeam AND idplayer = @idPlayer", invite, t);
                if (existingPlayer > 0) throw new Exception("Error.PlayerAlreadyInTeam");

                invite.InviteText = mSanitizer.Sanitize(invite.InviteText);

                var notifData = GetPlayerNotification(c, t, GetUserId(), invite.IdPlayer, invite.IdTeam);

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

        [HttpPost("generateplayersfile")]
        public IActionResult GeneratePlayersFile([FromBody] PlayersFilesRequest data)
        {
            var players = Enumerable.Empty<Player>();
            var team = new Team();
            var tournament = new Tournament();
            var org = new PublicOrganization();

            DbOperation(c =>
            {
                
                switch (data.PlayerType)
                {
                    case 1: // Only players
                        players = GetPlayers(c, "t.idTeam = @id AND t.fieldposition NOT IN(5,6,7)", new { id = data.TeamId });
                        break;
                    case 2: // Non-playing admin
                        players = GetPlayers(c, "t.idTeam = @id AND t.fieldposition = 5", new { id = data.TeamId });
                        break;
                    case 3: // Coach
                        players = GetPlayers(c, "t.idTeam = @id AND t.fieldposition = 6", new { id = data.TeamId });
                        break;
                    case 4: // Physiotherapist
                        players = GetPlayers(c, "t.idTeam = @id AND t.fieldposition = 7", new { id = data.TeamId });
                        break;
                    default:
                        players = GetPlayers(c, "t.idTeam = @id", new { id = data.TeamId });
                        break;
                }

                team = c.Get<Team>(data.TeamId);
                tournament = c.Get<Tournament>(data.TournamentId);
                org = c.Get<PublicOrganization>(1); // 🚧💥

                return null;
            });

            return GeneratePlayersFilePDF(data, players, team, tournament, org);
        }


        [HttpGet("forteam/{idTeam}")]
        public IActionResult PlayersForTeam(long idTeam)
        {
            return DbOperation(c =>
            {
                var players = GetPlayers(c, "t.idTeam = @id", new { id = idTeam });

                return FilterContentsForRole(c, players, idTeam);
            });
        }

        [HttpGet("user/{idUser}/{idTeam:long?}/{idTournament:long?}")]
        public IActionResult GetUser(long idUser, long idTeam = -1, long idTournament = -1)
        {
            return GetPlayerData(-1, idTeam, idTournament, idUser);
        }

        [HttpGet("{idPlayer:long}/{idTeam:long?}")]
        public IActionResult Get(long idPlayer, long idTeam = -1, [FromQuery] long idTournament = -1)
        {
            return GetPlayerData(idPlayer, idTeam, idTournament);
        }

        [HttpGet("export/{idPlayer}/{idTeam}/{idTournament}")]
        public IActionResult PlayerExport(long idPlayer, long idTeam, long idTournament)
        {
            string fileContent = "";

            // var result = GetPlayerData(idPlayer, idTeam, idTournament); 🔎 Alternative

            
            DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                // Player data
                var result = c.Get<Player>(idPlayer);
                if (result == null) throw new Exception("Error.NotFound");

                // 🔎 Sanitize sensitive and irrelevant data.
                result.UserData = c.Get<User>(result.IdUser);
                result.UserData.Password = null;
                result.UserData.Salt = null;
                result.UserData.DeviceToken = null;

                result.TeamData = c.QueryFirstOrDefault<TeamPlayer>(
                    "SELECT * FROM teamplayers WHERE idteam = @idTeam AND idplayer = @idPlayer",
                    new { idTeam = idTeam, idPlayer = idPlayer });


                fileContent = JObject.FromObject(result).ToString();
                return null;

                

            });
            return CreateJSONFileContentResult(fileContent);
        }

        private bool IsAdminOrSelf(long idUser)
        {
            return IsOrganizationAdmin() || idUser == GetUserId();
        }

        private IActionResult GetPlayerData(long idPlayer, long idTeam = -1, long idTournament = -1, long idUser = -1)
        {
            // Can reuse the PlayersForTeam logic, limit to single player

            // Return the details of the player.
            // For the org admin, full details, including associated documents
            // For the team admin, public details only
            // For the player herself, full details
            // Public data is available for everyone

            return DbOperation(c =>
            {
                var query = idUser > -1 ?
                    "SELECT p.*, u.email, u.mobile, u.avatarimgurl FROM players p LEFT JOIN users u ON p.iduser = u.id WHERE u.id = @idUser" :
                    "SELECT p.*, u.email, u.mobile, u.avatarimgurl FROM players p LEFT JOIN users u ON p.iduser = u.id WHERE p.id = @idPlayer";

                var player = c.Query<Player, User, Player>(
                        query,
                        (pl, user) =>
                        {
                            if (user == null) throw new Exception("Error.PlayerNoUser");
                            pl.UserData = user;
                            return pl;
                        },
                        new { idPlayer = idPlayer, idUser = idUser },
                        splitOn: "email").GetSingle();

                // Auth: org admin can see all events, player its own events, but only descriptions
                if (IsOrganizationAdmin())
                {
                    player.Events = c.Query<UserEvent, Upload, UserEvent>(
                        "SELECT * FROM userevents ue LEFT JOIN uploads u ON ue.idSecureUpload = u.id WHERE idUser = @id ORDER BY timestamp DESC",
                        (userEvent, upload) =>
                        {
                            userEvent.SecureUpload = upload;
                            return userEvent;
                        },
                        new { id = player.IdUser }
                    );
                }
                else if (GetUserId() == player.IdUser)
                {
                    player.Events = c.Query<UserEvent>(
                        "SELECT id, type, description, timestamp, idCreator FROM userevents WHERE idUser = @id ORDER BY timestamp DESC",
                        new { id = player.IdUser });
                }
                else if (IsTeamAdmin(idTeam, c))
                {
                    // Filter images and non public data. Allow IdPhoto.
                    player.UserData.Mobile = null;
                    player.UserData.Email = null;
                    player.IdCard1ImgUrl = null;
                    player.IdCard2ImgUrl = null;
                }
                else
                {
                    // filter non-public data for everyone else.
                    player.UserData.Mobile = null;
                    player.UserData.Email = null;
                    player.IdCard1ImgUrl = null;
                    player.IdCard2ImgUrl = null;
                    player.IdPhotoImgUrl = null;
                }

                var enrollmentFields = IsAdminOrSelf(player.IdUser) ? "p.enrollmentStep, p.enrollmentData, " : "";

                player.Teams = c.Query<TeamPlayer, Tournament, Team, Team>(
                    @"SELECT 
                        p.idteam, p.idplayer, p.apparelNumber, p.fieldSide, p.fieldPosition, p.status, p.isTeamAdmin, "
                        + enrollmentFields + @" tnmt.*, t.*
                        FROM    teamplayers p
                            JOIN teams t ON p.idteam = t.id
                            JOIN tournamentTeams tt ON tt.idteam = t.id
                            JOIN tournaments tnmt ON tt.idtournament = tnmt.id
                        WHERE p.idPlayer = @id
                        ORDER by p.idTeam DESC, tnmt.id
                        ",
                    (teamPlayer, tournament, team) =>
                    {
                        if (!IsOrganizationAdmin() && tournament != null && !tournament.Visible) return null;

                        team.TeamData = teamPlayer;
                        team.Tournament = tournament;
                        // MULTITEAM: have to prepare the client to handle multiple teams as well. No defaults anymore. 
                        if (idTeam == teamPlayer.IdTeam && (tournament != null && idTournament == tournament.Id)) player.TeamData = teamPlayer;
                        return team;
                    },
                    new { id = player.Id },
                    splitOn: "id"
                );

                if (player.Teams.Count() == 1 && idTournament == -1)
                {
                    // Fill stats for default tournament
                    var uniqueTeam = player.Teams.First();
                    if (uniqueTeam != null && uniqueTeam.Tournament != null) idTournament = uniqueTeam.Tournament.Id;
                }

                player.Awards = c.Query<Award, PlayDay, Tournament, Team, Award>(
                    "SELECT a.*, d.id, d.name, tnmt.*, t.* FROM awards a LEFT JOIN playdays d ON a.idDay = d.id LEFT JOIN tournaments tnmt ON tnmt.id = a.idTournament LEFT JOIN teams t ON a.idTeam = t.id WHERE idPlayer = @idPlayer",
                    (award, day, tournament, team) =>
                    {
                        award.Day = day;
                        award.Tournament = tournament;
                        award.Team = team;
                        return award;
                    },
                    new { idPlayer = player.Id, idTeam = idTeam },
                    splitOn: "id");

                player.Sanctions = SanctionsController.GetSanctionsForPlayer(c, null, player.Id, IsOrganizationAdmin());

                if (idTournament > -1 && idTeam > -1)
                {
                    // TODO: This query can be optimized, we only need to sum the playresults days, not all the joins happening here. 
                    var parameters = new { idPlayer = player.Id, idTournament = idTournament, idTeam = idTeam };
                    var playerSummary = TeamsController.GetPlayerStatistics(c, "pdr.idPlayer = @idPlayer AND pdr.idTournament = @idTournament AND pdr.idTeam = @idTeam ", parameters).FirstOrDefault();
                    if (playerSummary != null) player.DayResultSummary = playerSummary.DayResultSummary;

                    player.DayResults = c.Query<PlayerDayResult, PlayDay, PlayDay>(
                        "SELECT pdr.*, p.* FROM playerdayresults pdr JOIN playdays p ON pdr.idDay = p.id WHERE pdr.idPlayer = @idPlayer AND pdr.idTeam = @idTeam AND pdr.idTournament = @idTournament ORDER BY p.sequenceOrder",
                        (dayResult, day) =>
                        {
                            day.PlayerDayResults = new[] { dayResult };
                            return day;
                        },
                        parameters,
                        splitOn: "id");
                }

                return player;
            });
        }

        [HttpGet("fichapicture/{idUser}")]
        public IActionResult GetFichaPicture(long idUser)
        {
            return DbOperation(c =>
            {
                if (!IsOrganizationAdmin() && !IsReferee()) throw new UnauthorizedAccessException();

                var result = c.ExecuteScalar<string>("SELECT u.repositoryPath FROM uploads u JOIN userEvents e ON e.idSecureUpload = u.id WHERE e.idUser = @idUser AND u.type = 205 ORDER BY u.id DESC LIMIT 1", new { idUser });

                return result;
            });
        }


        [HttpGet("ficha/{idTournament}/{idTeam}")]
        public IActionResult GetFichaData(long idTournament, long idTeam)
        {
            return DbOperation(c =>
            {
                if (!IsLoggedIn()) throw new UnauthorizedAccessException();

                return GetFichaForPlayer(c, null, idTournament, idTeam, GetUserId());
            });
            
        }


        [HttpPut]
        public IActionResult Update([FromBody] Player player)
        {
            return DbTransaction((c, t) =>
            {
                if (player == null || player.UserData == null || player.TeamData == null) throw new NoDataException();

                Audit.Information(this, "{0}: Players.Update1 {Id} {Name} {Surname}", GetUserId(), player.Id, player.Name, player.Surname);

                var dbPlayer = c.Get<Player>(player.Id);
                if (dbPlayer == null) throw new Exception("Error.NotFound");

                var dbUser = c.Get<User>(dbPlayer.IdUser);
                if (dbUser == null) throw new Exception("Error.NotFound");

                Audit.Information(this, "{0}: Players.Update2 {Id} {2} {3} -> {4} {5}", GetUserId(), player.Id, dbPlayer.Name, dbPlayer.Surname, player.Name, player.Surname);

                if (player.UserData.Email != dbUser.Email) UsersController.CheckEmail(c, t, player.UserData.Email);

                var dbTeamPlayer = c.QueryFirstOrDefault<TeamPlayer>(
                    "SELECT * FROM teamplayers WHERE idteam = @idTeam AND idplayer = @idPlayer",
                    new { idTeam = player.TeamData.IdTeam, idPlayer = dbPlayer.Id });
                if (dbTeamPlayer == null)
                {
                    Audit.Error(this, "{0}: Players.Update: error, teamplayer not found {1}, {2}.", GetUserId(), player.TeamData.IdTeam, dbPlayer.Id);
                    throw new Exception("Error.PlayerNotLinkedToTeam");
                }


                if (IsOrganizationAdmin())
                {
                    // OrgAdmin: can update everything

                    // Player fields
                    Mapper.MapExcept(player, dbPlayer, new string[] { "Id", "IdUser" });
                    c.Update(dbPlayer, t);

                    string dbEmail = dbUser.Email;

                    // User fields

                    Mapper.MapExplicit(player.UserData, dbUser, OrgAdminWritableUserFields);
                    dbUser.Name = Player.GetName(player.Name, player.Surname);

                    var password = player.UserData.Password;
                    if (password != null && password.Length > 0)
                    {
                        UsersController.UpdatePassword(dbUser, password);
                        dbUser.EmailConfirmed = true;
                    }

                    c.Update(dbUser, t);

                    // TeamPlayer fields
                    Mapper.MapExplicit(player.TeamData, dbTeamPlayer, OrgAdminWritableTeamPlayerFields);
                    c.Update(dbTeamPlayer, t);

                    // Global DB. Last step since the transaction is on a different DB. 
                    if (player.UserData.Email != dbEmail) UpdateUserInGlobalDirectory(Request, dbUser.Id, player.UserData.Email);

                    return true;
                }

                if (IsTeamAdmin(player.TeamData.IdTeam, c))
                {
                    // TeamAdmin: Can only update team related data

                    // TeamPlayer fields
                    Mapper.MapExplicit(player.TeamData, dbTeamPlayer, TeamAdminWritableTeamPlayerFields);
                    c.Update(dbTeamPlayer, t);

                    return true;
                }

                if (GetUserId() == dbPlayer.IdUser)
                {
                    // Player fields
                    if (dbPlayer.Approved)
                    {
                        // Once approved, cannot modify idcard/name/address
                        Mapper.MapExcept(player, dbPlayer, new string[] { "Id", "IdCardNumber", "Name", "Surname", "Address1", "Address2", "City", "State", "CP", "Country", "IdUser", "EnrollmentStep", "Approved" });
                    }
                    else
                    {
                        Mapper.MapExcept(player, dbPlayer, new string[] { "Id", "IdUser", "EnrollmentStep", "Approved" });
                    }

                    c.Update(dbPlayer, t);

                    // User fields
                    Mapper.MapExplicit(player.UserData, dbUser, PlayerWritableUserFields);
                    dbUser.Name = Player.GetName(dbPlayer.Name, dbPlayer.Surname);

                    // check if email changed
                    if (dbUser.Email != player.UserData.Email)
                    {
                        dbUser.EmailConfirmed = false;
                        // TODO: Send email notification to activate new email. 
                    }

                    var password = player.UserData.Password;
                    if (password != null && password.Length > 0)
                    {
                        UsersController.UpdatePassword(dbUser, password);
                    }

                    c.Update(dbUser, t);

                    // TeamPlayer fields
                    Mapper.MapExplicit(player.TeamData, dbTeamPlayer, PlayerWritableTeamPlayerFields);
                    c.Update(dbTeamPlayer, t);

                    return true;
                }

                throw new UnauthorizedAccessException();
            });
        }

        [HttpPut("updateplayertacticposition")]
        public IActionResult UpdatePlayerTacticPosition([FromBody] PlayerTacticPositionInput data)
        {
            if (data == null) throw new Exception();

            return DbTransaction((c, t) =>
            {
                Audit.Information(this, "{0}: Players.UpdatedPlayerTacticPosition idPlayer: {1} idTeam: {2} idTacticPosition: {3}", GetUserId(), data.IdPlayer, data.IdTeam, data.IdTacticPosition);

                // Check if player with same position exists and set it to -1 
                var removePostion = c.Execute("UPDATE teamplayers SET idtacticposition = -1 WHERE idtacticposition = @idTacticPosition AND idteam = @idTeam ", data);
                var positionUpdated = c.Execute("UPDATE teamplayers SET idtacticposition = @IdTacticPosition WHERE idplayer = @idPlayer AND idteam = @idTeam ", data);
                if (positionUpdated == 0) throw new Exception("Error.NotFound");

                return true;
            });
        }

        [HttpPost("delete")]
        public IActionResult Delete([FromBody] Player player)
        {
            if (player == null) throw new Exception();

            return DbTransaction((c, t) =>
            {
                Audit.Information(this, "{0}: Players.Delete {Id}", GetUserId(), player.Id);

                // Only org admins should be able to really remove the player record in the database.
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                // Match events will also be associated with players directly, so history is retained even if the 
                // player team link is removed.

                var dbPlayer = c.Get<Player>(player.Id, t);
                c.Delete(player, t);

                // delete the user and the global directory entry
                c.Execute("DELETE FROM users WHERE id = @idUser", new { idUser = dbPlayer.IdUser }, t);
                DeleteUserInGlobalDirectory(Request, dbPlayer.IdUser);

                return true;
            });
        }


        [HttpPost("updateplayerapproved")]
        public IActionResult UpdatePlayerApprovedStatus([FromBody] PlayerApprovedInput data)
        {
            return DbTransaction((c, t) =>
            {
                if (data == null) throw new NoDataException();

                CheckAuthLevel(UserLevel.OrgAdmin);

                Audit.Information(this, "{0}: Player.UpdateApproved: {1} {2}", GetUserId(), data.IdPlayer, data.Approved);

                var numUpdated = c.Execute("UPDATE players SET approved = @approved WHERE id = @idPlayer", data);
                if (numUpdated == 0) throw new Exception("Error.NotFound");

                return true;
            });
        }
                
        // __ TeamPlayer actions ______________________________________________


        [HttpPost("unlink/{idPlayer}/{idTeam}")]
        public IActionResult Unlink(long idPlayer, long idTeam)
        {
            // Unlinks a player from a team

            if (idPlayer == 0 || idTeam == 0) throw new Exception("Error.NotFound");

            return DbTransaction( (c, t) =>
            {
                long currentUserId = GetUserId();

                Audit.Information(this, "{0}: Players.Unlink1: {idTeam} -> {idPlayer}", currentUserId, idTeam, idPlayer);

                if (!IsOrganizationAdmin() && !IsTeamAdmin(idTeam, c)) throw new UnauthorizedAccessException();

                var teamPlayer = new TeamPlayer
                {
                    IdTeam = idTeam,
                    IdPlayer = idPlayer
                };

                var dbPlayer = c.Query<Player>("SELECT p.id, idUser FROM players p JOIN teamplayers tp ON tp.idPlayer = p.id WHERE idTeam = @idTeam AND idPlayer = @idPlayer", teamPlayer, t).GetSingle();
                if (dbPlayer.IdUser == currentUserId) throw new Exception("Error.CantUnlinkYourself");

                var result = c.Delete(teamPlayer, t);

                // Delete the possible paymentconfigs associated to this idteam - idplayer combo.
                var numDeleted = c.Execute("DELETE FROM paymentconfigs WHERE idTeam = @idTeam AND idUser = @idUser", new { idTeam = idTeam, idUser = dbPlayer.IdUser }, t);

                var userFrom = UsersController.GetUserForId(c, t, currentUserId);
                var userTo = UsersController.GetUserForId(c, t, dbPlayer.IdUser);
                var team = c.QueryFirst<Team>($"SELECT id, name FROM teams WHERE id = {idTeam};");

                /*var mr = c.QueryMultiple(@"
                    SELECT id, name, avatarImgUrl, email, mobile FROM users WHERE id = @idFrom;
                    SELECT u.id, u.name, u.avatarImgUrl, email, mobile FROM users u JOIN players p ON p.iduser = u.id AND p.id = @idPlayerTo;
                    SELECT id, name FROM teams WHERE id = @idTeam;
                ",
                new { idFrom = GetUserId(), idPlayerTo = idPlayer, idTeam = idTeam }, t);*/

                var data = new PlayerNotificationData
                {
                    /*From = mr.ReadFirst<User>(),
                    To = mr.ReadFirst<User>(),
                    Team = mr.ReadFirst<Team>()*/
                    From = userFrom,
                    To = userTo,
                    Team = team
                };

                Audit.Information(this, "{0}: Players.Unlink2: unlink {1} from {2}", currentUserId, data.To.Name, data.Team.Name);

                mNotifications.NotifyEmail(Request, c, t, TemplateKeys.EmailPlayerUnlinkHtml, data);

                return result;
            });
        }

       
        [HttpPost("invite")]
        public IActionResult Invite([FromBody] InviteInput invite)
        {
            return DbTransaction( (c, t) =>
            {
                Audit.Information(this, "{0}: Players.Invite1: {IdTeam} {IdPlayer}", GetUserId(), invite.IdTeam, invite.IdPlayer);

                if (!IsOrganizationAdmin() && !IsTeamAdmin(invite.IdTeam, c)) throw new UnauthorizedAccessException();

                // Validate player is not already on the team. 

                var existingPlayer = c.ExecuteScalar<int>("SELECT COUNT(*) FROM teamplayers WHERE idteam = @idTeam AND idplayer = @idPlayer", invite, t);
                if (existingPlayer > 0) throw new Exception("Error.PlayerAlreadyInTeam");

                invite.InviteText = mSanitizer.Sanitize(invite.InviteText);

                var notifData = GetPlayerNotification(c, t, GetUserId(), invite.IdPlayer, invite.IdTeam);

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

        [HttpPost("resendemail")]
        public IActionResult ResendActivationEmail([FromBody] InviteInput invite)
        {
            return DbTransaction((c, t) =>
            {
                invite.InviteText = mSanitizer.Sanitize(invite.InviteText);

                var notifData = GetPlayerNotification(c, t, GetUserId(), invite.IdPlayer, invite.IdTeam, true);
                notifData.InviteMessage = invite.InviteText;

                Audit.Information(this, "{0}: Players.ResendInvite: {1} {2} {3}", GetUserId(), notifData.Team.Name, notifData.To.Name, notifData.To.Email);

                mNotifications.NotifyEmail(Request, c, t, TemplateKeys.EmailPlayerInviteHtml, notifData);

                return true;
            });
        }


        // __ Helpers _________________________________________________________


        private Player GetFichaForPlayer(IDbConnection c, IDbTransaction t, long idTournament, long idTeam, long idUser)
        {
            var player = c.QuerySingle<Player>("SELECT id, name, surname, idCardNumber, BirthDate, idUser FROM players WHERE idUser = @idUser", new { idUser = idUser });
            if (player == null) return null;

            var query = @"
                SELECT u.id, u.repositoryPath FROM uploads u JOIN userEvents e ON e.idSecureUpload = u.id WHERE e.idUser = @idUser AND u.type = 205 ORDER BY u.id DESC;
                SELECT t.id, t.name, t.logoImgUrl, tp.idTeam, tp.apparelNumber, tnm.id, tnm.name, s.id, s.name, tm.id, tm.name
                    FROM teams t 
	                    JOIN tournamentTeams tt ON tt.idTeam = t.id AND tt.idTournament = @idTournament
	                    JOIN teamPlayers tp ON tp.idTeam = tt.idTeam AND tp.idTeam = @idteam AND tp.idPlayer = @idPlayer
	                    JOIN tournaments tnm ON tt.idTournament = tnm.id
	                    JOIN seasons s ON tnm.idSeason = s.id
                        JOIN tournamentModes tm ON tnm.IdTournamentMode = tm.id;
            ";

            var qr = c.QueryMultiple(query, new { idPlayer = player.Id, idTeam = idTeam, idTournament = idTournament, idUser = idUser });

            var fichaImgUpload = qr.ReadFirstOrDefault<Upload>();
            if (fichaImgUpload != null) player.FichaPictureImgUrl = fichaImgUpload.RepositoryPath;

            // We could probably get tournament mode and season from the organization store in the client, 
            // but that's not yet clear, so for the moment, have it here. 

            player.Teams = qr.Read<Team, TeamPlayer, Tournament, Season, TournamentMode, Team>(
                (team, teamPlayer, tournament, season, tournamentMode) =>
                {
                    tournament.Season = season;
                    tournament.Mode = tournamentMode;
                    team.Tournaments = new[] { tournament };
                    team.TeamData = teamPlayer;

                    if (idTournament == tournament.Id && idTeam == team.Id) player.TeamData = teamPlayer;

                    return team;
                },
                splitOn: "idTeam,id,id,id"
            );

            return player;
        }

        private static FileContentResult CreateJSONFileContentResult(string content)
        {
            var contentType = System.Net.Mime.MediaTypeNames.Application.Json;
            var fileName = "player.json";
            var bytes = Encoding.GetEncoding(1252).GetBytes(content);
            var result = new FileContentResult(bytes, contentType)
            {
                FileDownloadName = fileName
            };

            return result;
        }
        
        public static Player InsertPlayer(IDbConnection c, IDbTransaction t, Player player, long idCreator, bool isOrgAdmin, HashedPassword password = null, UserEventType eventType = UserEventType.PlayerCreated, HttpRequest request = null)
        {
            var level = (int)UserLevel.Player;

            if (password == null) password = new HashedPassword { Hash = "", Salt = "" };

            UsersController.CheckEmail(c, t, player.UserData.Email);

            var newUser = new User
            {
                Level = (int)level,
                Email = player.UserData.Email,
                Name = Player.GetName(player.Name, player.Surname),
                Password = password.Hash,
                Salt = password.Salt,
                EmailConfirmed = false,
                Mobile = player.UserData.Mobile
            };

            // Inser User org > user and global > user
            InserUserPropsInGlobalDirectory(newUser);

            // 🚧 Id issue?

            var idUser = c.Insert(newUser, t);
            newUser.Id = idUser;

            var newPlayer = new Player
            {
                IdUser = idUser,
                Name = player.Name,
                Surname = player.Surname,
                UserData = newUser,
                Address1 = player.Address1,
                Address2 = player.Address2,
                City = player.City,
                State = player.State,
                CP = player.CP,
                Country = player.Country,
                IdCardNumber = player.IdCardNumber,
                Height = player.Height,
                Weight = player.Weight,
                BirthDate = player.BirthDate
            };

            var idPlayer = c.Insert(newPlayer, t);
            newPlayer.Id = idPlayer;

            if (player.TeamData != null && player.TeamData.IdTeam != 0)
            {

                var newTeamPlayer = new TeamPlayer
                {
                    IdPlayer = idPlayer,
                    IdTeam = player.TeamData.IdTeam,
                    Status = 1
                };

                c.Insert(newTeamPlayer, t);
                newPlayer.TeamData = newTeamPlayer;
            }

            // Add creation event
            c.Insert(new UserEvent { IdUser = idUser, IdCreator = idCreator, TimeStamp = DateTime.Now, Type = (int)eventType, Description = newPlayer.GetName() });

            return newPlayer;
        }


        private PlayerNotificationData GetPlayerNotification(IDbConnection c, IDbTransaction t, long idCreator, long idPlayer, long idTeam, bool wantsPin = false)
        {
            var mr = c.QueryMultiple(@"
                    SELECT id, name, avatarimgurl, email, mobile FROM users WHERE id = @idFrom;
                    SELECT u.id, u.name, u.email, u.mobile, u.emailConfirmed FROM users u JOIN players p ON p.idUser = u.id AND p.id = @idPlayer;
                    SELECT id, name, logoImgUrl FROM organizations LIMIT 1;
                    SELECT id, name, logoImgUrl FROM teams WHERE id = @idTeam;
                ", new { idFrom = idCreator, idPlayer = idPlayer, idTeam = idTeam });

            var fromUser = mr.ReadFirst<User>();
            var toUser = mr.ReadFirst<User>();
            var org = mr.ReadFirst<PublicOrganization>();
            var team = mr.ReadFirst<Team>();
            if (fromUser == null && idCreator >= 10000000) fromUser = UsersController.GetGlobalAdminForId(idCreator);

            if (team == null) throw new Exception("Error.NotFound.Team");
            if (toUser == null) throw new Exception("Error.NotFound.ToUser");
            if (fromUser == null) throw new Exception("Error.NotFound.FromUser");

            var activationLink = toUser.EmailConfirmed && !wantsPin ? "" : GetActivationLink(Request, mAuthTokenManager, toUser);
            var activationPin = toUser.EmailConfirmed && !wantsPin ? "" : UsersController.GetActivationPin(mAuthTokenManager, toUser);

            return new PlayerNotificationData
            {
                To = toUser,
                From = fromUser,
                Team = team,
                Org = org,
                ActivationLink = activationLink,
                ActivationPin =  activationPin,
                Images = new PlayerInviteImages
                {
                    OrgLogo = Utils.GetUploadUrl(Request, org.LogoImgUrl, org.Id, "org"),
                    TeamLogo = Utils.GetUploadUrl(Request, team.LogoImgUrl, team.Id, "team")
                }
            };
        }

        public static string GetActivationLink(HttpRequest request, AuthTokenManager authMan, User user)
        {
            var cfg = OrganizationManager.GetConfigForRequest(request);
            var token = authMan.CreateActivationToken(user.Id, user.Email);
            //var encodedToken = WebUtility.UrlEncode(token);

            return $"{cfg.PrivateWebBaseUrl}/login/activate?at={token}";
        }


        private IEnumerable<Player> FilterContentsForRole(IDbConnection c, IEnumerable<Player> players, long idTeam)
        {
            // OrgAdmin
            if (IsOrganizationAdmin()) return players;

            // TeamAdmin
            if (IsTeamAdmin(idTeam, c))
            {
                foreach (var p in players)
                {
                    Mapper.RedactExcept(p, TeamAdminReadablePlayerFields);
                    Mapper.RedactExcept(p.UserData, TeamAdminReadableUserFields);
                    Mapper.RedactExcept(p.TeamData, PublicReadableTeamDataFields);
                }

                return players;
            }

            // Public
            foreach (var p in players)
            {
                Mapper.RedactExcept(p, PublicReadablePlayerFields);
                Mapper.RedactExcept(p.UserData, PublicReadableUserFields);
                Mapper.RedactExcept(p.TeamData, PublicReadableTeamDataFields);
            }

            return players;
        }

        public static IEnumerable<Player> GetPlayers(IDbConnection c, string condition, object parameters)
        {
            // Returns full player data. 
            // Already not selecting here password, salt and other sensitive user fields.
            return c.Query<Player, User, TeamPlayer, Player>(
                $"SELECT p.*, u.email, u.mobile, u.avatarimgurl, t.* FROM players p LEFT JOIN users u ON p.iduser = u.id LEFT JOIN teamplayers t ON t.idplayer = p.id WHERE {condition}",
                (player, user, teamPlayer) =>
                {
                    player.UserData = user;
                    player.TeamData = teamPlayer;
                    return player;
                },
                parameters,
                splitOn: "email, idteam");
        }

        private FileContentResult GeneratePlayersFilePDF(PlayersFilesRequest data, IEnumerable<Player> players, Team team, Tournament tournament, PublicOrganization org)
        {
            HtmlToPdf converter = new HtmlToPdf();
            converter.Options.PdfPageSize = PdfPageSize.A4;
            converter.Options.PdfPageOrientation = PdfPageOrientation.Portrait;

            int sizeDivider = data.Size == 1 ? 4 : 10;
            string templateFileName = data.Size == 1 ? "acreditation.html" : "sheet.html";

            string str_directory = Environment.CurrentDirectory.ToString();
            string path = Path.Combine(str_directory, "BadgesTemplates", templateFileName);

            Handlebars.RegisterHelper("intial", (writer, context, parameters) =>
            {
                if (context.First == true) writer.WriteSafeString(@"<div class='Container'>");
                // Close Container add page break and open new Container
                if (context.Break == true) writer.WriteSafeString(@"</div><div class='page-break'></div><div class='Container'>");
            });

            Handlebars.RegisterHelper("final", (writer, context, parameters) =>
            {
                // Close Container
                if (context.Last == true) writer.WriteSafeString($"</div>");
            });

            Handlebars.RegisterHelper("avatar", (writer, context, parameters) =>
            {
                if (context.PlayerPhoto != null) writer.WriteSafeString($"<img class='image' src='{context.PlayerPhoto}' alt='player'>");
            });

            Handlebars.RegisterHelper("fields", (writer, context, parameters, arguments) =>
            {
                // Print if argument given is true
                if (parameters[(string)arguments[0]] == true) context.Template(writer, parameters);
            });

            var template = Handlebars.Compile(System.IO.File.ReadAllText(path));
            var uploadPath = OrganizationManager.GetOrgUploadPath(Request);

            JArray PlayersTemplate = new JArray();
            int counter = 0;

            foreach (var player in players)
            {
                JObject TemplatePlayer = new JObject();

                TemplatePlayer["First"] = counter == 0;
                TemplatePlayer["Break"] = counter % sizeDivider == 0 && counter != 0;
                TemplatePlayer["Last"] = counter == players.Count() - 1;

                TemplatePlayer["LabelName"] = Translation.Get("PlayerFiles.Label.Name");
                TemplatePlayer["LabelIdNumber"] = Translation.Get("PlayerFiles.Label.IdNumber");
                TemplatePlayer["LabelIdCard"] = Translation.Get("PlayerFiles.Label.IdCard");
                TemplatePlayer["LabelBirthDate"] = Translation.Get("PlayerFiles.Label.BirthDate");
                TemplatePlayer["LabelTournament"] = Translation.Get("PlayerFiles.Label.Tournament");
                TemplatePlayer["LabelTeam"] = Translation.Get("PlayerFiles.Label.Team");
                TemplatePlayer["LabelApparelNumber"] = Translation.Get("PlayerFiles.Label.ApparelNumber");
                TemplatePlayer["LabelRole"] = Translation.Get("PlayerFiles.Label.Role");

                TemplatePlayer["PFF_TournamentName"] = data.Fields.ElementAt(0).Value;
                TemplatePlayer["PFF_PlayerId"] = data.Fields.ElementAt(1).Value;
                TemplatePlayer["PFF_PlayerName"] = data.Fields.ElementAt(2).Value;
                TemplatePlayer["PFF_PlayerSurname"] = data.Fields.ElementAt(3).Value;
                TemplatePlayer["PFF_PlayerFilePicture"] = data.Fields.ElementAt(4).Value;
                TemplatePlayer["PFF_QrCode"] = data.Fields.ElementAt(5).Value;
                TemplatePlayer["PFF_TeamName"] = data.Fields.ElementAt(6).Value;
                TemplatePlayer["PFF_TeamLogo"] = data.Fields.ElementAt(7).Value;
                TemplatePlayer["PFF_TeamApparelNumber"] = data.Fields.ElementAt(8).Value;
                TemplatePlayer["PFF_IdCardNumber"] = data.Fields.ElementAt(9).Value;
                TemplatePlayer["PFF_BirthDate"] = data.Fields.ElementAt(10).Value;
                TemplatePlayer["PFF_Role"] = data.Fields.ElementAt(11).Value;
                TemplatePlayer["PFF_OrganizationLogo"] = data.Fields.ElementAt(12).Value;

                TemplatePlayer["Id"] = player.Id;
                TemplatePlayer["Name"] = player.Name;
                TemplatePlayer["Surname"] = player.Surname;
                TemplatePlayer["IdCardNumber"] = player.IdCardNumber;
                TemplatePlayer["BirthDate"] = player.BirthDate.ToString("dd-MM-yyyy");
                TemplatePlayer["ApparelNumber"] = player.TeamData.ApparelNumber;
                TemplatePlayer["FieldPosition"] = Translation.Get("FieldPosition" + player.TeamData.FieldPosition);
                TemplatePlayer["PlayerPhoto"] = player.IdPhotoImgUrl == null ? null : Utils.GetUploadUrl(Request, player.IdPhotoImgUrl, player.Id, "player");

                TemplatePlayer["OrgLogo"] = Utils.GetUploadUrl(Request, org.LogoImgUrl, org.Id, "org");
                TemplatePlayer["TeamName"] = team.Name;
                TemplatePlayer["TeamLogo"] = Utils.GetUploadUrl(Request, team.LogoImgUrl, team.Id, "team");
                TemplatePlayer["TournamentName"] = tournament.Name;

                PlayersTemplate.Add(TemplatePlayer);
                counter++;
            }

            var html = template(new
            {
                BgImage = data.BgImage != null ? uploadPath + "/" + data.BgImage : null,
                BgColor = data.BgColor,
                TextColor = data.TextColor,
                Players = PlayersTemplate,
            });


            var contentType = System.Net.Mime.MediaTypeNames.Application.Pdf;
            var fileName = "players-badge.pdf";

            PdfDocument doc = converter.ConvertHtmlString(html);

            byte[] Binary = doc.Save();

            var result = new FileContentResult(Binary, contentType)
            {
                FileDownloadName = fileName
            };

            return result;
        }

        // __ Global directory actions ________________________________________

        public static bool CheckUserExistsInGlobalDirectory(HttpRequest request, long idUser, string email)
        {
            using (var dc = GetGlobalDirectoryConn())
            {
                var t = dc.BeginTransaction();

                try
                {
                    var orgName = OrganizationManager.GetConfigForRequest(request).Name;

                    var user = dc.Query<GlobalUserOrganization>("SELECT * FROM userorganization WHERE idUser = @idUser AND email = @email AND organizationName = @orgName",
                        new { idUser = idUser, email = email, orgName = orgName }, t).FirstOrDefault();

                    if (user == null) return false;

                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static void AddUserToGlobalDirectory(HttpRequest request, long idUser, string email)
        {
            try
            {
                using (var directoryConn = GetGlobalDirectoryConn())
                {
                    var orgName = OrganizationManager.GetConfigForRequest(request).Name;

                    directoryConn.Insert(new GlobalUserOrganization
                    {
                        IdUser = idUser,
                        Email = email,
                        OrganizationName = orgName
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.IndexOf("userorg_email") > -1) throw new Exception("Error.Platform.EmailAlreadyExist");

                throw ex;
            }
        }

        public static void UpdateUserInGlobalDirectory(HttpRequest request, long idUser, string email)
        {
            using (var dc = GetGlobalDirectoryConn())
            {
                var t = dc.BeginTransaction();

                try
                {
                    var orgName = OrganizationManager.GetConfigForRequest(request).Name;

                    var dbUserOrg = dc.Query<GlobalUserOrganization>("SELECT * FROM userorganization WHERE idUser = @idUser AND organizationName = @orgName",
                        new { idUser = idUser, orgName = orgName }, t).FirstOrDefault();

                    if (dbUserOrg == null)
                    {
                        dc.Insert(new GlobalUserOrganization { IdUser = idUser, Email = email, OrganizationName = orgName }, t);
                    }
                    else
                    {
                        string prevEmail = dc.QueryFirst<string>($"SELECT email FROM userorganization WHERE idUser = {idUser};");
                        dc.Execute("UPDATE userorganization SET email = @email WHERE idUser = @idUser AND organizationName = @orgName",
                        new { idUser = idUser, email = email, orgName = orgName }, t);
                        dc.Execute($"UPDATE users SET email = '{email}' WHERE email = '{prevEmail}';");
                    }

                    t.Commit();
                }
                catch (Exception ex)
                {
                    if (ex.Message.IndexOf("userorg_email") > -1) throw new Exception("Error.Platform.EmailAlreadyExist");

                    t.Rollback();

                    throw ex;
                }
            }
        }

        public static void DeleteUserInGlobalDirectory(HttpRequest request, long idUser)
        {
            try
            {
                using (var directoryConn = GetGlobalDirectoryConn())
                {
                    var orgName = OrganizationManager.GetConfigForRequest(request).Name;

                    string email = directoryConn.QueryFirst<string>($"SELECT email FROM userorganization WHERE idUser = {idUser};");

                    directoryConn.Execute("DELETE FROM userorganization WHERE idUser = @idUser AND organizationName = @orgName",
                        new { idUser = idUser, orgName = orgName });

                    directoryConn.Execute($"DELETE FROM users WHERE email = '{email}';");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static void InserUserPropsInGlobalDirectory(User user)
        {
            using (var globalConn = GetGlobalDirectoryConn())
            {
                try
                {
                    globalConn.Insert(new UserGlobal
                    {
                        Email = user.Email,
                        Password = user.Password,
                        Salt = user.Salt,
                        EmailConfirmed = user.EmailConfirmed
                    });
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }

        private NotificationManager mNotifications;
        private AuthTokenManager mAuthTokenManager;
        private readonly HtmlSanitizer mSanitizer = new HtmlSanitizer();
    }

    public class PlayerApprovedInput
    {
        public long IdPlayer { get; set; }
        public bool Approved { get; set; }
    }

    public class PlayerTacticPositionInput
    {
        public long IdPlayer { get; set; }
        public long IdTeam { get; set; }
        public long IdTacticPosition { get; set; }
    }

    public class PlayerFile
    {
        public long IdTournament { get; set; }
        public long IdTeam { get; set; }
        public IFormFile File { get; set; }
    }    

    public class PlayersFilesRequest
    {
        public string BgColor { get; set; }
        public string TextColor { get; set; }
        public string BgImage { get; set; }
        public IEnumerable<PlayerFilesField> Fields { get; set; }
        public int PlayerType { get; set; }
        public int Size { get; set; }
        public int TeamId { get; set; }
        public int TournamentId { get; set; }
    }

    public class PlayerFilesField
    {
        public bool Value { get; set; }
        public string Name { get; set; }
    }
}
