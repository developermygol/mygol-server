using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using webapi;
using webapi.Controllers;
using webapi.Models.Db;
using System.Threading.Tasks;
using contracts;
using storage.disk;
using Dapper;
using Dapper.Contrib.Extensions;
using System.IO;
using System.Text;
using webapi.Importers;

namespace mygolcli
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (!CheckUsage(args)) return;

                ProcessArgs(args);

                Timer = Stopwatch.StartNew();

                InitConfig();

                if (OptAllOrgs)
                    RunCommandOnAllOrgs();
                else
                    RunCommand(mArgs[0]);

                //Log($"Time elapsed: {Timer.Elapsed}");
            }
            catch (Exception ex)
            {
                Log($"ERROR: {ex.Message} {ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}");
            }
        }


        // __ Commands ________________________________________________________


        public static void CreateDb()
        {
            if (mArgs.Count <= 1)
            {
                Log("Usage: CreateDb <org|global> [drop]");
                return;
            }

            bool drop = (mArgs.Count == 3 && mArgs[2].ToLower() == "drop");

            switch (mArgs[1].ToLower())
            {
                case "org":
                    mDbConfig.DeleteExistingDb = drop;
                    mDbConfig.AdminUser = mAdminDbConfig.AdminUser;
                    mDbConfig.AdminPassword = mAdminDbConfig.AdminPassword;
                    Log("Creating organization database: " + mDbConfig.DatabaseName);
                    var dl = new PostgresqlDataLayer(mDbConfig);
                    dl.CreateOrgDb();
                    break;
                case "global":
                    
                    var config = GetGlobalDbConfig();
                    config.DeleteExistingDb = drop;
                    config.AdminUser = mAdminDbConfig.AdminUser;
                    config.AdminPassword = mAdminDbConfig.AdminPassword;
                    Log("Creating global database: " + config.DatabaseName);
                    var dl2 = new PostgresqlDataLayer(config);
                    dl2.CreateGlobalDb();
                    break;
                default:
                    throw new Exception("Unknown option " + mArgs[1]);
            }
        }

        public static void GetUserPin()
        {
            if (mArgs.Count != 2)
            {
                Log("Usage: GetUserPin <useremail>");
                return;
            }

            //if (!long.TryParse(mArgs[1], out long idUser)) throw new ArgumentException("userid");

            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                var dbUser = c.QueryFirstOrDefault<User>("SELECT * FROM users WHERE email ilike @email", new { email = mArgs[1] });
                if (dbUser == null) throw new Exception("Email not found");

                var user = new User
                {
                    Id = dbUser.Id,
                    Email = mArgs[1]
                };

                var tokenManager = new AuthTokenManager(new TokenAuthConfig());

                var pin = UsersController.GetActivationPin(tokenManager, user);
                Log($"Pin: {pin}");
            }
        }

        public static void RandomizeTeams()
        {
            if (mArgs.Count != 2)
            {
                Log("Usage: RandomizeTeams <idTournament>");
                return;
            }

            if (!long.TryParse(mArgs[1], out long idTournament)) throw new ArgumentException("idTournament");

            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                var t = c.BeginTransaction();

                try
                {
                    var teams = c.Query<Team>("SELECT id, name FROM teams t JOIN tournamentteams tt ON tt.idTeam = t.id WHERE tt.idTournament = @idTournament", new { idTournament }, t);

                    var sql = new StringBuilder();

                    foreach (var team in teams)
                    {
                        sql.AppendFormat("UPDATE teams SET name = '{0}', logoImgUrl = '' WHERE id = {1};\r\n", NameGenerator.GetTeamName(team.Id), team.Id);
                    }

                    var query = sql.ToString();
                    c.Execute(query, t);

                    t.Commit();
                }
                catch
                {
                    t.Rollback();
                    throw;
                }
            }
        }

        public static void RandomizePlayers()
        {
            if (mArgs.Count != 2)
            {
                Log("Usage: RandomizePlayers <idTournament>");
                return;
            }

            if (!long.TryParse(mArgs[1], out long idTournament)) throw new ArgumentException("idTournament");

            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                var t = c.BeginTransaction();

                try
                {
                    var players = c.Query<Player>("SELECT id, idUser FROM players p JOIN teamplayers tp ON tp.idPlayer = p.id JOIN tournamentteams tt ON tt.idteam = tp.idTeam WHERE tt.idTournament = @idTournament", new { idTournament }, t);

                    var sql = new StringBuilder();

                    foreach (var player in players)
                    {
                        var name = NameGenerator.GetRandomName();
                        var surname = NameGenerator.GetRandomSurname();

                        sql.AppendFormat("UPDATE players SET name = '{0}', surname='{1}' WHERE id = {2};\r\n", name, surname, player.Id);
                        sql.AppendFormat("UPDATE users SET name = '{0}' WHERE id = {1};\r\n", name + " " + surname, player.IdUser);
                    }

                    var query = sql.ToString();
                    c.Execute(query, t);

                    t.Commit();
                }
                catch
                {
                    t.Rollback();
                    throw;
                }
            }
        }

        public static void SyncUsersToGlobal()
        {

            if (OptOrganizationName == null)
            {
                Log("--org option is mandatory for this command");
                Log("Sample SyncUsersToGlobal --org=\"aemf\"");
            }

            int type = 0;
            if (mArgs.Count >= 2)
            {
                if (!int.TryParse(mArgs[1], out type) || type < 1 || type > 4) throw new Exception("Invalid type. Supported types: 1, 2, 3 and 4");
            }

            Log($"Creating entries for users {(type > 0 ? "" : $"(type {type})")} in org {OptOrganizationName} (DB: {mDbConfig.DatabaseName}) into global directory.");
                        
            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                var condition = (type > 0) ? "WHERE type = " + type : "";

                var users = c.Query<User>($"SELECT id, email FROM users {condition} ORDER BY id");

                using (var globalConn = new PostgresqlDataLayer(GetGlobalDbConfig()).GetConn())
                {
                    foreach (var user in users)
                    {
                        try
                        {
                            globalConn.Insert(new GlobalUserOrganization
                            {
                                Email = user.Email,
                                IdUser = user.Id,
                                OrganizationName = OptOrganizationName
                            });
                        }
                        catch (Exception ex)
                        {
                            Log($"Error inserting '{user.Email}' ({user.Id}): {ex.Message}");
                        }
                    }
                }                       
            }
        }

        public static void ListOrgs()
        {
            foreach (var org in OrganizationManager.GetNames()) Log(org);
        }

        public static void ResetNotificationTemplates()
        {
            var dl = new PostgresqlDataLayer(mDbConfig);
            using (var c = dl.GetConn())
            {
                c.Execute("DELETE FROM notificationtemplates");
                dl.InsertNotificationTemplates(c);
                Log("Templates reset to platform defaults");
            }
        }

        public static void DbVersion()
        {
            var u = new DataUpdater(mDbConfig);
            Log($"Current DB version: {u.GetCurrentVersion().ToString()}");
        }

        public static void UpgradeDb()
        {
            var u = new DataUpdater(mDbConfig);
            u.Upgrade();
        }

        public static void DowngradeDb()
        {
            if (mArgs.Count < 2)
            {
                Log("Usage: Downgrade <toVersion>");
                return;
            }

            var targetVersion = int.Parse(mArgs[1]);

            var u = new DataUpdater(mDbConfig);
            u.Downgrade(targetVersion);
        }

        public static void GlobalDbVersion()
        {
            var globalConfig = GetGlobalDbConfig();
            var u = new GlobalDataUpdater(globalConfig);
            Log($"Current DB version: {u.GetCurrentVersion().ToString()}");
        }

        public static void GlobalUpgradeDb()
        {
            var globalConfig = GetGlobalDbConfig();
            var u = new GlobalDataUpdater(globalConfig);
            u.Upgrade();
        }

        public static void GlobalDowngradeDb()
        {
            if (mArgs.Count < 2)
            {
                Log("Usage: Downgrade <toVersion>");
                return;
            }

            var targetVersion = int.Parse(mArgs[1]);

            var globalConfig = GetGlobalDbConfig();
            var u = new GlobalDataUpdater(globalConfig);
            u.Downgrade(targetVersion);
        }

        public static void InsertNotificationTemplates()
        {
            var dl = new PostgresqlDataLayer(mDbConfig);
            using (var c = dl.GetConn())
            {
                dl.InsertNotificationTemplates(c);
            }
        }


        // __ News export _____________________________________________________


        public static void ExportNews()
        {
            if (mArgs.Count != 3)
            {
                Log("Usage: ExportNews <outputDir> <uploadsDir>");
                return;
            }

            var destPath = mArgs[1];
            var sourcePath = mArgs[2];
            var fileName = Path.Combine(destPath, "news.json");

            Directory.CreateDirectory(destPath);

            ExportToJson<Content>(fileName, "SELECT * FROM contents", (row) =>
            {
                CopyFile(sourcePath, destPath, row.MainImgUrl, row.Id);
            });
        }

        public static void ImportNews()
        {
            if (mArgs.Count != 3)
            {
                Log("Usage: ImportNews <inputDir> <uploadsDir>");
                return;
            }

            var sourcePath = mArgs[1];
            var destPath = mArgs[2];
            var fileName = Path.Combine(sourcePath, "news.json");

            ImportFromJson<Content>(fileName, (row) =>
            {
                CopyFile(sourcePath, destPath, row.MainImgUrl, row.Id);
            });
        }

        public static void PrintOrgData()
        {
            if (mArgs.Count != 3)
            {
                Log("Usage: PrintOrgData <orgName> <baseDomainNoHTTP>");
                Log("  Sample: PrintOrgData FutPlaya futplaya.mygol.es");
                return;
            }

            var org = mArgs[1];
            var orgL = org.ToLower();
            var domain = mArgs[2];

            var data = new OrganizationDispatchData
            {
                Name = org,
                DomainName = domain,

                DbName = $"mygol_{orgL}",
                DbUser = "aemf",
                DbPassword = "aemf",

                PrivateWebBaseUrl = $"http://{domain}/admin",
                PublicWebBaseUrl = $"http://{domain}",

                PrivateStaticBaseUrl = $"http://{domain}/admin/static",
                PublicStaticBaseUrl = $"http://{domain}/static",

                UploadsBaseUrl = $"http://{domain}/upload",

                MailgunConfiguration = new MailgunConfiguration
                {
                    EmailFromName = org,
                    EmailFromAddress = $"{orgL}@mg.mygol.es",
                    MailgunDomain = $"mg.mygol.es",
                    MailgunPrivateKey = ""
                }
            };

            var result = JsonConvert.SerializeObject(data, Formatting.Indented);
            Console.WriteLine(result);
        }


        private static void CopyFile(string sourcePath, string destPath, string img, long rowId)
        {
            try
            {
                if (img == null || img == "") return;
                var source = Path.Combine(sourcePath, img);
                var dest = Path.Combine(destPath, img);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(source, dest, true);
            }
            catch (Exception ex)
            {
                Log($"Failed to copy image for id {rowId} ({img}): {ex.Message}");
            }
        }

        public static void ImportFootballData()
        {
            if (mArgs.Count != 2)
            {
                Log("Usage: ImportFootballData <competiton id>");
                return;
            }

            var idCompetition = int.Parse(mArgs[1]);

            var importer = new webapi.Importers.FootballDataImporter("3f597bae00674c7493bef49fded661cc");
            var task = importer.ImportTournament(idCompetition);
            task.Wait();
            var competition = task.Result;

            var exporter = new webapi.Importers.FootballDataExporter();

            using (var conn = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                var t = conn.BeginTransaction();
                try
                {
                    var t2 = exporter.Export(conn, t, GetStorage(), competition);
                    t2.Wait();
                    var tournament = t2.Result;
                    Log("Data imported");

                    MatchEvent.ResetTournamentStats(conn, t, tournament.Id).Wait();
                    MatchEvent.ApplyTournamentStats(conn, t, tournament.Id).Wait();
                    Log("Stats updated");

                    t.Commit();
                }
                catch (Exception ex)
                {
                    Log(ex.Message + ex.StackTrace);
                    t.Rollback();
                }
            }
        }

        public static void GenSampleJson()
        {
            var input = new CalendarGenInput
            {
                Type = 0, // League
                TeamIds = new long[] { 1, 2, 3, 4, 5, 6, 7, 8 },
                WeekdaySlots = new DailySlot[][]
                {
                    new DailySlot[] { },    // Sunday
                    new DailySlot[] { },    // Monday
                    new DailySlot[] { },    // Tuesday
                    new DailySlot[] { },    // Wednesday
                    new DailySlot[]         // Thursday
                    {
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 10, 00, 00), EndTime = new DateTime(1, 1, 1, 11, 00, 00) },
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 16, 00, 00), EndTime = new DateTime(1, 1, 1, 17, 00, 00) }
                    },
                    new DailySlot[] { },    // Friday
                    new DailySlot[] { }     // Saturday
                },
                StartDate = new DateTime(2018, 01, 22),
                ForbiddenDays = new DateTime[]
                {
                    new DateTime(2018, 01, 25)
                },
                FieldIds = new long[] { 1001, 1002 },
                GameDuration = 60,
                IsPreview = true
            };

            var result = JsonConvert.SerializeObject(input, Formatting.Indented);
            Log(result);
        }

        public static void SetUserPassword()
        {
            if (mArgs.Count < 3)
            {
                Log("Usage: SetUserPassword <idUser> <password>");
                return;
            }

            var idUserString = mArgs[1];
            var newPassword = mArgs[2];

            if (!long.TryParse(idUserString, out long idUser)) throw new Exception("Invalid idUser: " + idUserString + ". Must be a long.");

            var tokenManager = new AuthTokenManager(new TokenAuthConfig());

            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                // Get the current user
                var dbUser = c.Get<User>(idUser);
                if (dbUser == null) throw new Exception($"User id {idUser} not found.");

                // set password
                UsersController.UpdatePassword(dbUser, newPassword);

                // save user
                c.Update(dbUser);
            }
        }

        public static void Search()
        {
            if (mArgs.Count != 2)
            {
                Log("Usage: Search \"<query>\"");
                return;
            }

            var query = mArgs[1];

            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                var result = SearchController.SearchEverywhere(c, query);

                foreach (var r in result.GetAll()) Log(r.Print());
            }

        }

        public static void CreateUser()
        {
            if (mArgs.Count < 5)
            {
                Log("Usage: CreateUser <name> <email> <password> <level>");
                return;
            }

            var name = mArgs[1];
            var email = mArgs[2];
            var password = mArgs[3];
            var level = int.Parse(mArgs[4]);

            if (level == 1)
            {
                Log("Error: cannot create players using the command line.");
                return;
            }

            var tokenManager = new AuthTokenManager(new TokenAuthConfig());
            
            var user = new User
            {
                Name = name,
                Level = level,
                Email = email,
                EmailConfirmed = true
            };

            var h = AuthTokenManager.HashPassword(password);

            user.Password = h.Hash;
            user.Salt = h.Salt;

            // Save the user on the database
            using (var conn = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                conn.Insert(user);
            }

            
        }

        public static void CreateGlobalAdmin()
        {
            if (mArgs.Count < 4)
            {
                Log("Usage: CreateGlobalAdmin <name> <email> <password>");
                return;
            }

            var name = mArgs[1];
            var email = mArgs[2];
            var password = mArgs[3];
            var level = 4;

            var tokenManager = new AuthTokenManager(new TokenAuthConfig());
            
            var user = new User
            {
                Name = name,
                Level = level,
                Email = email,
                EmailConfirmed = true
            };

            var h = AuthTokenManager.HashPassword(password);

            user.Password = h.Hash;
            user.Salt = h.Salt;

            // Save the user on the database
            var globalConfig = GetGlobalDbConfig();
            using (var conn = new PostgresqlDataLayer(globalConfig).GetConn())
            {
                var newId = conn.Execute("INSERT INTO globaladmins (name, email, password, salt, level, emailconfirmed) VALUES (@Name, @Email, @Password, @Salt, @Level, @EmailConfirmed)", user);
                //Log("New global admin created with id " + newId);
            }
        }

        public static void CreateSampledata()
        {
            // based on args, add this and that

            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                SampleDataCreator.CreateTournaments(c);
            }
        }

        public static void ResetPlayerInviteTexts()
        {
            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                DataUpdater.UpdateEmailTemplate(c, "email.player.invite.html", "NotificationTemplates.es.email.player.invite.html");
            }
        }

        public static void ListCalendarConfigs()
        {
            var supportedNumTeams = new int[] { 4, 6, 8, 10, 12, 14, 16, 18, 20 };

            foreach (var numTeams in supportedNumTeams) GetCalendarForNumTeams(numTeams);
        }


        public static void NotifyMatches()
        {
            try
            {
                using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
                {
                    var (start, end) = ScheduledNotifications.GetDefaultDateRange();

                    Log($"  Notify matches between '{start}' and '{end}'...");

                    ScheduledNotifications.NotifyMatchesInDateRange(c, null, start, end);
                }

                ExpoPushProvider.FlushAndStop();
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message + ex.StackTrace);
            }

            Log($"  Finished");
        }


        public static void StripeOneTimeUpdate()
        {

            var sql1 = @"
                    update teamplayers a set enrollmentpaymentdata = c.data1 from (
                        select p.id, tp.idplayer, p.iduser, ue.iduser, b.iduser, tp.idteam, tp.enrollmentdate, ue.timestamp, ue.data1
                        from 
	                        teamplayers tp join players p on tp.idplayer = p.id,
	                        (select iduser, count(timestamp) from userevents where type = 15 and timestamp < '2018-10-17 23:25' group by iduser having count(id) = 1) as b,
	                        userevents ue

                        where ue.type = 15 AND ue.iduser = b.iduser AND b.iduser = p.iduser AND tp.enrollmentdate < '2018-10-17 23:25'
                        ) as c 
                    where a.idplayer = c.idplayer AND a.idteam = c.idteam
                    ";

            var sql2 = @"
                    select p.id as idplayer, p.name, p.surname, t.id as idteam, t.name as teamname, data1, b.iduser, ue.timestamp, ue.type, ue.description
                    from 
	                    players p,
	                    (select iduser, count(timestamp) from userevents where type = 15 and timestamp < '2018-10-17 23:25' group by iduser having count(timestamp) > 1) as b,
	                    userevents ue left join teams t on t.name = ue.description
                    where (ue.type = 15 or ue.type = 10) AND ue.iduser = b.iduser AND b.iduser = p.iduser 
                    order by iduser, ue.timestamp asc
                    ";

            var result = new StringBuilder();

            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                var t = c.BeginTransaction();

                try
                {

                    Log("-- Step 1: Update players with only one team ___________________________________");

                    var numRows = c.Execute(sql1, t);
                    Log($"{numRows} updated");
                    

                    Log("-- Step 2: Update players in more than one team ________________________________");

                    c.Execute("UPDATE userevents SET description = 'LEGEND&SEGUROS BILBAO' WHERE description = 'SEGUROS BILBAO&LEGEND'", t);

                    // Get the list of players with payments in more than one team
                    // get the list of uservents for those users, invite and payment events
                    // use the payment event (stripeId) after the invite event (team name) to get the missing info.
                    // map the team name to team id

                    var paymentsWithoutContext = new List<dynamic>();
                    long idTeam = -1;
                    string teamName = null;

                    foreach (var row in c.Query(sql2, t))
                    {
                        if (row.type == 10)
                        {
                            if (row.idteam == null)
                            {
                                Log("-- TEAM NAME NOT FOUND: " + row.description);
                                idTeam = -1;
                            }
                            else
                            {
                                idTeam = (long)row.idteam;
                                teamName = row.teamname;
                            }
                        }
                        else if (row.type == 15)
                        {
                            if (idTeam != -1)
                            {
                                result.AppendLine($"UPDATE teamplayers SET enrollmentPaymentData = '{row.data1}' WHERE idplayer = {row.idplayer} AND idteam = {idTeam};");
                                Log($"'{row.idplayer}','{row.name} {row.surname}','{teamName}','{row.data1}'");
                                idTeam = -1;
                                teamName = null;
                            }
                            else
                            {
                                //Log($"-- PAYMENT FOUND WITHOUT team CONTEXT. idPlayer {row.idplayer}");
                                paymentsWithoutContext.Add(row);
                            }
                        }
                    }

                    //Log(result.ToString());

                    numRows = c.Execute(result.ToString(), t);
                    Log($"{numRows} updated");

                    Log("-- Step 3: Update first team of multi-team players _____________________________");

                    result.Clear();

                    foreach (var row in paymentsWithoutContext)
                    {
                        var range = new TimeSpan(4, 0, 0);
                        var t1 = row.timestamp - range;
                        var t2 = row.timestamp + range;
                        var tp = c.Query<TeamPlayer>($"SELECT * FROM teamplayers WHERE idplayer = @idplayer AND enrollmentdate BETWEEN @t1 and @t2", new { t1, t2, row.idplayer }, t);

                        if (tp.Count() != 1)
                        {
                            Log($"-- NOT FOUND: '{row.idplayer}','{row.name} {row.surname}','{row.data1}'");
                            continue;
                        }
                        else
                        {
                            var teamPlayer = tp.GetSingle();
                            idTeam = teamPlayer.IdTeam;
                            var ss = $"UPDATE teamplayers SET enrollmentpaymentdata = '{row.data1}' WHERE idPlayer = {row.idplayer} AND idTeam = {idTeam};";
                            result.AppendLine(ss);
                            Log($"'{row.idplayer}','{row.name} {row.surname}','{idTeam}','{row.data1}'");
                        }
                    }

                    numRows = c.Execute(result.ToString(), t);
                    Log($"{numRows} updated");

                    t.Commit();
                }
                catch (Exception ex)
                {
                    Log("-- EX: " + ex.Message);
                    t.Rollback();
                }
            }
        }



        // __ JSON import export ______________________________________________


        private static void ExportToJson<T>(string fileName, string query, Action<T> onRow = null) where T : class
        {
            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                var result = c.Query<T>(query);

                if (onRow != null) foreach (var item in result) onRow(item);

                using (var w = new JsonTextWriter(File.CreateText(fileName)) { Formatting = Formatting.Indented })
                {
                    var s = new JsonSerializer();
                    s.Serialize(w, result);
                }
            }
        }

        private static void ImportFromJson<T>(string fileName, Action<T> onRow = null) where T : class
        {
            using (var c = new PostgresqlDataLayer(mDbConfig).GetConn())
            {
                using (var r = new JsonTextReader(File.OpenText(fileName)))
                {
                    var s = new JsonSerializer();
                    var target = s.Deserialize<IEnumerable<T>>(r);

                    var t = c.BeginTransaction();
                    try
                    {
                        foreach (T item in target)
                        {
                            onRow?.Invoke(item);
                            c.Insert(item, t);
                        }

                        t.Commit();
                    }
                    catch
                    {
                        t.Rollback();
                    }
                }
            }
        }


        // __ Calendar rounds ________________________________________________


        private static void GetCalendarForNumTeams(int numTeams)
        {
            var teamIds = new long[numTeams];
            for (int i = 0; i < numTeams; ++i) teamIds[i] = i + 1;

            var input = new CalendarGenInput
            {
                Type = (int)CalendarType.League,
                TeamIds = teamIds,
                WeekdaySlots = new DailySlot[][]
               {
                    new DailySlot[] { },    // Sunday
                    new DailySlot[] { },    // Monday
                    new DailySlot[] { },    // Tuesday
                    new DailySlot[] { },    // Wednesday
                    new DailySlot[]         // Thursday
                    {
                        new DailySlot { StartTime = new DateTime(1, 1, 1, 0, 00, 00), EndTime = new DateTime(1, 1, 1, 23, 00, 00) },
                        new DailySlot { StartTime = new DateTime(1, 1, 2, 0, 00, 00), EndTime = new DateTime(1, 1, 2, 23, 00, 00) },
                    },
                    new DailySlot[] { },    // Friday
                    new DailySlot[] { }     // Saturday
               },
                StartDate = new DateTime(2018, 01, 22),
                Group = new GroupCoords { IdTournament = 1, IdStage = 2, IdGroup = 3 },
                ForbiddenDays = new DateTime[]
               {
                    new DateTime(2018, 01, 25)
               },
                FieldIds = new long[] { 1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008 },
                GameDuration = 60,
                IsPreview = true
            };


            var result = LeaguePlanner.Calculate(input, null, "es", null, null);
            if (result == null) return;

            Console.WriteLine($"Num. Equipos: {numTeams}\n");

            var days = result.Days;

            for (int day = 0; day < days.Count; ++day)
            {
                Console.WriteLine($"  Día {day + 1}");

                var dayMatches = days[day].Matches;

                Console.Write("    ");
                for (int i = 0; i < dayMatches.Count; ++i) Console.Write(string.Format("{0,4}", dayMatches[i].IdHomeTeam));
                Console.WriteLine();

                Console.Write("    ");
                for (int i = 0; i < dayMatches.Count; ++i) Console.Write(string.Format("{0,4}", dayMatches[i].IdVisitorTeam));
                Console.WriteLine();

                Console.WriteLine();
            }

            Console.WriteLine("\n");
        }


        // __ Plumbing ________________________________________________________


        private static void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        private static void SetupOrganizations()
        {
            if (mOrgsInitialized) return;

            var orgsFile = mConfig.OrganizationsFile;
            if (orgsFile == null || orgsFile == "") return;

            var orgs = OrganizationManager.LoadOrganizationsFromFile(orgsFile);
            OrganizationManager.Initialize(orgs);

            mOrgsInitialized = true;
        }

        private static void SetupDbConfig(IConfigurationRoot cfg)
        {
            var org = OptOrganizationName;
            if (org == null) org = Environment.GetEnvironmentVariable("MYGOL_ORG");

            mAdminDbConfig = new PostgresqlConfig();
            var dbSection = cfg.GetSection("db");
            dbSection.Bind(mAdminDbConfig);

            if (org != null)
            {
                Log($"ORG: {org}");
                mDbConfig = OrganizationManager.GetDbConfigForOrgName(org);
            }
            else
            {
                //Log($"ORG: not set, DB config from appsettings.json");
                mDbConfig = mAdminDbConfig;
            }

            //Log($"DB: {mDbConfig.DatabaseName}");
        }

        private static PostgresqlConfig GetGlobalDbConfig()
        {
            return OrganizationManager.GetDbConfigForOrgName("ORGDIR");
        }

        private static void InitConfig()
        {
            var file = "appsettings.json";
            //Log($"Loading config from {file}");

            var cfg = new ConfigurationBuilder()
                .AddJsonFile(file, optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            
            mConfig = new Config();
            var section = cfg.GetSection("App");
            section.Bind(mConfig);

            SetupOrganizations();
            SetupDbConfig(cfg);

            mDiskConfig = new DiskStorageProviderConfig();
            var diskStorageSection = cfg.GetSection("storage.disk");
            diskStorageSection.Bind(mDiskConfig);
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: mygolcli <command>");
            Console.WriteLine("  Available commands: ");

            foreach (var m in GetMethodsBySig(typeof(Program), typeof(void)))
            {
                Console.WriteLine($"    {m.Name}");
            }

            Console.WriteLine("  Global options:");
            Console.WriteLine("    --org=<organization name>");
            Console.WriteLine("    --allorgs  Applies command to all orgs except ORGDIR");
            Console.WriteLine("    --ignoreErrors  Ignore errors in an organization and continue to next.");
        }


        private static void ProcessArgs(string[] args)
        {
            mArgs = new List<string>();

            foreach (var arg in args)
            {
                if (arg.StartsWith("--"))
                    ProcessOption(arg);
                else
                    mArgs.Add(arg);
            }
        }

        private static void ProcessOption(string arg)
        {
            var aa = arg.Split('=');
            if (aa.Length != 2) throw new Exception("Invalid option: " + arg);

            var name = aa[0].TrimStart('-');
            var value = aa[1].Trim('"');

            switch (name.ToLower())
            {
                case "org": OptOrganizationName = value; break;
                case "allorgs": OptAllOrgs = true; break;
                case "ignoreerrors": OptIgnoreErrors = true; break;
                default: throw new Exception("Unknown option: " + arg);
            }
        }


        private static bool CheckUsage(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return false;
            }

            return true;
        }


        private static void RunCommandOnAllOrgs()
        {
            if (OptOrganizationName != null) throw new Exception("--org option is not compatible with --allorgs");

            foreach (var org in OrganizationManager.GetNames())
            {
                if (org == "ORGDIR") continue;

                try
                {
                    OptOrganizationName = org;
                    InitConfig();
                    RunCommand(mArgs[0]);
                }
                catch (Exception ex)
                {
                    Log($"ERROR ORG '{org}': {ex.Message} || {ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}");
                    if (!OptIgnoreErrors) return;
                }
            };
        }

        private static void RunCommand(string command)
        {
            command = command.ToLower();

            var methods = GetMethodsBySig(typeof(Program), typeof(void));

            foreach (var m in methods)
            {
                if (m.Name.ToLower() == command)
                {
                    m.Invoke(null, new object[] { });
                    return;
                }
            }

            Console.WriteLine("Unknown command: " + command);
        }

        private static IEnumerable<MethodInfo> GetMethodsBySig(Type type, Type returnType, params Type[] parameterTypes)
        {
            return type.GetTypeInfo().GetMethods(BindingFlags.Static | BindingFlags.Public).Where((m) =>
            {
                if (m.ReturnType != returnType) return false;
                var parameters = m.GetParameters();
                if ((parameterTypes == null || parameterTypes.Length == 0))
                    return parameters.Length == 0;
                if (parameters.Length != parameterTypes.Length)
                    return false;
                for (int i = 0; i < parameterTypes.Length; i++)
                {
                    if (parameters[i].ParameterType != parameterTypes[i])
                        return false;
                }
                return true;
            });
        }

        private static IStorageProvider GetStorage()
        {
            if (mDiskConfig == null) throw new Exception("No storage configuration found");
            return new DiskStorageProvider(mDiskConfig);
        }


        private static PostgresqlConfig mAdminDbConfig;
        private static PostgresqlConfig mDbConfig;
        private static Config mConfig;
        private static DiskStorageProviderConfig mDiskConfig;
        private static Stopwatch Timer;
        private static IList<string> mArgs;         // First argument is command name.

        private static string OptOrganizationName = null;
        private static bool OptAllOrgs = false;
        private static bool OptIgnoreErrors = false;

        private static bool mOrgsInitialized = false;
    }

    public class MyOptions<T> : IOptions<T> where T : class, new()
    {
        public MyOptions(T config)
        {
            mConfig = config;
        }

        public T Value => mConfig;

        private T mConfig;
    }
}
