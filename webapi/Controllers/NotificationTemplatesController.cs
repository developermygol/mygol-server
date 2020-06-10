using Dapper;
using Dapper.Contrib.Extensions;
using Ganss.XSS;
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
    
    public class NotificationTemplatesController: CrudController<NotificationTemplate>
    {
        public NotificationTemplatesController(IOptions<Config> config) : base(config)
        {
        }

        protected override object AfterEdit(NotificationTemplate value, IDbConnection conn, IDbTransaction t)
        {
            TemplateEngine.InvalidateCache();

            return base.AfterEdit(value, conn, t);
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "notificationtemplates"
            };
        }


        protected override bool IsAuthorized(RequestType reqType, NotificationTemplate target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.OrgAdmin, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(NotificationTemplate value, IDbConnection c, IDbTransaction t)
        {
            // Check if there is any tournament.
            var numTournaments = c.ExecuteScalar<int>($"SELECT count(id) FROM tournaments WHERE idseason = {value.Id}");

            if (numTournaments > 0) throw new Exception("Error.SeasonNotEmpty");

            return true;
        }

        protected override bool ValidateEdit(NotificationTemplate value, IDbConnection c, IDbTransaction t)
        {
            value.ContentTemplate = mSanitizer.Sanitize(value.ContentTemplate);

            return true;
        }

        protected override bool ValidateNew(NotificationTemplate value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        private HtmlSanitizer mSanitizer = new HtmlSanitizer();
    }
}
