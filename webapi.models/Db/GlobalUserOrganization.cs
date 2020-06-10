using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    [Table("userorganization")]
    public class GlobalUserOrganization
    {
        public long IdUser { get; set; }
        public string Email { get; set; }
        public string OrganizationName { get; set; }
    }
}
