using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using webapi.Models.Db;

namespace webapi.test
{
    [TestClass]
    public class AutoSanctionDispatcher_GetCardCombosForMatchEventsTests
    {
        [TestMethod]
        public void ComboMatchesEvents_Several()
        {
            var events = new MatchEvent[]               
            {
                // Two yellow cards
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 13, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 14, IdMatch = 1001, Type = (int)MatchEventType.Card2, IdPlayer = 101, MatchMinute = 30 },
            };

            var combos = new AutoSanctionCardConfig[]
            {
                new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 1, Penalty = new PenaltyConfig { Type1 = 2 } },
                new AutoSanctionCardConfig { Card1Type = 2, Card2Type = 0, Penalty = new PenaltyConfig { Type1 = 3 } },
            };

            var result = AutoSanctionDispatcher.GetCardCombosForMatchEvents(combos, events);

            Assert.AreEqual(2, result.Count());
            Assert.AreEqual(combos[0], result[0]);
            Assert.AreEqual(combos[1], result[1]);
        }

        [TestMethod]
        public void ComboMatchesEvents_Several2()
        {
            var events = new MatchEvent[]
            {
                // Two yellow cards
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 13, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 14, IdMatch = 1001, Type = (int)MatchEventType.Card2, IdPlayer = 101, MatchMinute = 30 },
            };

            var combos = new AutoSanctionCardConfig[]
            {
                new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 1, Penalty = new PenaltyConfig { Type1 = 2 } },
                new AutoSanctionCardConfig { Card1Type = 2, Card2Type = 3, Penalty = new PenaltyConfig { Type1 = 5 } },
            };

            var result = AutoSanctionDispatcher.GetCardCombosForMatchEvents(combos, events);

            Assert.AreEqual(2, result.Count());
            Assert.AreEqual(combos[0], result[0]);
            Assert.AreEqual(combos[1], result[1]);
        }


        [TestMethod]
        public void ComboMatchesEvents_Single()
        {
            var events = new MatchEvent[]
            {
                // Two yellow cards
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 13, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 14, IdMatch = 1001, Type = (int)MatchEventType.Card2, IdPlayer = 101, MatchMinute = 30 },
            };

            var combos = new AutoSanctionCardConfig[]
            {
                new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 1, Penalty = new PenaltyConfig { Type1 = 2 } },
            };

            var result = AutoSanctionDispatcher.GetCardCombosForMatchEvents(combos, events);

            Assert.AreEqual(1, result.Count());
            Assert.AreEqual(combos[0], result[0]);
        }

        [TestMethod]
        public void ComboMatchesEvents_SeveralSingle2()
        {
            var events = new MatchEvent[]
            {
                // Two yellow cards
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 13, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 14, IdMatch = 1001, Type = (int)MatchEventType.Card2, IdPlayer = 101, MatchMinute = 30 },
            };

            var combos = new AutoSanctionCardConfig[]
            {
                new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 2, Penalty = new PenaltyConfig { Type1 = 3 } },
            };

            var result = AutoSanctionDispatcher.GetCardCombosForMatchEvents(combos, events);

            Assert.AreEqual(1, result.Count());
            Assert.AreEqual(combos[0], result[0]);
        }


        // ____________________________________________________________________


        [TestMethod]
        public void GroupPlayerEvents12()
        {
            var events = new MatchEvent[]
            {
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 13, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 102, MatchMinute = 20 },
                new MatchEvent { Id = 14, IdMatch = 1001, Type = (int)MatchEventType.Card2, IdPlayer = 102, MatchMinute = 30 },
            };

            var result = AutoSanctionDispatcher.GroupPlayerEvents(events);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(1, result[101].Count);
            Assert.AreEqual(2, result[102].Count);

            Assert.AreEqual(events[0], result[101][0]);
            Assert.AreEqual(events[1], result[102][0]);
            Assert.AreEqual(events[2], result[102][1]);
        }


        [TestMethod]
        public void GroupPlayerEvents22()
        {
            var events = new MatchEvent[]
            {
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 13, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 102, MatchMinute = 20 },
                new MatchEvent { Id = 14, IdMatch = 1001, Type = (int)MatchEventType.Card2, IdPlayer = 102, MatchMinute = 30 },
            };

            var result = AutoSanctionDispatcher.GroupPlayerEvents(events);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(2, result[101].Count);
            Assert.AreEqual(2, result[102].Count);

            Assert.AreEqual(events[0], result[101][0]);
            Assert.AreEqual(events[1], result[101][1]);
            Assert.AreEqual(events[2], result[102][0]);
            Assert.AreEqual(events[3], result[102][1]);
        }

        [TestMethod]
        public void GroupPlayerEvents_Empty()
        {
            var events = new MatchEvent[]
            {
            };

            var result = AutoSanctionDispatcher.GroupPlayerEvents(events);

            Assert.AreEqual(0, result.Count);
        }


        // ____________________________________________________________________


        [TestMethod]
        public void GetCardComboNewCards_Single()
        {
            var events = new MatchEvent[]
            {
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 15, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 30 }
        };

            var combos = new AutoSanctionCardConfig[]
            {
                new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 1, Penalty = new PenaltyConfig { Type1 = 2 }, AddYellowCards = 1 },
                new AutoSanctionCardConfig { Card1Type = 2, Card2Type = 3, Penalty = new PenaltyConfig { Type1 = 5 } },
            };

            var newEvent = events[1];

            var result = AutoSanctionDispatcher.GetCardCombosNewCards(events, combos, newEvent);

            Assert.AreEqual(2, result.Count());

            var resultEvents = result.ToList();
            Assert.AreEqual((int)MatchEventType.Card2, resultEvents[0].Type);

            var ev2 = resultEvents[1];
            Assert.AreEqual((int)MatchEventType.AddToPdrData1, ev2.Type);
            Assert.AreEqual(-1, ev2.IntData1);
        }


        [TestMethod]
        public void GetCardComboNewCards_OnlyLast()
        {
            // Several events already happened to same player, yellow arrives.
            // Check that a new event is returned with a red card, and only that. 

            var events = new MatchEvent[]
            {   
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 13, IdMatch = 1001, Type = (int)MatchEventType.Card4, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 14, IdMatch = 1001, Type = (int)MatchEventType.Card5, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 15, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 30 }
            };

            var combos = new AutoSanctionCardConfig[]
            {
                new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 1, Penalty = new PenaltyConfig { Type1 = 2 }, AddYellowCards = 0 },
                new AutoSanctionCardConfig { Card1Type = 3, Card2Type = 0, Penalty = new PenaltyConfig { Type1 = 5 } },
            };

            var newEvent = events[4];

            var result = AutoSanctionDispatcher.GetCardCombosNewCards(events, combos, newEvent);

            Assert.AreEqual(2, result.Count());

            var resultEvents = result.ToList();

            Assert.AreEqual((int)MatchEventType.Card2, resultEvents[0].Type);

            var ev2 = resultEvents[1];
            Assert.AreEqual((int)MatchEventType.AddToPdrData1, ev2.Type);
            Assert.AreEqual(-2, ev2.IntData1);
        }
    }
}
