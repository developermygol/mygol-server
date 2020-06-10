using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace webapi.Controllers
{
    [Route("api/[controller]")]
    public abstract class DbController: AuthBasedController
    {
        public DbController(IOptions<Config> config) : base(config)
        {

        }

        protected IActionResult DbOperation(Func<IDbConnection, object> worker, IDbConnection conn = null)
        {
            try
            {
                var c = (conn != null) ? conn : GetConn();

                using (c)
                {
                    if (c == null) throw new Exception("Error.Database");

                    var result = worker(c);
                    return new JsonResult(result);
                }
            }
            catch (Exception ex)
            {
                Audit.Error(this, ex, "{0}: {Message}", GetUserId(), ex.Message);
                return Error(ex.Message);
            }
        }

        protected IActionResult DbTransaction(Func<IDbConnection, IDbTransaction, object> worker)
        {
            using (var c = GetConn())
            {
                using (var t = c.BeginTransaction())
                {
                    try
                    {
                        if (c == null) throw new Exception("Error.Database");

                        var result = new JsonResult(worker(c, t));

                        t.Commit();

                        return result;
                    }
                    catch (Exception ex)
                    {
                        t.Rollback();
                        Audit.Error(this, ex, "{0}: {Message}", GetUserId(), ex.Message);
                        return Error(ex.Message);
                    }
                }
            }
        }

        protected async Task<IActionResult> DbTransactionAsync(Func<IDbConnection, IDbTransaction, Task<object>> worker)
        {
            using (var c = GetConn())
            {
                using (var t = c.BeginTransaction())
                {
                    try
                    {
                        if (c == null) throw new Exception("Error.Database");

                        var result = new JsonResult(await worker(c, t));

                        t.Commit();

                        return result;
                    }
                    catch (Exception ex)
                    {
                        t.Rollback();
                        Audit.Error(this, ex, "{0}: {Message}", GetUserId(), ex.Message);
                        return Error(ex.Message);
                    }
                }
            }
        }

        protected IDbConnection GetConn()
        {
            var cfg = OrganizationManager.GetDbConfigForRequest(Request);
            
            return new PostgresqlDataLayer(cfg).GetConn();
        }

        protected IDbConnection GetConn(PostgresqlConfig config)
        {
            return new PostgresqlDataLayer(config).GetConn();
        }

        protected static IDbConnection GetGlobalDirectoryConn()
        {
            var cfg = OrganizationManager.GetDbConfigForOrgName("ORGDIR");

            return new PostgresqlDataLayer(cfg).GetConn();
        }


        protected PostgresqlConfig mDbOptions;
    }
}
