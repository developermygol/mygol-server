using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi.Controllers
{
    public class NotificationsController: DbController
    {
        public NotificationsController(IOptions<Config> config) : base(config)
        {

        }


        [HttpGet]
        public IActionResult GetNotificationsForCurrentUser([FromQuery] bool unreadOnly)
        {
            return DbOperation((c) =>
            {
                if (!IsLoggedIn()) throw new UnauthorizedAccessException();

                var idUser = GetUserId();
                var filter = unreadOnly ? $" AND n.status < {(int)NotificationStatus.Read} " : "";

                return c.Query<Notification, User, Notification>(
                    $"SELECT n.*, u.name, u.id, u.avatarImgUrl FROM notifications n LEFT JOIN users u ON n.idCreator = u.id WHERE n.idRcptUser = @id {filter} ORDER BY timestamp DESC",
                    (notification, user) =>
                    {
                        notification.CreatorData = user;
                        return notification;
                    },
                    new { id = idUser }, 
                    splitOn: "name");
            });
        }

        [HttpPost("markread/{id}")]
        public IActionResult MarkRead(long id)
        {
            return DbOperation(c =>
            {
                if (!IsLoggedIn()) throw new UnauthorizedAccessException();

                c.Execute("UPDATE notifications SET status = @status WHERE id = @id AND idRcptUser = @idUser",
                    new { status = (int)NotificationStatus.Read, idUser = GetUserId(), id = id });

                return true;
            });
        }


        // __ Push notifications ______________________________________________


        [HttpPost("notifyorganization")]
        public IActionResult NotifyOrganization([FromBody] NotificationPayload payload)
        {
            // Auth: only orgadmins
            if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

            return DbOperation(c =>
            {
                Audit.Information(this, "{0}: Notifications.NotifyOrganization: {1} | {2}", GetUserId(), payload.Title, payload.Message);

                return NotifyOrganization(c, null, payload.Title, payload.Message);
            });
        }

        [HttpPost("notifytournament")]
        public IActionResult NotifyTournament([FromBody] NotificationPayload payload)
        {
            // Auth: only orgadmins
            if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

            return DbOperation(c =>
            {
                Audit.Information(this, "{0}: Notifications.NotifyOrganization: {1} | {2}", GetUserId(), payload.Title, payload.Message);

                return NotifyTournament(c, null, payload.Id, payload.Title, payload.Message);
            });
        }

        [HttpPost("notifyteam")]
        public IActionResult NotifyTeam([FromBody] NotificationPayload payload)
        {
            // Auth: only orgadmins
            if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

            return DbOperation(c =>
            {
                Audit.Information(this, "{0}: Notifications.NotifyTeam: {1} | {2}", GetUserId(), payload.Title, payload.Message);

                return NotifyTeam(c, null, payload.Id, payload.Title, payload.Message);
            });
        }

        [HttpPost("notifyuser")]
        public IActionResult NotifyUser([FromBody] NotificationPayload payload)
        {
            // Auth: only orgadmins
            if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

            return DbOperation(c =>
            {
                Audit.Information(this, "{0}: Notifications.NotifyUser: {1} | {2}", GetUserId(), payload.Title, payload.Message);

                return NotifyUser(c, null, payload.Id, payload.Title, payload.Message);
            });
        }


        // __ Notif API _______________________________________________________

        public static int NotifyMatch(IDbConnection c, IDbTransaction t, Match m, string title, string message)
        {
            if (m == null) throw new Exception("Error.InvalidMatch");

            // Only players are notified for now
            var users = c.Query<User>("SELECT p.surname as name, devicetoken FROM teamplayers tp JOIN players p ON tp.idPlayer = p.id JOIN userdevices ud ON ud.idUser = p.idUser WHERE tp.idTeam = @idHomeTeam OR tp.idTeam = @idVisitorTeam", new { idHomeTeam = m.IdHomeTeam, idVisitorTeam = m.IdVisitorTeam });
            if (users.Count() == 0) return 0;

            return NotifyUsers(users, title, message);
        }

        public static int NotifyUser(IDbConnection c, IDbTransaction t, long idUser, string title, string message)
        {
            if (idUser == 0) throw new Exception("Error.InvalidUser");

            var users = c.Query<User>("SELECT devicetoken FROM users u JOIN userdevices ud ON ud.idUser = u.id WHERE u.id = @id", new { id = idUser });
            if (users.Count() == 0) throw new DataException("Error.UserNotFound", idUser.ToString());

            return NotifyUsers(users, title, message);
        }

        public static int TryNotifyUser(IDbConnection c, IDbTransaction t, long idUser, string title, string message)
        {
            // Same as notify user but does not throw in case of any problem. 

            var users = c.Query<User>("SELECT devicetoken FROM users u JOIN userdevices ud ON ud.idUser = u.id WHERE u.id = @id", new { id = idUser });
            if (users.Count() == 0) return 0;

            return NotifyUsers(users, title, message);
        }

        public static int NotifyTeam(IDbConnection c, IDbTransaction t, long idTeam, string title, string message)
        {
            if (idTeam == 0) throw new Exception("Error.InvalidTeam");

            var users = c.Query<User>("SELECT devicetoken FROM teamplayers tp JOIN players p ON tp.idPlayer = p.id JOIN userdevices ud ON ud.idUser = p.idUser WHERE tp.idTeam = @id", new { id = idTeam });
            if (users.Count() == 0) throw new Exception("Error.TeamNoPlayers");

            return NotifyUsers(users, title, message);
        }

        public static int NotifyTournament(IDbConnection c, IDbTransaction t, long idTournament, string title, string message)
        {
            if (idTournament == 0) throw new Exception("Error.InvalidTournament");

            var users = c.Query<User>("SELECT devicetoken FROM tournamentTeams tt JOIN teamPlayers tp ON tt.idTeam = tp.idTeam JOIN players p ON tp.idPlayer = p.id JOIN userdevices ud ON ud.idUser = p.idUser WHERE tt.idTournament = @id", new { id = idTournament });
            if (users.Count() == 0) throw new Exception("Error.TournamentNoPlayers");

            return NotifyUsers(users, title, message);
        }

        public static int NotifyOrganization(IDbConnection c, IDbTransaction t, string title, string message)
        {
            var users = c.Query<User>("SELECT devicetoken FROM userdevices ud");
            if (users.Count() == 0) throw new Exception("Error.OrgNoUsers");

            return NotifyUsers(users, title, message);
        }




        public static int NotifyUsers(IEnumerable<User> users, string title, string message)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Error.InvalidNotification");

            var notifications = ExpoPushAdapter.GetNotifications(users, title, message);
            ExpoPushProvider.EnqueueNotifications(notifications);

            return notifications.Count;
        }


    }


    public class NotificationPayload
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
    }
}
