using contracts;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;
using webapi.Models.Result;

namespace webapi.Controllers
{
    
    public class RefereesController: DbController
    {
        public RefereesController(NotificationManager notifier, IOptions<Config> config, AuthTokenManager authManager) : base(config)
        {
            mNotifier = notifier;
            mAuthTokenManager = authManager;
        }

        [HttpGet]
        public IActionResult Get()
        {
            return DbOperation(c => {
                var extra = "";
                if (IsOrganizationAdmin()) extra = ", u.email, u.mobile, u.emailConfirmed ";

                var query = $"SELECT u.id, u.name, u.avatarImgUrl, u.level {extra} FROM users u WHERE level = @level";
                return c.Query<User>(query, new { level = (int)UserLevel.Referee });
            });
        }

        [HttpGet("{idUser:long}")]
        public IActionResult Get(long idUser)
        {
            return DbOperation(c => {
                var extra = "";
                if (IsOrganizationAdmin() || (IsReferee() && idUser == GetUserId() ) ) extra = ", u.email, u.mobile, u.emailConfirmed ";

                var query = $"SELECT u.id, u.name, u.avatarImgUrl, u.level {extra} FROM users u WHERE id = @idUser AND level = @level";
                return c.Query<User>(query, new { level = (int)UserLevel.Referee, idUser }).FirstOrDefault();
            });
        }

        [HttpGet("details")]
        public IActionResult GetRefereeDetails()
        {
            return DbOperation(c => {
                if (!IsOrganizationAdmin() && !IsReferee()) throw new UnauthorizedAccessException();

                var idUser = GetUserId();

                var referee = c.Query<User>(@"
                    SELECT id, name, mobile, email, avatarImgUrl, level FROM users WHERE id = @idUser AND level = 2;
                ", new { idUser }).FirstOrDefault();

                var matches = MatchesController.GetMatchesForReferee(c, idUser);

                return new { Referee = referee, Matches = matches };
            });
        }

        [HttpGet("fortimeslot")]
        public IActionResult GetRefereesForTimeSlot([FromQuery(Name="from")] DateTime from, [FromQuery(Name="duration")] int duration)
        {
            return DbOperation(c => {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();


                var to = from.AddMinutes(duration);

                var busyRefsSql = @"
                    SELECT 
                        mr.iduser
                    FROM 
                        matchreferees mr
                        JOIN matches m ON mr.idmatch = m.id 
                    WHERE 
                        (m.startTime, m.startTime + m.duration * interval '1 minute') OVERLAPS (@startTime, @endTime)";

                var allRefsSql = "SELECT u.id, u.name, u.avatarImgUrl FROM users u WHERE level = @level";

                var busyReferees = c.Query<long>(busyRefsSql, new { startTime = from, endTime = to });
                var allReferees = c.Query<User>(allRefsSql, new { level = (int)UserLevel.Referee });

                // Now subtract busyRefs from allRefs
                return allReferees.Where((r) => !busyReferees.Contains(r.Id));
            });
        }

        [HttpPost]
        public IActionResult Create([FromBody] User referee)
        {
            return DbTransaction( (c, t) => {

                if (referee == null) throw new NoDataException();
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                UsersController.CheckEmail(c, t, referee.Email);

                var dbUser = new User {
                    Name = referee.Name,
                    Email = referee.Email,
                    Mobile = referee.Mobile,
                    Lang = referee.Lang,
                    AvatarImgUrl = referee.AvatarImgUrl,
                    Level = (int)UserLevel.Referee,
                    Password = "",
                    Salt = "",
                    EmailConfirmed = false
                };

                var idUser = c.Insert(dbUser, t);

                SendInvitation(c, t, dbUser);

                PlayersController.AddUserToGlobalDirectory(Request, idUser, referee.Email);

                return idUser;
            });
        }

        [HttpPost("resend/{idReferee}")]
        public IActionResult ResendInvitation(long idReferee)
        {
            return DbTransaction((c, t) =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                var dbReferee = c.Get<User>(idReferee);
                if (dbReferee == null) throw new Exception("Error.NotFound");

                SendInvitation(c, t, dbReferee);

                return true;
            });
        }

        [HttpPut]
        public IActionResult Edit([FromBody] User referee)
        {
            return DbTransaction((c, t) => {
                if (referee == null) throw new NoDataException();
                var isReferee = IsReferee();
                if (!IsOrganizationAdmin() && !isReferee) throw new UnauthorizedAccessException();
                if (isReferee && (GetUserId() != referee.Id)) throw new UnauthorizedAccessException();

                var dbUser = c.Get<User>(referee.Id);
                if (dbUser == null) throw new Exception("Error.NotFound");

                var isNewEmail = false;

                if (referee.Email != dbUser.Email)
                {
                    UsersController.CheckEmail(c, null, referee.Email);
                    isNewEmail = true;
                }

                Mapper.MapExplicit(referee, dbUser, new string[] {
                    "Name", "Email", "Mobile", "AvatarImgUrl"
                });

                if (!String.IsNullOrWhiteSpace(referee.Password))
                {
                    UsersController.UpdatePassword(dbUser, referee.Password);
                    dbUser.EmailConfirmed = true;
                }

                var result = c.Update(dbUser, t);

                if (isNewEmail) PlayersController.UpdateUserInGlobalDirectory(Request, dbUser.Id, referee.Email);

                return result;
            });
        }


        [HttpPost("delete")]
        public IActionResult Delete([FromBody] User referee)
        {
            return DbTransaction((c, t) => {
                if (referee == null) throw new NoDataException();

                CheckAuthLevel(UserLevel.OrgAdmin);

                // TODO: Verify that no matches are assigned to this referee. 
                
                // For now, assume it is a forced delete and remove referee from matches. 
                var sql = @"
                    DELETE FROM matchreferees WHERE idUser = @idUser;
                    DELETE FROM users WHERE id = @idUser AND level = @level;
                ";

                var result = c.Execute(sql, new { idUser = referee.Id, level = (int)UserLevel.Referee }, t);

                PlayersController.DeleteUserInGlobalDirectory(Request, referee.Id);

                return result;
            });
        }


        // __ Impl ____________________________________________________________


        private void SendInvitation(IDbConnection c, IDbTransaction t, User referee)
        {
            var notifData = GetRefereeNotification(c, t, referee.Id, true);
            mNotifier.NotifyEmail(Request, c, t, TemplateKeys.EmailRefereeInviteHtml, notifData);
        }


        private PlayerNotificationData GetRefereeNotification(IDbConnection c, IDbTransaction t, long idUser, bool wantsPin)
        {
            var fromId = GetUserId();

            var mr = c.QueryMultiple(@"
                    SELECT u.id, u.name, u.email, u.mobile, u.emailConfirmed FROM users u WHERE id = @idUser;
                    SELECT id, name, logoImgUrl FROM organizations LIMIT 1;
                    SELECT u.id, u.name, u.email, u.mobile FROM users u WHERE id = @idFrom;
                ", new { idUser, idFrom = fromId });

            var toUser = mr.ReadFirst<User>();
            var org = mr.ReadFirst<PublicOrganization>();
            var fromUser = mr.ReadFirstOrDefault<User>();
            if (fromUser == null && fromId >= 10000000) fromUser = UsersController.GetGlobalAdminForId(fromId);

            if (toUser == null) throw new Exception("Error.NotFound.ToUser");
            if (fromUser == null) throw new Exception("Error.NotFound.FromUser");
            //if (fromUser == null) fromUser = new User { Id=1, Name=org.Name, Email="admin@mygol.es" }; 

            var activationLink = toUser.EmailConfirmed && !wantsPin ? "" : PlayersController.GetActivationLink(Request, mAuthTokenManager, toUser);
            var activationPin = toUser.EmailConfirmed && !wantsPin ? "" : UsersController.GetActivationPin(mAuthTokenManager, toUser);

            return new PlayerNotificationData
            {
                From = fromUser,
                To = toUser,
                Org = org,
                ActivationLink = activationLink,
                ActivationPin = activationPin,
                Images = new PlayerInviteImages
                {
                    OrgLogo = Utils.GetUploadUrl(Request, org.LogoImgUrl, org.Id, "org"),
                }
            };
        }


        private NotificationManager mNotifier;
        private AuthTokenManager mAuthTokenManager;
    }
}
