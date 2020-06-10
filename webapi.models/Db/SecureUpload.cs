using System;
using System.Collections.Generic;

namespace webapi.Models.Db
{
    public class SecureUpload: BaseObject
    {
        public long IdUser { get; set; }
        public int Type { get; set; }
        public string Description { get; set; }
        public DateTime TimeStamp { get; set; }
        public long IdSecuredUpload { get; set; }
        public long IdUpload { get; set; }
    }

    // SecureUpload type is used to validate access (allow / disallow the upload)
    // and display icons in the client.
    public enum SecureUploadType
    {
        PlayerData      = 100,
        PlayerIdCard    = 101,
        PlayerInsurance = 102,
        PlayerPicture   = 103,
        TeamData        = 200,
        OrgData         = 500
    }
}
