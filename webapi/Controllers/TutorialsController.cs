using Microsoft.AspNetCore.Mvc;
using webapi.Models.Db;
using Microsoft.Extensions.Options;
using Dapper;
using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;

namespace webapi.Controllers
{
    public class TutorialsController : CrudController<Tutorial>
    {
        public TutorialsController(IOptions<PostgresqlConfig> dbOptions, IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "tutorials"
            };
        }

        [HttpPost]
        public override IActionResult Post([FromBody] Tutorial newTutorial)
        {
            return DbTransaction((c, t) =>
            {
                if (newTutorial == null) throw new NoDataException();

                Audit.Information(this, "{0}: {1}.Create", GetUserId(), typeof(Tutorial).Name);

                // if (!IsAuthorized(RequestType.Post, newTutorial, c)) throw new UnauthorizedAccessException();
                if (!IsGlobalAdmin()) throw new UnauthorizedAccessException();
                if (!ValidateNew(newTutorial, c, t)) throw new Exception(ValidationError);

                var r = c?.Insert(newTutorial);
                if (r == null) throw new Exception(AddError);

                long newId = r.Value;
                newTutorial.Id = newId;

                return AfterNew(newTutorial, c, t);
            });
        }

        [HttpPut]
        public override IActionResult Update([FromBody] Tutorial editTutorial)
        {
            return DbTransaction((c, t) =>
            {
                if (editTutorial == null) throw new NoDataException();

                Audit.Information(this, "{0}: {1}.Update: {2}", GetUserId(), typeof(Tutorial).Name, editTutorial.Id);

                // if (!IsAuthorized(RequestType.Put, editTutorial, c)) throw new UnauthorizedAccessException();
                if (!IsGlobalAdmin()) throw new UnauthorizedAccessException();
                if (!ValidateEdit(editTutorial, c, t)) throw new Exception(ValidationError);

                var result = c.Update(editTutorial, t);

                return AfterEdit(editTutorial, c, t);
            });
        }

        [HttpPost("delete")]
        public override IActionResult Delete([FromBody] Tutorial removeTutorial)
        {
            return DbTransaction((c, t) =>
            {
                if (removeTutorial == null) throw new NoDataException();

                Audit.Information(this, "{0}: {1}.Delete: {2}", GetUserId(), typeof(Tutorial).Name, removeTutorial.Id);

                //if (!IsAuthorized(RequestType.Delete, removeTutorial, c)) throw new UnauthorizedAccessException();
                if (!IsGlobalAdmin()) throw new UnauthorizedAccessException();

                if (!ValidateDelete(removeTutorial, c, t)) throw new Exception(ValidationError);

                var result = c.Delete(removeTutorial, t);
                return AfterDelete(removeTutorial, c, t);
            });
        }

        [HttpPut("saveorder")]
        public IActionResult UpdateTutorialsSequenceOrder([FromBody] UpdateTutorialsSequenceOrder data)
        {
            if (!IsGlobalAdmin()) throw new UnauthorizedAccessException();

            return DbOperation(c =>
            {
                CheckAuthLevel(UserLevel.OrgAdmin);

                IEnumerable<TutorialSequence> sequences = data.TutorialsSequence;

                foreach (var sequence in sequences)
                {
                    c.Execute($"UPDATE tutorials SET sequenceorder = '{sequence.SequenceOrder}' WHERE id = {sequence.Id};");
                }

                return c.Query<Tutorial>("SELECT * FROM tutorials"); ;
            });
        }

        protected override bool IsAuthorized(RequestType reqType, Tutorial target, IDbConnection conn)
        {
            if (reqType == RequestType.GetSingle) return false;

            return IsOrganizationAdmin();
        }

        protected override bool ValidateNew(Tutorial value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateEdit(Tutorial value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateDelete(Tutorial value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }
    }

    public class UpdateTutorialsSequenceOrder
    {
        public IEnumerable<TutorialSequence> TutorialsSequence { get; set; }
    }

    public class TutorialSequence
    {
        public long Id { get; set; }
        public long SequenceOrder { get; set; }
    }
}
