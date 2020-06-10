using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using webapi.Controllers;
using webapi.Models.Db;

namespace webapi.test
{
    [TestClass]
    public class AutoSanctionDispatcher_CycleTests
    {
        [TestMethod]
        public void GetCycleIterationForNumCards1()
        {
            var cc = GetCycleConfigs(5, 4, 3);

            ///   Cycles 5, 4, 3:  4 cards = 0, 5 cards = 1, 6 cards = 1, 8 cards = 1, 9 cards = 2, 10 cards = 2, 11 cards = 2, 12 cards = 3
            Check(cc, 4, 0, 0);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards2()
        {
            var cc = GetCycleConfigs(5, 4, 3);

            ///   Cycles 5, 4, 3:  4 cards = 0, 5 cards = 1, 6 cards = 1, 8 cards = 1, 9 cards = 2, 10 cards = 2, 11 cards = 2, 12 cards = 3
            Check(cc, 5, 1, 0);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards3()
        {
            var cc = GetCycleConfigs(5, 4, 3);

            ///   Cycles 5, 4, 3:  4 cards = 0, 5 cards = 1, 6 cards = 1, 8 cards = 1, 9 cards = 2, 10 cards = 2, 11 cards = 2, 12 cards = 3
            Check(cc, 6, 1, 1);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards4()
        {
            var cc = GetCycleConfigs(5, 4, 3);

            ///   Cycles 5, 4, 3:  4 cards = 0, 5 cards = 1, 6 cards = 1, 8 cards = 1, 9 cards = 2, 10 cards = 2, 11 cards = 2, 12 cards = 3
            Check(cc, 8, 1, 1);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards5()
        {
            var cc = GetCycleConfigs(5, 4, 3);

            ///   Cycles 5, 4, 3:  4 cards = 0, 5 cards = 1, 6 cards = 1, 8 cards = 1, 9 cards = 2, 10 cards = 2, 11 cards = 2, 12 cards = 3
            Check(cc, 9, 2, 1);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards6()
        {
            var cc = GetCycleConfigs(5, 4, 3);

            ///   Cycles 5, 4, 3:  4 cards = 0, 5 cards = 1, 6 cards = 1, 8 cards = 1, 9 cards = 2, 10 cards = 2, 11 cards = 2, 12 cards = 3
            Check(cc, 10, 2, 2);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards7()
        {
            var cc = GetCycleConfigs(5, 4, 3);

            ///   Cycles 5, 4, 3:  4 cards = 0, 5 cards = 1, 6 cards = 1, 8 cards = 1, 9 cards = 2, 10 cards = 2, 11 cards = 2, 12 cards = 3
            Check(cc, 11, 2, 2);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards8()
        {
            var cc = GetCycleConfigs(5, 4, 3);

            ///   Cycles 5, 4, 3:  4 cards = 0, 5 cards = 1, 6 cards = 1, 8 cards = 1, 9 cards = 2, 10 cards = 2, 11 cards = 2, 12 cards = 3
            Check(cc, 12, 3, 2);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards12()
        {
            var cycleConfigs = GetCycleConfigs(5, 4, 3);

            var result = AutoSanctionDispatcher.GetCycleIterationForNumCards(cycleConfigs, 0, out AutoSanctionCycleConfig rule);

            Assert.AreEqual(0, result);
            Assert.AreEqual(cycleConfigs[0], rule);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards13()
        {
            var cc = new AutoSanctionCycleConfig[0];

            var result = AutoSanctionDispatcher.GetCycleIterationForNumCards(cc, 0, out AutoSanctionCycleConfig rule);

            Assert.AreEqual(-1, result);
            Assert.AreEqual(null, rule);
        }


        [TestMethod]
        public void GetCycleIterationForNumCards_LastCycle1()
        {
            // Check that cards beyond cycle are also detected. This is right before the boundary.

            var cc = GetCycleConfigs(5);

            Check(cc, 5, 1, 0);
        }


        [TestMethod]
        public void GetCycleIterationForNumCards_LastCycle2()
        {
            // Check that cards beyond cycle are also detected. This is right before the boundary.

            var cc = GetCycleConfigs(5);

            Check(cc, 9, 1, 0);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards_LastCycle3()
        {
            // Check that cards beyond cycle are also detected, this is exactly in the boundary. 

            var cc = GetCycleConfigs(5);

            Check(cc, 10, 2, 0);
        }

        [TestMethod]
        public void GetCycleIterationForNumCards_LastCycle4()
        {
            // Check that cards beyond cycle are also detected. 

            var cc = GetCycleConfigs(5);

            Check(cc, 11, 2, 0);
        }


        // __ Helpers _________________________________________________________


        public static AutoSanctionCycleConfig[] GetCycleConfigs(params int[] numCards)
        {
            var result = new AutoSanctionCycleConfig[numCards.Length];

            for (int i = 0; i < numCards.Length; ++i)
            {
                result[i] = new AutoSanctionCycleConfig { Id = 10 + i, Name = "Cycle " + (i + 1), NumYellowCards = numCards[i], Penalty = new PenaltyConfig { Text = "Cycle " + (i + 1)} };
            }

            return result;
        }

        private void Check(AutoSanctionCycleConfig[] cycles, int numCards, int expectedIteration, int expectedRuleIndex)
        {
            var resultIteration = AutoSanctionDispatcher.GetCycleIterationForNumCards(cycles, numCards, out AutoSanctionCycleConfig rule);

            Assert.AreEqual(expectedIteration, resultIteration);
            Assert.AreEqual(cycles[expectedRuleIndex], rule);
        }


        // __ GetYellowCardsToSubtractForCardCombo ____________________________



        [TestMethod]
        public void GetYellowCardsToSubtractForCardCombo00()
        {
            var combo = new AutoSanctionCardConfig { AddYellowCards = 0, Card1Type = 0, Card2Type = 0 };
            var result = AutoSanctionDispatcher.GetYellowCardsToSubtractForCardCombo(combo);
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void GetYellowCardsToSubtractForCardCombo01()
        {
            var combo = new AutoSanctionCardConfig { AddYellowCards = 1, Card1Type = 0, Card2Type = 0 };
            var result = AutoSanctionDispatcher.GetYellowCardsToSubtractForCardCombo(combo);
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void GetYellowCardsToSubtractForCardCombo02()
        {
            var combo = new AutoSanctionCardConfig { AddYellowCards = 2, Card1Type = 0, Card2Type = 0 };
            var result = AutoSanctionDispatcher.GetYellowCardsToSubtractForCardCombo(combo);
            Assert.AreEqual(0, result);
        }



        [TestMethod]
        public void GetYellowCardsToSubtractForCardCombo10()
        {
            var combo = new AutoSanctionCardConfig { AddYellowCards = 0, Card1Type = 1, Card2Type = 0 };
            var result = AutoSanctionDispatcher.GetYellowCardsToSubtractForCardCombo(combo);
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void GetYellowCardsToSubtractForCardCombo11()
        {
            var combo = new AutoSanctionCardConfig { AddYellowCards = 1, Card1Type = 1, Card2Type = 0 };
            var result = AutoSanctionDispatcher.GetYellowCardsToSubtractForCardCombo(combo);
            Assert.AreEqual(0, result);
        }

        [TestMethod]
        public void GetYellowCardsToSubtractForCardCombo12()
        {
            var combo = new AutoSanctionCardConfig { AddYellowCards = 2, Card1Type = 1, Card2Type = 0 };
            var result = AutoSanctionDispatcher.GetYellowCardsToSubtractForCardCombo(combo);
            Assert.AreEqual(0, result);
        }


        [TestMethod]
        public void GetYellowCardsToSubtractForCardCombo20()
        {
            var combo = new AutoSanctionCardConfig { AddYellowCards = 0, Card1Type = 1, Card2Type = 1 };
            var result = AutoSanctionDispatcher.GetYellowCardsToSubtractForCardCombo(combo);
            Assert.AreEqual(2, result);
        }

        [TestMethod]
        public void GetYellowCardsToSubtractForCardCombo21()
        {
            var combo = new AutoSanctionCardConfig { AddYellowCards = 1, Card1Type = 1, Card2Type = 1 };
            var result = AutoSanctionDispatcher.GetYellowCardsToSubtractForCardCombo(combo);
            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void GetYellowCardsToSubtractForCardCombo22()
        {
            var combo = new AutoSanctionCardConfig { AddYellowCards = 2, Card1Type = 1, Card2Type = 1 };
            var result = AutoSanctionDispatcher.GetYellowCardsToSubtractForCardCombo(combo);
            Assert.AreEqual(0, result);
        }
    }
}
