using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using webapi.Models.Db;

namespace webapi
{
    public class LeagueClassification
    {
        public static IEnumerable<TeamDayResult> SortClassification(IEnumerable<TeamDayResult> accumulatedTeamDays, int[] orderCriteria, MatchProviderDelegate matchProvider = null)
        {
            var comparer = new ClassificationSorter(orderCriteria, matchProvider);
            var result = accumulatedTeamDays.OrderBy<TeamDayResult, TeamDayResult>(t => t, comparer);

            return result.ToList(); // Enumerate to execute sorting before returning.
        }
    }

    internal class ClassificationSorter : IComparer<TeamDayResult>
    {
        public ClassificationSorter(int[] orderCriteria, MatchProviderDelegate matchResultProvider)
        {
            mOrderCriteria = orderCriteria;
            mMatchProvider = matchResultProvider;
        }

        public int Compare(TeamDayResult x, TeamDayResult y)
        {
            var result = 0;

            foreach (var algorithmIndex in mOrderCriteria)
            {
                if (algorithmIndex < 0) continue;

                var sorter = SortingAlgorithms[algorithmIndex];

                result = sorter(x, y, mMatchProvider);

                if (result != 0) return result;
            }

            return result;
        }


        // __ Comparison algorithms ___________________________________________


        private static int CompareTournamentPoints(TeamDayResult a, TeamDayResult b, MatchProviderDelegate p)
        {
            return b.TournamentPoints - a.TournamentPoints;
        }

        private static int ComparePointDiff(TeamDayResult a, TeamDayResult b, MatchProviderDelegate p)
        {
            return b.PointDiff - a.PointDiff;
        }

        private static int CompareGamesWon(TeamDayResult a, TeamDayResult b, MatchProviderDelegate p)
        {
            return b.GamesWon - a.GamesWon;
        }

        private static int CompareDirectConfrontationPointDiff(TeamDayResult a, TeamDayResult b, MatchProviderDelegate matchesProvider)
        {
            if (matchesProvider == null) throw new Exception("Error.NoMatchComparerProvider");

            var matches = matchesProvider(a.IdTeam, b.IdTeam);
            if (matches.Count() == 0) return 0;

            // Go through the list of matches, sum points, return diff

            var teamAPoints = 0;
            var teamBPoints = 0;

            foreach (var m in matches)
            {
                teamAPoints += (m.IdHomeTeam == a.IdTeam) ? m.HomeScore : m.VisitorScore;
                teamBPoints += (m.IdHomeTeam == b.IdTeam) ? m.HomeScore : m.VisitorScore;
            }

            return teamBPoints - teamAPoints;
        }

        private static int CompareDirectConfrontationPoints(TeamDayResult a, TeamDayResult b, MatchProviderDelegate matchesProvider)
        {
            return 0;
        }

        private static int ComparePoints(TeamDayResult a, TeamDayResult b, MatchProviderDelegate matchesProvider)
        {
            return b.Points - a.Points;
        }


        private int[] mOrderCriteria;
        private MatchProviderDelegate mMatchProvider;

        public static readonly SortingAlgorithmDelegate[] SortingAlgorithms = new SortingAlgorithmDelegate[]
        {
            CompareTournamentPoints,
            ComparePointDiff, 
            CompareGamesWon,
            CompareDirectConfrontationPointDiff,
            ComparePoints, //CompareDirectConfrontationPoints
        };


        public delegate int SortingAlgorithmDelegate(TeamDayResult teamA, TeamDayResult teamB, MatchProviderDelegate matchProvider);
    }


    public class MatchFilter
    {
        public MatchFilter(IEnumerable<Match> matches)
        {
            mMatches = matches;
        }

        public IList<Match> GetMatchesForTeams(long idTeamA, long idTeamB)
        {
            var result = new List<Match>();

            foreach (var m in mMatches)
            {
                if ((idTeamA == m.IdHomeTeam && idTeamB == m.IdVisitorTeam) ||
                     (idTeamB == m.IdHomeTeam && idTeamA == m.IdVisitorTeam))
                {
                    result.Add(m);
                }
            }

            return result;
        }

        private IEnumerable<Match> mMatches; 
    }


    public delegate IList<Match> MatchProviderDelegate(long idTeamA, long idTeamB);
}
