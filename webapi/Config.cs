using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi
{
    public class Config
    {
        public string StorageProvider { get; set; } = "disk";
        public string DefaultLocale { get; set; } = "es";
        public bool RequireLogin { get; set; } = false;         // Is login required to access the read-only API endpoints?
        public string OrganizationsFile { get; set; }

    }

    public class CorsConfig
    {
        public string[] Origins { get; set; }
    }
}
