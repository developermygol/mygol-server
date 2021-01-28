using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi.Controllers
{
    
    public class DaysController: CrudController<PlayDay>
    {
        public DaysController(IOptions<Config> config) : base(config)
        {
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "playdays"
            };
        }


        [HttpPost("generateawards/{tournamentId:long}/{playDayId:long}")]
        public async Task<IActionResult> GenerateDayAwards(long tournamentId, long playDayId)
        {
            return await DbTransactionAsync(async (c, t) =>
            {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                //var day = await c.QueryFirstAsync<PlayDay>("SELECT * FROM playdays WHERE id = @dayId", new { dayId = dayId }, t);
                var day = c.Get<PlayDay>(playDayId);
                if (day == null) throw new Exception("Error.PlayDay.NotFound");
                                
                // Check if has allready been generated.
                if (day.LastUpdateTimeStamp != null && day.LastUpdateTimeStamp != default(DateTime)) throw new Exception("Error.PlayDay.AllreadySet");                

                // Check has Maches ended or Recors are closed
                var matches = c.Query<Match>("SELECT * FROM matches WHERE idday = @idDay", new { idDay = playDayId });

                foreach (var match in matches)
                {
                    if (match.Status != (int)MatchStatus.Finished && match.Status != (int)MatchStatus.Signed) throw new Exception("Error.PlayDay.MatchesNotFinished");
                }

                // Update PlayDay => LastUpdateTimeStamp
                day.LastUpdateTimeStamp = DateTime.UtcNow;
                c.Update(day, t);

                await MatchEvent.UpdatePlayersDayStats(c, t, playDayId, tournamentId); // Required?
                IEnumerable<Award> topPlayDayAwards = await MatchEvent.AddTopPlayDayAwards(c, t, day.Id, day.IdStage, day.IdGroup, day.IdTournament);

                var tournament = c.Get<Tournament>(tournamentId);

                if (!string.IsNullOrEmpty(tournament.NotificationFlags))
                {
                    try
                    {
                        JObject notificationFlags = JsonConvert.DeserializeObject<JObject>(tournament.NotificationFlags);

                        if (notificationFlags["notifyAward"] != null && (bool)notificationFlags["notifyAward"] == true)
                        {
                            foreach (var award in topPlayDayAwards)
                            {
                                string title = Translation.Get("Push.Award.Title");
                                string message = "";
                                switch (award.Type)
                                {
                                    case (int)AwardType.TopMVP:
                                        message = Translation.Get($"Push.Award.Type${(int)AwardType.TopMVP}.Text");
                                        break;
                                    case (int)AwardType.TopScorer:
                                        message = Translation.Get($"Push.Award.Type${(int)AwardType.TopScorer}.Text");
                                        break;
                                    case (int)AwardType.TopGoalKeeper:
                                        message = Translation.Get($"Push.Award.Type${(int)AwardType.TopGoalKeeper}.Text");
                                        break;
                                    case (int)AwardType.TopAssistances:
                                        message = Translation.Get($"Push.Award.Type${(int)AwardType.TopAssistances}.Text");
                                        break;
                                }

                                NotifyPlayer(c, t, award.IdPlayer, title, message);
                            }
                        }

                    }
                    catch { }
                }

                return true;
            });
        }


        protected override bool IsAuthorized(RequestType reqType, PlayDay target, IDbConnection c)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateDelete(PlayDay value, IDbConnection c, IDbTransaction t)
        {
            // Make sure there are no matches associated to this day
            var numMatches = c.ExecuteScalar<int>("SELECT count(id) FROM matches WHERE idday = @idDay", new { idDay = value.Id }, t);
            if (numMatches > 0) throw new Exception("Error.PlayDayHasMatches");

            return true;
        }

        protected override bool ValidateEdit(PlayDay value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateNew(PlayDay value, IDbConnection c, IDbTransaction t)
        {
            return true;
        }

        private void NotifyPlayer(IDbConnection c, IDbTransaction t, long playerId, string title, string message)
        {
            Audit.Information(this, "{0}: Notifications.NotifyPlayer: {1} | {2}", GetUserId(), title, message);

            var player = c.Get<Player>(playerId);

            if (player.IdUser != 0)
            {
                var users = c.Query<User>("SELECT devicetoken FROM users u JOIN userdevices ud ON ud.idUser = u.id WHERE u.id = @id", new { id = player.IdUser });
                if (users.Count() > 0)
                {
                    int usersNotified = NotificationsController.NotifyUsers(users, title, message);
                }
            }

            c.Insert(new Notification { IdCreator = GetUserId(), IdRcptUser = player.IdUser, Status = (int)NotificationStatus.Unread, Text = message, Text2 = title, TimeStamp = DateTime.Now });
        }
    }
}
