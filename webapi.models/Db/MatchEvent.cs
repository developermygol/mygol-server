using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Dapper.Contrib.Extensions;
using Newtonsoft.Json;

namespace webapi.Models.Db
{
    [DebuggerDisplay("TYPE:{Type}")]
    public class MatchEvent: BaseObject
    {
        public const int TournamentPointsForWinning = 3;
        public const int TournamentPointsForDraw = 1;


        public long IdMatch { get; set; }
        public long IdTeam { get; set; }
        public long IdPlayer { get; set; }
        public long IdDay { get; set; }
        public long IdCreator { get; set; }         // user id --> referee

        public DateTime TimeStamp { get; set; }
        public int Type { get; set; }
        public int MatchMinute { get; set; }
        public int IntData1 { get; set; }
        public int IntData2 { get; set; }
        public int IntData3 { get; set; }
        public int IntData4 { get; set; }
        public int IntData5 { get; set; }
        public int IntData6 { get; set; }
        public string Data1 { get; set; }
        public string Data2 { get; set; }
        public bool IsAutomatic { get; set; }

        public MatchEvent Clone()
        {
            return new MatchEvent
            {
                IdMatch = this.IdMatch,
                IdTeam = this.IdTeam,
                IdPlayer = this.IdPlayer,
                IdDay = this.IdDay,
                IdCreator = this.IdCreator,
                TimeStamp = this.TimeStamp,
                Type = this.Type,
                MatchMinute = this.MatchMinute,
                IntData1 = this.IntData1,
                IntData2 = this.IntData2,
                IntData3 = this.IntData3,
                IntData4 = this.IntData4,
                IntData5 = this.IntData5,
                IntData6 = this.IntData6,
                Data1 = this.Data1,
                Data2 = this.Data2,
                IsAutomatic = this.IsAutomatic
            };
        }


        public static (Match, MatchEvent) Create(IDbConnection c, IDbTransaction tr, MatchEvent ev, bool insertEvent = true)
        {
            Match match = null;
            // These cases alter statistics (player and team)
            switch ((MatchEventType)ev.Type)
            {
                case MatchEventType.Point:
                    match = ApplyStatsChange(c, tr, ev,
                        null,      // Update matchPlayer   // datos del jugador en el partido (estado) 
                        p => p.Points++,        // Update playerDayResults        // Resultados del jugador en la jornada
                        t => { t.Points++; UpdateDiff(t); },        // Update teamDayResults     // Resultados del equipo en la jornada
                        other => { other.PointsAgainst++; UpdateDiff(other); },           // Update the other team points against
                        m =>                    // Update match
                        {                  
                            if (ev.IdTeam == m.IdHomeTeam)
                                m.HomeScore++;
                            else if (ev.IdTeam == m.IdVisitorTeam)
                                m.VisitorScore++;
                        }, insertEvent: insertEvent);
                    break;

                case MatchEventType.PointInOwn:
                    match = ApplyStatsChange(c, tr, ev, 
                        null,
                        p => p.PointsInOwn++,
                        t => { t.PointsAgainst++; UpdateDiff(t); },
                        other => { other.Points++; UpdateDiff(other); },
                        m =>
                        {
                            if (ev.IdTeam == m.IdHomeTeam)
                                m.VisitorScore++;
                            else if (ev.IdTeam == m.IdVisitorTeam)
                                m.HomeScore++;
                        }, insertEvent: insertEvent);
                    break;
                case MatchEventType.MatchStarted:
                    match = ApplyStatsChange(c, tr, ev, null, null, null, null, m => m.Status = (int)MatchStatus.Playing, null, false, insertEvent: insertEvent);
                    break;
                case MatchEventType.MatchEnded:
                    match = ApplyStatsChange(c, tr, ev, null, null, null, null, m => m.Status = (int)MatchStatus.Finished, null, false, insertEvent: insertEvent);
                    break;
                case MatchEventType.FirstHalfFinish:
                    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                    break;
                case MatchEventType.SecondHalfStarted:
                    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                    break;
                case MatchEventType.SecondHalfFinish:
                    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                    break;
                case MatchEventType.ExtraTimeFirstHalfStart:
                    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                    break;
                case MatchEventType.ExtraTimeFirstHalfEnd:
                    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                    break;
                case MatchEventType.ExtraTimeSecondHalfStart:
                    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                    break;
                case MatchEventType.ExtraTimeSecondHalfEnd:
                    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                    break;

                case MatchEventType.Assist:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.Assistances++, null, null, null, insertEvent: insertEvent);
                    break;
                case MatchEventType.Corner:
                    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                    break;
                case MatchEventType.Fault:
                    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                    break;

                case MatchEventType.Penalty:
                    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                    break;
                case MatchEventType.PenaltyFailed:
                    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                    
                    break;
                case MatchEventType.PenaltyStopped:
                    // TODO: Still no field in the table, I need to add it (not just record the matchevent)
                    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                    break;

                case MatchEventType.Injury:
                    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                    break;

                case MatchEventType.Card1:
                    match = ApplyStatsChange(c, tr, ev, null, p => { p.CardsType1++; p.Data1++; } , insertEvent: insertEvent);
                    break;
                case MatchEventType.Card2:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.CardsType2++, insertEvent: insertEvent);
                    break;
                case MatchEventType.Card3:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.CardsType3++, insertEvent: insertEvent);
                    break;
                case MatchEventType.Card4:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.CardsType4++, insertEvent: insertEvent);
                    break;
                case MatchEventType.Card5:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.CardsType5++, insertEvent: insertEvent);
                    break;

                case MatchEventType.MVP:                    
                    match = ApplyStatsChange(c, tr, ev, null, p => {
                        bool MPVLimitExided = p.MVPPoints > 0;
                        if (MPVLimitExided) throw new Exception("Error.MVPAlreadyExists");
                        p.MVPPoints++;
                    }, null, null, null, insertEvent: insertEvent);
                    AddMvpAward(c, tr, match, ev);
                    break;


                case MatchEventType.RecordClosed:
                    if (insertEvent && RecordClosedExists(c, tr, ev)) throw new Exception("Error.RecordAlreadyClosed");

                    // Also update MVP ranking?

                    //match = ApplyStatsChange(c, tr, ev, finalCallback: UpdateTeamsMatchStats, checkTeams: false, insertEvent: insertEvent);
                    match = ApplyStatsChange(c, tr, ev, null, null, null, null, m => m.Status = (int)MatchStatus.Signed, finalCallback: UpdateTeamsMatchStats, checkTeams: false, insertEvent: insertEvent);                     
                    break;
                case MatchEventType.AddToPdrData1:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.Data1 += ev.IntData1, insertEvent: insertEvent);
                    break;
                case MatchEventType.ChangeTeamStats:
                    match = ApplyStatsChange(c, tr, ev, null, null, t => 
                    {
                        t.TournamentPoints += ev.IntData1;
                        t.GamesWon += ev.IntData2;
                        t.GamesDraw += ev.IntData3;
                        t.GamesLost += ev.IntData4;
                    }, insertEvent: insertEvent);
                    break;

            }

            // Notify?

            return (match, ev);
        }

        public static Match Delete(IDbConnection c, IDbTransaction tr,  MatchEvent ev)
        {
            // Delete the record in the database and undo any previous side effects
            //return c.Delete(ev);

            var insertEvent = false;

            Match match = null;

            c.Delete(ev, tr);

            // These cases alter statistics (player and team)
            switch ((MatchEventType)ev.Type)
            {
                case MatchEventType.Point:
                    match = ApplyStatsChange(c, tr, ev,
                        null,      // Update matchPlayer   // datos del jugador en el partido (estado) 
                        p => p.Points--,        // Update playerDayResults        // Resultados del jugador en la jornada
                        t => { t.Points--; UpdateDiff(t); },        // Update teamDayResults     // Resultados del equipo en la jornada
                        other => { other.PointsAgainst--; UpdateDiff(other); },           // Update the other team points against
                        m =>                    // Update match
                        {
                            if (ev.IdTeam == m.IdHomeTeam)
                                m.HomeScore--;
                            else if (ev.IdTeam == m.IdVisitorTeam)
                                m.VisitorScore--;
                        }, insertEvent: insertEvent);
                    break;

                case MatchEventType.PointInOwn:
                    match = ApplyStatsChange(c, tr, ev,
                        null,
                        p => p.PointsInOwn--,
                        t => { t.PointsAgainst--; UpdateDiff(t); },
                        other => { other.Points--; UpdateDiff(other); },
                        m =>
                        {
                            if (ev.IdTeam == m.IdHomeTeam)
                                m.VisitorScore--;
                            else if (ev.IdTeam == m.IdVisitorTeam)
                                m.HomeScore--;
                        }, insertEvent: insertEvent);
                    break;

                case MatchEventType.MatchStarted:
                    match = ApplyStatsChange(c, tr, ev, null, null, null, null, m => m.Status = (int)MatchStatus.Scheduled, null, false, insertEvent: insertEvent);
                    break;
                case MatchEventType.MatchEnded:
                    match = ApplyStatsChange(c, tr, ev, null, null, null, null, m => m.Status = (int)MatchStatus.Playing, null, false, insertEvent: insertEvent);
                    break;
                //case MatchEventType.FirstHalfFinish:
                //    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                //    break;
                //case MatchEventType.SecondHalfStarted:
                //    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                //    break;
                //case MatchEventType.SecondHalfFinish:
                //    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                //    break;
                //case MatchEventType.ExtraTimeFirstHalfStart:
                //    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                //    break;
                //case MatchEventType.ExtraTimeFirstHalfEnd:
                //    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                //    break;
                //case MatchEventType.ExtraTimeSecondHalfStart:
                //    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                //    break;
                //case MatchEventType.ExtraTimeSecondHalfEnd:
                //    match = ApplyStatsChange(c, tr, ev, checkTeams: false, insertEvent: insertEvent);
                //    break;

                case MatchEventType.Assist:
                    // TODO: Still no field in the table, I need to add it (not just record the matchevent)
                    match = ApplyStatsChange(c, tr, ev, null, p => p.Assistances--, null, null, null, insertEvent: insertEvent);
                    break;
                //case MatchEventType.Corner:
                //    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                //    break;
                //case MatchEventType.Fault:
                //    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                //    break;

                //case MatchEventType.Penalty:
                //    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                //    break;
                //case MatchEventType.PenaltyFailed:
                //    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);

                //    break;
                //case MatchEventType.PenaltyStopped:
                //    // TODO: Still no field in the table, I need to add it (not just record the matchevent)
                //    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                //    break;

                //case MatchEventType.Injury:
                //    match = ApplyStatsChange(c, tr, ev, insertEvent: insertEvent);
                //    break;

                case MatchEventType.Card1:
                    match = ApplyStatsChange(c, tr, ev, null, p => { p.CardsType1--; p.Data1--; }, insertEvent: insertEvent);
                    break;
                case MatchEventType.Card2:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.CardsType2--, insertEvent: insertEvent);
                    break;
                case MatchEventType.Card3:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.CardsType3--, insertEvent: insertEvent);
                    break;
                case MatchEventType.Card4:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.CardsType4--, insertEvent: insertEvent);
                    break;
                case MatchEventType.Card5:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.CardsType5--, insertEvent: insertEvent);
                    break;

                case MatchEventType.MVP:                    
                    match = ApplyStatsChange(c, tr, ev, null, p => p.MVPPoints--, null, null, null, insertEvent: insertEvent);
                    RemoveMvpAward(c, tr, match, ev);
                    break;


                case MatchEventType.RecordClosed:
                    // Also update MVP ranking? 
                    match = ApplyStatsChange(c, tr, ev, finalCallback: UndoUpdateTeamsMatchStats, checkTeams: false, insertEvent: insertEvent);
                    break;

                case MatchEventType.AddToPdrData1:
                    match = ApplyStatsChange(c, tr, ev, null, p => p.Data1 -= ev.IntData1, insertEvent: insertEvent);
                    break;
                case MatchEventType.ChangeTeamStats:
                    match = ApplyStatsChange(c, tr, ev, null, null, t =>
                    {
                        t.TournamentPoints -= ev.IntData1;
                        t.GamesWon -= ev.IntData2;
                        t.GamesDraw -= ev.IntData3;
                        t.GamesLost -= ev.IntData4;
                    }, insertEvent: insertEvent);
                    break;
            }

            // Notify?

            return match;
        }



        public static async Task ResetTournamentStats(IDbConnection c, IDbTransaction t, long idTournament)
        {
            // This method refreshes all the stats by removing / resetting all stats entries and replaying all the tournament events. 

            var parameters = new { id = idTournament };

            // Delete all stats
            await c.ExecuteAsync(@"
                    DELETE FROM teamDayResults WHERE idTournament = @id;
                    DELETE FROM playerDayResults WHERE idTournament = @id;
                    DELETE FROM awards WHERE idTournament = @id;
                    UPDATE matches SET homescore = 0, visitorscore = 0 WHERE idTournament = @id;
                ",
            parameters, t);
        }

        /*public static async Task ApplyTournamentStats(IDbConnection c, IDbTransaction t, long idTournament)
        {
            await ApplyAllTournamentEvents(c, t, idTournament);
            await RecalculateTournamentGameEnds(c, t, idTournament);
            await RecalculateTournamentDayEnds(c, t, idTournament);
        }*/

        public static async Task ApplyTournamentStats(IDbConnection c, IDbTransaction t, long idTournament)
        {
            await ApplyAllTournamentEvents(c, t, idTournament);
            await RecalculateTournamentGameEnds(c, t, idTournament);
            await RecalculateTournamentDayEnds(c, t, idTournament);
        }

        private static async Task ApplyAllTournamentEvents(IDbConnection c, IDbTransaction t, long idTournament)
        {
            var parameters = new { id = idTournament };

            var events = await c.QueryAsync<MatchEvent>(
                "SELECT * FROM matchevents me join matches m on me.idmatch = m.id WHERE m.idTournament = @id ORDER BY idMatch, matchminute, timestamp", 
                parameters, t);

            foreach (var ev in events)
            {
                MatchEvent.Create(c, t, ev, insertEvent: false);
            }
        }

        private static async Task RecalculateTournamentGameEnds(IDbConnection c, IDbTransaction t, long idTournament)
        {
            // This method resets all the values set on game end (like games won, lost, etc) and recalculates them for all finished matches. 

            var parameters = new { id = idTournament, status = (int)MatchStatus.Finished };

            // Delete all stats
            await c.ExecuteAsync(@"
                    UPDATE teamdayresults SET
                        gamesplayed = 0, gameswon = 0, gamesdraw = 0, gameslost = 0, tournamentPoints = 0
                    WHERE idTournament = @id
                ",
            parameters, t);

            var matches = await c.QueryAsync<Match>("SELECT * FROM matches WHERE idTournament = @id AND status = @status ORDER BY starttime", parameters, t);

            // 💥 tournamentPoints are been lost wen recalaculating it is not adding sanctions

            var ev = new MatchEvent();

            foreach (var m in matches)
            {
                ev.IdDay = m.IdDay;
                ev.IdMatch = m.Id;
                MatchEvent.UpdateTeamsMatchStats(c, t, ev, m);
            }
        }

        private static async Task RecalculateTournamentDayEnds(IDbConnection c, IDbTransaction t, long idTournament)
        {
            List<Award> awards = new List<Award>();
            var days = await c.QueryAsync<PlayDay>("SELECT * FROM playdays WHERE idTournament = @idTournament", new { idTournament = idTournament }, t);
            if (days == null) return;
            
            foreach (var d in days)
            {
                await UpdatePlayersDayStats(c, t, d.Id, idTournament);
                await AddTopPlayDayAwards(c, t, d.Id, d.IdStage, d.IdGroup, idTournament);
            }
        }


        private static void UpdateDiff(TeamDayResult tdr)
        {
            tdr.PointDiff = tdr.Points - tdr.PointsAgainst;
        }


        // __ MVP _____________________________________________________________


        private static void AddMvpAward(IDbConnection c, IDbTransaction t, Match match, MatchEvent ev)
        {
            var award = new Award
            {
                IdDay = match.IdDay,
                IdGroup = match.IdGroup, 
                IdStage = match.IdStage, 
                IdTournament = match.IdTournament,

                IdPlayer = ev.IdPlayer,
                IdTeam = ev.IdTeam,
                Type = (int)AwardType.MVP,
            };

            c.Insert(award, t);
        }

        private static void RemoveMvpAward(IDbConnection c, IDbTransaction t, Match match, MatchEvent ev)
        {
            var result = c.Execute(
                "DELETE FROM awards WHERE idPlayer = @idPlayer AND idDay = @idDay AND type = @type AND idTournament = @idTournament",
                new { idDay = match.IdDay, idPlayer = ev.IdPlayer, idTournament = match.IdTournament, type = (int)AwardType.MVP }, t);

            if (result != 1) throw new Exception("Error.CantLocateSpecificAward"); // Limit to one MVP x player x day
        }

               
        // __ Record closed ___________________________________________________


        private static bool RecordClosedExists(IDbConnection c, IDbTransaction t, MatchEvent ev)
        {
            var result = c.QueryFirstOrDefault<MatchEvent>("SELECT * FROM matchevents WHERE idMatch = @IdMatch AND type = @Type", new { IdMatch = ev.IdMatch, Type = (int)MatchEventType.RecordClosed } , t);
            
            return (result != null);
        }


        // __ Player Day Results ______________________________________________


        public static void UpdateStatsForAllPlayersInMatch(IDbConnection c, IDbTransaction t, Match match)
        {
            // Stats updated after the match
            // Update player day result: games won/draw/lost played

            if (match.HomeScore == match.VisitorScore)
            {
                UpdateStatsForAllPlayersInMatch(c, t, match.IdHomeTeam, match.Id, match.IdTournament, match.IdStage, match.IdGroup, 1, 0, 1, 0);
                UpdateStatsForAllPlayersInMatch(c, t, match.IdVisitorTeam, match.Id, match.IdTournament, match.IdStage, match.IdGroup, 1, 0, 1, 0);
            }
            else if (match.HomeScore > match.VisitorScore)
            {
                UpdateStatsForAllPlayersInMatch(c, t, match.IdHomeTeam, match.Id, match.IdTournament, match.IdStage, match.IdGroup, 1, 1, 0, 0);
                UpdateStatsForAllPlayersInMatch(c, t, match.IdVisitorTeam, match.Id, match.IdTournament, match.IdStage, match.IdGroup, 1, 0, 0, 1);
            }
            else
            {
                UpdateStatsForAllPlayersInMatch(c, t, match.IdHomeTeam, match.Id, match.IdTournament, match.IdStage, match.IdGroup, 1, 0, 0, 1);
                UpdateStatsForAllPlayersInMatch(c, t, match.IdVisitorTeam, match.Id, match.IdTournament, match.IdStage, match.IdGroup, 1, 1, 0, 0);
            }
        }

        public static void UpdateStatsForAllPlayersInMatch(IDbConnection c, IDbTransaction t, long idTeam, long idMatch, long idTournament, long idStage, long idGroup, int gamesPlayed, int gamesWon, int gamesDraw, int gamesLost)
        {
            // Should batch these inserts. It's taking large amounts of time to insert this data. 

            var matchPlayers = c.Query<MatchPlayer>("select * from matchplayers where idmatch = @idMatch and idteam = @idTeam",
                new { idMatch = idMatch, idTeam = idTeam }, t);

            if (matchPlayers == null) return;

            foreach (var p in matchPlayers)
            {
                // Now update each player player day result. Create the record if it doesn't exist already (may be the case for players without any events in the match). 
                // TODO: this syntax is postgres specific
                var sql = @"
                INSERT INTO playerdayresults (idday, idplayer, idteam, idtournament, idstage, idgroup, iduser, gamesplayed, gameswon, gamesdraw, gameslost)
	                VALUES (@idDay, @idPlayer, @idTeam, @idTournament, @idStage, @idGroup, @idUser,
	                @gamesPlayed, @gamesWon, @gamesDraw, @gamesLost)
                ON CONFLICT (idday, idplayer, idteam, idtournament, idstage, idgroup) DO 
	                UPDATE SET gamesplayed = @gamesPlayed, gameswon = @gamesWon, gamesdraw = @gamesDraw, gameslost = @gamesLost
            ";

                c.Execute(sql, new
                {
                    idDay = p.IdDay,
                    idPlayer = p.IdPlayer,
                    idTournament = idTournament,
                    idTeam = idTeam,
                    idStage = idStage, 
                    idGroup = idGroup,
                    idUser = p.IdUser,
                    gamesPlayed = gamesPlayed,
                    gamesWon = gamesWon,
                    gamesDraw = gamesDraw,
                    gamesLost = gamesLost
                }, t);
            }
        }



        public static async Task UpdatePlayersDayStats(IDbConnection c, IDbTransaction t, long idDay, long idTournament)
        {
            // Rankings

            // Goleadores
            var dataColumn = "points";
            var targetColumn = "ranking1";
            var sql = $@"
                UPDATE playerdayresults pdr
                SET {targetColumn} = ranked.rank 
                FROM (
	                SELECT 
		                idplayer, 
		                p.idtournament, 
		                sum({dataColumn}) AS data, 
		                rank() OVER (PARTITION BY p.idtournament ORDER BY COALESCE(sum({dataColumn}),0) DESC) AS rank
	                FROM playerdayresults 
		                JOIN playdays p ON idday = p.id
	                WHERE sequenceorder <= (select sequenceorder from playdays where id = @idDay) 
		                AND p.idtournament = @idTournament
	                group by idplayer, p.idtournament
	                order by data desc
                ) AS ranked
                WHERE pdr.idTournament = ranked.idtournament and pdr.idday = @idDay and pdr.idplayer = ranked.idplayer
             ";

            await c.ExecuteAsync(sql, new { idDay = idDay, idTournament = idTournament }, t);

            // Goalkeepers
            dataColumn = "tr.pointsagainst";
            targetColumn = "ranking2";

            sql = $@"
            UPDATE playerdayresults pdr
            SET {targetColumn} = ranked.rank 
            FROM (
	            SELECT 
		            idplayer, 
		            p.idtournament, 
		            sum({dataColumn}) AS data, 
		            rank() OVER (PARTITION BY p.idtournament ORDER BY COALESCE(sum({dataColumn}),0) ASC) AS rank
	            FROM playerdayresults 
		        JOIN playdays p ON idday = p.id
                JOIN teams t ON idTeam = t.id
                JOIN teamdayresults tr ON @idTournament = tr.idTournament AND @idDay = tr.idday AND t.id = tr.idteam
	            WHERE sequenceorder <= (select sequenceorder from playdays where id = @idDay) 
		        AND p.idtournament = @idTournament                
	            group by idplayer, p.idtournament
	            order by data ASC
            ) AS ranked
            WHERE pdr.idTournament = ranked.idtournament and pdr.idday = @idDay and pdr.idplayer = ranked.idplayer
            ";

            await c.ExecuteAsync(sql, new { idDay = idDay, idTournament = idTournament }, t);

            // Assistances 🚧🚧🚧

            // MVPs 🚧🚧🚧
            dataColumn = "mvppoints";
            targetColumn = "ranking4";

            sql = $@"
                UPDATE playerdayresults pdr
                SET {targetColumn} = ranked.rank 
                FROM (
	                SELECT 
		                idplayer, 
		                p.idtournament, 
		                sum({dataColumn}) AS data, 
		                rank() OVER (PARTITION BY p.idtournament ORDER BY COALESCE(sum({dataColumn}),0) DESC) AS rank
	                FROM playerdayresults 
		                JOIN playdays p ON idday = p.id
	                WHERE sequenceorder <= (select sequenceorder from playdays where id = @idDay) 
		                AND p.idtournament = @idTournament
	                group by idplayer, p.idtournament
	                order by data desc
                ) AS ranked
                WHERE pdr.idTournament = ranked.idtournament and pdr.idday = @idDay and pdr.idplayer = ranked.idplayer
             ";

            await c.ExecuteAsync(sql, new { idDay = idDay, idTournament = idTournament }, t);
        }

        public static async Task<List<Award>> AddTopPlayDayAwards(IDbConnection c, IDbTransaction t, long idDay, long idStage, long idGroup, long idTournament)
        {
            int maxRank = 1;

            List<Award> dayAwards = new List<Award>();

            // 🚧 Send Notifications 🔎 Check if already exists(status?).

            // Top Scorer
            var playerAwarded = await c.QueryFirstOrDefaultAsync<PlayerDayResult>("SELECT DISTINCT ON(pd.id) p.idplayer, p.idteam, p.ranking1 FROM playerdayresults p JOIN playdays pd on pd.id = p.idday  WHERE p.ranking1 = @maxRank AND p.idday = @idDay", new { maxRank = maxRank, idDay = idDay }, t);
            
            if(playerAwarded != null)
            {
                var award = new Award
                {
                    IdDay = idDay,
                    IdGroup = idGroup, 
                    IdStage = idStage, 
                    IdTournament = idTournament,

                    IdPlayer = playerAwarded.IdPlayer,
                    IdTeam = playerAwarded.IdTeam,
                    Type = (int)AwardType.TopScorer,
                };

                c.Insert(award, t);

                dayAwards.Add(award);
            }

            // Top Assitances
            // 🔎 ONLY team official goalkeeper
            playerAwarded = await c.QueryFirstOrDefaultAsync<PlayerDayResult>("SELECT DISTINCT ON(pd.id) p.idplayer, p.idteam, p.ranking2 FROM playerdayresults p JOIN playdays pd on pd.id = p.idday JOIN teams t ON t.id = p.idteam  WHERE t.idgoalkeeper = p.idplayer  AND p.ranking2 = @maxRank AND p.idday = @idDay", new { maxRank = maxRank, idDay = idDay }, t);

            if (playerAwarded != null)
            {
                var award = new Award
                {
                    IdDay = idDay,
                    IdGroup = idGroup,
                    IdStage = idStage,
                    IdTournament = idTournament,

                    IdPlayer = playerAwarded.IdPlayer,
                    IdTeam = playerAwarded.IdTeam,
                    Type = (int)AwardType.TopGoalKeeper,
                };

                c.Insert(award, t);

                dayAwards.Add(award);
            }

            // Top MVP            
            playerAwarded = await c.QueryFirstOrDefaultAsync<PlayerDayResult>("SELECT DISTINCT ON(pd.id) p.idplayer, p.idteam, p.ranking4 FROM playerdayresults p JOIN playdays pd on pd.id = p.idday  WHERE p.ranking4 = @maxRank AND p.idday = @idDay", new { maxRank = maxRank, idDay = idDay }, t);

            if (playerAwarded != null)
            {
                var award = new Award
                {
                    IdDay = idDay,
                    IdGroup = idGroup,
                    IdStage = idStage,
                    IdTournament = idTournament,

                    IdPlayer = playerAwarded.IdPlayer,
                    IdTeam = playerAwarded.IdTeam,
                    Type = (int)AwardType.TopGoalKeeper,
                };

                c.Insert(award, t);

                dayAwards.Add(award);
            }

            return dayAwards;
        }


        // __ Team Day Results ________________________________________________

        public static void UpdateTeamDayStats(IDbConnection c, IDbTransaction t, long idTeam, long idDay, long idTournament)
        {
            // Rankings?
        }

        public static void UpdateTeamsMatchStats(IDbConnection c, IDbTransaction t, MatchEvent ev, Match match)
        {
            UpdateStatsForAllPlayersInMatch(c, t, match);

            var homeDelta = new TeamDayResult { GamesPlayed = 1 };
            var visitorDelta = new TeamDayResult { GamesPlayed = 1 };

            if (match.VisitorScore == match.HomeScore)
            {
                homeDelta.GamesDraw++;
                visitorDelta.GamesDraw++;
                homeDelta.TournamentPoints += TournamentPointsForDraw;
                visitorDelta.TournamentPoints += TournamentPointsForDraw;
            }
            else if (match.HomeScore > match.VisitorScore)
            {
                homeDelta.GamesWon++;
                visitorDelta.GamesLost++;
                homeDelta.TournamentPoints += TournamentPointsForWinning;    // TODO: Take this value from tournament config 
            }
            else
            {
                homeDelta.GamesLost++;
                visitorDelta.GamesWon++;
                visitorDelta.TournamentPoints += TournamentPointsForWinning;
            }

            // 🚧🚧🚧 Get match sanctions 
            match.SanctionsMatch = GetSanctionsMatch(c, match.Id);
            if (match.SanctionsMatch != null)
            {
                foreach (var sanction in match.SanctionsMatch)
                {
                    // 🚧 Recalculate TeamSanctions  
                    CreateTeamSanctionPenalty(c, t, sanction);
                }
            }
            // 🚧🚧🚧

            // Update home team
            ev.IdTeam = match.IdHomeTeam;
            UpdateTeamDayResult(c, t, ev, tdr => ApplyTeamDayResultDelta(tdr, homeDelta), match);

            // Update visitor team
            ev.IdTeam = match.IdVisitorTeam;
            UpdateTeamDayResult(c, t, ev, tdr => ApplyTeamDayResultDelta(tdr, visitorDelta), match);

            // Update player

            // Update sanctions status
            SetSanctionsAsFinished(c, t, match.Id);
        }

        private static void SetSanctionsAsFinished(IDbConnection c, IDbTransaction t, long idMatch)
        {
            // Set sanctions as Finished for those where this match is the last in their list of sanctionMatches.
            var sql = @"UPDATE sanctions s SET status = @status 
                        FROM sanctionmatches sm WHERE sm.idsanction = s.id AND sm.idmatch = @idMatch AND islast = 't'";

            c.Execute(sql, new { idMatch, status = (int)SanctionStatus.Finished }, t);
        }

        public static void UndoUpdateTeamsMatchStats(IDbConnection c, IDbTransaction t, MatchEvent ev, Match match)
        {
            // Have to undo all this. 


            //UpdatePlayerMatchStats(c, t, match);

            //var homeDelta = new TeamDayResult { GamesPlayed = 1 };
            //var visitorDelta = new TeamDayResult { GamesPlayed = 1 };

            //if (match.VisitorScore == match.HomeScore)
            //{
            //    homeDelta.GamesDraw++;
            //    visitorDelta.GamesDraw++;
            //    homeDelta.TournamentPoints++;
            //    visitorDelta.TournamentPoints++;
            //}
            //else if (match.HomeScore > match.VisitorScore)
            //{
            //    homeDelta.GamesWon++;
            //    visitorDelta.GamesLost++;
            //    homeDelta.TournamentPoints += TournamentPointsForWinning;    // TODO: Take this value from tournament config 
            //}
            //else
            //{
            //    homeDelta.GamesLost++;
            //    visitorDelta.GamesWon++;
            //    visitorDelta.TournamentPoints += TournamentPointsForWinning;
            //}

            //// Update home team
            //ev.IdTeam = match.IdHomeTeam;
            //UpdateTeamDayResult(c, t, ev, tdr => ApplyTeamDayResultDelta(tdr, homeDelta), match);

            //// Update visitor team
            //ev.IdTeam = match.IdVisitorTeam;
            //UpdateTeamDayResult(c, t, ev, tdr => ApplyTeamDayResultDelta(tdr, visitorDelta), match);
        }


        private static void ApplyTeamDayResultDelta(TeamDayResult tdr, TeamDayResult delta)
        {
            tdr.GamesDraw += delta.GamesDraw;
            tdr.GamesWon += delta.GamesWon;
            tdr.GamesLost += delta.GamesLost;
            tdr.TournamentPoints += delta.TournamentPoints;
            tdr.GamesPlayed += delta.GamesPlayed;
        }


        // __ Event stats _____________________________________________________

        private static Match ApplyStatsChange(
            IDbConnection c, IDbTransaction t, MatchEvent ev,
            Action<MatchPlayer> matchPlayerCallback = null,
            Action<PlayerDayResult> playerDayCallback = null,
            Action<TeamDayResult> teamDayCallback = null,
            Action<TeamDayResult> otherTeamCallback = null,
            Action<Match> matchCallback = null, 
            Action<IDbConnection, IDbTransaction, MatchEvent, Match> finalCallback = null,
            bool checkTeams = true,
            bool insertEvent = true)
        {
            if (ev.IdMatch == 0) throw new ArgumentException("idMatch");

            

            // Get the existing match record
            var dbMatch = c.Get<Match>(ev.IdMatch, t);
            if (dbMatch == null) throw new Exception("Error.NotFound");

            // Validate that the operation affects the involved teams
            if (checkTeams)
            {
                if (ev.IdTeam != dbMatch.IdHomeTeam && ev.IdTeam != dbMatch.IdVisitorTeam) throw new Exception("Error.InvalidTeam");
            }

            ev.IdDay = dbMatch.IdDay;

            // Create matchevent
            if (insertEvent)
            {
                ev.TimeStamp = DateTime.Now;
                c.Insert(ev, t);
            }

            if (ev.IdTeam > 0)
            {
                if (ev.IdPlayer > 0)
                {
                    UpdatePlayerDayResult(c, t, ev, playerDayCallback, dbMatch);
                    UpdateMatchPlayer(c, t, ev, matchPlayerCallback, dbMatch);
                }

                UpdateTeamDayResult(c, t, ev, teamDayCallback, dbMatch);
                var otherTeamEv = new MatchEvent { IdDay = ev.IdDay, IdMatch = ev.IdMatch, IdTeam = GetOtherTeam(dbMatch, ev.IdTeam) };
                UpdateTeamDayResult(c, t, otherTeamEv, otherTeamCallback, dbMatch);
            }

            var match = UpdateMatch(c, t, ev, matchCallback, dbMatch);

            finalCallback?.Invoke(c, t, ev, match);

            return match;
        }

        private static long GetOtherTeam(Match m, long idTeam)
        {
            if (m.IdHomeTeam == idTeam) return m.IdVisitorTeam;
            if (m.IdVisitorTeam == idTeam) return m.IdHomeTeam;

            throw new Exception("Error.InvalidTeam");
        }

        private static void UpdatePlayerDayResult(IDbConnection c, IDbTransaction t, MatchEvent ev, Action<PlayerDayResult> playerDayCallback, Match dbMatch)
        {
            // Update playerDayResults      // Resultados del jugador en la jornada (acumulado)

            if (playerDayCallback == null) return;
            
            var playerDayResult = c.QuerySingleOrDefault<PlayerDayResult>(
                "SELECT * FROM playerdayresults WHERE idDay = @idDay AND idTournament = @idTournament AND idPlayer = @idPlayer AND idTeam = @idTeam", 
                new { idDay = ev.IdDay, idPlayer = ev.IdPlayer, idTournament = dbMatch.IdTournament, idTeam = ev.IdTeam }, t);

            var isCreatingPlayerDayResult = false;

            if (playerDayResult == null)
            {
                // Record does not exist, create
                playerDayResult = new PlayerDayResult
                {
                    // Fill in initial field values
                    IdDay = ev.IdDay,
                    IdPlayer = ev.IdPlayer,
                    IdTeam = ev.IdTeam,
                    IdTournament = dbMatch.IdTournament,
                    IdStage = dbMatch.IdStage,
                    IdGroup = dbMatch.IdGroup
                };

                isCreatingPlayerDayResult = true;
            }

            UpdateOrInsert(c, t, playerDayCallback, playerDayResult, isCreatingPlayerDayResult);
        }

        //private static T GetSingleRecord<T>(IDbConnection c, string sql, object param = null, IDbTransaction t = null) where T: class
        //{
        //    var result = c.QuerySingle<T>(sql, param, t);

        //    if (result )
        //}

        private static void UpdateMatchPlayer(IDbConnection c, IDbTransaction t, MatchEvent ev, Action<MatchPlayer> callback, Match dbMatch)
        {
            // Update matchPlayer      // Resultados del jugador en el partido (no acumulativo)

            if (callback == null) return;
            
            var target = c.QueryFirstOrDefault<MatchPlayer>("SELECT * FROM matchPlayers WHERE idMatch = @idMatch AND idPlayer = @idPlayer AND idTeam = @idTeam", 
                new { idMatch = ev.IdMatch, idPlayer = ev.IdPlayer, idTeam = ev.IdTeam }, t);

            // MatchPlayer must exist at this point (created when the match is scheduled)
            if (target == null) throw new Exception("Error.MatchPlayerData missing");

            if (callback != null)
            {
                callback(target);
                c.Update(target, t);
            }
        }

        private static void UpdateTeamDayResult(IDbConnection c, IDbTransaction t, MatchEvent ev, Action<TeamDayResult> teamDayCallback, Match dbMatch)
        {
            // Update teamDayResults        // Resultados del equipo en la jornada (acumulado)

            if (teamDayCallback == null) return;
            
            var teamDayResult = c.QueryFirstOrDefault<TeamDayResult>(
                "SELECT * FROM teamdayresults WHERE idDay = @idDay AND idTournament = @idTournament AND idTeam = @idTeam",
                new { idDay = ev.IdDay, idTeam = ev.IdTeam, idTournament = dbMatch.IdTournament }, t);

            var isNew = false;

            if (teamDayResult == null)
            {
                // Record does not exist, create
                teamDayResult = new TeamDayResult
                {
                    // Fill in initial field values
                    IdDay = ev.IdDay,
                    IdTeam = ev.IdTeam,
                    IdTournament = dbMatch.IdTournament,
                    IdStage = dbMatch.IdStage,
                    IdGroup = dbMatch.IdGroup
                };

                //teamDayResult = GetTeamDayResultHistory(c, t, ev.IdDay, ev.IdTeam, dbMatch.IdTournament);

                isNew = true;
            }

            UpdateOrInsert(c, t, teamDayCallback, teamDayResult, isNew);
        }

        // This is not correct, have to use this to query, not to insert: this query assumes that teamdayresults hold the independent day results, not
        // the accumulated. But the solution is probably to use this as the 
        //private static TeamDayResult GetTeamDayResultHistory(IDbConnection c, IDbTransaction t, long idDay, long idTeam, long idTournament)
        //{
        //    var sum = c.QueryFirstOrDefault<TeamDayResult>(@"
        //        SELECT 
        //            sum(gamesplayed) as gamesplayed, sum(gameswon) as gameswon, sum(gamesdraw) as gamesdraw, sum(gameslost) as gameslost,
        //            sum(points) as points, sum(pointdiff) as pointdiff, sum(sanctions) as sanctions, sum(tournamentpoints) as tournamentpoints
        //        FROM teamdayresults 
        //        WHERE idtournament = @idTournament AND idTeam = @idTeam",
        //        new { idTournament = idTournament, idTeam = idTeam }, t);

        //    if (sum == null) sum = new TeamDayResult();

        //    sum.IdTournament = idTournament;
        //    sum.IdTeam = idTeam;
        //    sum.IdDay = idDay;

        //    return sum;            
        //}

        private static Match UpdateMatch(IDbConnection c, IDbTransaction t, MatchEvent ev, Action<Match> callback, Match dbMatch)
        {
            // Update match

            if (callback == null) return dbMatch;

            callback(dbMatch);
            c.Update(dbMatch, t);

            return dbMatch;
        }

        // 🚧🚧🚧 Refarctor duplicity between controllers and Models
        private static IEnumerable<Sanction> GetSanctionsMatch(IDbConnection c, long idMatch)
        {
            return c.Query<Sanction>("SELECT * FROM sanctions WHERE idMatch = @idMatch", new { idMatch = idMatch });
        }
        public static void CreateTeamSanctionPenalty(IDbConnection c, IDbTransaction t, Sanction sanction)
        {
            var eventsToCreate = new List<MatchEvent>();

            // lost match penalty
            if (sanction.LostMatchPenalty > 0)
            {
                // retrieve the match, if the team is winner, add match events for losing
                var match = c.Get<Match>(sanction.IdMatch);
                if (match == null) return;

                var lostMatchEvents = GetLostMatchEvents(match, sanction.IdTeam);
                if (lostMatchEvents != null) eventsToCreate.AddRange(lostMatchEvents);
            }

            // tournament points penalty
            if (sanction.TournamentPointsPenalty > 0)
            {
                eventsToCreate.Add(new MatchEvent
                {
                    Type = (int)MatchEventType.ChangeTeamStats,
                    IntData1 = -sanction.TournamentPointsPenalty,
                    IdMatch = sanction.IdMatch,
                    IdTeam = sanction.IdTeam,
                    MatchMinute = 200,
                    IsAutomatic = true
                });
            }

            var createdEventIds = CreateMatchEvents(c, t, eventsToCreate);

            UpdateSanctionMatchEvents(c, t, sanction, createdEventIds);
        }

        public static void UpdateSanctionMatchEvents(IDbConnection c, IDbTransaction t, Sanction sanction, IEnumerable<long> eventIds)
        {
            sanction.SanctionMatchEvents = JsonConvert.SerializeObject(eventIds);
            c.Update(sanction, t);
        }

        public static IEnumerable<long> CreateMatchEvents(IDbConnection c, IDbTransaction t, IEnumerable<MatchEvent> events)
        {
            var result = new List<long>();

            if (events != null)
            {
                foreach (var ev in events)
                {
                    var (_, me) = MatchEvent.Create(c, t, ev);
                    result.Add(me.Id);
                }
            }

            return result;
        }
        
        public static IEnumerable<MatchEvent> GetLostMatchEvents(Match m, long idTeam)
        {
            var isHomeTeam = (idTeam == m.IdHomeTeam);
            var isDraw = m.HomeScore == m.VisitorScore;
            var homeWon = m.HomeScore > m.VisitorScore;
            var homeLost = !homeWon;

            const int win = MatchEvent.TournamentPointsForWinning;
            const int draw = MatchEvent.TournamentPointsForDraw;

            if (isHomeTeam)
            {
                // sanctioned team is home
                if (homeWon)
                {
                    // sanctioned team won the match, create events to compensate victory
                    return CreateCompensationEvents(m, m.IdHomeTeam, m.IdVisitorTeam, -win, win, -1, 1, 0, 0, 1, -1);
                }
                else if (isDraw)
                {
                    // remove 1 point from home, add 2 to visitor
                    return CreateCompensationEvents(m, m.IdHomeTeam, m.IdVisitorTeam, -draw, win - draw, 0, 1, -1, -1, 1, 0);
                }
            }
            else
            {
                // sanctioned team is visitor
                if (isDraw)
                {
                    // remove 1 point from visitor, add 2 to home
                    return CreateCompensationEvents(m, m.IdHomeTeam, m.IdVisitorTeam, win - draw, -draw, 1, 0, -1, -1, 0, 1);
                }
                else if (!homeWon)
                {
                    // sanctioned team (visitor) won the match, create events to compensate victory
                    return CreateCompensationEvents(m, m.IdHomeTeam, m.IdVisitorTeam, win, -win, 1, -1, 0, 0, -1, 1);
                }
            }

            return null;
        }
        
        private static IEnumerable<MatchEvent> CreateCompensationEvents(Match m, long idTeam1, long idTeam2, int points1, int points2, int gamesWon1, int gamesWon2, int gamesDraw1, int gamesDraw2, int gamesLost1, int gamesLost2)
        {
            var result = new List<MatchEvent>();

            result.Add(new MatchEvent
            {
                Type = (int)MatchEventType.ChangeTeamStats,
                IntData1 = points1,
                IntData2 = gamesWon1,
                IntData3 = gamesDraw1,
                IntData4 = gamesLost1,
                IdMatch = m.Id,
                IdTeam = idTeam1,
                MatchMinute = 200,
                IsAutomatic = true
            });

            result.Add(new MatchEvent
            {
                Type = (int)MatchEventType.ChangeTeamStats,
                IntData1 = points2,
                IntData2 = gamesWon2,
                IntData3 = gamesDraw2,
                IntData4 = gamesLost2,
                IdMatch = m.Id,
                IdTeam = idTeam2,
                MatchMinute = 200,
                IsAutomatic = true
            });

            return result;
        }
        // 🚧🚧🚧

        private static void UpdateOrInsert<T>(IDbConnection c, IDbTransaction t, Action<T> callback, T target, bool isNew) where T: class
        {
            if (callback != null)
            {
                callback(target);
                if (isNew)
                    c.Insert(target, t);
                else
                    c.Update(target, t);
            }
        }
    }






    public class MatchEventAutoSanctionComparer : IEqualityComparer<MatchEvent>
    {
        public bool Equals(MatchEvent e1, MatchEvent e2)
        {
            return (e1.MatchMinute == e2.MatchMinute && e1.Type == e2.Type);
        }

        public int GetHashCode(MatchEvent obj)
        {
            return (obj.MatchMinute.ToString() + obj.Type.ToString()).GetHashCode();
        }
    }


    public enum MatchEventType
    {
        MatchStarted            = 1,
        FirstHalfFinish         = 10,
        SecondHalfStarted       = 11,
        SecondHalfFinish        = 12,
        MatchEnded              = 13,
        ExtraTimeFirstHalfStart = 15,
        ExtraTimeFirstHalfEnd   = 16,
        ExtraTimeSecondHalfStart = 17,
        ExtraTimeSecondHalfEnd  = 18,

        Assist                  = 30,
        Point                   = 31,
        PointInOwn              = 32,
        Corner                  = 33,
        Fault                   = 34,
        
        Penalty                 = 40,
        PenaltyFailed           = 41,
        PenaltyStopped          = 42,

        Injury                  = 50,

        CardStart               = 60,
        Card1                   = 61,
        Card2                   = 62,
        Card3                   = 63,
        Card4                   = 64,
        Card5                   = 65,

        MVP                     = 70,
            
        RecordClosed            = 100,

        // Maintenance event types: for sanctions and other regularization of the player and team points
        AddToPdrData1 = 1001, 
        ChangeTeamStats = 1002,
    }
}
