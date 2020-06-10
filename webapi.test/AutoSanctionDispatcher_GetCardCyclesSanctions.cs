using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using webapi.Models.Db;

namespace webapi.test
{
    [TestClass]
    public class AutoSanctionDispatcher_GetCardCyclesSanctions
    {
        [TestMethod]
        public void GetCardCyclesSanctions1()
        {
            var cc = AutoSanctionDispatcher_CycleTests.GetCycleConfigs(5, 4, 3);

            var result = Check(cc, 3, 5);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());

            var sanction = result.First();
            Assert.IsNotNull(sanction);
            Assert.AreEqual("Cycle 1", sanction.InitialContent);
        }

        [TestMethod]
        public void GetCardCyclesSanctions2()
        {
            var cc = AutoSanctionDispatcher_CycleTests.GetCycleConfigs(5, 4, 3);

            var result = Check(cc, 5, 5);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public void GetCardCyclesSanctions3()
        {
            var cc = AutoSanctionDispatcher_CycleTests.GetCycleConfigs(5, 4, 3);

            var result = Check(cc, 6, 8);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count());
        }


        [TestMethod]
        public void GetCardCyclesSanctions4()
        {
            var cc = AutoSanctionDispatcher_CycleTests.GetCycleConfigs(5, 4, 3);

            var result = Check(cc, 8, 9);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count());

            var sanction = result.First();
            Assert.IsNotNull(sanction);
            Assert.AreEqual("Cycle 2", sanction.InitialContent);
        }


        [TestMethod]
        public void GetCardCyclesSanctionsNull1()
        {
            var cc = AutoSanctionDispatcher_CycleTests.GetCycleConfigs(5, 4, 3);
            var result = AutoSanctionDispatcher.GetCardCyclesSanctions(null, null, cc, new Match());

            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetCardCyclesSanctionsNull2()
        {
            var cc = AutoSanctionDispatcher_CycleTests.GetCycleConfigs(5, 4, 3);

            var previousAccumulated = new PlayerDayResult[]
            {
                new PlayerDayResult { IdPlayer = 10, IdTeam = 1, Data1 = 1 },
            };
            
            var previous = previousAccumulated.ToDictionary(pdr => pdr.IdPlayer);

            var result = AutoSanctionDispatcher.GetCardCyclesSanctions(previous, null, cc, new Match());
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetCardCyclesSanctionsNull3()
        {
            var cc = AutoSanctionDispatcher_CycleTests.GetCycleConfigs(5, 4, 3);

            var currentAccumulated = new PlayerDayResult[]
            {
                new PlayerDayResult { IdPlayer = 10, IdTeam = 1, Data1 = 1 },
            };

            var current = currentAccumulated.ToDictionary(pdr => pdr.IdPlayer);

            var result = AutoSanctionDispatcher.GetCardCyclesSanctions(current, null, cc, new Match());
            Assert.IsNull(result);
        }




        private IEnumerable<Sanction> Check(AutoSanctionCycleConfig[] cc, int previousData1, int currentData1)
        {
            var currentAccumulated = new PlayerDayResult[]
            {
                new PlayerDayResult { IdPlayer = 10, IdTeam = 1, Data1 = currentData1 },
            };

            var previousAccumulated = new PlayerDayResult[]
            {
                new PlayerDayResult { IdPlayer = 10, IdTeam = 1, Data1 = previousData1 },
            };

            var current = currentAccumulated.ToDictionary(pdr => pdr.IdPlayer);
            var previous = previousAccumulated.ToDictionary(pdr => pdr.IdPlayer);

            var match = new Match();

            var result = AutoSanctionDispatcher.GetCardCyclesSanctions(current, previous, cc, match);
            return result;
        }

    }
}
