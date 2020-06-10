using Dapper;
using Dapper.Contrib.Extensions;
using Ganss.XSS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi.Controllers
{
    
    public class ContentsController: CrudController<Content>
    {
        public ContentsController(IOptions<PostgresqlConfig> dbOptions, IOptions<Config> config, NotificationManager notifier) : base(config)
        {
            mNotifier = notifier;
        }


        [HttpGet("summaries/fororganization")]
        public IActionResult GetSummaries()
        {
            return DbOperation(c =>
            {
                // Auth: everyone

                var result = c.Query<Content>("SELECT id, title, subtitle, idCategory, mainImgUrl, thumbImgUrl, videoUrl FROM contents WHERE status = @status AND idTournament = 0 AND idTeam = 0 ORDER BY idCategory, timeStamp DESC", new { status = (int)ContentStatus.Published });

                return result;
            });
        }


        [HttpPost("contact")]
        public IActionResult Contact([FromBody] ContactData contactData)
        {
            return DbOperation(c =>
            {
                // find the admin users, notify them of the contact
                var users = c.Query<User>("SELECT name, email FROM users WHERE level = 4");

                foreach (var user in users)
                {
                    mNotifier.SendEmail(Request, user.Email, Localization.Get("Formulario de contacto", null), GetTextForContactForm(contactData), null);
                }

                return true;
            });
        }


        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                // Non-deleted posts
                GetAllQuery = "SELECT * FROM contents WHERE status < 10 ORDER BY timestamp DESC",
                TableName = "contents"
            };
        }


        public override IActionResult Delete([FromBody] Content value)
        {
            return DbTransaction((c, t) =>
            {
                if (value == null) throw new ArgumentNullException();

                if (!IsAuthorized(RequestType.Delete, value, c)) throw new UnauthorizedAccessException();
                if (!ValidateDelete(value, c, t)) throw new Exception(ValidationError);

                c.Execute("UPDATE contents SET status = 10 WHERE id = @id", new { id = value.Id });

                return true;
            });
        }


        protected override object AfterNew(Content value, IDbConnection c, IDbTransaction t)
        {
            // Update the idUpload of the main article image with this object if image was uploaded on item creation
            if (value.MainImgUploadId > 0)
            {
                var upload = c.Get<Upload>(value.MainImgUploadId, t);
                if (upload != null)
                {
                    upload.IdObject = value.Id;
                    c.Update(upload, t);
                }
            }

            // Return the full object, not just the id of the inserted object,
            // so frontend can display the timestamp generated in the backend.
            // remove the content to reduce network traffic.
            value.RawContent = null;
            return value;
        }

        protected override object AfterEdit(Content value, IDbConnection conn, IDbTransaction t)
        {
            // Return the full object, not just the id of the inserted object,
            // so frontend can display the timestamp generated in the backend.
            // remove the content to reduce network traffic.
            value.RawContent = null;
            return value;
        }

        protected override bool IsAuthorized(RequestType reqType, Content target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(Content value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateEdit(Content value, IDbConnection c, IDbTransaction t)
        {
            ValidateSizes(value);

            value.IdCreator = GetUserId();
            value.TimeStamp = DateTime.Now;
            
            return true;
        }

        protected override bool ValidateNew(Content value, IDbConnection c, IDbTransaction t)
        {
            ValidateSizes(value);

            value.IdCreator = GetUserId();
            value.TimeStamp = DateTime.Now;

            value.RawContent = mSanitizer.Sanitize(value.RawContent);

            return true;
        }

        private static void ValidateSizes(Content value)
        {
            ValidateLen(value.Title, 500, "Title");
            ValidateLen(value.SubTitle, 1000, "SubTitle");

            ValidateLen(value.UserData1, 1000, "UserData1");
            ValidateLen(value.UserData2, 1000, "UserData2");
            ValidateLen(value.UserData3, 1000, "UserData3");
            ValidateLen(value.UserData4, 1000, "UserData4");

            ValidateLen(value.ThumbImgUrl, 1000, "ThumbImg");
            ValidateLen(value.MainImgUrl, 1000, "MainImg");

            ValidateLen(value.Keywords, 500, "Keywords");
            ValidateLen(value.Path, 500, "Path");

            ValidateLen(value.VideoUrl, 500, "VideoUrl");
            ValidateLen(value.LayoutType, 50, "LayoutType");
        }



        private static string GetTextForContactForm(ContactData cd)
        {
            return $@"
{Localization.Get("Nombre", null)}: {cd.Name},
{Localization.Get("Email", null)}: {cd.Email},
{Localization.Get("Asunto", null)}: {cd.Subject},
{Localization.Get("Texto del mensaje", null)}:

{cd.Text}

";
        }

        private HtmlSanitizer mSanitizer = new HtmlSanitizer();
        private NotificationManager mNotifier;
    }
}
