using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using webapi.Controllers;
using webapi.Models.Db;

namespace webapi.test
{
    [TestClass]
    public class CalendarTest
    {
        [TestMethod]
        public void CreateRoundRobinTest()
        {
            var teamIds = new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };

            var r = LeaguePlanner.CreateRoundRobinMatches(teamIds, 1);

            Assert.IsNotNull(r);
            Assert.IsTrue(CheckMatchList(r[0], new long[] { 1, 14, 2, 13, 3, 12, 4, 11, 5, 10, 6, 9, 7, 8 } ));
            Assert.IsTrue(CheckMatchList(r[12], new long[] { 2, 1, 3, 14, 4, 13, 5, 12, 6, 11, 7, 10, 8, 9 }));
        }

        [TestMethod]
        public void CreateRoundRobinTest2Rounds()
        {
            var teamIds = new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };

            var r = LeaguePlanner.CreateRoundRobinMatches(teamIds, 2);

            Assert.IsNotNull(r);
            Assert.IsTrue(CheckMatchList(r[0], new long[] { 1, 14, 2, 13, 3, 12, 4, 11, 5, 10, 6, 9, 7, 8 }));
            Assert.IsTrue(CheckMatchList(r[12], new long[] { 2, 1, 3, 14, 4, 13, 5, 12, 6, 11, 7, 10, 8, 9 }));

            Assert.IsTrue(CheckMatchList(r[13], new long[] { 14, 1, 13, 2, 12, 3, 11, 4, 10, 5, 9, 6, 8, 7 }));
            Assert.IsTrue(CheckMatchList(r[25], new long[] { 1, 2, 14, 3, 13, 4, 12, 5, 11, 6, 10, 7, 9, 8 }));
        }

        [TestMethod]
        public void CreateRoundRobinTest3Rounds()
        {
            var teamIds = new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };

            var r = LeaguePlanner.CreateRoundRobinMatches(teamIds, 3);

            Assert.IsNotNull(r);
            Assert.IsTrue(CheckMatchList(r[0], new long[] { 1, 14, 2, 13, 3, 12, 4, 11, 5, 10, 6, 9, 7, 8 }));
            Assert.IsTrue(CheckMatchList(r[12], new long[] { 2, 1, 3, 14, 4, 13, 5, 12, 6, 11, 7, 10, 8, 9 }));

            Assert.IsTrue(CheckMatchList(r[13], new long[] { 14, 1, 13, 2, 12, 3, 11, 4, 10, 5, 9, 6, 8, 7 }));
            Assert.IsTrue(CheckMatchList(r[25], new long[] { 1, 2, 14, 3, 13, 4, 12, 5, 11, 6, 10, 7, 9, 8 }));

            Assert.IsTrue(CheckMatchList(r[26], new long[] { 1, 14, 2, 13, 3, 12, 4, 11, 5, 10, 6, 9, 7, 8 }));
            Assert.IsTrue(CheckMatchList(r[38], new long[] { 2, 1, 3, 14, 4, 13, 5, 12, 6, 11, 7, 10, 8, 9 }));

        }

        [TestMethod]
        public void CreateRoundRobinTest4Rounds()
        {
            var teamIds = new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 };

            var r = LeaguePlanner.CreateRoundRobinMatches(teamIds, 4);

            Assert.IsNotNull(r);
            Assert.IsTrue(CheckMatchList(r[0], new long[] { 1, 14, 2, 13, 3, 12, 4, 11, 5, 10, 6, 9, 7, 8 }));
            Assert.IsTrue(CheckMatchList(r[12], new long[] { 2, 1, 3, 14, 4, 13, 5, 12, 6, 11, 7, 10, 8, 9 }));

            Assert.IsTrue(CheckMatchList(r[13], new long[] { 14, 1, 13, 2, 12, 3, 11, 4, 10, 5, 9, 6, 8, 7 }));
            Assert.IsTrue(CheckMatchList(r[25], new long[] { 1, 2, 14, 3, 13, 4, 12, 5, 11, 6, 10, 7, 9, 8 }));

            Assert.IsTrue(CheckMatchList(r[26], new long[] { 1, 14, 2, 13, 3, 12, 4, 11, 5, 10, 6, 9, 7, 8 }));
            Assert.IsTrue(CheckMatchList(r[38], new long[] { 2, 1, 3, 14, 4, 13, 5, 12, 6, 11, 7, 10, 8, 9 }));

            Assert.IsTrue(CheckMatchList(r[39], new long[] { 14, 1, 13, 2, 12, 3, 11, 4, 10, 5, 9, 6, 8, 7 }));
            Assert.IsTrue(CheckMatchList(r[51], new long[] { 1, 2, 14, 3, 13, 4, 12, 5, 11, 6, 10, 7, 9, 8 }));
        }

        [TestMethod]
        public void CreateRoundRobinTestLocalityPairs()
        {
            var teamIds = new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };

            var r = LeaguePlanner.CreateRoundRobinMatches(teamIds, 1);

            Assert.IsNotNull(r);
            Assert.IsTrue(CheckMatchList(r[10], new long[] { 2, 1, 3, 12, 4, 11, 5, 10, 6, 9, 7, 8 }));
        }


        private static bool CheckMatchList(IList<Match> matchList, long[] matchPairs)
        {
            for (int i = 0; i < matchList.Count; ++i)
            {
                if (!CheckMatch(matchList[i], matchPairs[i * 2], matchPairs[i * 2 + 1])) throw new Exception($"No match: {i}");
            }

            return true;
        }

        private static bool CheckMatch(Match m, long homeId, long visitorId)
        {
            return (m.IdHomeTeam == homeId && m.IdVisitorTeam == visitorId);
        }


        [TestMethod]
        public void RoundRobinTest()
        {
            var list1 = new long[] { 1, 2, 3, 4, 5, 6, 7 };
            var list2 = new long[] { 14, 13, 12, 11, 10, 9, 8 };

            LeaguePlanner.ApplyRoundRobin(list1, list2);

            Assert.AreEqual(list1[0], 1);
            Assert.AreEqual(list1[1], 14);
            Assert.AreEqual(list1[6], 6);
            Assert.AreEqual(list2[0], 13);
            Assert.AreEqual(list2[1], 12);
            Assert.AreEqual(list2[6], 7);
        }

        [TestMethod]
        public void ShiftArrayTest()
        {
            var list1 = new long[] { 1, 2, 3, 4, 5, 6, 7 };

            var r = LeaguePlanner.ShiftArrayRight(list1, 14);

            Assert.AreEqual(list1[0], 1);
            Assert.AreEqual(list1[1], 14);
            Assert.AreEqual(list1[6], 6);
            Assert.AreEqual(r, 7);

            var list2 = new long[] { 14, 13, 12, 11, 10, 9, 8 };
            r = LeaguePlanner.ShiftArrayLeft(list2, r);

            Assert.AreEqual(list2[0], 13);
            Assert.AreEqual(list2[1], 12);
            Assert.AreEqual(list2[6], 7);
            Assert.AreEqual(r, 14);
        }

        [TestMethod]
        public void ShiftArrayTestMinimal()
        {
            var list = new long[] { 1, 2 };

            var r = LeaguePlanner.ShiftArrayRight(list, 4);

            Assert.AreEqual(list[0], 1);
            Assert.AreEqual(list[1], 4);
            Assert.AreEqual(r, 2);
        }

        [TestMethod]
        public void BasicTest()
        {
            // 8 teams, 2 hours, 2 fields with no previous matches

            var input = new CalendarGenInput
            {
                Type = (int)CalendarType.League,
                TeamIds = new long[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                WeekdaySlots = new DailySlot[][]
                {
                    new DailySlot[] { },    // Sunday
                    new DailySlot[] { },    // Monday
                    new DailySlot[] { },    // Tuesday
                    new DailySlot[] { },    // Wednesday
                    new DailySlot[]         // Thursday
                    {
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 10, 00, 00), EndTime = new DateTime(1, 1, 1, 11, 00, 00) },
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 16, 00, 00), EndTime = new DateTime(1, 1, 1, 17, 00, 00) }
                    },
                    new DailySlot[] { },    // Friday
                    new DailySlot[] { }     // Saturday
                },
                StartDate = new DateTime(2018, 01, 22),
                Group = new GroupCoords { IdTournament = 1, IdStage = 2, IdGroup = 3 },
                ForbiddenDays = new DateTime[]
                {
                    new DateTime(2018, 01, 25)
                },
                FieldIds = new long[] { 1001, 1002 },
                GameDuration = 60,
                IsPreview = true
            };

            // Could have some availablity set up here. This is the basic test, not there yet.
            var fields = new Field[]
            {
                new Field { Id = 1001, Name = "Campo1" },
                new Field { Id = 1002, Name = "Campo2" }
            };

            var result = LeaguePlanner.Calculate(input, fields, "es", null, null);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Days);
            Assert.AreEqual(result.Days.Count, 7);

            var r1 = result.Days[0];
            Assert.IsNotNull(r1);
            Assert.IsNotNull(r1.Matches);
            Assert.AreEqual(r1.Matches.Count, 4);

            var m1 = r1.Matches[0];
            Assert.AreEqual(new DateTime(2018, 01, 25, 10, 00, 00), m1.StartTime);
        }



        [TestMethod]
        public void KnockoutBasicTest()
        {
            var teamIds = new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

            var r = KnockoutPlanner.CreateRounds(teamIds, new GroupCoords { IdTournament = 1, IdStage = 2, IdGroup = 3 } );

            Assert.IsNotNull(r);
            Assert.IsTrue(CheckMatchList(r[0], new long[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 }));
        }

        [TestMethod]
        public void KnockoutPowerOfTwoTest()
        {
            try
            {
                var fields = new Field[]
                {
                    new Field { Id = 1001, Name = "Campo1" },
                    new Field { Id = 1002, Name = "Campo2" }
                };

                var input = GetKnockOutInput(14);
                var result = KnockoutPlanner.Calculate(input, fields, "es", null);

                Assert.Fail("Should raise exception, numTeams not power of 2");
            }
            catch (PlannerException ex)
            {
                if (ex.Message == "Error.NotPowerOfTwo") return;
            }

            Assert.Fail("Expected exception not thrown");
        }

        [TestMethod]
        public void KnockoutSchedule()
        {
            var fields = new Field[]
            {
                new Field { Id = 1001, Name = "Campo1" },
                new Field { Id = 1002, Name = "Campo2" }
            };

            var input = GetKnockOutInput(8);
            var result = KnockoutPlanner.Calculate(input, fields, "es", null);

            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Days);
            Assert.AreEqual(result.Days.Count, 3);
        }

        private CalendarGenInput GetKnockOutInput(int numTeams)
        {
            var teams = new long[numTeams];
            for (int i = 0; i < numTeams; ++i) teams[i] = i + 1;

            var input = new CalendarGenInput
            {
                Type = (int)CalendarType.Knockout,
                TeamIds = teams,
                WeekdaySlots = new DailySlot[][]
                {
                    new DailySlot[] { },    // Sunday
                    new DailySlot[] { },    // Monday
                    new DailySlot[] { },    // Tuesday
                    new DailySlot[] { },    // Wednesday
                    new DailySlot[]         // Thursday
                    {
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 10, 00, 00), EndTime = new DateTime(1, 1, 1, 11, 00, 00) },
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 16, 00, 00), EndTime = new DateTime(1, 1, 1, 17, 00, 00) }
                    },
                    new DailySlot[] { },    // Friday
                    new DailySlot[] { }     // Saturday
                },
                StartDate = new DateTime(2018, 01, 22),
                ForbiddenDays = new DateTime[]
                {
                    new DateTime(2018, 01, 25)
                },
                FieldIds = new long[] { 1001, 1002 },
                GameDuration = 60,
                IsPreview = true,
                Group = new GroupCoords { IdTournament = 1, IdStage = 10, IdGroup = 100 }
            };

            return input;
        }

        [TestMethod]
        public void KnockoutRoundNames()
        {
            Assert.AreEqual(KnockoutPlanner.GetRoundName(1, 4, null), "Octavos de final");
            Assert.AreEqual(KnockoutPlanner.GetRoundName(2, 4, null), "Cuartos de final");
            Assert.AreEqual(KnockoutPlanner.GetRoundName(3, 4, null), "Semifinales");
            Assert.AreEqual(KnockoutPlanner.GetRoundName(4, 4, null), "Final");
        }


        [TestMethod]
        public void ForbiddenDaysIsWeekInForbiddenDays()
        {
            var startDate = new DateTime(2018, 10, 03);     // Monday
            var weekdaySlots = new DailySlot[][]
                {
                    new DailySlot[]    // Sunday
                    {
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 10, 00, 00), EndTime = new DateTime(1, 1, 1, 11, 00, 00) },
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 16, 00, 00), EndTime = new DateTime(1, 1, 1, 17, 00, 00) }
                    },
                    new DailySlot[] { },    // Monday
                    new DailySlot[] { },    // Tuesday
                    new DailySlot[] { },    // Wednesday
                    new DailySlot[]         // Thursday
                    {
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 10, 00, 00), EndTime = new DateTime(1, 1, 1, 11, 00, 00) },
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 16, 00, 00), EndTime = new DateTime(1, 1, 1, 17, 00, 00) }
                    },
                    new DailySlot[] { },    // Friday
                    new DailySlot[] { }     // Saturday
                };

            Assert.IsTrue(PlannerScheduler.IsWeekInForbiddenDays(startDate, weekdaySlots, new DateTime[] { new DateTime(2018, 10, 4) }));
            Assert.IsTrue(PlannerScheduler.IsWeekInForbiddenDays(startDate, weekdaySlots, new DateTime[] { new DateTime(2018, 10, 9) }));
            
        }
    }
}
