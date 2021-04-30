using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using webapi.Models.Db;
using Dapper;
using Dapper.Contrib.Extensions;
using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Serilog;

namespace webapi.Controllers
{
    [Route("api/[controller]")]
    public class UsersController : CrudController<User>
    {
        public UsersController(
            AuthTokenManager tokenManager, 
            NotificationManager notificationManager,
            IOptions<Config> config) : base(config)
        {
            mTokenManager = tokenManager;
            mNotifier = notificationManager;
        }


        [HttpPost("login")]
        public IActionResult Login([FromBody] InputLoginInfo loginInfo)
        {
            return DbOperation(c =>
            {
                if (loginInfo == null) throw new NoDataException();

                Audit.Information(this, "Users.Login1: {Email}", loginInfo.Email);

                var user = ValidateUser(c, loginInfo);
                if (user == null) throw new LoginException(loginInfo.Email);

                LoginResult result = GetLoginResultForUser(c, null, user);

                if (loginInfo.DeviceToken != null && loginInfo.DeviceToken != "") CheckUserDevice(c, null, user, loginInfo.DeviceToken, loginInfo.DeviceName);

                Audit.Information(this, "Users.Login2: Success {Email} '{Name}' ({Id})", user.Email, user.Name, user.Id);

                return result;
            });
        }

        [HttpPost("pinlogin")]
        public IActionResult LoginWithPin([FromBody] InputLoginInfo loginInfo)
        {
            return DbTransaction((c, t) =>
            {
                if (loginInfo == null) throw new NoDataException();

                Audit.Information(this, "Users.LoginWithPin1 {0}", loginInfo.Email);

                var dbUser = GetUserForEmail(c, loginInfo.Email);
                if (dbUser.Password != null && dbUser.Password != "") throw new Exception("Error.NeedPin");

                if (!ValidatePin(c, dbUser, loginInfo.EnrollPin)) throw new LoginException(loginInfo.Email);

                LoginResult result = GetLoginResultForUser(c, t, dbUser);

                dbUser.EmailConfirmed = true;

                c.Update(dbUser, t);

                if (loginInfo.DeviceToken != null && loginInfo.DeviceToken != "") CheckUserDevice(c, t, dbUser, loginInfo.DeviceToken, loginInfo.DeviceName);

                Audit.Information(this, "Users.LoginWithPin2: Success {Email} '{Name}' ({Id})", dbUser.Email, dbUser.Name, dbUser.Id);

                return result;
            });
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return null;
        }


        [HttpPost("register")]
        public IActionResult Register()
        {
            return null;
        }

        [HttpGet("resetpassword")]
        public IActionResult SendResetPasswordEmail([FromQuery(Name = "e")] string email)
        {
            return DbTransaction((c, t) =>
            {
                if (string.IsNullOrWhiteSpace(email)) throw new NoDataException();

                var nd = GetPasswordResetNotifData(c, email);
                if (nd.To == null) throw new Exception("Error.Email.NotFound");

                //var updated = c.Execute("UPDATE users SET emailConfirmed = 'f' WHERE id = @idUser", new { idUser = nd.To.Id }, t);
                using (var dc = GetGlobalDirectoryConn())
                {
                    var dt = dc.BeginTransaction();

                    try
                    {
                        var updated = dc.Execute("UPDATE users SET emailConfirmed = 'f' WHERE id = @idUser", new { idUser = nd.To.Id }, dt);

                        if (updated != 1) throw new Exception("Error.Email.CouldNotUpdate");

                        mNotifier.NotifyEmail(Request, c, t, TemplateKeys.PlayerResetPassword, nd);

                        return true;
                    }
                    catch (Exception)
                    {
                        throw new Exception("Error");
                    }
                }
            });
        }

        [HttpPost("activate")]
        public IActionResult ActivatePlayer([FromQuery(Name = "at")] string activationToken, [FromBody] Player player)
        {
            if (activationToken == null || player == null || player.UserData == null) throw new Exception("Error.EmptyData");

            return DbTransaction((c, t) =>
            {
                Audit.Information(this, "Users.ActivatePlayer: pid: {0}", player.Id);

                // Verify the email from the activation token

                var dbUser = GetUserFromActivationToken(c, activationToken);
                if (dbUser.EmailConfirmed) throw new AlreadyActivatedException(dbUser.Email);

                var user = player.UserData;

                // Update user data (specially password and email confirmed)

                UpdatePassword(dbUser, user.Password);
                dbUser.EmailConfirmed = true;

                using (var globalConn = GetGlobalDirectoryConn())
                {
                    try
                    {
                        globalConn.Insert(new UserGlobal
                        {
                            Email = dbUser.Email,
                            Password = dbUser.Password,
                            Salt = dbUser.Salt,
                            EmailConfirmed = dbUser.EmailConfirmed
                        });
                    }
                    catch (Exception ex)
                    {
                        throw ex;
                    }
                }

                if (!c.Update(dbUser, t)) throw new EmailException("Error.EmailNotFound", dbUser.Email);

                // Update the player data as well

                return true;
            });
        }


        [HttpPost("basiclogin")]
        public IActionResult BasicLogin([FromBody] InputLoginInfo login)
        {
            try
            {
                if (login == null) throw new NoDataException();

                Audit.Information(this, "Users.BasicLogin {0}", login.Email);

                // Locate the email in the directory
                var orgForUser = GetOrgNameForEmail(login.Email);
                if (orgForUser == null) throw new Exception("Error.NonExistent");

                var orgDbConfig = OrganizationManager.GetDbConfigForOrgName(orgForUser);
                var orgConn = GetConn(orgDbConfig);
                var orgConfig = OrganizationManager.GetConfigForOrgName(orgForUser);

                // Have to return the org domain

                return DbOperation(c =>
                {
                    var result = new BasicLoginResult
                    {
                        EndPoints = new EndPoints
                        {
                            Api= orgConfig.ApiUrl,
                            PrStatic = orgConfig.PrivateStaticBaseUrl,
                            Uploads = orgConfig.UploadsBaseUrl
                        },
                        Action = (int)BasicLoginResultType.PasswordRequired
                    };

                    var userFromglobal = GetGlobalUserForEmail(login.Email);

                    var users = c.Query<User>(@"SELECT * FROM users WHERE email iLIKE @email;", new { email = login.Email });
                    var count = users.Count();
                    if (count == 0) return result;  // Not found, but we are not telling.
                                                    //if (count > 1) throw new Exception("Error.DuplicateEmail"); // This is an internal error, we should proceed.

                    var user = users.First();
                    result.IdUser = user.Id;

                    // This columns should not be in org>users anymore they should be in global>users
                    user.Email = userFromglobal.Email;
                    user.Password = userFromglobal.Password;
                    user.Salt = userFromglobal.Salt;
                    user.EmailConfirmed = userFromglobal.EmailConfirmed;

                    if (user.Password == null || user.Password == "")
                        result.Action = (int)BasicLoginResultType.NoPasswordSet;
                    else
                        result.Action = (int)BasicLoginResultType.PasswordRequired;
                     
                    return result;
                }, orgConn);
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }


        // __ Impl ____________________________________________________________


        private PlayerNotificationData GetPasswordResetNotifData(IDbConnection c, string email)
        {
            var user = GetUserForEmail(c, email);

            /*var mr = c.QueryMultiple(@"
                    SELECT u.id, u.name, u.email, u.mobile, u.emailConfirmed FROM users u WHERE email ilike @email;
                    SELECT id, name, logoImgUrl FROM organizations LIMIT 1;
                ", new { email = email });*/
            var mr = c.QueryFirst<PublicOrganization>(@"SELECT id, name, logoImgUrl FROM organizations LIMIT 1;");

            var toUser = user;
            //var toUser = mr.ReadFirst<User>();
            var fromUser = toUser;
            //var org = mr.ReadFirst<PublicOrganization>();
            var org = mr;

            var activationLink = PlayersController.GetActivationLink(Request, mTokenManager, toUser);

            var result = new PlayerNotificationData
            {
                From = fromUser,
                To = toUser,
                Org = org,
                ActivationLink = activationLink,
                Images = new PlayerInviteImages
                {
                    OrgLogo = Utils.GetUploadUrl(Request, org.LogoImgUrl, org.Id, "org"),
                }
            };

            return result;
        }


        private string GetOrgNameForEmail(string email)
        {
            using (var c = GetGlobalDirectoryConn())
            {
                var user = c.QueryFirstOrDefault<GlobalUserOrganization>("SELECT organizationName FROM userorganization WHERE email iLIKE @email", new { email = email });
                if (user == null) return null;

                return user.OrganizationName;
            }
        }


        private LoginResult GetLoginResultForUser(IDbConnection c, IDbTransaction t, User user)
        {
            var adminTeamsIds = c.Query<long>("select idteam from teamplayers tp join players p on tp.idplayer = p.id where p.iduser = @idUser and tp.isTeamAdmin = 't'; ", new { idUser = user.Id }, t);

            return new LoginResult
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                AvatarImgUrl = user.AvatarImgUrl,
                Level = user.Level,
                Token = mTokenManager.CreateToken(user),
                AdminTeamIds = adminTeamsIds
            };
        }


        private static bool ValidatePassword(string password)
        {
            // Check uppercase/lowercase/numbers/symbols rules.

            return password.Length >= 8;
        }

        public static void UpdatePassword(User target, string password)
        {
            if (!ValidatePassword(password)) throw new Exception("Error.PasswordNoRules");

            target.Password = password;
            HashPasswordInUser(target);
        }

        private User GetUserFromActivationToken(IDbConnection c, string activationToken)
        {
            var email = GetEmailFromActivationToken(activationToken);
            if (email == null) throw new EmailException("Error.EmailNotFound", "<no email in activation token>");

            /*var users = c.Query<User>("SELECT * FROM users WHERE email iLIKE @email", new { email = email }, t);
            if (users.Count() > 1) throw new Exception("Error.DuplicateEmail");

            var user = users.FirstOrDefault();*/
            var user = GetUserForEmail(c, email);
            if (user == null) throw new EmailException("Error.EmailNotFound", email);

            return user;
        }

        private string GetEmailFromActivationToken(string token)
        {
            var claims = mTokenManager.DecryptToken(token);
            return claims.Where(claim => claim.Type == "email").FirstOrDefault()?.Value;
        }

        public static User GetUserForEmail(IDbConnection c, string email)
        {
            // Get from the database now from global>users
            var userGlobal = GetGlobalAdminForEmail(email);
            if (userGlobal == null)
            {
                // Try global admin
                userGlobal = GetGlobalUserForEmail(email);

                if (userGlobal == null) return null;

                var user = c.QueryFirstOrDefault<User>("SELECT * FROM users WHERE id = @id", new { id = userGlobal.Id });
                // No Org user swap to userGlobal values
                if (user == null)
                {
                    user = userGlobal;
                }

                // This columns should not be in org>users anymore they should be in global>users
                user.Email = userGlobal.Email;
                user.Password = userGlobal.Password;
                user.Salt = userGlobal.Salt;
                user.EmailConfirmed = userGlobal.EmailConfirmed;

                return user;
            }

            return userGlobal;

            /* 🔎 INITIAL
            var users = c.Query<User>("SELECT * FROM users WHERE email iLIKE @email", new { email = email }, t);
            if (users.Count() != 1)
            {
                // Try global admin
                return GetGlobalAdminForEmail(email);
            }

            return users.First();
            */
        }

        public static User GetUserForId(IDbConnection c, long userId)
        {
            string email = GetGlobalEmailForId(c, userId);
            return GetUserForEmail(c, email);
        }

        public static User GetGlobalUserForEmail(string email)
        {
            using (var c = GetGlobalDirectoryConn())
            {
                var users = c.Query<User>("SELECT * FROM users u WHERE email ilike @email", new { email = email });
                if (users.Count() != 1) return null;

                var user = users.First();
                var userOrgGlobal = c.QueryFirstOrDefault<User>($"SELECT iduser AS id FROM userorganization WHERE email ilike '{email}'");
                
                if(userOrgGlobal != null) user.Id = userOrgGlobal.Id; // 💥🔎 NO well enteres users

                return users.First();
            }
        }

        public static string GetGlobalEmailForId(IDbConnection c, long userId)
        {
            using (var dc = GetGlobalDirectoryConn())
            {
                string email = dc.QueryFirstOrDefault<String>("SELECT email FROM userorganization WHERE iduser = @userId", new { userId = userId });
                // 💥 This should not be necessary in the future just using it for global admins
                if(email == null) email = c.QueryFirstOrDefault<String>("SELECT email FROM users WHERE id = @userId", new { userId = userId });
                return email;
            }
        }

        public static User GetGlobalAdminForEmail(string email)
        {
            using (var c = GetGlobalDirectoryConn())
            {
                var users = c.Query<User>("SELECT * FROM globalAdmins WHERE email ilike @email", new { email = email });
                if (users.Count() != 1) return null;

                return users.First();
            }
        }

        public static User GetGlobalAdminForId(long idUser)
        {
            using (var c = GetGlobalDirectoryConn())
            {
                var users = c.Query<User>("SELECT * FROM globalAdmins WHERE id = @idUser", new { idUser });
                if (users.Count() != 1) return null;

                return users.First();
            }
        }

        private bool ValidatePin(IDbConnection c, User user, string pin)
        {
            var dbPin = GetActivationPin(mTokenManager, user);

            return dbPin.ToLower() == pin.ToLower();
        }

        public static string GetActivationPin(AuthTokenManager tm,  User user)
        {
            var token = tm.CreateActivationToken(user.Id, user.Email);
            int hash = (byte)token[token.Length - 2] << 8 + (byte)token[token.Length - 1];
            if (hash < 0) hash = -hash;
            var pin = hash.ToString("0000").Substring(0, 4);
            if (pin.StartsWith('-')) pin = '0' + pin.Substring(1);

            return pin;
        }

        private User ValidateUser(IDbConnection c, InputLoginInfo loginInfo)
        {
            var user = GetUserForEmail(c, loginInfo.Email);
            if (user == null) return null;

            // the email matches, now check password
            var salt = Convert.FromBase64String(user.Salt);
            var hashedPassword = AuthTokenManager.HashPassword(loginInfo.Password, salt);

            if (!hashedPassword.Equals(user.Password)) return null;

            return user;

            // May require email confirmation to allow login. 
        }

        private void CheckUserDevice(IDbConnection c, IDbTransaction t, User user, string deviceToken, string deviceName)
        {
            // Checks if the user device is already there, if not, it is added. 
            var count = c.ExecuteScalar<int>("SELECT count(id) FROM userdevices WHERE deviceToken = @token", new { token = deviceToken }, t);
            
            if (count == 1) return;  // Already registered. 

            if (count > 1) throw new Exception($"Device registered more than once ({count})");

            RegisterUserDevice(c, t, user, deviceToken, deviceName);
        }

        private void RegisterUserDevice(IDbConnection c, IDbTransaction t, User user, string deviceToken, string deviceName)
        {
            c.Insert(new UserDevice
            {
                Name = deviceName,
                DeviceToken = deviceToken,
                IdUser = user.Id
            }, t);
        }

        private void UnregisterUserDevice(IDbConnection c, IDbTransaction t, User user, string deviceToken)
        {
            c.Execute("DELETE FROM userdevices WHERE deviceToken = @token AND idUser = @idUser", new { token = deviceToken, idUser = user.Id }, t);
        }

        private static void HashPasswordInUser(User user)
        {
            if (user == null || user.Password == null || user.Password == "") return;

            // Take the clearTextPassword in the password field and hash it

            var hashedPass = AuthTokenManager.HashPassword(user.Password);
            user.Password = hashedPass.Hash;
            user.Salt = hashedPass.Salt;
        }
        

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "users",
                GetAllQuery = "SELECT id, name, email, mobile, level, avatarImgUrl, lang FROM users WHERE level = 4 ORDER BY id",
                GetSingleQuery = "SELECT id, name, email, mobile, level, avatarImgUrl, lang FROM users WHERE level = 4 AND id = @id ORDER BY id"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, User target, IDbConnection conn)
        {
            return IsOrganizationAdmin();
        }

        protected override bool ValidateNew(User value, IDbConnection conn, IDbTransaction t)
        {
            CheckEmail(conn, null, value.Email);

            HashPasswordInUser(value);

            return true;
        }

        public static void CheckEmail(IDbConnection c, IDbTransaction t,  string email)
        {
            if (EmailExists(c, t, email)) throw new Exception("Error.EmailAlreadyExists");
        }

        public static bool EmailExists(IDbConnection c, IDbTransaction t, string email)
        {
            using (var cd = GetGlobalDirectoryConn())
            {
                var emailCount = cd.ExecuteScalar<int>("SELECT COUNT(id) FROM USERS WHERE email ILIKE @email", new { email }, t);
                return emailCount > 0;
            }
            /*var emailCount = c.ExecuteScalar<int>("SELECT COUNT(id) FROM USERS WHERE email ILIKE @email", new { email }, t);
            return emailCount > 0;*/
        }

        protected override object AfterNew(User value, IDbConnection conn, IDbTransaction t)
        {
            var token = mTokenManager.CreateToken(new[] { new Claim("id", value.Id.ToString()), new Claim("email", value.Email) });

            // 🚧 Add to globalOrg userorganitzation and users

            using (var dc = GetGlobalDirectoryConn())
            {
                var dt = dc.BeginTransaction();

                try
                {
                    // 💥 var orgName = OrganizationManager.GetConfigForRequest(request).Name;

                    dc.Insert(new GlobalUserOrganization { IdUser = value.Id, Email = value.Email, OrganizationName = "" }, dt);
                    dc.Insert(new UserGlobal { Email = value.Email, Password = value.Password, Salt = value.Salt, EmailConfirmed = value.EmailConfirmed }, dt);
                    
                    dt.Commit();
                }
                catch (Exception ex)
                {                 
                    dt.Rollback();
                    throw ex;
                }
            }

            // TODO: Notify, but only if not a player because it already has its notification. 
            // More and more I think I should unify the users and players tables. It's one hell of a refactor...

            // Also need to send the new user to the global database. May not be needed, since the DB is only used for players.

            // TODO: Notify
            //mNotifier.SendCannedNotification(
            //    conn, null, "NewUser", GetUserId(), value.Id, 
            //    value.Name, token);

            return value.Id;
        }

        protected override object AfterEdit(User value, IDbConnection conn, IDbTransaction t)
        {
            // 🚧 Update to globalOrg userorganitzation and users
            using (var dc = GetGlobalDirectoryConn())
            {
                var dt = dc.BeginTransaction();

                try
                {
                    dc.Execute($"UPDATE userorganization SET email = '{value.Email}' WHERE iduser = {value.Id};");
                    dc.Execute($"UPDATE users SET email = '{value.Email}', password = '{value.Password}', salt = '{value.Salt}', emailconfirmed = {value.EmailConfirmed} WHERE email = '{value.Email}';");
                    dt.Commit();
                }
                catch (Exception ex)
                {
                    dt.Rollback();
                    throw ex;
                }
            }

            return value.Id;
        }

        protected override object AfterDelete(User value, IDbConnection conn, IDbTransaction t)
        {
            // 🚧 Delete to globalOrg userorganitzation and users
            using (var dc = GetGlobalDirectoryConn())
            {
                var dt = dc.BeginTransaction();

                try
                {
                    dc.Execute($"DELETE FROM userorganization WHERE iduser = {value.Id} AND email = '{value.Email}';");
                    dc.Execute($"DELETE FROM users WHERE email = '{value.Email}';");
                    dt.Commit();
                }
                catch (Exception ex)
                {
                    dt.Rollback();
                    throw ex;
                }
            }

            return value.Id;
        }

        protected override bool ValidateEdit(User user, IDbConnection conn, IDbTransaction t)
        {
            var dbUser = conn.Get<User>(user.Id);
            //user.NotificationPushToken = dbUser.NotificationPushToken;  // Updated through specific API
            user.EmailConfirmed = dbUser.EmailConfirmed;                // Updated through specific API

            if (user.Email != dbUser.Email) CheckEmail(conn, t, user.Email);

            if (!ValidateEmail(user.Email)) throw new Exception("Error.InvalidEmail");

            if (user.Password == null || user.Password == "")
            {
                user.Password = dbUser.Password;
                user.Salt = dbUser.Salt;
            }
            else
            {
                HashPasswordInUser(user);
            }

            return true;
        }

        protected override bool ValidateDelete(User value, IDbConnection conn, IDbTransaction t)
        {
            // Check if no other entities depend on the user
            return true;
        }

        private bool ValidateEmail(string email)
        {
            return EmailRegex.IsMatch(email);
        }

        private NotificationManager mNotifier;
        private AuthTokenManager mTokenManager;

        private static readonly Regex EmailRegex = new Regex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase);
    }


    public class InputLoginInfo
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string DeviceToken { get; set; }
        public string DeviceName { get; set; }
        public string EnrollPin { get; set; }
    }

    public class BasicLoginResult
    {
        public EndPoints EndPoints { get; set; }
        public long IdUser { get; set; }
        public int Action { get; set; }
    }

    public class EndPoints
    {
        public string Api { get; set; }
        public string PrStatic { get; set; }
        public string Uploads { get; set; }
    }

    public class LoginResult
    {
        public long Id { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public string AvatarImgUrl { get; set; }
        public string Token { get; set; }
        public IEnumerable<long> AdminTeamIds { get; set; }
    }

    public enum BasicLoginResultType
    {
        PasswordRequired = 1,
        NoPasswordSet = 10
    }
}