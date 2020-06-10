using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Result
{
    public class InviteInput
    {
        public long IdPlayer { get; set; }
        public long IdTeam { get; set; }
        public string InviteText { get; set; }
    }
}
