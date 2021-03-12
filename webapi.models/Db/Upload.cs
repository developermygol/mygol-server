using System;
using System.Collections.Generic;

namespace webapi.Models.Db
{
    public class Upload: BaseObject
    {
        public int Type { get; set; }
        public long IdObject { get; set; }
        public string RepositoryPath { get; set; }

        public static bool IsTeamType(UploadType type)
        {
            var v = (int)type;

            // Team admin can upload for player and team
            return (v >= 100 && v <= 199) || (v >= 200 && v <= 299);
        }
    }

    // Upload type is used to validate access (allow / disallow the upload) 
    // and also update tables for the known types. Unknown types need to update 
    // data in the client and post the update.
    public enum UploadType
    {
        TeamLogo                = 100,
        TeamSponsor             = 110,
        TeamImg1                = 111, 
        TeamImg2                = 112,
        TeamImg3                = 113,

        PlayerAvatar            = 200,
        PlayerBackgroundFile    = 201,
        PlayerSignatureFile     = 202,
        PlayerIdCard1           = 203,
        PlayerIdCard2           = 204,
        PlayerIdPhoto           = 205, 
        PlayerInsuranceScan     = 206, 
        PlayerGenericSecureScan = 207,

        PlayerGalleryImage      = 210,

        TournamentLogo          = 300,

        BadgesImage             = 400,

        OrgContent              = 500,  // Images from CMS
        OrgSponsor              = 501,
        OrgLogo                 = 502, 
        OrgOther                = 510   // Any other upload. Only org admin allowed to upload this
    }
}
