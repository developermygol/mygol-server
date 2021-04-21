using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using webapi.Models.Db;
using Dapper;
using Dapper.Contrib.Extensions;

namespace webapi
{
    public class PostgresqlConfig
    {
        public string User { get; set; }
        public string Password { get; set; }
        public string DatabaseName { get; set; }

        public string TablespaceName { get; set; }

        public string AdminUser { get; set; }
        public string AdminPassword { get; set; }

        public bool DeleteExistingDb { get; set; } = false;
    }

    public class PostgresqlDataLayer
    {

        protected const int DatabaseVersion = 33;

        //protected IDbConnection mConnection;        

        private PostgresqlConfig mConfig;

        public PostgresqlDataLayer(PostgresqlConfig config)
        {
            mConfig = config;
        }

        public void CreateOrgDb()
        {
            CreateDb();

            using (var c = GetConn())
            {
                string dbCreationScript = ReadResourceFile("PostgresDbCreationScript.sql");

                ExecNonQuery(c, dbCreationScript);

                ExecNonQuery(c, $"INSERT INTO version(v) VALUES ({DatabaseVersion})");

                InsertNotificationTemplates(c);
            }
        }

        public void InsertNotificationTemplates(IDbConnection c)
        {
            var t = c.BeginTransaction();

            c.Insert(new NotificationTemplate
            {
                Key = "email.player.invite.html",
                Lang = "es",
                Title = "Invitación para unirte al equipo {{Team.Name}}",
                ContentTemplate = ReadResourceFile("NotificationTemplates.es.email.player.invite.html")
            }, t);

            c.Insert(new NotificationTemplate
            {
                Key = "email.player.unlink.html",
                Lang = "es",
                Title = "Ya no formas parte del equipo {{Team.Name}}",
                ContentTemplate = ReadResourceFile("NotificationTemplates.es.email.player.unlink.html")
            }, t);

            c.Insert(new NotificationTemplate
            {
                Key = "push.referee.match.link.txt",
                Lang = "es",
                Title = "",
                ContentTemplate = ""
            }, t);

            c.Insert(new NotificationTemplate
            {
                Key = "push.referee.match.unlink.txt",
                Lang = "es",
                Title = "",
                ContentTemplate = ""
            }, t);

            c.Insert(new NotificationTemplate
            {
                Key = "email.referee.invite.html",
                Lang = "es",
                Title = "Invitación para arbitrar en {{Org.Name}}",
                ContentTemplate = ReadResourceFile("NotificationTemplates.es.email.referee.invite.html")
            }, t);

            c.Insert(new NotificationTemplate
            {
                Key = "email.player.forgotpassword.html",
                Lang = "es",
                Title = "Reiniciar contraseña",
                ContentTemplate = ReadResourceFile("NotificationTemplates.es.email.player.forgotpassword.html")
            }, t);

            t.Commit();
        }

        private string GetConfiguredQuery(string query)
        {
            return string.Format(query, mConfig.DatabaseName, mConfig.User, mConfig.Password, mConfig.TablespaceName);
        }

        public IDbConnection GetConn()
        {
            return GetConn(mConfig.DatabaseName, mConfig.User, mConfig.Password);
        }

        protected IDbConnection GetConn(string databaseName, string user, string passwd)
        {
            var result = new NpgsqlConnection(GetConnString(databaseName, user, passwd));
            result.Open();

            return result;
        }


        // __ Global directory database _______________________________________

        private void CreateDb()
        {
            //using (var conn = new NpgsqlConnection(GetConnString("template1", mConfig.AdminUser, mConfig.AdminPassword)))
            using (var adminConn = GetConn("template1", mConfig.AdminUser, mConfig.AdminPassword))
            {
                if (mConfig.DeleteExistingDb)
                {
                    // For development only
                    ExecNonQuery(adminConn, GetConfiguredQuery("DROP DATABASE IF EXISTS {0};"));
                    ExecNonQuery(adminConn, GetConfiguredQuery("DROP USER IF EXISTS {1}"), ignoreError: true);
                }

                ExecNonQuery(adminConn, GetConfiguredQuery("CREATE USER {1} WITH PASSWORD '{2}'"), ignoreError: true);
                ExecNonQuery(adminConn, GetConfiguredQuery("CREATE DATABASE {0};GRANT ALL PRIVILEGES ON DATABASE {0} TO {1}"));

                // Tablespace creation has to be done manually. To do so: 
                // CREATE TABLESPACE fastspace LOCATION 'C:\postgres\data';
                // GRANT CREATE ON TABLESPACE fastspace TO cifuser;
                if (mConfig.TablespaceName != null)
                {
                    ExecNonQuery(adminConn, GetConfiguredQuery("ALTER DATABASE {0} SET default_tablespace TO {3};"));
                }
            }
        }


        public void CreateGlobalDb()
        {
            CreateDb();

            using (var c = GetConn())
            {
                string dbCreationScript = ReadResourceFile("OrgDbCreationScript.sql");

                ExecNonQuery(c, dbCreationScript);
            }
        }



        //protected int GetInsertedRowId(string table, string field)
        //{
        //    int result = 0;
        //    var query = string.Format("SELECT currval(pg_get_serial_sequence('{0}','{1}'));", table, field);

        //    QuerySingleRow(query, null, (r) =>
        //    {
        //        result = Convert.ToInt32(r[0]);
        //    });

        //    return result;
        //}


        private string GetConnString(string databaseName, string user, string passwd)
        {
            return String.Format("Server = 127.0.0.1; Port = 5432; Database = {0}; User Id = {1}; Password = {2};Command Timeout = 0", databaseName, user, passwd);
        }



        // ___________________________________



        protected int ExecNonQuery(IDbConnection conn, string query, Dictionary<string, object> parameters = null, bool ignoreError = false)
        {
            if (conn == null) return -1;

            try
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = query;

                    AddParametersToCommand(cmd, parameters);

                    //LogQuery(query, parameters);

                    int numRecords = cmd.ExecuteNonQuery();

                    return numRecords;
                }
            }
            catch (Exception ex)
            {
                if (!ignoreError) throw ex;
            }

            return 0;
        }

        //protected void QuerySingleRow(string query, Dictionary<string, object> parameters, Action<IDataReader> dataCallback, IDbConnection conn = null)
        //{
        //    if (conn == null) conn = mConnection;
        //    if (conn == null) return;

        //    using (var cmd = conn.CreateCommand())
        //    {
        //        cmd.CommandText = query;

        //        AddParametersToCommand(cmd, parameters);

        //        using (var r = cmd.ExecuteReader(CommandBehavior.SingleRow))
        //        {
        //            if (!r.Read()) return;

        //            dataCallback?.Invoke(r);
        //        }
        //    }
        //}

        //protected IEnumerable<object[]> QueryReader(string query, Dictionary<string, object> parameters, IDbConnection conn = null)
        //{
        //    if (conn == null) conn = mConnection;
        //    if (conn == null) yield break;

        //    using (var cmd = conn.CreateCommand())
        //    {
        //        cmd.CommandText = query;

        //        AddParametersToCommand(cmd, parameters);

        //        using (var r = cmd.ExecuteReader())
        //        {
        //            var numFields = r.FieldCount;
        //            var buffer = new object[numFields];

        //            while (r.Read())
        //            {
        //                r.GetValues(buffer);
        //                yield return buffer;
        //            }
        //        }
        //    }
        //}

        protected void AddParametersToCommand(IDbCommand cmd, Dictionary<string, object> parameters)
        {
            if (parameters == null) return;

            foreach (var pair in parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@" + pair.Key;
                p.Value = (pair.Value != null) ? pair.Value : DBNull.Value;
                cmd.Parameters.Add(p);
            }
        }


        public static string ReadResourceFile(string fileName)
        {
            var assembly = Assembly.GetCallingAssembly();
            var resourceName = "webapi." + fileName;

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        //private static string GetDateString(DateTime? d)
        //{
        //    if (d == null) return null;

        //    return GetDateString(d.Value);
        //}

        //private static string GetDateString(DateTime d)
        //{
        //    return d.ToString("yyyy-MM-dd");
        //}

        //private static DateTime? GetNullableDate(object d)
        //{
        //    if (d is DBNull) return null;

        //    return GetDate(d);
        //}

        //private static DateTime GetDate(object d)
        //{
        //    if (d is DBNull) return DateTime.MinValue;

        //    var s = d as string;
        //    if (d == null) return DateTime.MinValue;

        //    return DateTime.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        //}

    }
}
