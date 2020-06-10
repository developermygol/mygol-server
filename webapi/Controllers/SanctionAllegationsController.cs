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
    
    public class SanctionAllegationsController: CrudController<SanctionAllegation>
    {
        public SanctionAllegationsController(IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "sanctionallegations"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, SanctionAllegation target, IDbConnection c)
        {
            if (target == null) return false;

            var sanction = c.Get<Sanction>(target.IdSanction);
            if (sanction == null) return false;

            var isTeamAdminForSanction = IsTeamAdmin(sanction.IdTeam, c);
            var isSanctionEditable = sanction.Status == (int)SanctionStatus.AutomaticallyGenerated || sanction.Status == (int)SanctionStatus.InProgress;

            // Team admin can only crete allegations if it is a sanction of his team and the sanction is still in an editable state.
            if (reqType == RequestType.Post && isTeamAdminForSanction && isSanctionEditable) return true;

            // Team admin can only edit his own allegations. 
            if (reqType == RequestType.Put && isTeamAdminForSanction && isSanctionEditable && target.IdUser == GetUserId()) return true;

            // Otherwise, only organizer can edit, add or delete. Everyone can list.
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(SanctionAllegation value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateEdit(SanctionAllegation value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateNew(SanctionAllegation value, IDbConnection c, IDbTransaction t)
        {
            value.IdUser = GetUserId();
            value.Date = DateTime.Now;
            value.Status = (int)SanctionAllegationStatus.Created;

            if (!IsOrganizationAdmin())
            {
                value.Visible = false;
            }

            return true;
        }

        protected override object AfterNew(SanctionAllegation value, IDbConnection c, IDbTransaction t)
        {
            FillUser(c, t, value);
            return value;
        }


        protected override object AfterEdit(SanctionAllegation value, IDbConnection conn, IDbTransaction t)
        {
            FillUser(conn, t, value);
            return value;
        }

        protected override object AfterDelete(SanctionAllegation value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }


        private static void FillUser(IDbConnection c, IDbTransaction t, SanctionAllegation value)
        {
            // first or default beacuse it can be a platform admin not present in the DB. 
            var user = c.Query<User>("SELECT id, name, avatarImgUrl, level FROM users WHERE id = @id", new { id = value.IdUser }, t).FirstOrDefault();
            value.User = user;
        }

    }
}
