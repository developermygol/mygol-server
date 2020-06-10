using webappModels;
using Dapper;
using Dapper.Contrib.Extensions;

namespace data.sql
{
    public class SqlDataStoreProvider: IDataStoreProvider
    {
        public long AddMatchEvent(MatchEvent matchEvent)
        {
            using (var c = DbFactory.Get())
            {
                return c.Insert<MatchEvent>(matchEvent);
            }
        }
    }
}
