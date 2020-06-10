using contracts;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
//using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi.Controllers
{
    
    public class UploadController: DbController
    {
        public UploadController(IStorageProvider storage, IOptions<Config> config) : base(config)
        {
            mStorage = storage;
        }

        [HttpPost("secure")]
        public IActionResult UploadSecureDoc(IFormCollection form)
        {
            if (form.Files == null || form.Files.Count != 1) return BadRequest();

            int type = GetIntValue(form["type"]);
            int idObject = GetIntValue(form["idobject"]);
            if (type == -1 || idObject == -1) return BadRequest();

            return DbTransaction((c, t) =>
            {
                Authorize(c, type, idObject);

                var repoPath = ProcessFile(form.Files[0]);

                long uploadId = CreateUpload(c, t, type, idObject, repoPath);
                var userEvent = CreateUserEvent(c, t, type, uploadId);

                return repoPath;
            });
        }

        [HttpGet("secure/{id}")]
        public IActionResult GetSecureDoc(long id)
        {
            // This was supposed to return the document itself. 
            // Maybe just create docs with random enough urls and return urls in the uploadsecuredoc method. 
            // the docs may be stored in amazon / DO Spaces at some point. 

            throw new NotImplementedException();
        }



        [HttpPost]
        public IActionResult Upload(IFormCollection form)
        {
            return DbOperation(c =>
            {
                if (form.Files == null || form.Files.Count != 1) throw new NoDataException();

                int type = GetIntValue(form["type"]);
                int idObject = GetIntValue(form["idobject"]);
                if (type == -1 || idObject == -1) throw new Exception("Error.BadTypeOrId");

                Authorize(c, type, idObject);

                var repoPath = ProcessFile(form.Files[0]);

                long uploadId = CreateUpload(c, null, type, idObject, repoPath);

                return new { Id = uploadId, RepositoryPath = repoPath };
            });
        }

        private static long CreateUpload(IDbConnection c, IDbTransaction t, int type, int idObject, string repoPath)
        {
            return c.Insert(new Models.Db.Upload
            {
                IdObject = idObject,
                Type = type,
                RepositoryPath = repoPath
            }, t);
        }

        private UserEvent CreateUserEvent(IDbConnection c, IDbTransaction t, int uploadType, long idUpload)
        {
            var userEventType = -1;

            switch ((UploadType)uploadType)
            {
                case UploadType.PlayerIdCard1:
                case UploadType.PlayerIdCard2:
                case UploadType.PlayerInsuranceScan:
                case UploadType.PlayerGenericSecureScan: userEventType = (int)UserEventType.PlayerUploadedSecureDoc; break;
                case UploadType.PlayerIdPhoto: userEventType = (int)UserEventType.PlayerUploadedPicture; break;
                default: throw new Exception("Error.InvalidSecureUploadType");
            }

            var userEvent = new Models.Db.UserEvent
            {
                IdCreator = GetUserId(),
                IdSecureUpload = idUpload,
                IdUser = GetUserId(),
                TimeStamp = DateTime.Now,
                Type = userEventType,
                Description = ""            // Or generate one based on type and localization
            };

            userEvent.Id = c.Insert(userEvent, t);

            return userEvent;
        }

        private string ProcessFile(IFormFile file)
        {
            return mStorage.SaveBinaryContent(file.OpenReadStream(), Path.GetExtension(file.FileName));
        }



        private void Authorize(IDbConnection conn, int type, int idObject)
        {
            // New logic: logged in users can upload just fine.
            if (!IsLoggedIn()) throw new UnauthorizedAccessException();
        }

        private int GetIntValue(StringValues v)
        {
            if (v.Count != 1) return -1;
            if (!int.TryParse(v[0], out int result)) return -1;

            return result;
        }


        private IStorageProvider mStorage;
    }
}
