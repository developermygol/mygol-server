using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace webapi.Models.Db
{
    [Table("tutorials")]
    public class Tutorial : BaseObject
    {
        public string Language { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Data1 { get; set; }
        public string Data2 { get; set; }

        public long Status { get; set; }
        public long Type { get; set; }
        public long SequenceOrder { get; set; }
    }

    public enum TutorialStatus
    {
        Published          = 1
    }

    public enum TutorialType
    {
        Normal              = 1
    }
}
