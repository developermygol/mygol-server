using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi.Controllers
{
    
    public class SponsorsController: CrudController<Sponsor>
    {
        public SponsorsController(IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "Sponsors"
            };
        }

        [HttpGet("fororganization/{idOrganization}")]
        public IActionResult GetOrgSponsors(long idOrganization)
        {
            return DbOperation(c =>
            {
                if (idOrganization == 0) throw new Exception("Error.BadOrgId");

                return c.Query<Sponsor>("SELECT * FROM sponsors WHERE idOrganization = @idOrg", new { idOrg = idOrganization });
            });
        }

        [HttpGet("forteam/{idteam}")]
        public IActionResult GetTeamSponsors(long idTeam)
        {
            return DbOperation(c =>
            {
                if (idTeam == 0) throw new Exception("Error.BadTeamId");
                return c.Query<Sponsor>("SELECT * FROM sponsors WHERE idTeam = @id", new { id = idTeam });
            });
        }

        [HttpGet("fortournament/{idTournament}")]
        public IActionResult GetTournamentSponsors(long idTournament)
        {
            return DbOperation(c =>
            {
                if (idTournament == 0) throw new Exception("Error.BadTournamentId");
                return c.Query<Sponsor>("SELECT * FROM sponsors WHERE idTournament = @id", new { id = idTournament });
            });
        }


        protected override bool IsAuthorized(RequestType reqType, Sponsor value, IDbConnection c)
        {
            if (!IsWriteRequest()) return true;

            if (IsOrganizationAdmin()) return true;

            if (IsTeamAdmin(value.IdTeam, c))
            {
                value.IdOrganization = -1;
                value.IdTournament = -1;
                return true;
            }

            return false;
        }

        protected override bool ValidateDelete(Sponsor value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateEdit(Sponsor value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length >= 3;
        }

        protected override bool ValidateNew(Sponsor value, IDbConnection c, IDbTransaction t)
        {
            var result = value.Name != null && value.Name.Length >= 3;

            return result;
        }
    }
}
