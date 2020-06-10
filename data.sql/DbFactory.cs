using System.Data;

namespace data.sql
{
    public class DbFactory
    {
        public static void Configure(IDatabaseProvider config)
        {
            mConfig = config;
        }

        public static IDbConnection Get()
        {
            if (mConfig == null) throw new System.Exception("DbFactory not initialized");

            var c = mConfig.GetConnection();
            c.Open();

            return c;
        }
        
        private static IDatabaseProvider mConfig;
    }
}
