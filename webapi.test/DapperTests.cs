using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Dapper;
using Dapper.Contrib.Extensions;
using webapi.Models.Db;
using System.Linq;

namespace webapi.test
{
    [TestClass]
    public class DapperTests
    {


        [TestMethod]
        public void MultiMapping2()
        {
            var sql = "SELECT * FROM players p JOIN users u ON p.iduser = u.id";

            using (var c = new PostgresqlDataLayer(mConfig).GetConn())
            {
                var players = c.Query<Player, User, Player>(sql, (player, user) =>
                {
                    player.UserData = user;
                    return player;
                },
                new { },
                splitOn: "id").AsList();

                Assert.IsTrue(players.Count > 0);
            }
        }

        [TestMethod]
        public void MultiMapping3()
        {
            var sql = "SELECT p.*, email, mobile, avatarimgurl, status FROM players p JOIN teamplayers t ON t.idplayer = p.id JOIN users u ON p.iduser = u.id WHERE t.idteam = @idteam";

            using (var c = new PostgresqlDataLayer(mConfig).GetConn())
            {
                var players = c.Query<Player, User, TeamPlayer, Player>(
                    sql,
                    (player, user, teamPlayer) =>
                    {
                        player.UserData = user;
                        player.TeamData = teamPlayer;
                        return player;
                    },
                new { idteam = 9 },
                splitOn: "email, status").AsList();

                Assert.IsTrue(players.Count > 0);
            }
        }


        [TestMethod]
        public void LinqGroupBy()
        {
            var sql = "SELECT * FROM matchevents ORDER BY idMatch LIMIT 100";

            using (var c = new PostgresqlDataLayer(mConfig).GetConn())
            {
                var events = c.Query<MatchEvent>(sql);

                var eventsByMatch = events.GroupBy(e => e.IdMatch, e => e, (key, group) => new { IdMatch = key, Events = group });
            }
        }



        private PostgresqlConfig mConfig = new PostgresqlConfig
        {
            User = "aemf",
            Password = "aemf",
            DatabaseName = "mygol_aemf"
        };
    }
}
