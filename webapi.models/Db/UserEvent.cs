using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    public class UserEvent: BaseObject
    {
        public long IdUser { get; set; }
        public int Type { get; set; }
        public string Description { get; set; }
        public DateTime TimeStamp { get; set; }
        public long IdSecureUpload { get; set; }
        public long IdUpload { get; set; }
        public long IdCreator { get; set; }

        public string Data1 { get; set; }
        public string Data2 { get; set; }
        public string Data3 { get; set; }

        [Write(false)] public Upload SecureUpload { get; set; }
    }

    public enum UserEventType
    {
        PlayerCreated = 1,
        PlayerImported = 2,
        PlayerUploadedPicture = 3,
        PlayerUploadedSecureDoc = 5,

        PlayerInvitedToTeam = 10,
        PlayerAcceptedInvitation = 11,
        PlayerRejectedInviation = 12,

        PlayerPaymentSuccess = 15,
        PlayerPaymentError = 16,

        PlayerRemovedFromTeam = 20,
        PlayerInjured = 30,
        PlayerSanctioned = 31
    }
}
