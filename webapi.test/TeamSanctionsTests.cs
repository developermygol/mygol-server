using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using webapi.Controllers;
using webapi.Models.Db;

namespace webapi.test
{
    [TestClass]
    public class TeamSanctionsTests
    {
       
        [TestMethod]
        public void GetLostMatchEvents_HomeDraw()
        {
            TestMatch(new Match { IdHomeTeam = 1, HomeScore = 1, VisitorScore = 1 }, -1, 2, 0, 1, -1, -1, 1, 0);
        }

        [TestMethod]
        public void GetLostMatchEvents_HomeWin()
        {
            TestMatch(new Match { IdHomeTeam = 1, HomeScore = 2, VisitorScore = 1 }, -3, 3, -1, 1, 0, 0, 1, -1);
        }

        [TestMethod]
        public void GetLostMatchEvents_HomeLose()
        {
            TestMatchNoEvents(new Match { IdHomeTeam = 1, HomeScore = 1, VisitorScore = 2 });
        }

        [TestMethod]
        public void GetLostMatchEvents_VisitorDraw()
        {
            TestMatch(new Match { IdVisitorTeam = 1, HomeScore = 1, VisitorScore = 1 }, 2, -1, 1, 0, -1, -1, 0, 1);
        }

        [TestMethod]
        public void GetLostMatchEvents_VisitorWin()
        {
            TestMatch(new Match { IdVisitorTeam = 1, HomeScore = 1, VisitorScore = 2 }, 3, -3, 1, -1, 0, 0, -1, 1);
        }

        [TestMethod]
        public void GetLostMatchEvents_VisitorLose()
        {
            TestMatchNoEvents(new Match { IdVisitorTeam = 1, HomeScore = 2, VisitorScore = 1 });
        }



        private void TestMatch(Match m, int ev1Points, int ev2Points, int ev1Won, int ev2Won, int ev1Draw, int ev2Draw, int ev1Lost, int ev2Lost)
        {
            var result = SanctionsController.GetLostMatchEvents(m, 1);

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count());

            var rr = result.ToList();
            var ev1 = rr[0];
            var ev2 = rr[1];

            Assert.AreEqual((int)MatchEventType.ChangeTeamStats, ev1.Type);
            Assert.AreEqual((int)MatchEventType.ChangeTeamStats, ev2.Type);

            Assert.IsTrue(ev1.IdTeam == m.IdHomeTeam);
            Assert.IsTrue(ev2.IdTeam == m.IdVisitorTeam);
            Assert.IsTrue(ev1Points == ev1.IntData1 && ev2Points == ev2.IntData1);
            Assert.IsTrue(ev1Won == ev1.IntData2 && ev2Won == ev2.IntData2);
            Assert.IsTrue(ev1Draw == ev1.IntData3 && ev2Draw == ev2.IntData3);
            Assert.IsTrue(ev1Lost == ev1.IntData4 && ev2Lost == ev2.IntData4);
        }

        private void TestMatchNoEvents(Match m)
        {
            var result = SanctionsController.GetLostMatchEvents(m, 1);

            Assert.IsNull(result);
        }

    }
}
