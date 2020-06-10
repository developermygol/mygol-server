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
    
    public class TournamentModesController: CrudController<TournamentMode>
    {
        public TournamentModesController(IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "tournamentModes"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, TournamentMode target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(TournamentMode value, IDbConnection c, IDbTransaction t)
        {
            // Check if there is any tournament.
            var numTournaments = c.ExecuteScalar<int>($"SELECT count(id) FROM tournaments WHERE idTournamentMode = {value.Id}");

            if (numTournaments > 0) throw new Exception("Error.TournamentModeNotEmpty");

            return true;
        }

        protected override bool ValidateEdit(TournamentMode value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }

        protected override bool ValidateNew(TournamentMode value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }
    }
}
