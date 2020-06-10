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
    
    public class GroupsController: CrudController<StageGroup>
    {
        public GroupsController(IOptions<Config> config) : base(config)
        {
        }

        [HttpPost("deletecalendar/{idGroup}")]
        public IActionResult DeleteGroupCalendar(long idGroup)
        {
            return DbTransaction((c, t) =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                CalendarStorer.DeleteCalendar(c, t, idGroup);

                return true;
            });
        }


        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "stagegroups"
            };
        }

        protected override object AfterEdit(StageGroup value, IDbConnection conn, IDbTransaction t)
        {
            // Delete any teams beyond the group size
            conn.Execute("DELETE FROM teamgroups WHERE idGroup = @idGroup AND sequenceOrder >= @numTeams", new { idGroup = value.Id, numTeams = value.NumTeams });

            return true;
        }

        protected override object AfterDelete(StageGroup value, IDbConnection conn, IDbTransaction t)
        {
            // Also delete any teams in this group
            conn.Execute("DELETE FROM teamgroups WHERE idGroup = @idGroup", new { idGroup = value.Id });

            return true;
        }


        protected override bool IsAuthorized(RequestType reqType, StageGroup target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(StageGroup value, IDbConnection c, IDbTransaction t)
        {
            // Can't delete if there are matches
            var numMatches = c.ExecuteScalar<int>("SELECT COUNT(id) FROM matches WHERE idGroup = @id", new { id = value.Id }, t);
            if (numMatches > 0) throw new Exception("Error.GroupHasMatches");

            // Can't delete if there are teams
            var numTeams = c.ExecuteScalar<int>("SELECT COUNT(id) FROM teamgroups WHERE idGroup = @id", new { id = value.Id }, t);
            if (numTeams > 0) throw new Exception("Error.GroupHasTeams");

            return true;
        }

        protected override bool ValidateEdit(StageGroup value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length >= 1;
        }

        protected override bool ValidateNew(StageGroup value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length >= 1;
        }
    }
}
