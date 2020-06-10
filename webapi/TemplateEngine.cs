using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.Extensions.Options;
using HandlebarsDotNet;

namespace webapi
{
    public class TemplateEngine
    {
        public static string Process(string templateText, object data)
        {
            if (templateText == null) return "";

            if (!mCachedTemplates.TryGetValue(templateText, out Func<object, string> template)) 
            {
                template = Handlebars.Compile(templateText);
                mCachedTemplates[templateText] = template;
            }

            return template(data);
        }

        public static string ProcessDbTemplate(IDbConnection c, IDbTransaction t, string lang, string templateKey, object data)
        {
            // Select template from the DB, then run it through Process()
            var templateText = c.ExecuteScalar<string>("SELECT templateContent FROM notificationTemplates WHERE land = @lang AND key = @key",
                new { lang = lang, key = templateKey }, t);

            if (templateText == null) throw new Exception("Error.NotFound");

            return Process(templateText, data);
        }

        public static Notification GetNotificationFromDbTemplate(IDbConnection c, IDbTransaction t, string lang, string templateKey, BaseTemplateData data)
        {
            if (data == null || data.To == null || data.From == null) throw new ArgumentNullException();

            var template = c.QueryFirstOrDefault<NotificationTemplate>(
                "SELECT * FROM notificationTemplates WHERE lang = @lang AND key = @key",
                new { lang = lang, key = templateKey }, t);
            if (template == null) throw new Exception("Error.NotFound.Template");

            var result = new Notification
            {
                Text = Process(template.ContentTemplate, data),
                Text2 = Process(template.Title, data),
            };

            return result;
        }

        public static void InvalidateCache()
        {
            mCachedTemplates.Clear();
        }

        private static Dictionary<string, Func<object, string>> mCachedTemplates = new Dictionary<string, Func<object, string>>();
    }
}
