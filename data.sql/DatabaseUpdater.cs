using Logger;
using System;
using System.Collections.Generic;

namespace sqlDataProvider
{
    public class DatabaseUpdater: IDisposable
    {
        public DatabaseUpdater(SqlDataAdapter adapter)
        {
            mAdapter = adapter;
            if (adapter == null) throw new ArgumentNullException("adapter");

            Init();
        }

        public void Dispose()
        {

        }

        public void Update()
        {
            var version = mAdapter.GetCurrentVersion();
            if (version == 0) return;

            mLog.Info($"Current DB version: {version}");

            try
            {
                while (true)
                {
                    if (!mUpdaters.TryGetValue(version, out UpdateMethodDelegate func)) break;

                    version = RunUpdate(func);

                    mLog.Info($"Upgraded DB to {version}");
                }
            }
            catch (Exception ex)
            {
                mLog.Error("ERROR: " + ex.Message + ex.StackTrace);
            }
        }


        // __ Update methods _________________________________________________


        private void Init()
        {
            mUpdaters.Add(11, Update11);
            mUpdaters.Add(12, Update12);
        }


        private int Update11(SqlDataLayer conn)
        {
            mLog.Info($"11: Still to do. change only affects the parser.");
            return 12;
        }

        private int Update12(SqlDataLayer conn)
        {
            //mLog.Info($"12: Adding activecompanies column to people...");

            //conn.ExecNonQuery(@"ALTER TABLE people ADD COLUMN activecompanies integer");

            return 14;
        }

        

        // __ Helpers ________________________________________________________


        private int RunUpdate(UpdateMethodDelegate updateMethod)
        {
            if (updateMethod == null) throw new ArgumentNullException("updateMethod");

            using (var conn = mAdapter.GetConnection())
            {
                var transaction = conn.Connection.BeginTransaction();

                try
                {
                    var result = updateMethod(conn);

                    SetNewVersion(result, conn);

                    transaction.Commit();

                    return result;
                }
                catch (Exception)
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }


        private void SetNewVersion(int version, SqlDataLayer conn)
        {
            conn.ExecNonQuery($"UPDATE version SET v = @version", new Dictionary<string, object> { { "version", version } });
        }
        

        private static Log mLog = LogFactory.Get("DbUpdate");
        private SqlDataAdapter mAdapter;
        private Dictionary<int, UpdateMethodDelegate> mUpdaters = new Dictionary<int, UpdateMethodDelegate>();


        private delegate int UpdateMethodDelegate(SqlDataLayer conn);
    }

    
}
