using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi
{
    public class RequestDomainEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            //if (HttpContext.Current == null) return;
            //if (HttpContext.Current.Request == null) return;

            

            //var domainProperty = new LogEventProperty("Domain", new Serilog.Events.LogEventPropertyValue  StringValue(HttpContext.Current.Host.Value));
            //logEvent.AddPropertyIfAbsent(domainProperty);
        }
    }

    public static class Audit
    {
        public static void Information(Controller controller, string msg, params object[] arguments)
        {
            Log.Information(GetMessage(controller, msg), arguments);
        }

        public static void Error(Controller controller, string msg, params object[] arguments)
        {
            Log.Error(GetMessage(controller, msg), arguments);
        }

        public static void Error(Controller controller, Exception ex, string msg, params object[] arguments)
        {
            if (ex is EmailException)
            {
                var email = ex.Data["email"];
                Log.Error(GetMessage(controller, $"{ex.Message} ({email})"), arguments);
                return;
            }

            if (ex is DataException)
            {
                var data = ex.Data["data"];
                Log.Error(GetMessage(controller, $"{ex.Message} ({data})"), arguments);
                return;
            }

            if (KnownExceptions.Contains(ex.Message))
                Log.Error(GetMessage(controller, msg), arguments);
            else
                Log.Error(ex, GetMessage(controller, msg), arguments);
        }


        private static string GetMessage(Controller controller, string msg)
        {
            return $"[{controller.Request.Host}] {msg}";
        }


        private static string[] KnownExceptions = new string[]
        {
            "Error.AlreadyActivated",
            "Error.LoginIncorrect",
            "Error.PasswordNoRules",
            "Error.EmailAlreadyExists",
            "Error.TournamentNotVisible"
        };
    }
}
