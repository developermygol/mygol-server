using System;
using System.Collections.Generic;
using System.Text;

namespace webapi.Models.Db
{
    public class InsuranceReport
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string IdCardNumber { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public DateTime BirthDate { get; set; }
        public long IdTeam { get; set; }
        public string TeamName { get; set; }
        public string Data1 { get; set; }
    }
}
