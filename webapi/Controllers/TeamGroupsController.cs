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

namespace webapi.Controllers
{
    
    public class TeamGroupsController: CrudController<TeamGroup>
    {
        public TeamGroupsController(IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "groupteams"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, TeamGroup target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.None, add: UserLevel.OrgAdmin, edit: UserLevel.None, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(TeamGroup value, IDbConnection c, IDbTransaction t)
        {
            // Now allowed if there are matches in the group already
            var numMatches = c.ExecuteScalar<int>("SELECT COUNT(id) FROM matches WHERE idGroup = @idGroup AND (idHomeTeam = @idTeam OR idVisitorTeam = @idTeam)", new { idGroup = value.IdGroup, idTeam = value.IdTeam }, t);
            if (numMatches > 0) throw new Exception("Error.TeamGroupHasMatches");

            return true;
        }

        protected override bool ValidateEdit(TeamGroup value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateNew(TeamGroup value, IDbConnection c, IDbTransaction t)
        {
            // Ensure the team isn't already on the same stage.

            return true;
        }
    }
}
