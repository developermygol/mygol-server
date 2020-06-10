using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace webapi
{
    public class Utils
    {
        public static string GetUploadUrl(HttpRequest request, string localPath, long idObject, string type)
        {
            var uploadPath = OrganizationManager.GetOrgUploadPath(request);

            if (localPath != null && localPath != "") return uploadPath + "/" + localPath;

            var staticPath = OrganizationManager.GetOrgPrivateStaticPath(request);

            return $"{staticPath}/{type}/default1.png";
        }

        public static string GetJoined<T>(IEnumerable<T> items, string separator = ", ", string surround = "")
        {
            bool isFirst = true;
            var sb = new StringBuilder();

            foreach (var i in items)
            {
                if (isFirst)
                    isFirst = false;
                else
                    sb.Append(separator);

                sb.Append(surround + i.ToString() + surround);
            }

            return sb.ToString();
        }

        public static IEnumerable<string> GetAllRoutes(HttpContext httpContext)
        {
            var routeContext = new RouteContext(httpContext);
            var routes = routeContext.RouteData.Routers.OfType<RouteCollection>().FirstOrDefault();

            Debug.WriteLine(routes);

            return null;
        }
    }

    public static class ExtensionMethods
    {
        public static T Run<T>(this Task<T> targetTask)
        {
            targetTask.Wait();
            return targetTask.Result;
        }

        public static T GetSingle<T>(this IEnumerable<T> resultSet)
        {
            if (resultSet == null) throw new Exception("Error.NoResult");

            var count = resultSet.Count();

            if (count > 1) throw new Exception("Error.MoreThanOne");
            if (count == 0) throw new Exception("Error.NotFound");

            return resultSet.First();
        }
    }
}
