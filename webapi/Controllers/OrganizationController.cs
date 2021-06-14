using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Data;
using webapi.Models.Db;

namespace webapi.Controllers
{
    public class OrganizationController: DbController
    {
        // Org data can be edited by the org admin, but they don't need to be on the 
        // database, they can be read directly from the json with all the org data
        // (that also contains seasons, etc). 
        // Having it in the database will make access more uniform and compatible with
        // the rest of the infrastracture.


        public OrganizationController(IOptions<Config> config) : base(config)
        {
            Utils.GetAllRoutes(this.HttpContext);
        }


        public IActionResult Get()
        {
            return DbOperation((c) =>
            {
                var query = @"
                    SELECT * FROM organizations;
                    SELECT * FROM tournamentModes ORDER BY numPlayers, name;
                    SELECT * FROM seasons ORDER BY name;
                    SELECT * FROM categories ORDER BY id;
                    SELECT id, title, categoryPosition1, categoryPosition2 FROM contents 
                        WHERE idtournament = 0 AND idteam = 0 AND idCategory = @section AND status = @status ORDER BY categoryPosition1, categoryPosition2;
                    SELECT * FROM sponsors WHERE idOrganization = @idOrg;
                ";

                var multi = c.QueryMultiple(query, new { section = 1, status = (int)ContentStatus.Published, idOrg = 1 });
                var result = multi.Read<PublicOrganization>().GetSingle();

                result.Modes = multi.Read<TournamentMode>().AsList();
                result.Seasons = multi.Read<Season>().AsList();
                result.Categories = multi.Read<Category>().AsList();
                result.MenuEntries = multi.Read<BasicContent>().AsList();
                result.Sponsors = multi.Read<Sponsor>().AsList();

                if(Translation.Lang == "")
                {
                    Translation.LoadTranslation(result.DefaultLang);
                }                

                return result;
            });
        }

        [HttpGet("userexistandorganitzation/{email}")]
        public IActionResult GetUserOrganizations(string email)
        {
            try
            {
                var orgForUser = GetOrgNameForEmail(email);
                if (orgForUser == null) throw new Exception("Error.NonExistent");

                return DbOperation(c =>
                {
                    var userGlobal = UsersController.GetGlobalUserForEmail(email);
                    if (userGlobal == null) return false;

                    // var userOrg = c.Query<User>($"SELECT id FROM users WHERE email = '{email}'").FirstOrDefault();
                    var userOrg = c.Query<User>($"SELECT id FROM users WHERE id = {userGlobal.Id}").FirstOrDefault();

                    if (userOrg != null)
                    {
                        var simpleOrg = c.Query<PublicOrganization>($"SELECT name, motto FROM organizations").FirstOrDefault();
                        return new { userId = userOrg.Id, organitzation = simpleOrg };
                    }

                    return false;
                });
            }
            catch (Exception ex)
            {
                return Error(ex.Message);
            }
        }

        [HttpPut]
        public virtual IActionResult Update([FromBody] PublicOrganization value)
        {
            return DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();
                if (!ValidateEdit(value, c)) throw new Exception(CrudController<PublicOrganization>.ValidationError);

                var result = c.Update(value);
                return true;
            });
        }

        [HttpGet("secret")]
        public IActionResult GetOrgWithSecrets()
        {
            return DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                var result = c.Query<OrganizationWithSecrets>("SELECT * FROM organizations").GetSingle();

                return result;
            });
        }

        [HttpPut("secret")]
        public virtual IActionResult UpdateOrgWithSecrets([FromBody] OrganizationWithSecrets value)
        {
            return DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();
                if (!ValidateEdit(value, c)) throw new Exception(CrudController<PublicOrganization>.ValidationError);

                var result = c.Update(value);
                return true;
            });
        }

        [HttpPut("sponsordata")]
        public IActionResult SetSponsorData([FromBody] UpdateOrganizationSponsorDataRequest data)
        {
            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                c.Execute($"UPDATE organizations SET sponsordata = '{data.SectionsJson}' WHERE id = {data.IdOrganization};");
                return true;
            });
        }

        [HttpPut("appearance")]
        public IActionResult SetAppearanceData([FromBody] UpdateOrganizationAppearanceDataRequest data)
        {
            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                c.Execute($"UPDATE organizations SET appearancedata = '{data.AppearanceJsonString}' WHERE id = {data.IdOrganization};");
                return true;
            });
        }

        [HttpGet("active")]
        public bool GetOrganizationActive()
        {
            return true;
        }

        [HttpGet("globaluserscount")]
        public int GetGlobalUsersCount()
        {
            using (var c = GetGlobalDirectoryConn())
            {
               return c.QueryFirst<int>("SELECT COUNT(*) FROM users;");
            }
        }

        // 🚧 globalappdownloadscount
                
        private string GetOrgNameForEmail(string email)
        {
            using (var c = GetGlobalDirectoryConn())
            {
                var user = c.QueryFirstOrDefault<GlobalUserOrganization>("SELECT organizationName FROM userorganization WHERE email iLIKE @email", new { email = email });
                if (user == null) return null;

                return user.OrganizationName;
            }
        }

        private bool ValidateEdit(PublicOrganization val, IDbConnection c)
        {
            if (val.Name != null && val.Name.Length > 50) return false;
            if (val.Motto != null && val.Motto.Length > 80) return false;

            return true;
        }
    }

    public class UpdateOrganizationSponsorDataRequest
    {
        public long IdOrganization { get; set; }
        public string SectionsJson { get; set; }
    }

    public class UpdateOrganizationAppearanceDataRequest
    {
        public long IdOrganization { get; set; }
        public string AppearanceJsonString { get; set; }
    }
}
