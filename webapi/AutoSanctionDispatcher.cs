using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;
using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using webapi.Controllers;

namespace webapi
{
    public class AutoSanctionDispatcher
    {
        public static IEnumerable<Sanction> GetSanctionsForMatch(IDbConnection c, IDbTransaction t, Match match) 
        {
            if (match == null) return null;

            var config = GetConfig(c, t, match.IdTournament);
            if (config == null) return null;

            var result = new List<Sanction>();

            // Card combo sanctions 
            var events = GetMatchCardEvents(c, t, match.Id);
            var cardComboSanctions = GetCardCombosSanctions(events, config.Cards, match.IdTournament, match.StartTime);
            if (cardComboSanctions != null) result.AddRange(cardComboSanctions);

            // Cycle combo sanctions
            var playDay = c.Get<PlayDay>(match.IdDay);
            if (playDay != null)
            {
                // 🚧🚧🚧
                bool isAcrossStages = config.CyclesAcrossStages ? true : false;

                var currentDayResults = GetCardsSummaryForMatchPlayers(c, t, match, playDay.SequenceOrder + 1, isAcrossStages);
                var previousDayResults = GetCardsSummaryForMatchPlayers(c, t, match, playDay.SequenceOrder, isAcrossStages);

                var cardCycleSanctions = GetCardCyclesSanctions(currentDayResults, previousDayResults, config.Cycles, match);
                if (cardCycleSanctions != null) result.AddRange(cardCycleSanctions);
            }

            return result;
        }

        public static IEnumerable<MatchEvent> GetNewCardEventsAfterCard(IDbConnection c, IDbTransaction t, MatchEvent matchEvent, long idTournament)
        {
            // Calculate new generated cards after a match event. This happens inmediately after a card event. 
            // It only has to cycle through the combos list and generate a new card if a combination is found with the new event. 

            // loop all the combos. Check if the new card event is involved in any of them. Then, if other card is involved in the combo, try to find it in the player 
            // events. 
            var config = GetConfig(c, t, idTournament);
            if (config == null) return null;

            var events = GetMatchCardEvents(c, t, matchEvent.IdMatch);

            return GetNewCardEventsAfterCard(events, config, matchEvent);
        }

        private static IEnumerable<MatchEvent> GetNewCardEventsAfterCard(IEnumerable<MatchEvent> cardEvents, AutoSanctionConfigContent config, MatchEvent newMatchEvent)
        {
            if (config == null || config.Cards == null) return null;

            // Filter for events of this player
            var playerCardsEvents = cardEvents.Where(e => e.IdPlayer == newMatchEvent.IdPlayer);

            var newEvents = GetCardCombosNewCards(playerCardsEvents, config.Cards, newMatchEvent);

            return newEvents;
        }

        public static bool IsValidConfig(string config)
        {
            if (string.IsNullOrWhiteSpace(config)) return true;

            var parsed = ParseJsonConfig(config);
            return (parsed != null);
        }


        // __ Reset & recalculate _____________________________________________


        public static void ClearAllAutomaticSanctions(IDbConnection c, IDbTransaction t, long idTournament)
        {
            var query = @"
                DELETE FROM matchevents WHERE id IN (SELECT me.id FROM matchevents me JOIN matches m ON me.idmatch = m.id WHERE m.idtournament = @idTournament AND me.isAutomatic = 't');
                DELETE FROM sanctionmatches WHERE idsanction IN (SELECT s.id FROM sanctions s WHERE s.idtournament = @idTournament AND s.isAutomatic = 't');
                DELETE FROM sanctions WHERE idtournament = @idTournament AND isAutomatic = 't';
                UPDATE playerdayresults SET data1 = 0 WHERE idTournament = @idTournament;
            ";

            var result = c.Execute(query, new { idTournament }, t);
        }


        public static void ApplyAllAutomaticSanctions(IDbConnection c, IDbTransaction t, long idTournament, long idUser)
        {
            ApplyAllCards(c, t, idTournament);
            
            ApplyAllSanctions(c, t, idTournament, idUser);
        }

        private static void ApplyAllCards(IDbConnection c, IDbTransaction t, long idTournament)
        {
            var config = GetConfig(c, t, idTournament);
            if (config == null || config.Cards == null) return;

            // Create events for each card in each match
            var cardEvents = GetCardEvents(c, t, idTournament);
            if (cardEvents == null || cardEvents.Count() == 0) return;

            // Divide by matches, then apply one by one each event, giving the list of previous events 
            var eventsByMatch = cardEvents.GroupBy(e => e.IdMatch, e => e, (key, group) => new { IdMatch = key, Events = group });

            foreach (var match in eventsByMatch)
            {
                var matchCardEvents = match.Events;
                if (matchCardEvents == null || matchCardEvents.Count() == 0) continue;

                var eventList = new List<MatchEvent>();

                foreach (var ev in matchCardEvents)
                {
                    eventList.Add(ev);
                    var newCardEvents = GetNewCardEventsAfterCard(eventList, config, ev);
                    MatchesController.ApplyMatchEvents(c, t, newCardEvents);
                }
            }
        }

        private static void ApplyAllSanctions(IDbConnection c, IDbTransaction t, long idTournament, long idUser)
        {
            // Get all the matches and reapply the sanctions
            var matches = GetMatches(c, t, idTournament);
            if (matches == null || matches.Count() == 0) return;

            foreach (var match in matches)
            {
                var sanctions = GetSanctionsForMatch(c, t, match);
                MatchesController.ApplyMatchSanctions(c, t, idUser, sanctions);
            }
        }

        private static IEnumerable<MatchEvent> GetCardEvents(IDbConnection c, IDbTransaction t, long idTournament)
        {
            var sql = @"SELECT me.* FROM matchevents me JOIN matches m ON m.id = me.idmatch WHERE type >= 61 AND type <= 70 AND idtournament = @idTournament ORDER BY me.idMatch, me.matchminute, me.timestamp";
            var cards = c.Query<MatchEvent>(sql, new { idTournament }, t);
            return cards;
        }

        private static IEnumerable<Match> GetMatches(IDbConnection c, IDbTransaction t, long idTournament)
        {
            var sql = @"SELECT * FROM matches WHERE idtournament = @idTournament";
            var matches = c.Query<Match>(sql, new { idTournament }, t);
            return matches;
        }


        // __ Data gathering (SQL impl) _______________________________________


        public static AutoSanctionConfigContent GetConfig(IDbConnection c, IDbTransaction t, long idTournament)
        {
            var raw = c.QuerySingleOrDefault<AutoSanctionConfig>("SELECT * FROM autosanctionconfigs WHERE idtournament = @idTournament", new { idTournament }, t);
            if (raw == null) return null;

            return ParseJsonConfig(raw.Config);
        }

        private static IEnumerable<MatchEvent> GetMatchCardEvents(IDbConnection c, IDbTransaction t, long idMatch)
        {
            var result = c.Query<MatchEvent>("SELECT * FROM matchevents e WHERE e.idMatch = @idMatch AND e.type > 60 AND e.type < 70 ORDER BY e.matchminute, e.timestamp", new { idMatch }, t);

            return result;
        }

        private static IDictionary<long, PlayerDayResult> GetCardsSummaryForMatchPlayers(IDbConnection c, IDbTransaction t, Match match, int daySequence = -1, bool isAcrossStages = true)
        {
            long idMatch = match.Id;
            long idStage = match.IdStage;

            // Get players with yellow cards in the match
            var playerIds = c.Query<long>("SELECT DISTINCT me.idplayer from matchplayers mp JOIN matchEvents me ON me.idMatch = mp.idMatch AND mp.idMatch = @idMatch WHERE me.type = 61 ", new { idMatch }, t);
                                                           
            if (playerIds == null || playerIds.Count() == 0) return null;

            //var match = c.Get<Match>(idMatch);
            if (match == null) throw new Exception("Error.NotFound");

            // Sum their data1 for this tournament into a playerDayResult object
            // if IdDay is defined, add constraint to indicate everything before the provided day (so to get the status of the previous day)
            var acrossStagesCondition = isAcrossStages ? "" : $"AND pdr.idStage = {idStage} "; 
            var additionalCondition = daySequence > -1 ? "AND pd.sequenceOrder < @daySequence " : "";

            var query = $@"SELECT COALESCE(SUM(pdr.data1), 0) as data1, pdr.idPlayer, pdr.idTeam 
                            FROM playerDayResults pdr JOIN playdays pd ON pd.id = pdr.idday
                            WHERE 
	                            pdr.idtournament = @IdTournament
	                            AND (pdr.idTeam = @IdHomeTeam OR pdr.idTeam = @IdVisitorTeam)
	                            AND pdr.idplayer IN ({Utils.GetJoined(playerIds)})
                                {acrossStagesCondition}                                
                                {additionalCondition}
                            GROUP BY pdr.idPlayer, pdr.idTeam";

            var dd = c.Query<PlayerDayResult>(query, new
            {
                match.IdDay,
                match.IdHomeTeam,
                match.IdVisitorTeam,
                match.IdTournament,
                daySequence
            }, t);

            var result = dd.ToDictionary(pdr => pdr.IdPlayer);

            return result;
        }


        // __ Sanction calculators ____________________________________________


        public static IEnumerable<MatchEvent> GetCardCombosNewCards(IEnumerable<MatchEvent> matchCardEvents, IEnumerable<AutoSanctionCardConfig> cardComboConfigs, MatchEvent currentEvent)
        {
            var previousMatchCardEvents = matchCardEvents.Where(e => e.Id != currentEvent.Id);

            var existingEvents = GetCardEventsForCards(previousMatchCardEvents, cardComboConfigs);
            var eventsWithNewEvent = GetCardEventsForCards(matchCardEvents, cardComboConfigs);

            return eventsWithNewEvent.Except(existingEvents, new MatchEventAutoSanctionComparer());
        }

        public static IEnumerable<Sanction> GetCardCombosSanctions(IEnumerable<MatchEvent> matchCardEvents, IEnumerable<AutoSanctionCardConfig> cardComboConfigs, long idTournament, DateTime matchStarTime)
        {
            // Group card events by player
            var playerGroups = GroupPlayerEvents(matchCardEvents);

            var result = new List<Sanction>();

            if (matchCardEvents.Count() > 0)
            {
                foreach (var player in playerGroups)
                {
                    var playerCardEvents = player.Value;
                    var idPlayer = player.Key;

                    if (playerCardEvents.Count() == 0) continue;

                    var ev = playerCardEvents.First();
                    var idMatch = ev.IdMatch;
                    var idDay = ev.IdDay;
                    var idTeam = ev.IdTeam;

                    var playerMatchingCombos = GetCardCombosForMatchEvents(cardComboConfigs, playerCardEvents);
                    result.AddRange(CreateSanctionsForCardCombos(playerMatchingCombos, idTournament, idMatch, idDay, idTeam, idPlayer, matchStarTime));
                }
            }

            return result;
        }

        public static IEnumerable<Sanction> CreateSanctionsForCardCombos(IEnumerable<AutoSanctionCardConfig> cardCombos, long idTournament, long idMatch, long idDay, long idTeam, long idPlayer, DateTime matchStartDate)
        {
            var result = new List<Sanction>();

            if (cardCombos != null)
            {
                // Generate the sanction for each matching combo. Card penalties have been already 
                // processed here, so only sanctions with a number of matches need to be created. 
                foreach (var combo in cardCombos)
                {
                    if (combo == null || combo.Penalty == null) continue;

                    if (combo.Penalty.Type2 > 0)
                    {
                        // Sanctions matches
                        result.Add(new Sanction
                        {
                            IdMatch = idMatch, 
                            IdDay = idDay, 
                            IdTournament = idTournament, 
                            IdPlayer = idPlayer, 
                            IdTeam = idTeam,
                            InitialContent = combo.Penalty.Text, 
                            NumMatches = combo.Penalty.Type2,
                            Type = (int)SanctionType.Player, 
                            IsAutomatic = true, 
                            Status = (int)SanctionStatus.AutomaticallyGenerated, 
                            StartDate = matchStartDate,
                            SourceCardConfigRule = combo,
                            //IdSanctionConfigRuleId = combo.Id,
                        });
                    }
                }
            }

            return result;
        }

        public static IEnumerable<Sanction> GetCardCyclesSanctions(IDictionary<long, PlayerDayResult> currentAccumulated, IDictionary<long, PlayerDayResult> previousAccumulated, IEnumerable<AutoSanctionCycleConfig> cycleConfigs, Match match)
        {
            if (currentAccumulated == null || previousAccumulated == null) return null;

            var result = new List<Sanction>();

            foreach (var playerCurrent in currentAccumulated.Values)
            {
                if (!previousAccumulated.TryGetValue(playerCurrent.IdPlayer, out PlayerDayResult playerPrevious)) continue;

                var diff = playerCurrent.Data1 - playerPrevious.Data1;
                if (diff == 0) continue;    // Accumulated cards didn't change, nothing to do. 
                
                // Get last boundary of previous, compare to previous boundary of current, if different, apply new boundary sancion
                var previousCycleIteration = GetCycleIterationForNumCards(cycleConfigs, playerPrevious.Data1, out AutoSanctionCycleConfig previousRule);
                var currentCycleIteration = GetCycleIterationForNumCards(cycleConfigs, playerCurrent.Data1, out AutoSanctionCycleConfig currentRule);

                // Check if the cards in the match have made a change in the cycle. 
                if (previousCycleIteration == currentCycleIteration) continue;
                if (previousCycleIteration > currentCycleIteration) throw new Exception("Error.AutoSanctions.CycleCalculation.UnexpectedOutcome");

                if (currentRule.Penalty == null) continue;

                // We have a change in cycle, add the sanction of the rule that applies.
                result.Add(new Sanction
                {
                    IdMatch = match.Id,
                    IdDay = match.IdDay,
                    IdTournament = match.IdTournament,
                    IdPlayer = playerCurrent.IdPlayer,
                    IdTeam = playerCurrent.IdTeam,
                    InitialContent = currentRule.Penalty.Text,
                    NumMatches = currentRule.Penalty.Type2,
                    Type = (int)SanctionType.Player,
                    IsAutomatic = true,
                    Status = (int)SanctionStatus.AutomaticallyGenerated,
                    StartDate = match.StartTime,
                    //IdSanctionConfigRuleId = combo.Id,
                });
            }


            return result;
        }

        /// <returns>
        /// If the number of cards is within the ranges of defined cycles, it returns the cycle for the number of cards. 
        /// If the number of cards matches exactly the boundary of a cycle, it returns the next cycle (signaling a sanction should be applied)
        /// Examples: 
        ///   Cycles 5, 4, 3:  4 cards = 0, 5 cards = 1, 6 cards = 1, 8 cards = 1, 9 cards = 2, 10 cards = 2, 11 cards = 2, 12 cards = 3
        /// </returns>
        public static int GetCycleIterationForNumCards(IEnumerable<AutoSanctionCycleConfig> cycleConfigs, int numCards, out AutoSanctionCycleConfig matchingRule)
        {
            matchingRule = null;
            if (cycleConfigs == null || cycleConfigs.Count() == 0) return -1;

            int iteration = 0;
            int ruleAccumulated = 0;
            
            foreach (var rule in cycleConfigs)
            {
                matchingRule = rule;
                ruleAccumulated += rule.NumYellowCards;
                if (ruleAccumulated == numCards) return ++iteration;
                if (ruleAccumulated > numCards) return iteration;

                iteration++;
            }

            // More cards than covered by cycles, keep iterating the last cycle.

            while (true)
            {
                ruleAccumulated += matchingRule.NumYellowCards;
                if (ruleAccumulated == numCards) return ++iteration;
                if (ruleAccumulated > numCards) return iteration;

                iteration++;
            }
        }

        // __ Impl ____________________________________________________________        


        private static IEnumerable<MatchEvent> GetCardEventsForCards(IEnumerable<MatchEvent> matchCardEvents, IEnumerable<AutoSanctionCardConfig> cardComboConfigs)
        {
            // Group card events by player
            var playerGroups = GroupPlayerEvents(matchCardEvents);

            var result = new List<MatchEvent>();

            foreach (var player in playerGroups)
            {
                var playerCardEvents = player.Value;
                var playerId = player.Key;

                var playerMatchingCombos = GetCardCombosForMatchEvents(cardComboConfigs, playerCardEvents);
                if (playerMatchingCombos != null)
                {
                    foreach (var combo in playerMatchingCombos)
                    {
                        if (combo.Penalty != null && combo.Penalty.Type1 > 0)
                        {
                            var newEvent = playerCardEvents.First().Clone();
                            newEvent.MatchMinute = GetNewestTimeFromEvents(combo.EventMatch1, combo.EventMatch2);
                            newEvent.TimeStamp = DateTime.Now;
                            newEvent.IdCreator = -1;
                            newEvent.Type = (int)MatchEventType.CardStart + combo.Penalty.Type1;
                            newEvent.IsAutomatic = true;
                            result.Add(newEvent);
                        }

                        var yellowCardsCompensationEvent = GetYellowCardCompensationEvent(combo, playerCardEvents.First());
                        if (yellowCardsCompensationEvent != null)
                        {
                            yellowCardsCompensationEvent.MatchMinute = GetNewestTimeFromEvents(combo.EventMatch1, combo.EventMatch2);
                            result.Add(yellowCardsCompensationEvent);
                        }
                    }
                }
            }

            return result;
        }

        private static MatchEvent GetYellowCardCompensationEvent(AutoSanctionCardConfig combo, MatchEvent sampleEvent)
        {
            if (combo == null) return null;

            // Automatic sanction card combo: insert yellow card compensation event
            var numCards = GetYellowCardsToSubtractForCardCombo(combo);
            if (numCards == 0) return null;

            // Create data1 compensation event for this sanction
            var newEvent = sampleEvent.Clone();
            newEvent.Type = (int)MatchEventType.AddToPdrData1;
            newEvent.IntData1 = -numCards;
            newEvent.TimeStamp = DateTime.Now;
            newEvent.IsAutomatic = true;

            return newEvent;
        }

        // Return the number of cards to subtract for the given sanction rule
        public static int GetYellowCardsToSubtractForCardCombo(AutoSanctionCardConfig combo)
        {
            int numYellowCards = 0;
            if (combo.Card1Type == 1) numYellowCards++;
            if (combo.Card2Type == 1) numYellowCards++;

            if (numYellowCards == 0) return 0;

            switch (combo.AddYellowCards)
            {
                case 0: return numYellowCards;
                case 1:
                    switch (numYellowCards)
                    {
                        case 0: return 0;
                        case 1: return 0;
                        case 2: return 1;
                        default: return 0;
                    }
                case 2:
                    switch (numYellowCards)
                    {
                        case 0: return 1;
                        case 1: return 0;
                        case 2: return 0;
                        default: return 0;
                    }
                default: return 0;
            }
        }

        private static int GetNewestTimeFromEvents(MatchEvent e1, MatchEvent e2)
        {
            if (e1 == null && e2 == null) return 0;

            if (e1 == null) return e2.MatchMinute;
            if (e2 == null) return e1.MatchMinute;

            return Math.Max(e1.MatchMinute, e2.MatchMinute);
        }

        public static IList<AutoSanctionCardConfig> GetCardCombosForMatchEvents(IEnumerable<AutoSanctionCardConfig> cardComboConfigs, IEnumerable<MatchEvent> matchEvents)
        {
            var result = new List<AutoSanctionCardConfig>();

            foreach (var combo in cardComboConfigs)
            {
                if (ComboAppliesToMatchEvents(combo, matchEvents, out MatchEvent ev1, out MatchEvent ev2))
                {
                    combo.EventMatch1 = ev1;
                    combo.EventMatch2 = ev2;
                    result.Add(combo);
                }
            }

            return result;
        }


        public static bool ComboAppliesToMatchEvents(AutoSanctionCardConfig cardCombo, IEnumerable<MatchEvent> matchEvents, out MatchEvent event1, out MatchEvent event2)
        {
            event1 = null;
            event2 = null;

            // Only one card in combo rule.
            if (cardCombo.Card2Type == 0)
            {
                foreach (var ev in matchEvents)
                {
                    var evCardType = ev.Type - (int)MatchEventType.CardStart;
                    if (evCardType == cardCombo.Card1Type)
                    {
                        event1 = ev;
                        return true;
                    }
                }

                return false;
            }

            // Two card combo rule
            bool c1Direct = false, c2Direct = false, c1Reverse = false, c2Reverse = false;

            foreach (var ev in matchEvents)
            {
                var evCardType = ev.Type - (int)MatchEventType.CardStart;

                if (evCardType == cardCombo.Card1Type && !c1Direct)
                {
                    c1Direct = true;
                    c2Reverse = true;
                    event1 = ev;
                }
                else if (evCardType == cardCombo.Card2Type && !c2Direct)
                {
                    c2Direct = true;
                    c1Reverse = true;
                    event2 = ev;
                }
            }

            var result = (c1Direct && c2Direct) || (c1Reverse && c2Reverse);

            if (!result)
            {
                event1 = null;
                event2 = null;
            }

            return result;
        }


        public static Dictionary<long, IList<MatchEvent>> GroupPlayerEvents(IEnumerable<MatchEvent> events)
        {
            var sortedByPlayer = events.OrderBy(ev => ev.IdPlayer);

            var result = new Dictionary<long, IList<MatchEvent>>();

            long lastPlayerId = -1;
            List<MatchEvent> lastPlayerEvents = null;

            foreach (var ev in sortedByPlayer)
            {
                if (ev.IdPlayer != lastPlayerId)
                {
                    lastPlayerEvents = new List<MatchEvent>();
                    lastPlayerId = ev.IdPlayer;
                    result.Add(lastPlayerId, lastPlayerEvents);
                }

                lastPlayerEvents.Add(ev);
            }

            return result;
        }


        public static AutoSanctionConfigContent ParseJsonConfig(string jsonConfig)
        {
            var result = JsonConvert.DeserializeObject<AutoSanctionConfigContent>(jsonConfig, new JsonSerializerSettings { });

            return result;
        }
    }
}
