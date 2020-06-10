using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace webapi
{
    public class NotificationManager
    {
        public NotificationManager(Config config)
        {
            mConfig = config;
        }
        
        public void NotifyEmail(HttpRequest req, IDbConnection c, IDbTransaction t, string templateKey, BaseTemplateData notificationData)
        {
            var n = CreateNotification(c, t, templateKey, notificationData);

            // Queue through the email provider
            var result = mEmailProvider.SendEmail(req, n.RecipientData.Email, n.Text2, null, n.Text);

            if (!result) throw new Exception("Error.SendingEmailFailed");
        }

        public void NotifyPush(HttpRequest req, IDbConnection c, IDbTransaction t, string templateKey, BaseTemplateData notificationData)
        {
            var n = CreateNotification(c, t, templateKey, notificationData);

            // Queue through the push provider
        }

        public void SendEmail(HttpRequest req, string emailAddress, string subject, string textContent, string htmlContent)
        {
            var result = mEmailProvider.SendEmail(req, emailAddress, subject, textContent, htmlContent);

            if (!result) throw new Exception("Error.SendingEmailFailed");
        }

        public void NotifyAdminsGenericEmail(HttpRequest req, IDbConnection c, IDbTransaction t, long fromUserId, string subject, string textContent, string htmlContent)
        {
            var admins = c.Query<User>("SELECT id, name, email FROM users WHERE level = 4", t);
            foreach (var admin in admins)
            {
                if (admin.Email == null || admin.Email == "") continue;

                try
                {
                    var notification = new Notification
                    {
                        Text = textContent, 
                        Text2 = subject, 
                    };

                    SetupNotification(notification, fromUserId, admin.Id);
                    c.Insert(notification, t);

                    SendEmail(req, admin.Email, subject, textContent, htmlContent);
                }
                catch
                {

                }                
            }
        }


        // __ Impl ____________________________________________________________


        private Notification CreateNotification(IDbConnection c, IDbTransaction t, string templateKey, BaseTemplateData notificationData)
        {
            var notification = TemplateEngine.GetNotificationFromDbTemplate(c, t, mConfig.DefaultLocale, templateKey, notificationData);

            SetupNotification(notification, notificationData);

            c.Insert(notification, t);

            return notification;
        }

        private void SetupNotification(IDbConnection c, IDbTransaction t, Notification notification, long idCreator, long idRcptUser)
        {
            var query = @"
                SELECT id, name, avatarImgUrl FROM users WHERE id = @idFrom;
                SELECT id, name, avatarImgUrl FROM users WHERE id = @idTo;
            ";
            var mr = c.QueryMultiple(query, new { idFrom = idCreator, idTo = idRcptUser }, t);

            notification.CreatorData = mr.ReadFirst<User>();
            notification.RecipientData = mr.ReadFirst<User>();
        }


        private void SetupNotification(Notification notification, BaseTemplateData data)
        {
            SetupNotification(notification, data.From, data.To);
        }

        private void SetupNotification(Notification notification, User from, User to)
        {
            SetupNotification(notification, from.Id, to.Id);
            notification.CreatorData = from;
            notification.RecipientData = to;
        }

        private void SetupNotification(Notification notification, long fromIdUser, long toIdUser)
        {
            notification.Status = (int)NotificationStatus.Unread;
            notification.TimeStamp = DateTime.Now;
            notification.IdCreator = fromIdUser;
            notification.IdRcptUser = toIdUser;
        }

        private Config mConfig;
        private MailgunEmailProvider mEmailProvider = new MailgunEmailProvider();
    }


    public class TemplateKeys
    {
        public const string EmailPlayerInviteHtml = "email.player.invite.html";
        public const string EmailPlayerInviteText = "email.player.invite.txt";

        public const string EmailPlayerUnlinkHtml = "email.player.unlink.html";
        public const string EmailPlayerUnlinkTxt = "email.player.unlink.txt";

        public const string EmailRefereeInviteHtml = "email.referee.invite.html";

        public const string PushRefereeLinkedToMatch = "push.referee.match.link.txt";
        public const string PushRefereeUnlinkedFromMatch = "push.referee.match.unlink.txt";

        public const string PlayerResetPassword = "email.player.forgotpassword.html";
    }
}
