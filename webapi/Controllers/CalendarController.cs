using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using webapi.Models.Db;
using Utils;
using Microsoft.Extensions.Options;
using System.Data;
using Dapper;
using Dapper.Contrib.Extensions;
using System.Linq;
using System.Diagnostics;

namespace webapi.Controllers
{
    [Route("api/calendar")]
    public class CalendarController: DbController
    {
        public CalendarController(IOptions<Config> config): base(config)
        {

        }

        [HttpPost("generate")]
        public IActionResult GenerateTournamentCalendar([FromBody] CalendarGenInput input)
        {
            return DbOperation(c => {
                if (input == null) throw new NoDataException();
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                CalendarResult result = null;

                var teams = GetTeams(c, null, input.TeamIds);

                switch ((CalendarType)input.Type)
                {
                    case CalendarType.League:
                        result = LeaguePlanner.Calculate(input, null, GetUserLocale(), teamIds => GetTeamPreferences(c, null, teamIds), idTeam => GetTeamName(teams, idTeam));
                        break;
                    case CalendarType.Knockout:
                        result = KnockoutPlanner.Calculate(input, null, GetUserLocale(), teamIds => GetTeamPreferences(c, null, teamIds));
                        break;
                    default:
                        break;
                }
                
                if (!input.IsPreview)
                {
                    CalendarStorer.SaveRounds(c, input.Group, result);
                }

                return result;
            });
        }

        [HttpPost("delete/group")]
        public IActionResult Delete([FromBody] GroupCoords coords)
        {
            return DbTransaction((c, t) => {
                if (!IsOrganizationAdmin()) throw new UnauthorizedAccessException();

                CalendarStorer.DeleteGroupRounds(c, t, coords);
                return true;
            });
        }

        private IEnumerable<TeamLocationPreference> GetTeamPreferences(IDbConnection c, IDbTransaction t, long[] teamIds)
        {
            var ids = Utils.GetJoined(teamIds);

            var query = $"SELECT id as idTeam, idField, prefTime as time FROM teams WHERE id IN ({ids}) AND (idField > 0 OR prefTime is not null)";
            return c.Query<TeamLocationPreference>(query, t).ToList();
        }

        private string GetTeamName(IEnumerable<Team> teams, long idTeam)
        {
            return teams.Where(t => t.Id == idTeam).FirstOrDefault()?.Name;
        }

        private IEnumerable<Team> GetTeams(IDbConnection c, IDbTransaction t, long[] teamIds)
        {
            if (teamIds == null) return new Team[0];

            var ids = Utils.GetJoined(teamIds);
            var query = $"SELECT id, name FROM teams WHERE id IN ({ids})";
            return c.Query<Team>(query, t);
        }
    }

    public delegate string TeamNameProvider(long idTeam);
    public delegate IEnumerable<TeamLocationPreference> TeamLocationPreferenceProviderDelegate(long[] teamIds);

    public class CalendarGenInput
    {
        public GroupCoords Group { get; set; }
        public int Type { get; set; }
        public long[] TeamIds { get; set; }
        public DailySlot[][] WeekdaySlots { get; set; }      // Each weekday, slots
        public DateTime StartDate { get; set; }
        public DateTime[] ForbiddenDays { get; set; }
        public long[] FieldIds { get; set; }
        public int GameDuration { get; set; } = 60;
        public bool IsPreview { get; set; } = true;
        public bool RandomizeFields { get; set; }
        public int NumRounds { get; set; }
    }

    public class GroupCoords
    {
        public long IdTournament { get; set; }
        public long IdStage { get; set; }
        public long IdGroup { get; set; }

        [Write(false)] public Tournament Tournament { get; set; }
        [Write(false)] public TournamentStage Stage { get; set; }
        [Write(false)] public StageGroup Group { get; set; }
    }

    public enum CalendarType
    {
        League = 1,
        Knockout = 2
    }

    public class DailySlot
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
    }



    public class CalendarStorer
    {
        public static int DeleteGroupRounds(IDbConnection c, IDbTransaction t, GroupCoords coords)
        {
            var tournament = c.Get<Tournament>(coords.IdTournament, t);
            if (tournament == null) throw new Exception("Error.NotFound");
            if (tournament.Status >= (int)TournamentStatus.Playing) throw new Exception("Tournament.AlreadyStarted");

            var sql = @"
                DELETE FROM matches m JOIN playdays p ON p.idMatch = m.id WHERE p.idGroup = @idGroup;
            ";

            var result = c.Execute(sql, new { idGroup = coords.IdGroup }, t);
            
            return result;
        }

        public static void SaveRounds(IDbConnection conn, GroupCoords coords, CalendarResult result)
        {
            var transaction = conn.BeginTransaction();

            try
            {
                var dbGroup = conn.Get<StageGroup>(coords.IdGroup);
                if (dbGroup == null) throw new Exception("Error.NotFound");
                if ( (dbGroup.Flags & (int)StageGroupFlags.HasGeneratedCalendar) > 0 ) throw new Exception("Tournament.AlreadyHasCalendar");

                foreach (var day in result.Days)
                {
                    InsertDay(conn, transaction, day);   
                }

                dbGroup.Flags = dbGroup.Flags | (int)StageGroupFlags.HasGeneratedCalendar;
                conn.Update(dbGroup, transaction);

                transaction.Commit();
            }
            catch (System.Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        public static void DeleteCalendar(IDbConnection c, IDbTransaction t, long idGroup)
        {
            var args = new { idGroup };
            var numEvents = c.ExecuteScalar<int>("select count(e.id) from matchevents e join matches m on m.id = e.idmatch where m.idgroup = @idGroup", args, t);
            if (numEvents > 0) throw new Exception("Error.CalendarHasEvents");

            var dbGroup = c.Get<StageGroup>(idGroup, t);
            if (dbGroup == null) throw new Exception("Error.NotFound");
            if ((dbGroup.Flags & (int)StageGroupFlags.HasGeneratedCalendar) == 0) throw new Exception("Error.GroupHasNoCalendar");

            var idTournament = dbGroup.IdTournament;

            // Playdays are reused in the tournament for other groups
            var sql = @"
                DELETE FROM matchplayers WHERE idMatch IN (SELECT id FROM matches WHERE idGroup = @idGroup);
                DELETE FROM matches WHERE idGroup = @idGroup;
                DELETE FROM playdays WHERE idtournament = @idTournament AND id NOT IN (SELECT idday FROM matches WHERE idtournament = @idTournament AND idgroup <> @idGroup);
            ";

            c.Execute(sql, new { idGroup, idTournament }, t);

            dbGroup.Flags = dbGroup.Flags & ~(int)StageGroupFlags.HasGeneratedCalendar;
            c.Update(dbGroup, t);
        }

        public static PlayDay GetOrInsertDay(IDbConnection c, IDbTransaction t, PlayDay day)
        {
            var dbDays = c.Query<PlayDay>("SELECT * FROM playdays WHERE idTournament = @IdTournament AND idStage = @IdStage AND name = @Name", day, t);

            long dayId = -1;
            var count = dbDays.Count();

            if (count == 1)
            {
                // Exists, reuse it
                return dbDays.First();
            }
            else if (count == 0)
            {
                // Doesn't exist, create
                dayId = c.Insert(day, t);
                day.Id = dayId;
                return day;
            }
            else
            {
                throw new PlannerException("Error.InternalInconsistency.MoreThanOneMatchDayForSame");
            }
        }

        private static void InsertDay(IDbConnection c, IDbTransaction t, PlayDay day)
        {
            var dbDay = GetOrInsertDay(c, t, day);

            foreach (var match in day.Matches)
            {
                match.IdDay = dbDay.Id;
                match.Id = c.Insert(match, t);
            }
        }
    }


    public class KnockoutPlanner
    {
        public static CalendarResult Calculate(CalendarGenInput input, IList<Field> fields, string locale, TeamLocationPreferenceProviderDelegate preferenceProvider)
        {
            Assert.IsNotNull(input);
            Assert.IsNotNull(input.TeamIds);
            Assert.IsNotNull(input.WeekdaySlots);
            Assert.IsNotNull(input.FieldIds);

            var result = new CalendarResult();

            var teams = input.TeamIds;
            if (!IsPowerOfTwo(teams.Length)) throw new PlannerException("Error.NotPowerOfTwo");
            // Should also check that teams are actually valid (!= -1)

            var matchRounds = CreateRounds(teams, input.Group);

            string roundNameCallback(int index) => GetRoundName(index, matchRounds.Count, locale);

            var numSlotsNeededPerRound = teams.Length / 2;
            //var numFields = input.FieldIds.Length;
            //var availableTimeSlots = PlannerScheduler.GetTimeSlots(input.WeekdaySlots, input.GameDuration);
            //var availableSlots = availableTimeSlots * numFields;
            //if (availableSlots < numSlotsNeededPerRound) throw new PlannerException("Error.Calendar.NotEnoughHours");

            var teamPrefs = preferenceProvider?.Invoke(input.TeamIds);

            PlannerScheduler.SpreadMatchesInCalendar(
                result, 
                matchRounds,
                input.WeekdaySlots,
                input.StartDate,
                input.FieldIds,
                numSlotsNeededPerRound,
                input.GameDuration,
                false,
                input.Group,
                input.ForbiddenDays,
                teamPrefs,
                roundNameCallback);

            return result;
        }

        public static string GetRoundName(int index, int numRounds, string locale)
        {
            var reverseIndex = numRounds + 1 - index;
            var numTeams = 1 << reverseIndex;

            switch (numTeams)
            {
                case 128: return Localization.Get("64avos", locale);
                case 64: return Localization.Get("32avos", locale);
                case 32: return Localization.Get("16avos", locale);
                case 16: return Localization.Get("Octavos de final", locale);
                case 8: return Localization.Get("Cuartos de final", locale);
                case 4: return Localization.Get("Semifinales", locale);
                case 2: return Localization.Get("Final", locale);
                default: return Localization.Get("Desconocido", locale);
            }
        }

        public static List<List<Match>> CreateRounds(IList<long> teamIds, GroupCoords coords)
        {
            var num = teamIds.Count;
            var numDays = (int)Math.Log(num, 2);
            var result = new List<List<Match>>();
            var teams = teamIds;

            for (var i = numDays; i > 0; --i)
            {
                var day = CreateRoundMatches( (1 << i) / 2, coords, teams);
                teams = null;   // Only fill teamids in the first round

                result.Add(day);
            }

            return result;
        }

        private static List<Match> CreateRoundMatches(int numMatches, GroupCoords coords, IList<long> teamIds)
        {
            var result = new List<Match>(numMatches);

            for (int i = 0; i < numMatches; ++i)
            {
                var match = new Match
                {
                    IdTournament = coords.IdTournament, 
                    IdStage = coords.IdStage,
                    IdGroup = coords.IdGroup
                };

                if (teamIds != null)
                {
                    match.IdHomeTeam = teamIds[i * 2];
                    match.IdVisitorTeam = teamIds[i * 2 + 1];
                }

                result.Add(match);
            }

            return result;
        }

        private static bool IsPowerOfTwo(int x)
        {
            // https://stackoverflow.com/questions/600293/how-to-check-if-a-number-is-a-power-of-2
            return (x > 0) && ((x & (x - 1)) == 0);
        }
    }


    public class LeaguePlanner
    {
        /* 
        LEAGUE

            input 
                - list of teams (ids)
                - start date
                - weekdays available
                    - with range of hours
                - days forbidden
                - list of play fields
                    - with their availability
                - game duration (minutes)
                - is preview?

            output: 
                - a collection of days, each containing: 
                    - date
                    - list of matches, each containing: 
                        - teams
                        - hour
                        - play field
                        - status: scheduled
                    
            Notes: 
                - First create a list of matches per day, then spread it in the available slots. 
                - From the number of teams, the algorithm can predict how many slots are needed in each day 
                    (it's the same as the number of teams). This should be used in the UI to set up the 
                    calendar parameters. 
                - To create the list of matches per day, start with the list of teams.  
                    - In the UI, allow the organizer to order them any way the want (display a message for this). 
                    - Then do every possible combination. If odd number of teams, one will be left out each round, in order.
                - To spread the matches in slots, randomize the list and assign to slots. 
                - Have to consider the "home" setting of each team. Only one team should play at home, but then, 
                    what if two have then same home? no matter what, they will have to play, and their home won't 
                    change. 

            Should this be implemented in the server instead? Having this not validated on the client implies an
            organizer can mess the calendar. Is it possible that it can ultimately generate payments to users? 
            In the server this could be implemented as a simple API call that calculates the preview. An argument 
            can decide whether the generated result is just a preview or has to be commited to the DB. 
            Since the game field availability is a lot of data that has to be queried to the database, it's 
            benefitial to do it on the server side. 


            Test environment for this. 
        */            

        
        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="fields">List of fields, including matches already scheduled to consider in the plan</param>
        /// <returns></returns>
        public static CalendarResult Calculate(CalendarGenInput input, IList<Field> fields, string locale, TeamLocationPreferenceProviderDelegate preferenceProvider, TeamNameProvider nameProvider)
        {
            //-First create a list of matches per round, then spread it in the available slots.

            //      - From the number of teams, the algorithm can predict how many slots are needed in each round
            //          (it's the same as the number of teams). This should be used in the UI to set up the 
            //            calendar parameters. 
            //        -To create the list of matches per round, start with the list of teams.  
            //            - In the UI, allow the organizer to order them any way the want(display a message for this).
            //            - Then apply the round robin algorithm, preserving the first entry in the list.
            //     - To spread the matches in slots, randomize each round list and assign to slots in sequence.
            //     - Have to consider the "home" setting of each team. Only one team should play at home, but then,
            //       what if two have the same home? no matter what, they will have to play, and their home won't 
            //          change.

            Assert.IsNotNull(input);
            Assert.IsNotNull(input.TeamIds);
            Assert.IsNotNull(input.WeekdaySlots);
            Assert.IsNotNull(input.FieldIds);

            var result = new CalendarResult();

            if (input.TeamIds.Length <= 2) throw new PlannerException("Error.Calendar.NotEnoughTeams");

            var teams = input.TeamIds;

            var oddNumTeams = teams.Length % 2 == 1;
            if (oddNumTeams)
            {
                // Add a "free day" team (id = -1)
                Array.Resize(ref teams, teams.Length + 1);
                teams[teams.Length - 1] = -1;
            }

            var numSlotsNeededPerRound = teams.Length / 2;
            //var numFields = input.FieldIds.Length;
            //var availableTimeSlots = PlannerScheduler.GetTimeSlots(input.WeekdaySlots, input.GameDuration);
            //var availableSlots = availableTimeSlots * numFields;
            //if (availableSlots < numSlotsNeededPerRound) throw new PlannerException("Error.Calendar.NotEnoughHours");

            var matchRounds = CreateRoundRobinMatches(teams, input.NumRounds);

            string roundNameCallback(int index) => Localization.Get("Jornada {0}", locale, index);

            var locationPreferences = preferenceProvider?.Invoke(input.TeamIds);

            // Now assign matches to days. Consider field availability.
            PlannerScheduler.SpreadMatchesInCalendar(
                result,
                matchRounds, 
                input.WeekdaySlots, 
                input.StartDate, 
                input.FieldIds, 
                numSlotsNeededPerRound, 
                input.GameDuration, 
                input.RandomizeFields, 
                input.Group, 
                input.ForbiddenDays,
                locationPreferences,
                roundNameCallback);

            return result;
        }

        public static List<List<Match>> CreateRoundRobinMatches(long[] teamIds, int numRounds)
        {
            // https://en.wikipedia.org/wiki/Round-robin_tournament

            // One list of matches per round
            var result = new List<List<Match>>();

            var n = teamIds.Length;
            var n2 = n / 2;
            var list1 = new long[n2];
            var list2 = new long[n2];

            // Create initial list
            for (int i = 0; i < n2; ++i) list1[i] = teamIds[i];
            for (int j = teamIds.Length - 1, i = 0; j >= n2; --j, ++i) list2[i] = teamIds[j];

            // Loop rounds
            for (int i = 0; i < n - 1; ++i)
            {
                var odd = (i % 2 == 1);

                var l1 = odd ? list2 : list1;
                var l2 = odd ? list1 : list2;

                var roundMatches = GetMatchesFromLists(l1, l2);
                result.Add(roundMatches);

                ApplyRoundRobin(list1, list2);
            }

            AdjustForLocalityPairs(result);

            AddAdditionalRounds(result, numRounds);

            return result;
        }

        public static void AdjustForLocalityPairs(List<List<Match>> days)
        {
            // From day n/2, reverse first match. Magic! See DYQ-171

            var n2 = days.Count / 2 + 1;

            for (int i = n2; i < days.Count; ++i)
            {
                var day = days[i];
                if (day == null || day.Count == 0) throw new ArgumentNullException("day");

                var match = day[0];

                ReverseMatch(match);
            }
        }

        public static void ReverseMatch(Match m)
        {
            var intermediate = m.IdHomeTeam;
            m.IdHomeTeam = m.IdVisitorTeam;
            m.IdVisitorTeam = intermediate;
        }

        public static void AddAdditionalRounds(List<List<Match>> matches, int numRounds)
        {
            if (numRounds == 1) return;

            var option = ReverseMatches(matches);

            for (int i = 1; i < numRounds; ++i)
            {
                AppendListOfMatches(matches, option);
                option = ReverseMatches(option);
            }
        }

        public static void AppendListOfMatches(List<List<Match>> target, List<List<Match>> matchesToAdd)
        {
            foreach (var matchList in matchesToAdd) target.Add(matchList);
        }

        public static List<List<Match>> ReverseMatches(List<List<Match>> matches)
        {
            var result = new List<List<Match>>();

            foreach (var dayList in matches)
            {
                var newList = new List<Match>();

                foreach (var match in dayList)
                {
                    newList.Add(new Match { IdHomeTeam = match.IdVisitorTeam, IdVisitorTeam = match.IdHomeTeam });
                }

                result.Add(newList);
            }

            return result;
        }

        public static void ApplyRoundRobin(long[] list1, long[] list2)
        {
            var r = ShiftArrayRight(list1, list2[0]);
            ShiftArrayLeft(list2, r);
        }

        public static T ShiftArrayRight<T>(T[] list, T itemToPush)
        {
            var result = list[list.Length - 1];

            for (int i = list.Length - 1; i >= 2; --i) list[i] = list[i - 1];

            list[1] = itemToPush;

            return result;
        }

        public static T ShiftArrayLeft<T>(T[] list, T itemToPush)
        {
            var result = list[0];

            for (int i = 0; i < list.Length - 1; ++i) list[i] = list[i + 1];

            list[list.Length - 1] = itemToPush;

            return result;
        }

        private static List<Match> GetMatchesFromLists(long[] list1, long[] list2)
        {
            var result = new List<Match>(list1.Length);

            for (int i = 0; i < list1.Length; ++i)
            {
                result.Add(new Match { IdHomeTeam = list1[i], IdVisitorTeam = list2[i] });
            }

            return result;
        }

        private static bool IsBackMatch(Match m1, Match m2)
        {
            return (m1.IdHomeTeam == m2.IdVisitorTeam && m1.IdVisitorTeam == m2.IdHomeTeam);
        }

    }

    public class PlannerScheduler
    {
        public static void SpreadMatchesInCalendar(
            CalendarResult result,
            List<List<Match>> matchRounds, 
            DailySlot[][] weekdaySlots, 
            DateTime startDate, 
            IList<long> fieldIds, 
            int numSlots, 
            int gameDuration, 
            bool wantsRandom, 
            GroupCoords coords, 
            DateTime[] forbiddenDays,
            IEnumerable<TeamLocationPreference> fieldPreferences,
            Func<int, string> roundNameCallback)
        {
            var days = new List<PlayDay>();
            //var fieldIds = GetFieldIds(fields);

            var roundNumber = 1;

            // First, spread the rounds in days in the calendar
            foreach (var matchRound in matchRounds)
            {
                startDate = GetUnforbiddenStartDate(startDate, weekdaySlots, forbiddenDays);

                var slots = GetRoundSlots(startDate, weekdaySlots, numSlots, gameDuration, fieldIds);
                var roundName = roundNameCallback(roundNumber);

                var round = CreateAndFillRound(result, matchRound, slots, coords, fieldPreferences, roundName);
                round.IdTournament = coords.IdTournament;
                round.IdStage = coords.IdStage;
                round.IdGroup = coords.IdGroup;
                round.Name = roundName;
                round.SequenceOrder = roundNumber;
                days.Add(round);

                startDate = startDate.AddDays(7);

                roundNumber++;
            }

            result.Days = days;
        }

        public static DateTime GetUnforbiddenStartDate(DateTime startDate, DailySlot[][] weekdaySlots, DateTime[] forbiddenDays)
        {
            // Check days in slots and see if any is forbidden. If it is, advance to next week and do so until we are clear
            while (IsWeekInForbiddenDays(startDate, weekdaySlots, forbiddenDays))
            {
                startDate = startDate.AddDays(7);
            }

            return startDate;
        }

        public static bool IsWeekInForbiddenDays(DateTime startDate, DailySlot[][] weekdaySlots, DateTime[] forbiddenDays)
        {
            var startWeekDay = (int)startDate.DayOfWeek;

            for (int i = startWeekDay; i < 7; ++i)
            {
                if (weekdaySlots[i] != null && weekdaySlots[i].Length > 0)
                {
                    var targetDate = startDate.AddDays(i - startWeekDay);
                    if (IsDateForbidden(targetDate, forbiddenDays)) return true;
                }
            }

            for (int i = 0; i < startWeekDay; ++i)
            {
                if (weekdaySlots[i] != null && weekdaySlots[i].Length > 0)
                {
                    var targetDate = startDate.AddDays(7 + i - startWeekDay);
                    if (IsDateForbidden(targetDate, forbiddenDays)) return true;
                }
            }

            return false;
        }

        public static bool IsDateForbidden(DateTime date, DateTime[] forbiddenDays)
        {
            foreach (var f in forbiddenDays)
            {
                if (IsSameDay(date, f)) return true;
            }

            return false;
        }

        public static bool IsSameDay(DateTime a, DateTime b)
        {
            return a.Day == b.Day && a.Month == b.Month;    // Ignore year?
        }

        public static PlayDay CreateAndFillRound(CalendarResult result, List<Match> matchesInRound, IList<CalendarSlot> slots, GroupCoords coords, IEnumerable<TeamLocationPreference> fieldPreferences, string roundName)
        {
            // Round Id -1: when saving to the database, will have to set proper ID here and to the matches. 
            var day = new PlayDay { Id = -1, Matches = new List<Match>() };

            // Spread fieldPreferences in the slots (consuming them). Then fill the rest of the matches. 
            AssignPreferencesToSlots(result, matchesInRound, slots, fieldPreferences, roundName);

            foreach (var match in matchesInRound)
            {
                match.IdTournament = coords.IdTournament;
                match.IdStage = coords.IdStage;
                match.IdGroup = coords.IdGroup;
                match.Status = (int)MatchStatus.Created;
                match.IdDay = day.Id;
                
                match.Status = (int)MatchStatus.Created;

                if (IsFillerMatch(match))
                {
                    // Add match, but do not consume slot
                    match.Status = (int)MatchStatus.Skip;
                    day.Matches.Add(match);
                    continue;
                }

                // If the match doesn't already have time 
                if (match.StartTime == default(DateTime) )
                {
                    if (slots.Count == 0) throw new Exception("Error.Calendar.NotEnoughHours");

                    // Assign first available slot and remove from the list.
                    var slot = slots[0];
                    slot.AssignToMatch(match);
                    slots.RemoveAt(0);
                }

                day.Matches.Add(match);
            }

            day.SetDatesFromDatesList();

            return day;
        }

        public static void AssignPreferencesToSlots(CalendarResult result, List<Match> matchesInRound, IList<CalendarSlot> slots, IEnumerable<TeamLocationPreference> teamPreferences, string roundName)
        {
            if (teamPreferences == null) return;

            var localTeamIds = GetLocalTeams(matchesInRound);

            // * Assign first LOCAL teams with field AND time preference
            var prefs = GetPreferencesForLocalTeams(GetFieldAndTimePreferences(teamPreferences), localTeamIds);
            AssignSpecificPreferencesToSlots(result, matchesInRound, slots, prefs, roundName);
            

            // * Now assign LOCAL teams with field preference to any available hour (time preference only is not allowed)
            prefs = GetPreferencesForLocalTeams(GetFieldPreferences(teamPreferences), localTeamIds);
            AssignSpecificPreferencesToSlots(result, matchesInRound, slots, prefs, roundName);
        }

        public static void AssignSpecificPreferencesToSlots(CalendarResult result, List<Match> matchesInRound, IList<CalendarSlot> slots, IEnumerable<TeamLocationPreference> prefs, string roundName)
        {
            // Locate slots for those prefs, remove slots and assign them to matches
            foreach (var pref in prefs)
            {
                // Locate slot for that preference
                var slot = GetSlotForPreference(pref, slots);
                if (slot == null)
                {
                    result.AddMessage(OutputLineType.Warning, "{1}: El horario preferido del equipo {0} no está en los rangos de horas especificados, o el campo preferido no está en la lista de seleccionados. Ignorando la preferencia del equipo.", pref.IdTeam, roundName);
                    continue;
                }

                // Assign the slot to the match (find the match for this team) and remove the slot in slots.
                var match = GetMatchForHomeTeamId(matchesInRound, pref.IdTeam);
                if (match == null)
                {
                    result.AddMessage(OutputLineType.Error, "{1}: Hay una preferencia para el equipo {0} pero no está en la lista de equipos. Inconsistencia interna.", pref.IdTeam, roundName);
                }
                else
                {
                    // Assign slot to match and remove from the list of available slots
                    slot.AssignToMatch(match);
                    slots.Remove(slot);
                }
            }
        }

        public static Match GetMatchForHomeTeamId(List<Match> matchesInRound, long idTeam)
        {
            return matchesInRound.Where(m => m.IdHomeTeam == idTeam).FirstOrDefault();
        }

        public static CalendarSlot GetSlotForPreference(TeamLocationPreference pref, IList<CalendarSlot> slots)
        {
            // First try to match time and field
            var result = slots.Where(s =>
            {
                // Get the first slot within that hour range and that field. 
                return GetTime(s.StartTime) <= pref.Time && pref.Time < GetTime(s.EndTime) && s.IdField == pref.IdField;

            }).FirstOrDefault();

            if (result != null) return result;


            // If that didn't match, try to match field only
            return slots.Where(s =>
            {
                // Get the first slot with that field.
                return (s.IdField == pref.IdField);
            }).FirstOrDefault();
        }

        public static IEnumerable<TeamLocationPreference> GetPreferencesForLocalTeams(IEnumerable<TeamLocationPreference> prefs, IEnumerable<long> localTeamIds)
        {
            return prefs.Where(p => localTeamIds.Contains(p.IdTeam));
        }

        public static IEnumerable<TeamLocationPreference> GetFieldAndTimePreferences(IEnumerable<TeamLocationPreference> prefs)
        {
            return prefs.Where(p => (p.Time != null && p.IdField > 0));
        }

        public static IEnumerable<TeamLocationPreference> GetFieldPreferences(IEnumerable<TeamLocationPreference> prefs)
        {
            return prefs.Where(p => (p.Time == null && p.IdField > 0));
        }

        public static IEnumerable<long> GetLocalTeams(IList<Match> matches)
        {
            return matches.Select(m => m.IdHomeTeam);
        }

        private static DateTime? GetSlotForTime(CalendarResult result, IList<DateTime> slots, int gameDuration, DateTime time, long idTeam)
        {
            foreach (var slot in slots)
            {
                var slotStartTime = GetTime(slot);
                var slotEndTime = slotStartTime.AddMinutes(gameDuration);

                if (slotStartTime >= time && time >= slotEndTime) return slot;
            }

            result.Messages.Add(new OutputLine(OutputLineType.Warning, Localization.Get("El equipo {0} prefiere la hora {1} que no está en el rango de horas asignado. Ignorando la preferencia.", null, idTeam, time)));
            return null;
        }

        private static DateTime GetTime(DateTime? d)
        {
            if (d == null) return default(DateTime);

            return new DateTime(1, 1, 1, d.Value.Hour, d.Value.Minute, 0);
        }

        //private static bool IsValidField(CalendarResult result, long[] fieldIds, long idField, long idTeam)
        //{
        //    if (!fieldIds.Contains(idField))
        //    {
        //        result.Messages.Add(new OutputLine(OutputLineType.Warning, Localization.Get("El equipo {0} prefiere el campo {1} que no está en la lista de campos disponibles. Ignorando la preferencia.", null, idTeam, idField)));
        //        return false;
        //    }

        //    return true;
        //}

        private static bool IsFillerMatch(Match match)
        {
            // Check if this is a filler match (descansa). 

            return match.IdHomeTeam == -1 || match.IdVisitorTeam == -1;
        }

        //public static DateTime GetDay(DateTime timeStamp)
        //{
        //    return new DateTime(timeStamp.Year, timeStamp.Month, timeStamp.Day);
        //}

        public static IList<CalendarSlot> GetRoundSlots(DateTime startDate, DailySlot[][] weekdaySlots, int numSlots, int gameDuration, IList<long> fieldIds)
        {
            // if no fields are available, we should still have slots
            if (fieldIds == null || fieldIds.Count == 0) fieldIds = new long[] { 0 };

            // This generates a list of slots available in the round. For instance, in there is a range of 4 hours on friday, 
            // 2 fields available and 1 hour games, it will generate a list of 8 datetimes for the 4 hours, 2 each hour.

            var daySlots = new List<CalendarSlot>();
            var startWeekDay = (int)startDate.DayOfWeek;

            for (int i = startWeekDay; i < 7; ++i)
            {
                var date = startDate.AddDays(i - startWeekDay);
                AddDaySlots(weekdaySlots[i], gameDuration, fieldIds, daySlots, date);
            }

            for (int i = 0; i < startWeekDay; ++i)
            {
                var date = startDate.AddDays(7 + i - startWeekDay);
                AddDaySlots(weekdaySlots[i], gameDuration, fieldIds, daySlots, date);
            }

            if (daySlots.Count == 0)
            {
                // No weekdaySlots were specified, so create as many empty slots as needed for the matches and fields (matches x fields)
                // so later the preference allocator can always find a slot for the field preference. 
                for (var s = 0; s < numSlots; ++s)
                {
                    for (var f = 0; f < fieldIds.Count; ++f)
                    {
                        daySlots.Add(new CalendarSlot
                        {
                            IdField = fieldIds[f]
                        });
                    }
                }
            }

            //throw new PlannerException("Not enough hours allocated for the matches");
            return daySlots;
        }

        private static void AddDaySlots(DailySlot[] weekdaySlots, int gameDuration, IList<long> fieldIds, List<CalendarSlot> result, DateTime targetDate)
        {
            foreach (var range in weekdaySlots)
            {
                var slotStart = AddTime(targetDate, range.StartTime);
                var rangeEnd = AddTime(targetDate, range.EndTime);

                while (true)
                {
                    var slotEnd = slotStart.AddMinutes(gameDuration);
                    if (slotEnd > rangeEnd) break;

                    // Add as many slots as fields are available. Should check here if that particular slot is available for the given fields.
                    for (int h = 0; h < fieldIds.Count; ++h)
                    {
                        result.Add(new CalendarSlot
                        {
                            StartTime = slotStart,
                            EndTime = slotEnd,
                            GameDuration = gameDuration,
                            IdField = fieldIds[h]
                        });
                    }

                    // Commented this out, so that all possible slots are returned, even if more than needed. May need them 
                    // later if fields are not available.
                    //if (result.Count >= numSlots) return result;      

                    slotStart = slotEnd;
                }
            }
        }

        public static DateTime AddTime(DateTime date, DateTime time)
        {
            return new DateTime(date.Year, date.Month, date.Day, time.Hour, time.Minute, time.Second);
        }

        public static void RandomizeRounds(List<List<Match>> matchRounds)
        {
            foreach (var round in matchRounds) Shuffle(round);
        }

        public static void Shuffle<T>(IList<T> list)
        {
            Random rng = new Random();

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static long[] CopyAndShuffle(IList<long> array, bool wantsRandom)
        {
            var result = new long[array.Count];
            array.CopyTo(result, 0);
            if (wantsRandom) Shuffle(result);
            return result;
        }

        public static long[] GetFieldIds(IList<Field> fields)
        {
            var result = new long[fields.Count];
            for (int i = 0; i < fields.Count; ++i) result[i] = fields[i].Id;

            return result;
        }

        public static int GetTimeSlots(DailySlot[][] weekdaySlots, int gameDuration)
        {
            int result = 0;

            foreach (var weekDay in weekdaySlots)
            {
                foreach (var slot in weekDay)
                {
                    var slotSize = slot.EndTime - slot.StartTime;
                    var slots = (int)(Math.Floor(slotSize.TotalMinutes / gameDuration));
                    result += slots;
                }
            }

            return result;
        }


    }

    public class PlannerException : Exception
    {
        public PlannerException(string msg) : base(msg) { }
    }

    public class CalendarResult
    {
        public IList<PlayDay> Days { get; set; }
        public IList<OutputLine> Messages { get; set; } = new List<OutputLine>();

        public void AddMessage(OutputLineType type, string msg, params object[] args)
        {
            Messages.Add(new OutputLine(type, Localization.Get(msg, null, args)));
        }
    }

    [DebuggerDisplay("Time: {StartTime} Field: {IdField}")]
    public class CalendarSlot
    {
        public DateTime? StartTime;
        public DateTime? EndTime;
        public long? IdField;
        public int GameDuration;


        public void AssignToMatch(Match m)
        {
            if (StartTime != null) m.StartTime = StartTime.Value;
            if (IdField != null) m.IdField = IdField.Value;
            m.Duration = GameDuration;
        }
    }

    public class OutputLine
    {
        public string Message { get; set; }
        public int Type { get; set; }       // Error, warning, info

        public OutputLine(OutputLineType type, string msg)
        {
            Message = msg;
            Type = (int)type;
        }
    }

    public enum OutputLineType
    {
        Info = 1, 
        Warning = 2, 
        Error = 3
    }

    public class TeamLocationPreference
    {
        public long IdTeam { get; set; }
        public DateTime? Time { get; set; }
        public long IdField { get; set; }
    }
}


namespace Utils
{

    public class Assert
    {
        public static void IsNotNull(object target)
        {
            if (target == null) throw new NullReferenceException();
        }
    }
}