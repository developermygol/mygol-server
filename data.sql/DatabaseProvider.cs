using Npgsql;
using System.Data;

namespace data.sql
{
    public interface IDatabaseProvider
    {
        IDbConnection GetConnection();
    }

    public class PostgresDatabaseProvider: IDatabaseProvider
    {
        public string User { get; set;  }
        public string Password { get; set; }
        public string Database { get; set; }
        public string Server { get; set; } = "127.0.0.1";
        public string Port { get; set; } = "5432";

        public PostgresDatabaseProvider()
        {

        }

        public PostgresDatabaseProvider(string database, string user, string password, string server = "127.0.0.1", string port = "5432")
        {
            User = user;
            Password = password;
            Database = database;
            Server = server;
            Port = port;
        }

        public IDbConnection GetConnection()
        {
            return new NpgsqlConnection(GetConnString());
        }

        private string GetConnString()
        {
            return $"Server = {Server}; Port = {Port}; Database = {Database}; User Id = {User}; Password = {Password}; Command Timeout = 0";
        }

    }
}
