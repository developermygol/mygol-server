using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    [DebuggerDisplay("{Name} ({Id})")]
    public class UserDevice: BaseObject
    {
        public string Name { get; set; }
        public long IdUser { get; set; }
        public string DeviceToken { get; set; }
    }

}
