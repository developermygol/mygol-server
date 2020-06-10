using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi.Controllers
{
    
    public class StagesController: CrudController<TournamentStage>
    {
        public StagesController(IOptions<Config> config) : base(config)
        {
        }

        [HttpPost("wipe")]
        public IActionResult WipeStage([FromBody] TournamentStage value)
        {
            // Stage mass destruction
            return DbTransaction((c, t) =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                c.Execute(@"
                    DELETE FROM playerdayresults WHERE idStage = @id;
                    DELETE FROM teamdayresults  WHERE idStage = @id;
                    DELETE FROM matches WHERE idStage = @id;
                    DELETE FROM playdays WHERE idStage = @id;
                    DELETE FROM teamGroups WHERE idStage = @id;
                    DELETE FROM stageGroups WHERE idStage = @id;
                    DELETE FROM tournamentStages WHERE id = @id;
                ", new { id = value.Id }, t);

                return true;
            });
        }

        protected override object AfterDelete(TournamentStage value, IDbConnection conn, IDbTransaction t)
        {
            // Only playdays should be left by now. 
            conn.Execute(@"
                DELETE FROM playdays WHERE idStage = @id;
            ", new { id = value.Id }, t);

            return true;
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "tournamentstages"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, TournamentStage target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(TournamentStage value, IDbConnection c, IDbTransaction t)
        {
            // Check that it doesn't have any groups inside. 
            var numGroups = c.ExecuteScalar<int>("SELECT COUNT(id) FROM stageGroups WHERE idStage = @id", new { id = value.Id }, t);
            if (numGroups > 0) throw new Exception("Error.StageNotEmpty");

            return true;
        }

        protected override bool ValidateEdit(TournamentStage value, IDbConnection c, IDbTransaction t)
        {
            ValidateClassificationCriteria(value.ClassificationCriteria);

            return value.Name != null && value.Name.Length > 3;
        }

        protected override bool ValidateNew(TournamentStage value, IDbConnection c, IDbTransaction t)
        {
            ValidateClassificationCriteria(value.ClassificationCriteria);

            return value.Name != null && value.Name.Length > 3;
        }


        private void ValidateClassificationCriteria(string criteria)
        {
            if (string.IsNullOrWhiteSpace(criteria)) return;

            try
            {
                var cc = JsonConvert.DeserializeObject<int[]>(criteria);

                foreach (var ai in cc)
                {
                    if (ai < -1 || ai >= ClassificationSorter.SortingAlgorithms.Length) throw new Exception("Error.InvalidClassificationCriteriaIndex");
                }
            }
            catch (JsonException)
            {
                throw new Exception("Error.InvalidClassificationCriteria");
            }
        }

    }
}
