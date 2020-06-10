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
    
    public class FieldsController: CrudController<Field>
    {
        public FieldsController(IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "Fields"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, Field target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(Field value, IDbConnection c, IDbTransaction t)
        {
            // Para borrar un campo hay que comprobar que no hay ninguna jornada ni equipo asociado a él
            return true;
        }

        protected override bool ValidateEdit(Field value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }

        protected override bool ValidateNew(Field value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }
    }
}
