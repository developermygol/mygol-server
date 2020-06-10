using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi.Models.Db
{
    public class Content: BaseObject
    {
        public long IdCreator { get; set; }
        public DateTime TimeStamp { get; set; }
        public DateTime PublishDate { get; set; }
        public string Path { get; set; }
        public int ContentType { get; set; }
        public int Status { get; set; }
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string RawContent { get; set; }
        public string MainImgUrl { get; set; }
        public string ThumbImgUrl { get; set; }
        public int Priority { get; set; }
        public string Keywords { get; set; }
        public string UserData1 { get; set; }
        public string UserData2 { get; set; }
        public string UserData3 { get; set; }
        public string UserData4 { get; set; }

        public int IdCategory { get; set; }
        public string VideoUrl { get; set; }
        public long IdTournament { get; set; }
        public long IdTeam { get; set; }
        public string LayoutType { get; set; }
        public int CategoryPosition1 { get; set; }
        public int CategoryPosition2 { get; set; }

        [Write(false)] public long MainImgUploadId { get; set; }    // Use it to update upload with id after creation (when the idContent is still not available)
    }

    public class BasicContent: BaseObject
    {
        public string Title { get; set; }
        public int CategoryPosition1 { get; set; }
        public int CategoryPosition2 { get; set; }
    }


    public enum ContentType
    {
        Html        = 1,
        MarkDown    = 2
    }

    public enum ContentStatus
    {
        Draft               = 1,
        Published           = 2, 
        Deleted             = 10
    }


    [Table("contentcategories")]
    public class ContentCategory : BaseObject
    {
        public string Name { get; set; }
    }


    public class ContactData
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Subject { get; set; }
        public string Text { get; set; }

    }
}
