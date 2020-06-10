using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dapper.Contrib.Extensions;

namespace webapi.Models.Db
{
    //[DebuggerDisplay("{Name} ({Id})")]
    [Table("autosanctionconfigs")]
    public class AutoSanctionConfig: BaseObject
    {
        public long IdTournament { get; set; }
        public string Config { get; set; }          // JSON data
    }


    // __ JSON config objects _________________________________________________

    public class AutoSanctionConfigContent
    {
        public AutoSanctionCardConfig[] Cards { get; set; }
        public AutoSanctionCycleConfig[] Cycles { get; set; }
    }


    [DebuggerDisplay("{Card1Type} {Card2Type}")]
    public class AutoSanctionCardConfig : BaseObject
    {
        public int Card1Type { get; set; }
        public int Card2Type { get; set; }

        public PenaltyConfig Penalty { get; set; }

        public int AddYellowCards { get; set; }

        public MatchEvent EventMatch1;
        public MatchEvent EventMatch2;
    }

    [DebuggerDisplay("{Id} {Name} Cards:{NumYellowCards}")]
    public class AutoSanctionCycleConfig : BaseObject
    {
        public string Name { get; set; }
        public int NumYellowCards { get; set; }

        public PenaltyConfig Penalty { get; set; }
    }

    public class PenaltyConfig
    {
        public int Type1 { get; set; }      // New card is generated
        public int Type2 { get; set; }      // Matches
        public int Type3 { get; set; }      // $$
        public int Type4 { get; set; }      // Da future...
        public int Type5 { get; set; }      // Da future...

        public string Text { get; set; }
    }

}
