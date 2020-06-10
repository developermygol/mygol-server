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
using webapi.Importers;
using webapi.Models.Db;

namespace webapi.Controllers
{
    
    public class ImportController: DbController
    {
        public ImportController(IStorageProvider storage, IOptions<Config> config) : base(config)
        {
            mStorage = storage;
        }

        [HttpPost("players")]
        public IActionResult Import([FromBody] ImportInputData importData)
        {
            return DbOperation(c =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                return PlayerImporter.Import(c, mStorage.GetPhysicalPath(importData.UploadedFile), GetUserId());
            });
        }

        private IStorageProvider mStorage;
    }



    public class ImportInputData
    {
        public string UploadedFile { get; set; }
    }
}
