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
    
    public class DaysController: CrudController<PlayDay>
    {
        public DaysController(IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "playdays"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, PlayDay target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(PlayDay value, IDbConnection c, IDbTransaction t)
        {
            // Make sure there are no matches associated to this day
            var numMatches = c.ExecuteScalar<int>("SELECT count(id) FROM matches WHERE idday = @idDay", new { idDay = value.Id }, t);
            if (numMatches > 0) throw new Exception("Error.PlayDayHasMatches");

            return true;
        }

        protected override bool ValidateEdit(PlayDay value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateNew(PlayDay value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }
    }
}
