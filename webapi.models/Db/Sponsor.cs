using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    public class Sponsor: BaseObject
    {
        public long IdTournament { get; set; }
        public long IdOrganization { get; set; }
        public long IdTeam { get; set; }
        public string Name { get; set; }
        public string RawCode { get; set; }
        public string Url { get; set; }
        public string ImgUrl { get; set; }
        public string AltText { get; set; }
        public int Position { get; set; }
        public int SequenceOrder { get; set; }
    }
}
