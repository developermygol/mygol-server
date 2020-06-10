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
    
    public class ContentCategoriesController: CrudController<ContentCategory>
    {
        public ContentCategoriesController(IOptions<PostgresqlConfig> dbOptions, IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "contentcategories"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, ContentCategory target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(ContentCategory value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateEdit(ContentCategory value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }

        protected override bool ValidateNew(ContentCategory value, IDbConnection c, IDbTransaction t)
        {
            return value.Name != null && value.Name.Length > 3;
        }
    }
}
