using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using webapi.Models.Db;

namespace webapi.test
{
    [TestClass]
    public class AutoSanctionDispatcher_ComboMatchesEventsTests
    {
        [TestMethod]
        public void ComboMatchesEvents_EqualCards()
        {
            // Ensure two yellow cards are matched

            var events = new MatchEvent[]               
            {
                // Two yellow cards
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
            };
 
            // Two yellow cards generate a red card
            var combo = new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 1, Penalty = new PenaltyConfig { Type1 = 2 } };

            var result = AutoSanctionDispatcher.ComboAppliesToMatchEvents(combo, events, out MatchEvent ev1, out MatchEvent ev2);
            Assert.IsTrue(result);

        }

        [TestMethod]
        public void ComboMatchesEvents_DirectMatch()
        {
            // Ensure yellow + blue matches yellow + blue combo

            var events = new MatchEvent[]
            {
                // Yellow + Blue
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 101, MatchMinute = 20 },
            };

            // Yellow + Blue generate red card
            var combo = new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 3, Penalty = new PenaltyConfig { Type1 = 2 } };

            var result = AutoSanctionDispatcher.ComboAppliesToMatchEvents(combo, events, out MatchEvent ev1, out MatchEvent ev2);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ComboMatchesEvents_InverseMatch()
        {
            // Ensure blue + yellow matches yellow + blue combo

            var events = new MatchEvent[]
            {
                // Blue + yellow
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
            };

            // Yellow + Blue generate red card
            var combo = new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 3, Penalty = new PenaltyConfig { Type1 = 2 } };

            var result = AutoSanctionDispatcher.ComboAppliesToMatchEvents(combo, events, out MatchEvent ev1, out MatchEvent ev2);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ComboMatchesEvents_Larger()
        {
            // Ensure yellow, yellow, blue, yellow matches blue alone

            var events = new MatchEvent[]
            {
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
            };

            // Blue alone generates red card
            var combo = new AutoSanctionCardConfig { Card1Type = 3, Card2Type = 0, Penalty = new PenaltyConfig { Type1 = 2 } };

            var result = AutoSanctionDispatcher.ComboAppliesToMatchEvents(combo, events, out MatchEvent ev1, out MatchEvent ev2);
            Assert.IsTrue(result);
        }





        [TestMethod]
        public void ComboMatchesEvents_Negative0()
        {
            // Ensure blue doesn't match yellow + yellow + red

            var events = new MatchEvent[]
            {
                // Blue + yellow
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
                new MatchEvent { Id = 13, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
            };

            // Blue generates red card
            var combo = new AutoSanctionCardConfig { Card1Type = 3, Card2Type = 0, Penalty = new PenaltyConfig { Type1 = 2 } };

            var result = AutoSanctionDispatcher.ComboAppliesToMatchEvents(combo, events, out MatchEvent ev1, out MatchEvent ev2);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ComboMatchesEvents_Negative1()
        {
            // Ensure blue + yellow doesn't match yellow + yellow

            var events = new MatchEvent[]
            {
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card3, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
            };

            // Yellow + Yellow generate red card
            var combo = new AutoSanctionCardConfig { Card1Type = 1, Card2Type = 1, Penalty = new PenaltyConfig { Type1 = 2 } };

            var result = AutoSanctionDispatcher.ComboAppliesToMatchEvents(combo, events, out MatchEvent ev1, out MatchEvent ev2);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void ComboMatchesEvents_Negative2()
        {
            // Ensure yellow + yellow doesn't match blue alone

            var events = new MatchEvent[]
            {
                new MatchEvent { Id = 11, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 8 },
                new MatchEvent { Id = 12, IdMatch = 1001, Type = (int)MatchEventType.Card1, IdPlayer = 101, MatchMinute = 20 },
            };

            // Blue alone generates red card
            var combo = new AutoSanctionCardConfig { Card1Type = 3, Card2Type = 0, Penalty = new PenaltyConfig { Type1 = 2 } };

            var result = AutoSanctionDispatcher.ComboAppliesToMatchEvents(combo, events, out MatchEvent ev1, out MatchEvent ev2);
            Assert.IsFalse(result);
        }


        [TestMethod]
        public void DeserializeCardComboConfig()
        {
            //var input = "{\"cards\":[{\"card1Type\":1,\"card2Type\":1,\"s1\":\"\",\"penalty\":{\"text\":\"Tarjeta roja por 2 amarillas\",\"type1\":2,\"type2\":\"2\",\"type3\":0},\"s2\":\"\",\"addYellowCards\":1,\"card2type\":0,\"id\":1545848047566},{\"card1Type\":3,\"card2Type\":0,\"s1\":\"\",\"penalty\":{\"text\":\"5 partidos por tarjeta azul\",\"type1\":0,\"type2\":\"5\",\"type3\":0},\"s2\":\"\",\"addYellowCards\":0,\"card2type\":0,\"id\":1545848072412}],\"cycles\":[]}";
            //var input = @"{""cards"":[{""card1Type"":1,""card2Type"":1,""penalty"":{""text"":""Tarjeta roja por 2 amarillas"",""type1"":2,""type2"":""2"",""type3"":0},""addYellowCards"":1,""id"":1545848047566},{""card1Type"":3,""card2Type"":0,""s1"":"""",""penalty"":{""text"":""5 partidos por tarjeta azul"",""type1"":0,""type2"":""5"",""type3"":0},""s2"":"""",""addYellowCards"":0,""card2type"":0,""id"":1545848072412}],""cycles"":[]}";
            var input = @"
                {
                  ""cards"": [
                    {
                      ""card1Type"": 1,
                      ""card2Type"": 1,
                      ""s1"": """",
                      ""penalty"": {
                        ""text"": ""Roja por dos amarillas"",
                        ""type1"": 2,
                        ""type2"": ""0"",
                        ""type3"": 0
                      },
                      ""s2"": """",
                      ""addYellowCards"": 1,
                      ""id"": 1545862775520
                    },
                    {
                      ""card1Type"": 2,
                      ""card2Type"": 0,
                      ""s1"": """",
                      ""penalty"": {
                        ""text"": ""2 partidos de sanción por tarjeta roja"",
                        ""type1"": 0,
                        ""type2"": ""2"",
                        ""type3"": 0
                      },
                      ""s2"": """",
                      ""addYellowCards"": 0,
                      ""id"": 1545862802034
                    },
                    {
                      ""card1Type"": 3,
                      ""card2Type"": 0,
                      ""s1"": """",
                      ""penalty"": {
                        ""text"": ""5 partidos de sanción por tarjeta azul"",
                        ""type1"": 0,
                        ""type2"": ""5"",
                        ""type3"": 0
                      },
                      ""s2"": """",
                      ""addYellowCards"": 0,
                      ""id"": 1545862829857
                    }
                  ],
                  ""cycles"": []
                }
                ";

            var config = AutoSanctionDispatcher.ParseJsonConfig(input);

            Assert.AreEqual(1, config.Cards[0].Card2Type);
        }
    }
}
