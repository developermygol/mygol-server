using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi.Controllers
{
    [Route("api/[controller]")]
    public abstract class CrudController<T>: DbController where T: BaseObject, new()
    {
        public const long ErrorKey = -10;

        public const string AddError = "Error.Adding";
        public const string EditError = "Error.Editing";
        public const string DeleteError = "Error.Delete";
        public const string ValidationError = "Error.Validation";


        public CrudController(IOptions<Config> config) : base(config)
        {
            mCrudConfig = GetConfig();
        }


        [HttpGet]
        public virtual IActionResult Get()
        {
            return DbOperation(c =>
            {
                if (!IsAuthorized(RequestType.GetAll, null, c)) throw new UnauthorizedAccessException();

                var query = mCrudConfig.GetAllQuery ?? $"SELECT * FROM {mCrudConfig.TableName}";

                return c.Query<T>(query);
            });
        }

        [HttpGet("{id}")]
        public virtual IActionResult Get(long id)
        {
            return DbOperation(c =>
            {
                if (!IsAuthorized(RequestType.GetSingle, new T { Id = id }, c)) throw new UnauthorizedAccessException();

                if (mCrudConfig.GetSingleQuery != null)
                    return c.Query<T>(mCrudConfig.GetSingleQuery, new { id = id }).GetSingle();
                else
                    return c.Get<T>(id);
            });
        }

        [HttpPost]
        public virtual IActionResult Post([FromBody] T newValue)
        {
            return DbTransaction((c, t) =>
            {
                if (newValue == null) throw new NoDataException();

                Audit.Information(this, "{0}: {1}.Create", GetUserId(), typeof(T).Name);

                if (!IsAuthorized(RequestType.Post, newValue, c)) throw new UnauthorizedAccessException();
                if (!ValidateNew(newValue, c, t)) throw new Exception(ValidationError);

                var r = c?.Insert(newValue);
                if (r == null) throw new Exception(AddError);

                long newId = r.Value;
                newValue.Id = newId;

                return AfterNew(newValue, c, t);
            });
        }

        [HttpPut]
        public virtual IActionResult Update([FromBody] T value)
        {
            return DbTransaction((c, t) =>
            {
                if (value == null) throw new NoDataException();

                Audit.Information(this, "{0}: {1}.Update: {2}", GetUserId(), typeof(T).Name, value.Id);

                if (!IsAuthorized(RequestType.Put, value, c)) throw new UnauthorizedAccessException();
                if (!ValidateEdit(value, c, t)) throw new Exception(ValidationError);

                var result = c.Update(value, t);
                return AfterEdit(value, c, t);
            });
        }

        [HttpPost("delete")]
        public virtual IActionResult Delete([FromBody] T value)
        {
            return DbTransaction((c, t) =>
            {
                if (value == null) throw new NoDataException();

                Audit.Information(this, "{0}: {1}.Delete: {2}", GetUserId(), typeof(T).Name, value.Id);

                if (!IsAuthorized(RequestType.Delete, value, c)) throw new UnauthorizedAccessException();
                if (!ValidateDelete(value, c, t)) throw new Exception(ValidationError);

                var result = c.Delete(value, t);
                return AfterDelete(value, c, t);
            });
        }


        protected abstract CrudConfig GetConfig();

        protected abstract bool IsAuthorized(RequestType reqType, T target, IDbConnection conn);

        protected abstract bool ValidateNew(T value, IDbConnection conn, IDbTransaction t);

        protected abstract bool ValidateEdit(T value, IDbConnection conn, IDbTransaction t);

        protected abstract bool ValidateDelete(T value, IDbConnection conn, IDbTransaction t);

        protected virtual object AfterNew(T value, IDbConnection conn, IDbTransaction t)
        {
            return value.Id;
        }

        protected virtual object AfterEdit(T value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }

        protected virtual object AfterDelete(T value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }

        protected static void ValidateLen(string target, int len, string fieldNameForError)
        {
            if (target != null && target.Length > len) throw new Exception($"Error.TooLong.{fieldNameForError}");
        }

        protected CrudConfig mCrudConfig;
    }

    public enum RequestType
    {
        GetAll, GetSingle, Post, Put, Delete
    }

    public class CrudConfig
    {
        public string TableName { get; set; }
        public string GetAllQuery { get; set; }
        public string GetSingleQuery { get; set; }
    }
}
