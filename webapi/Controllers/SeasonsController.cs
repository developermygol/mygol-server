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
    
    public class SeasonsController: CrudController<Season>
    {
        public SeasonsController(IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "seasons"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, Season target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(Season value, IDbConnection c, IDbTransaction t)
        {
            // Check if there is any tournament.
            var numTournaments = c.ExecuteScalar<int>($"SELECT count(id) FROM tournaments WHERE idseason = {value.Id}");

            if (numTournaments > 0) throw new Exception("Error.SeasonNotEmpty");

            return true;
        }

        protected override bool ValidateEdit(Season value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }

        protected override bool ValidateNew(Season value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }
    }
}
