using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;
using System.Data;
using Dapper;
using webapi.Controllers;
using Serilog;

namespace webapi
{
    public class ScheduledNotifications
    {
        public const int HoursBeforeNotification = 24;
        public const int NumHoursInRange = 1;


        public static (DateTime, DateTime) GetDefaultDateRange()
        {
            var now = DateTime.Now;
            var baseDate = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            var targetStart = baseDate.AddHours(HoursBeforeNotification);
            var targetEnd = targetStart.AddHours(NumHoursInRange);

            return (targetStart, targetEnd);
        }

        public static void NotifyMatchesInDateRange(IDbConnection c, IDbTransaction t, DateTime startTime, DateTime endTime)
        {
            var matches = c.Query<Match, Team, Team, Field, Match>(@"
                    SELECT m.*, t1.id, t1.name, t2.id, t2.name, f.id, f.name 
                    FROM matches m 
                        LEFT JOIN teams t1 ON m.idHomeTeam = t1.id 
                        LEFT JOIN teams t2 ON m.idvisitorteam = t2.id 
                        LEFT JOIN fields f ON m.idField = f.id 
                    WHERE m.startTime BETWEEN @startTime AND @endTime",
                (match, homeTeam, visitorTeam, field) =>
                {
                    match.HomeTeam = homeTeam;
                    match.VisitorTeam = visitorTeam;
                    match.Field = field;
                    return match;
                }, new { startTime, endTime }, t, splitOn: "id");

            Log.Information("ScheduledNotifications: notifying {0} matches", matches.Count());

            foreach (var m in matches) NotifyMatchPeople(c, t, m);
        }

        public static void NotifyMatchPeople(IDbConnection c, IDbTransaction t, Match m)
        {
            var text = GetSpanishMessage(m);
            var title = "Horario de partido";

            NotificationsController.NotifyMatch(c, t, m, title, text);
        }


        private static string GetSpanishMessage(Match m)
        {
            var f = m.Field?.Name;
            var t1 = m.HomeTeam?.Name ?? "?";
            var t2 = m.VisitorTeam?.Name ?? "?";

            var field = (f != null) ? $" en {f}" : "";
            var day = m.StartTime.ToString("dd") + " de " + m.StartTime.ToString("MMMM");
            var hour = m.StartTime.ToString("HH:mm");
            
            var text = $"Tu equipo juega en el partido '{t1}' vs '{t2}' el {day} a las {hour}{field}";

            return text;
        }
    }
}
