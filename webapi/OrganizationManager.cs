using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using webapi.Models.Db;

namespace webapi
{
    public class OrganizationManager
    {
        public static void Initialize(IEnumerable<OrganizationDispatchData> organizations)
        {
            mOrganizations = new Dictionary<string, OrganizationDispatchData>();
            mOrgDbsByName = new Dictionary<string, PostgresqlConfig>();
            mOrgsByName = new Dictionary<string, OrganizationDispatchData>();
            mDbConfigs = new Dictionary<string, PostgresqlConfig>();

            foreach (var org in organizations)
            {
                mOrgsByName[org.Name] = org;
                mOrganizations[org.DomainName] = org;

                var dbConfig = new PostgresqlConfig
                {
                    DatabaseName = org.DbName,
                    User = org.DbUser,
                    Password = org.DbPassword
                };
                
                mOrgDbsByName[org.Name] = dbConfig;
                mDbConfigs[org.DomainName] = dbConfig;

                if (org.OtherNames != null)
                {
                    foreach (var dn in org.OtherNames)
                    {
                        mOrganizations[dn] = org;
                        mDbConfigs[dn] = dbConfig;
                    }
                }
            }
        }

        public static string GetOrgUploadPath(HttpRequest request)
        {
            var result = GetConfigForRequest(request).UploadsBaseUrl;
            if (result == null || result == "") throw new Exception("Error.NoUploadPath");

            return result;
        }

        public static string GetOrgPrivateStaticPath(HttpRequest request)
        {
            var result = GetConfigForRequest(request).PrivateStaticBaseUrl;
            if (result == null || result == "") throw new Exception("Error.NoStaticPath");

            return result;
        }

        //public static IDbConnection GetOrgConnectionData(HttpRequest request)
        //{
        //    var org = GetConfigForRequest(request);

        //    // Return a connection to the specific database used for the domain in this request.

        //    // Should cache things here. On the other hand, we already have an orgdata cache, so...
        //    // It may be better to return a postgtresConfig object (cached?)

        //    return null;
        //}

        public static IEnumerable<string> GetNames()
        {
            if (mOrgDbsByName == null) throw new Exception(ErrorNoOrgsConfiguredMessage);

            return mOrgDbsByName.Keys;
        }


        public static IEnumerable<OrganizationDispatchData> LoadOrganizationsFromFile(string fileName)
        {
            using (var r = new StreamReader(fileName))
            {
                return JsonConvert.DeserializeObject<List<OrganizationDispatchData>>(r.ReadToEnd());
            }
        }

        public static IEnumerable<OrganizationDispatchData> LoadOrganizationsFromDb(IDbConnection c)
        {
            throw new NotImplementedException();
        }

        public static OrganizationDispatchData GetConfigForRequest(HttpRequest request)
        {
            if (mOrganizations == null) throw new Exception(ErrorNoOrgsConfiguredMessage);

            var domain = request.Host.Value;

            if (mOrganizations.TryGetValue(domain, out OrganizationDispatchData result)) return result;

            throw new Exception(ErrorOrgNotFoundMessage);
        }

        public static OrganizationDispatchData GetConfigForOrgName(string orgName)
        {
            if (mOrgsByName == null) throw new Exception(ErrorNoOrgsConfiguredMessage);

            if (mOrgsByName.TryGetValue(orgName, out OrganizationDispatchData result)) return result;

            throw new Exception(ErrorOrgNotFoundMessage);
        }

        public static PostgresqlConfig GetDbConfigForRequest(HttpRequest request)
        {
            if (mDbConfigs == null) throw new Exception(ErrorNoOrgsConfiguredMessage);

            var domain = request.Host.Value;

            if (mDbConfigs.TryGetValue(domain, out PostgresqlConfig result)) return result;

            throw new Exception(ErrorOrgNotFoundMessage);
        }

        public static PostgresqlConfig GetDbConfigForOrgName(string orgName)
        {
            if (mOrgDbsByName == null) throw new Exception(ErrorNoOrgsConfiguredMessage);

            if (mOrgDbsByName.TryGetValue(orgName, out PostgresqlConfig result)) return result;

            throw new Exception(ErrorOrgNotFoundMessage);
        }

        private static Dictionary<string, OrganizationDispatchData> mOrganizations;
        private static Dictionary<string, OrganizationDispatchData> mOrgsByName;
        private static Dictionary<string, PostgresqlConfig> mDbConfigs;
        private static Dictionary<string, PostgresqlConfig> mOrgDbsByName;

        
        private const string ErrorNoOrgsConfiguredMessage = "Error.NoOrgsConfigured";
        private const string ErrorOrgNotFoundMessage = @"
   _____          ________       .__   
  /     \ ___.__./  _____/  ____ |  |  
 /  \ /  <   |  /   \  ___ /  _ \|  |  
/    Y    \___  \    \_\  (  <_> )  |__
\____|__  / ____|\______  /\____/|____/
        \/\/            \/             ";
    }

    public class OrganizationDispatchData: BaseObject
    {
        public string Name { get; set; }
        public string DomainName { get; set; }
        public string[] OtherNames { get; set; }
        public string ApiUrl { get; set; }
        public string PrivateWebBaseUrl { get; set; }
        public string PublicWebBaseUrl { get; set; }
        public string UploadsBaseUrl { get; set; }
        public string PrivateStaticBaseUrl { get; set; }
        public string PublicStaticBaseUrl { get; set; }
        public string DbName { get; set; }
        public string DbUser { get; set; }
        public string DbPassword { get; set; }

        public MailgunConfiguration MailgunConfiguration { get; set; }
    }

    public class MailgunConfiguration
    {
        public string EmailFromName { get; set; }
        public string EmailFromAddress { get; set; }
        public string MailgunDomain { get; set; }
        public string MailgunPrivateKey { get; set; }
    }
}
