using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    [Table("organizations")]
    public class PublicOrganization: BaseObject
    {
        public string Name { get; set; }
        public string LogoImgUrl { get; set; }
        public string BackgroundImgUrl { get; set; }

        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string Address3 { get; set; }
        public string Motto { get; set; }
        public string Social1 { get; set; }
        public string Social1link { get; set; }
        public string Social2 { get; set; }
        public string Social2link { get; set; }
        public string Social3 { get; set; }
        public string Social3link { get; set; }
        public string Social4 { get; set; }
        public string Social4link { get; set; }
        public string Social5 { get; set; }
        public string Social5link { get; set; }
        public string Social6 { get; set; }
        public string Social6link { get; set; }

        public string DefaultLang { get; set; }

        public string SponsorData { get; set; }
        public string AppearanceData { get; set; }
        
        public string DpCompanyName { get; set; }
        public string DpCompanyId { get; set; }
        public string DpCompanyAddress { get; set; }
        public string DpCompanyEmail { get; set; }
        public string DpCompanyPhone { get; set; }

        public int TermsVersion { get; set; }

        public string PaymentKeyPublic { get; set; }
        public string PaymentGetawayType { get; set; }

        public string DefaultDateFormat { get; set; }

        //public object FrontPageLayoutData { get; set; }

        [Write(false)] public List<TournamentMode> Modes { get; set; }
        [Write(false)] public List<Season> Seasons { get; set; }
        [Write(false)] public List<Category> Categories { get; set; }
        [Write(false)] public List<BasicContent> MenuEntries { get; set; }
        [Write(false)] public List<Sponsor> Sponsors { get; set; }
    }

    [Table("organizations")]
    public class OrganizationWithSecrets: PublicOrganization
    {        
        public string PaymentKey { get; set; }
        public string PaymentDescription { get; set; }
        public string PaymentCurrency { get; set; }
    }


    [Table("seasons")]
    public class Season: BaseObject
    {
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }


    public class TournamentMode: BaseObject
    {
        // FUT5, FUT7, FUT11, ... ?
        public string Name { get; set; }
        public int NumPlayers { get; set; }
    }


    [Table("categories")]
    public class Category: BaseObject
    {
        public string Name { get; set; }
    }

}
