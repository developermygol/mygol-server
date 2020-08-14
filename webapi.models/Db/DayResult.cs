using contracts;
using System;
using System.Collections.Generic;
using System.Data;
using Dapper;
using Dapper.Contrib.Extensions;

namespace webapi.Models.Db
{
    public class TeamDayResult
    {
        [ExplicitKey] public long IdDay { get; set; }
        [ExplicitKey] public long IdTeam { get; set; }
        [ExplicitKey] public long IdTournament { get; set; }
        [ExplicitKey] public long IdStage { get; set; }
        [ExplicitKey] public long IdGroup { get; set; }
        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int GamesDraw { get; set; }
        public int GamesLost { get; set; }
        public int Points { get; set; }
        public int PointsAgainst { get; set; }
        public int PointDiff { get; set; }
        public int Sanctions { get; set; }

        public int Ranking1 { get; set; }
        public int Ranking2 { get; set; }
        public int Ranking3 { get; set; }
        public int TournamentPoints { get; set; }

        public TeamDayResult()
        {

        }

        public TeamDayResult(int gamesPlayed = 0, int gamesWon = 0, int gamesDraw = 0, int gamesLost = 0, int points = 0, int pointsAgainst = 0, int pointDiff = 0, int tournamentPoints = 0)
        {
            GamesPlayed = gamesPlayed;
            GamesWon = gamesWon;
            GamesDraw = gamesDraw;
            GamesLost = gamesLost;
            Points = points;
            PointsAgainst = pointsAgainst;
            PointDiff = pointDiff;
            TournamentPoints = tournamentPoints;
        }
    }

    public class PlayerDayResult
    {
        [ExplicitKey] public long IdDay { get; set; }
        [ExplicitKey] public long IdPlayer { get; set; }
        [ExplicitKey] public long IdTeam { get; set; }
        [ExplicitKey] public long IdTournament { get; set; }
        [ExplicitKey] public long IdStage { get; set; }
        [ExplicitKey] public long IdGroup { get; set; }
        public long IdUser { get; set; }

        public int GamesPlayed { get; set; }
        public int GamesWon { get; set; }
        public int GamesDraw { get; set; }
        public int GamesLost { get; set; }
        public int Points { get; set; }
        public int PointsAgainst { get; set; }
        public int PointsInOwn { get; set; }

        public int CardsType1 { get; set; }
        public int CardsType2 { get; set; }
        public int CardsType3 { get; set; }
        public int CardsType4 { get; set; }
        public int CardsType5 { get; set; }

        public int Ranking1 { get; set; }
        public int Ranking2 { get; set; }
        public int Ranking3 { get; set; }
        public int Ranking4 { get; set; }
        public int Ranking5 { get; set; }

        public int Data1 { get; set; }      // Num.of accumulated yellow cards for automatic cycle sanctions, filled with each yellow card event, but discounted with sanction rules. Automatic sanctions generate a hidden event that discounts this value. 
        public int Data2 { get; set; }
        public int Data3 { get; set; }
        public int Data4 { get; set; }
        public int Data5 { get; set; }
        public int Assistances { get; set; }
        public int MVPPoints { get; set; }

        [Write(false)] public string PlayerName { get; set; }
        [Write(false)] public string PlayerSurname { get; set; }
    }
}
