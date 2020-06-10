using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    public class TextBlob: BaseObject
    {
        public int Type { get; set; }
        public long IdObject { get; set; }
        public string Data { get; set; }
    }
}
