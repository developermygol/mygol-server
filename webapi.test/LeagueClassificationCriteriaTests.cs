using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using webapi.Controllers;
using webapi.Models.Db;

namespace webapi.test
{
    [TestClass]
    public class LeagueClassificationCriteriaTests
    {
        [TestMethod]
        public void BasicCriteria()
        {
            // Test points sorter

            var table = new TeamDayResult[]
            {
                new TeamDayResult(2, 2, 0, 0, 5, 0, 5, 6) { IdStage = 1, IdTeam = 1 },
                new TeamDayResult(2, 2, 0, 0, 4, 0, 4, 6) { IdStage = 1, IdTeam = 2 },
                new TeamDayResult(2, 2, 0, 0, 3, 0, 3, 6) { IdStage = 1, IdTeam = 3 },
            };

            var classification = LeagueClassification.SortClassification(table, new int[] { 0, 1, 2 });

            var c = classification.ToArray();
            Assert.AreEqual(1, c[0].IdTeam);
            Assert.AreEqual(2, c[1].IdTeam);
            Assert.AreEqual(3, c[2].IdTeam);
        }

        [TestMethod]
        public void BasicCriteria2()
        {
            // Test tournamentpoints sorter

            var table = new TeamDayResult[]
            {
                new TeamDayResult(2, 2, 0, 0, 5, 0, 5, 3) { IdStage = 1, IdTeam = 1 },
                new TeamDayResult(2, 2, 0, 0, 4, 0, 4, 6) { IdStage = 1, IdTeam = 2 },
                new TeamDayResult(2, 2, 0, 0, 3, 0, 3, 6) { IdStage = 1, IdTeam = 3 },
            };

            var classification = LeagueClassification.SortClassification(table, new int[] { 0, 1, 2 });

            var c = classification.ToArray();
            Assert.AreEqual(1, c[2].IdTeam);
            Assert.AreEqual(2, c[0].IdTeam);
            Assert.AreEqual(3, c[1].IdTeam);
        }

        [TestMethod]
        public void BasicCriteria3()
        {
            // Tames gameswon sorter

            var table = new TeamDayResult[]
            {
                new TeamDayResult(2, 1, 0, 0, 4, 0, 4, 6) { IdStage = 1, IdTeam = 1 },
                new TeamDayResult(2, 3, 0, 0, 4, 0, 4, 6) { IdStage = 1, IdTeam = 2 },
                new TeamDayResult(2, 2, 0, 0, 4, 0, 4, 6) { IdStage = 1, IdTeam = 3 },
            };

            var classification = LeagueClassification.SortClassification(table, new int[] { 0, 1, 2 });

            var c = classification.ToArray();
            Assert.AreEqual(2, c[0].IdTeam);
            Assert.AreEqual(3, c[1].IdTeam);
            Assert.AreEqual(1, c[2].IdTeam);
        }


        [TestMethod]
        public void DirectConfrontation1Match()
        {
            var matches = new Match[]
            {
                new Match { IdHomeTeam = 1, IdVisitorTeam = 2, HomeScore = 30, VisitorScore = 1 },
                new Match { IdHomeTeam = 3, IdVisitorTeam = 2, HomeScore = 20, VisitorScore = 0 },
                new Match { IdHomeTeam = 1, IdVisitorTeam = 3, HomeScore = 1, VisitorScore = 47 },
            };

            var table = new TeamDayResult[]
            {
                new TeamDayResult(2, 0, 0, 0, 0, 0, 0, 6) { IdStage = 1, IdTeam = 1 },
                new TeamDayResult(2, 0, 0, 0, 0, 0, 0, 6) { IdStage = 1, IdTeam = 2 },
                new TeamDayResult(2, 0, 0, 0, 0, 0, 0, 6) { IdStage = 1, IdTeam = 3 },
            };

            var matchFilter = new MatchFilter(matches);

            var classification = LeagueClassification.SortClassification(table, new int[] { 0, 3 }, matchFilter.GetMatchesForTeams);

            var c = classification.ToArray();
            Assert.AreEqual(3, c[0].IdTeam);
            Assert.AreEqual(1, c[1].IdTeam);
            Assert.AreEqual(2, c[2].IdTeam);
        }
    }
}
