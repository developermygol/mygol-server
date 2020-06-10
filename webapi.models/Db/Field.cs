using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    public class Field: BaseObject
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string ImgUrl { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }

        public override string Print()
        {
            return $"Field: id:{Id} name:'{Name}' address:{Address} location:{Location}";
        }
    }
}
