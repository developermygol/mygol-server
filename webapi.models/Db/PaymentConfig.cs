using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    public class PaymentConfig: BaseObject
    {
        public long IdTeam { get; set; }
        public long IdTournament { get; set; }
        public long IdOrganization { get; set; }
        public long IdUser { get; set; }
        public string EnrollmentWorkflow { get; set; }
        public string GatewayConfig { get; set; }
    }
}
