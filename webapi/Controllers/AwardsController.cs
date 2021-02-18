using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using webapi.Models.Db;
using Microsoft.Extensions.Options;
using Dapper;
using Dapper.Contrib.Extensions;
using contracts;
using Utils;
using Microsoft.AspNetCore.Http;
using Serilog;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace webapi.Controllers
{
    public class AwardsController : CrudController<Award>
    {
        public AwardsController(IOptions<PostgresqlConfig> dbOptions, IOptions<Config> config) : base(config)
        {          
        }

        protected override CrudConfig GetConfig()
        {
            return new CrudConfig
            {
                TableName = "awards"
            };
        }

        [HttpGet("{idAward}")]
        public override IActionResult Get(long idAward)
        {
            return DbOperation(c =>
            {
                var award = c.Get<Award>(idAward);

                if(award == null) throw new Exception("Error.AwardDoesNotExists");

                award.Player = GetPlayerData(c, award.IdPlayer);
                award.Day = GetDayData(c, award.IdDay);
                award.Tournament = GetTorunamentData(c, award.IdTournament);
                award.Team = GetTeamData(c, award.IdTeam);

                return award;
            });
        }

        private Player GetPlayerData(IDbConnection c, long idPlayer)
        {
            string query = @"
                SELECT p.id, p.iduser, p.name, p.surname, p.birthdate, p.height, p.weight, p.enrollmentstep, p.approved, u.id, u.avatarimgurl
                FROM players p
                INNER JOIN users u ON u.id = p.iduser 
                WHERE p.id = @idPlayer
                ";

            var result = c.Query<Player, User, Player>(query,
                (player, user) =>
                {
                    
                    player.UserData = user;
                    return player;
                },
                new { idPlayer = idPlayer },
                splitOn: "id");

            return result.First();
        }

        private PlayDay GetDayData(IDbConnection c, long idDay)
        {
            string query = $"SELECT name, idtournament, idstage, idgroup, sequenceorder, status, dates, id FROM playdays WHERE id = {idDay};";
            var result = c.QueryFirst<PlayDay>(query);
            return result;
        }

        private Tournament GetTorunamentData(IDbConnection c, long idTournament)
        {
            string query = $"SELECT name, type, idseason, idtournamentmode, idcategory, visible, sequenceorder, notificationFlags, id FROM tournaments WHERE id = {idTournament};";
            var result = c.QueryFirst<Tournament>(query);
            return result;
        }

        private Team GetTeamData(IDbConnection c, long idTeam)
        {
            string query = $"SELECT name, logoImgUrl, idField, status, idTactic, idGoalKeeper, id FROM teams WHERE id = {idTeam};";
            var result = c.QueryFirst<Team>(query);
            return result;
        }

        private bool IsAdminOrSelf(long idUser)
        {
            return IsOrganizationAdmin() || idUser == GetUserId();
        }

        protected override bool IsAuthorized(RequestType reqType, Award target, IDbConnection conn)
        {
            return AuthByRequestType(list: UserLevel.All, add: UserLevel.OrgAdmin, edit: UserLevel.OrgAdmin, delete: UserLevel.OrgAdmin);
        }

        protected override bool ValidateNew(Award value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateEdit(Award value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }

        protected override bool ValidateDelete(Award value, IDbConnection conn, IDbTransaction t)
        {
            return true;
        }
    }
}
