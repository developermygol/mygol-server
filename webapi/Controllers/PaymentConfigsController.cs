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

    public class PaymentConfigsController : CrudController<PaymentConfig>
    {
        public PaymentConfigsController(IOptions<Config> config) : base(config)
        {
        }

        [HttpGet("forany/{idTeam}/{idTournament}/{idUser:long?}")]
        public IActionResult GetApplicableEnrollmentWorkflow(long idTeam, long idTournament, long idUser = -1)
        {
            // Return the options for the team and tournament

            return DbOperation(c =>
            {
                if (!IsLoggedIn()) throw new UnauthorizedAccessException();
                return GetEnrollmentPaymentWorkflowForUser(c, null, idTeam, idTournament, idUser);
            });
        }


        [HttpGet("fororganization")]
        public IActionResult GetOrgEnrollmentWorkflow()
        {
            return DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();
                return GetEnrollmentWorkflowForQuery(c, null, OrgQuery, null);
            });
        }

        [HttpGet("fortournament/{idTournament}")]
        public IActionResult GetTournamentEnrollmentWorkflow(long idTournament)
        {
            return DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();
                return GetEnrollmentWorkflowForQuery(c, null, TournamentQuery, new { idTournament });
            });
        }

        [HttpGet("forteam/{idTeam}/{idTournament}")]
        public IActionResult GetTeamEnrollmentWorkflow(long idTeam, long idTournament)
        {
            return DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();
                return GetEnrollmentWorkflowForQuery(c, null, TeamQuery, new { idTeam, idTournament });
            });
        }

        [HttpGet("foruser/{idTeam}/{idTournament}/{idUser}")]
        public IActionResult GetUserEnrollmentWorkflow(long idTeam, long idTournament, long idUser)
        {
            return DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();
                return GetEnrollmentWorkflowForQuery(c, null, UserQuery, new { idTeam, idTournament, idUser });
            });
        }


        private static PaymentConfig GetEnrollmentWorkflowForQuery(IDbConnection c, IDbTransaction t, string query, object args)
        {
            var result = c.QueryFirstOrDefault<PaymentConfig>(query, args, t);

            return result;
        }

        private const string OrgQuery = "SELECT * FROM paymentConfigs WHERE idOrganization = @idOrganization AND idTeam = -1 AND idTournament = -1;";
        private const string TournamentQuery = "SELECT * FROM paymentConfigs WHERE idTournament = @idTournament AND idTeam = -1 AND idOrganization = -1;";
        private const string TeamQuery = "SELECT * FROM paymentConfigs WHERE idTeam = @idTeam AND idTournament = @idTournament AND idOrganization = -1 AND idUser = -1;";
        private const string UserQuery = "SELECT * FROM paymentConfigs WHERE idTeam = @idTeam AND idTournament = @idTournament AND idOrganization = -1 AND idUser = @idUser;";

        public static PaymentConfig GetEnrollmentPaymentWorkflowForUser(IDbConnection c, IDbTransaction t, long idTeam, long idTournament, long idUser)
        {
            var query = UserQuery + TeamQuery + TournamentQuery + OrgQuery;

            var qr = c.QueryMultiple(query, new { idTeam, idTournament, idOrganization = 1, idUser });
            var userConfig = qr.ReadFirstOrDefault<PaymentConfig>();
            var teamConfig = qr.ReadFirstOrDefault<PaymentConfig>();
            var tournamentConfig = qr.ReadFirstOrDefault<PaymentConfig>();
            var orgConfig = qr.ReadFirstOrDefault<PaymentConfig>();

            if (userConfig != null) return userConfig;
            if (teamConfig != null) return teamConfig;
            if (tournamentConfig != null) return tournamentConfig;

            return orgConfig;
        }

        protected override object AfterDelete(PaymentConfig value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "paymentconfigs"
            };
        }

        protected override bool IsAuthorized(RequestType reqType, PaymentConfig target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.OrgAdmin, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(PaymentConfig value, IDbConnection c, IDbTransaction t)
        {
            return ValidatePaymentConfig(value.EnrollmentWorkflow);
        }

        protected override bool ValidateEdit(PaymentConfig value, IDbConnection c, IDbTransaction t)
        {
            return ValidatePaymentConfig(value.EnrollmentWorkflow);
        }

        protected override bool ValidateNew(PaymentConfig value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }


        // __ Impl ____________________________________________________________


        private bool ValidatePaymentConfig(string config)
        {
            // Desearlize json, ensure steps and options are in place with prices. Throw if not. 
            return true;
        }
    }
}
