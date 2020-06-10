using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    public class BaseObject
    {
        [Key] public long Id { get; set; }

        public virtual string Print()
        {
            return $"Id: {Id}";
        }
    }
}
