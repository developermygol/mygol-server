using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace webapi.Models.Db
{
    [Table("notices")]
    public class Notice : BaseObject
    {
        public string Name { get; set; }
        public string Text{ get; set; }
        public string ConfirmationText1 { get; set; }
        public string ConfirmationText2 { get; set; }
        public string ConfirmationText3 { get; set; }
        public string AcceptText { get; set; }
        public int HoursInAdvance { get; set; }
        public bool Enabled { get; set; }
        public long IdTournament { get; set; }
    }
}
