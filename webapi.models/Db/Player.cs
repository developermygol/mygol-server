using Dapper.Contrib.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace webapi.Models.Db
{
    // Full public player details, used in player info screens
    [DebuggerDisplay("{Name} {Surname} ({Id})")]
    [Table("players")]
    public class Player: BaseObject
    {
        public long IdUser { get; set; }
        
        public string Name { get; set; }
        public string Surname { get; set; }

        public DateTime BirthDate { get; set; }
        
        public string LargeImgUrl { get; set; }
        public string SignatureImgUrl { get; set; }
        public string Motto { get; set; }

        public long Height { get; set; }
        public long Weight { get; set; }        

        public string FacebookKey { get; set; }
        public string InstagramKey { get; set; }
        public string TwitterKey { get; set; }

        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string CP { get; set; }
        public string Country { get; set; }
        public string IdCardNumber { get; set; }

        public int EnrollmentStep { get; set; }
        public bool Approved { get; set; }

        public string IdPhotoImgUrl { get; set; }
        public string IdCard1ImgUrl { get; set; }
        public string IdCard2ImgUrl { get; set; }


        [Write(false)] public User UserData { get; set; }
        [Write(false)] public TeamPlayer TeamData { get; set; }
        [Write(false)] public IEnumerable<Team> Teams { get; set; }
        [Write(false)] public IEnumerable<UserEvent> Events { get; set; }
        [Write(false)] public MatchPlayer MatchData { get; set; }
        [Write(false)] public IEnumerable<PlayDay> DayResults { get; set; }
        [Write(false)] public PlayerDayResult DayResultSummary { get; set; }
        [Write(false)] public IEnumerable<Award> Awards { get; set; }
        [Write(false)] public string FichaPictureImgUrl { get; set; }
        [Write(false)] public IEnumerable<Sanction> Sanctions { get; set; }
        [Write(false)] public Team Team { get; set; }       // Team data for this particular query.
        [Write(false)] public Tournament Tournament { get; set; }       // Tournament data for this particular query.
        [Write(false)] public Season Season { get; set; }       // Season data for this particular query.
        [Write(false)] public long? IdSanction { get; set; }
        [Write(false)] public long? IdSanctionTeam { get; set; }

        public string GetName()
        {
            return GetName(Name, Surname);
        }

        public static string GetName(string name, string surname)
        {
            return $"{name} {surname}";
        }

        public override string Print()
        {
            return $"Player: id:{Id} uid:{IdUser} name:'{Name} {Surname}' email:'{UserData?.Email}' mobile:'{UserData?.Mobile}'";
        }
    }    

    public class TeamPlayer
    {
        [ExplicitKey] public long IdTeam { get; set; }
        [ExplicitKey] public long IdPlayer { get; set; }

        public int Status { get; set; }
        public int ApparelNumber { get; set; }
        public int FieldPosition { get; set; }
        public int FieldSide { get; set; }
        public bool IsTeamAdmin { get; set; }
        public int IdTacticPosition { get; set; } = -1;
        public int EnrollmentStep { get; set; }
        public string EnrollmentData { get; set; }
        public DateTime EnrollmentDate { get; set; }
        public string EnrollmentPaymentData { get; set; }
    }

    [Flags]
    public enum TeamPlayerStatusFlags
    {
        InvitationSent = 1,

        RegistrationCompleted = 4,
        IdCardUploaded = 8, 

        Paid = 128, 
        ApprovedForPlay = 256
    }
}
