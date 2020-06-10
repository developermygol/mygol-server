using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;


namespace webapi.Models.Db
{
    public class Notification: BaseObject
    {
        public long IdCreator { get; set; }
        public long IdRcptUser { get; set; }
        public int Status { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Text { get; set; }
        public string Text2 { get; set; }
        public string Data1 { get; set; }
        public string Data2 { get; set; }

        public string ApiActionLabel1 { get; set; }
        public string ApiActionUrl1 { get; set; }

        public string ApiActionLabel2 { get; set; }
        public string ApiActionUrl2 { get; set; }

        public string ApiActionLabel3 { get; set; }
        public string ApiActionUrl3 { get; set; }

        public string FrontActionLabel1 { get; set; }
        public string FrontActionUrl1 { get; set; }

        public string FrontActionLabel2 { get; set; }
        public string FrontActionUrl2 { get; set; }

        public string FrontActionLabel3 { get; set; }
        public string FrontActionUrl3 { get; set; }


        [Write(false)] public User RecipientData { get; set; }
        [Write(false)] public User CreatorData { get; set; }
    }


    public enum NotificationStatus
    {
        Queued = 1,
        Sent = 2,
        Delivered = 3, 
        Unread = 4,
        Read = 5
    }

    public class BaseTemplateData
    {
        public User To { get; set; }
        public User From { get; set; }

        public BaseTemplateData FillUsers(IDbConnection c, IDbTransaction t, long fromUserId, long toUserId)
        {
            var qr = c.QueryMultiple(@"
                SELECT id, name, avatarImgUrl, email FROM users WHERE id = @from;
                SELECT id, name, avatarImgUrl, email FROM users WHERE id = @to;
            ", new { from = fromUserId, to = toUserId }, t);

            To = qr.ReadFirstOrDefault<User>();
            From = qr.ReadFirstOrDefault<User>();

            return this;
        }
    }

    public class PlayerNotificationData: BaseTemplateData
    {
        public PublicOrganization Org { get; set; }
        public Team Team { get; set; }
        public PlayerInviteImages Images { get; set; }
        public string InviteMessage { get; set; }
        public string ActivationLink { get; set; }
        public string ActivationPin { get; set; }
    }

    public class PlayerInviteImages
    {
        public string OrgLogo { get; set; }
        public string TeamLogo { get; set; }
    }

    public class MatchNotificationData: BaseTemplateData
    {
        public Match Match { get; set; }
    }

    public class RefereeNotificationData : BaseTemplateData
    {

    }

    public class GenericTextNotificationData : BaseTemplateData
    {
        public string Subject { get; set; }
        public string Message { get; set; }
    }

    
}
