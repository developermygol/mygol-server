using System.Diagnostics;
using Dapper.Contrib.Extensions;

namespace webapi.Models.Db
{
    [DebuggerDisplay("{Name} ({Id})")]
    public class User: BaseObject
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Salt { get; set; }
        public int Level { get; set; }
        public string AvatarImgUrl { get; set; }
        public bool EmailConfirmed { get; set; }
        public string Mobile { get; set; }
        public string Lang { get; set; }
        //public string NotificationPushToken { get; set; }

        [Write(false)] public string DeviceToken { get; set; }
    }
}
