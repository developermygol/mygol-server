using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using webapi.Models.Db;
using Microsoft.Extensions.Options;
using Dapper;
using Dapper.Contrib.Extensions;
using contracts;
using Utils;
using Microsoft.AspNetCore.Http;
using Serilog;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace webapi.Controllers
{
    public class NoticesController : CrudController<Notice>
    {
        public NoticesController(IOptions<PostgresqlConfig> dbOptions, IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "notices"
            };
        }

        [HttpGet("fortournament/{tournamentId}")]
        public override IActionResult Get(long tournamentId)
        {
            return DbOperation(c =>
            {
                string query = $"SELECT * FROM notices WHERE idtournament = {tournamentId};";

                var notices = c.Query<Notice>(query);

                return notices;
            });
        }

        [HttpPost]
        public override IActionResult Post([FromBody] Notice newNotice)
        {
            return DbTransaction((c, t) =>
            {
                if (newNotice == null) throw new NoDataException();

                Audit.Information(this, "{0}: {1}.Create", GetUserId(), typeof(Notice).Name);

                if (!IsAuthorized(RequestType.Post, newNotice, c)) throw new UnauthorizedAccessException();
                if (!ValidateNew(newNotice, c, t)) throw new Exception(ValidationError);

                var r = c?.Insert(newNotice);
                if (r == null) throw new Exception(AddError);

                long newId = r.Value;
                newNotice.Id = newId;

                // 🚧 Notify logic construction                

                return AfterNew(newNotice, c, t);
            });
        }

        [HttpPut]
        public override IActionResult Update([FromBody] Notice editNotice)
        {
            return DbTransaction((c, t) =>
            {
                if (editNotice == null) throw new NoDataException();

                Audit.Information(this, "{0}: {1}.Update: {2}", GetUserId(), typeof(Notice).Name, editNotice.Id);

                if (!IsAuthorized(RequestType.Put, editNotice, c)) throw new UnauthorizedAccessException();
                if (!ValidateEdit(editNotice, c, t)) throw new Exception(ValidationError);

                var result = c.Update(editNotice, t);

                // 🚧 Notify logic construction   

                return AfterEdit(editNotice, c, t);
            });
        }

        [HttpPost("delete")]
        public override IActionResult Delete([FromBody] Notice removeNotice)
        {
            return DbTransaction((c, t) =>
            {
                if (removeNotice == null) throw new NoDataException();

                Audit.Information(this, "{0}: {1}.Delete: {2}", GetUserId(), typeof(Notice).Name, removeNotice.Id);

                if (!IsAuthorized(RequestType.Delete, removeNotice, c)) throw new UnauthorizedAccessException();
                if (!ValidateDelete(removeNotice, c, t)) throw new Exception(ValidationError);

                // 🚧 Remove respective notices events

                var result = c.Delete(removeNotice, t);
                return AfterDelete(removeNotice, c, t);
            });
        }
        
        protected override bool IsAuthorized(RequestType reqType, Notice target, IDbConnection conn)
        {
            if (reqType == RequestType.GetSingle) return false; 

            // if (reqType == RequestType.GetAll) return true;

            // Write actions: only orgadmin
            return (IsOrganizationAdmin());
        }

        protected override bool ValidateNew(Notice value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateEdit(Notice value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateDelete(Notice value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }
    }
}
