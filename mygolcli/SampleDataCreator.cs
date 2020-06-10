using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Dapper;
using Dapper.Contrib.Extensions;
using webapi.Models.Db;

namespace mygolcli
{
    public class SampleDataCreator
    {
        public static void Create(IDbConnection conn)
        {

        }


        public static void CreateTournaments(IDbConnection c)
        {
            var cat = CreateCategories(c);
            var mode = CreateTournamentModes(c);
            var season = CreateSeasons(c);

            Console.WriteLine("Creating sample tournaments");
            c.Insert(new Tournament { IdCategory = cat, IdSeason = season, IdTournamentMode = mode, Status = 1, Type = 1, Name = "Liga nocturna FUT5" });
            c.Insert(new Tournament { IdCategory = cat, IdSeason = season, IdTournamentMode = mode, Status = 1, Type = 1, Name = "Liga sábados FUT5" });
            c.Insert(new Tournament { IdCategory = cat, IdSeason = season, IdTournamentMode = mode, Status = 1, Type = 2, Name = "Mundial 2018" });
        }

        public static long CreateTournamentModes(IDbConnection c)
        {
            Console.WriteLine("Creating sample tournament modes");
            c.Insert(new TournamentMode { Name = "Fútbol 6", NumPlayers = 6 });
            c.Insert(new TournamentMode { Name = "Fútbol 7", NumPlayers = 7 });
            c.Insert(new TournamentMode { Name = "Fútbol 11", NumPlayers = 11 });
            return c.Insert(new TournamentMode { Name = "Fútbol 5", NumPlayers = 5 });
        }

        public static long CreateCategories(IDbConnection c)
        {
            Console.WriteLine("Creating sample tournament categories");
            c.Insert(new Category { Name = "Senior" });
            return c.Insert(new Category { Name = "Junior" });
        }

        public static long CreateSeasons(IDbConnection c)
        {
            Console.WriteLine("Creating sample seasons");
            c.Insert(new Season { Name = "2016" });
            c.Insert(new Season { Name = "2017" });
            return c.Insert(new Season { Name = "2018" });
        }

    }
}
