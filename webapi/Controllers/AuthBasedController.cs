using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;

namespace webapi.Controllers
{
    [Route("api/[controller]")]
    public abstract class AuthBasedController: Controller
    {
        public AuthBasedController(IOptions<Config> config)
        {
            mConfig = config.Value;
        }

        protected IActionResult Operation(Func<object> worker)
        {
            try
            {
                return new JsonResult(worker());
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        protected bool IsLoggedIn()
        {
            return User.Identity.IsAuthenticated;
        }

        protected long GetUserId()
        {
            if (!User.Identity.IsAuthenticated) return -1;

            return long.Parse(User.FindFirst(ClaimTypes.Sid).Value);
        }

        protected bool IsPlayer()
        {
            return User.IsInRole("1");
        }

        protected bool IsReferee()
        {
            return User.IsInRole("2");
        }

        protected bool IsTeamAdmin(long idTeam, IDbConnection c)
        {
            var idUser = GetUserId();
            return c.ExecuteScalar<bool>("SELECT isTeamAdmin FROM teamplayers t JOIN players p ON t.idPlayer = p.id WHERE p.idUser = @idUser AND t.idTeam = @idTeam", new { idUser = idUser, idTeam = idTeam });
        }

        protected bool IsOrganizationAdmin()
        {
            return User.IsInRole("4");
        }

        protected void CheckAuthLevel(UserLevel minimumLevel)
        {
            var role = GetUserRole();
            if ( (role == null && mConfig.RequireLogin) || role < (int)minimumLevel) throw new UnauthorizedAccessException();
        }

        protected bool AuthByRequestType(UserLevel list, UserLevel add, UserLevel edit, UserLevel delete)
        {
            var role = GetUserRole();
            if (role == null) role = (int)UserLevel.All;

            switch (Request.Method)
            {
                case "GET":
                    return (int)list <= role;
                case "POST":
                    var p = Request.Path.Value.ToLower().TrimEnd('/');
                    if (p.EndsWith("delete"))
                        return (int)delete <= role;
                    else
                        return (int)add <= role;
                case "PUT":
                    return (int)edit <= role;
                case "DELETE":
                    return (int)delete <= role;
                default:
                    return false;
            }
        }

        protected bool IsWriteRequest()
        {
            return Request.Method != "GET";
        }

        protected int? GetUserRole()
        {
            return GetIntClaim(ClaimTypes.Role);
        }

        protected string GetUserLocale()
        {
            return User.FindFirst(ClaimTypes.Locality).Value ?? mConfig.DefaultLocale;
        }

        protected IActionResult Error(string msg)
        {
            return new BadRequestObjectResult(msg);
        }


        private int? GetIntClaim(string claim)
        {
            string claimStr = User.FindFirstValue(claim);
            if (claimStr == null || claimStr == "") return null;
            if (!int.TryParse(claimStr, out int result)) return null;

            return result;
        }

        protected Config mConfig;
    }

    public enum UserLevel
    {
        All = 0,
        Player = 1,
        Referee = 2,
        OrgAdmin = 4,
        MasterAdmin = 5,
        None = 6
    }
}
