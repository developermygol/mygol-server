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
    
    public class AutoSanctionsController: DbController
    {
        public AutoSanctionsController(IOptions<Config> config) : base(config)
        {

        }

        [HttpGet("fortournament/{idTournament:long}")]
        public IActionResult GetForTournament(long idTournament)
        {
            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                var result = c.QuerySingleOrDefault<AutoSanctionConfig>("SELECT * FROM autosanctionconfigs WHERE idTournament = @idTournament", new { idTournament });

                return result;
            });
        }

        [HttpPost("insertorupdate")]
        public IActionResult InsertOrUpdate([FromBody] AutoSanctionConfig asc)
        {
            return DbTransaction((c, t) =>
            {
                if (asc == null) throw new NoDataException();

                if (!AutoSanctionDispatcher.IsValidConfig(asc.Config)) throw new Exception("Error.InvalidConfig");

                CheckAuthLevel(UserLevel.OrgAdmin);

                var dbSanctionConfig = c.Query<AutoSanctionConfig>("SELECT * FROM autosanctionconfigs WHERE idtournament = @idTournament", new { idTournament = asc.IdTournament }, t).SingleOrDefault();

                if (dbSanctionConfig == null)
                {
                    var newId = c.Insert(asc, t);
                    return newId;
                }
                else
                {
                    dbSanctionConfig.Config = asc.Config;
                    var result = c.Update(dbSanctionConfig, t);
                    return asc.Id;
                }
            });
        }
    }
}
