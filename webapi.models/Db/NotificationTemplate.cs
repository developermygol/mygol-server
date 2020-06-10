using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    public class NotificationTemplate: BaseObject
    {
        public string Lang { get; set; }
        public string Key { get; set; }
        public string Title { get; set; }
        public string ContentTemplate { get; set; }
    }
}
