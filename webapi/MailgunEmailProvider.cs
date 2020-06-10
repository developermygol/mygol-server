using Microsoft.AspNetCore.Http;
using RestSharp;
using RestSharp.Authenticators;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace webapi
{
    public class MailgunEmailProvider
    {
        public bool SendEmail(HttpRequest httpRequest, string toAddress, string subject, string textContent, string htmlContent)
        {
            if (httpRequest == null) throw new ArgumentNullException("httpRequest");
            if (toAddress == null) throw new ArgumentNullException("toAddress");
            if (subject == null) throw new ArgumentNullException("subject");

            var cfg = OrganizationManager.GetConfigForRequest(httpRequest);
            var mailgunCfg = cfg.MailgunConfiguration;
            if (mailgunCfg == null) throw new Exception("Error.EmailNotconfigured");
            
            var result = SendEmail(mailgunCfg, toAddress, subject, textContent, htmlContent);
            if (result == null) return true;  // ignore empty config

            if (!result.IsSuccessful) Log.Error(result.Content);

            return result.IsSuccessful;
        }

        private IRestResponse SendEmail(MailgunConfiguration cfg, string toAddress, string subject, string textContent, string htmlContent)
        {
            // Should enqueue and return immediately.

            if (cfg.MailgunPrivateKey == null || cfg.MailgunPrivateKey == "") return null;

            RestClient client = new RestClient();
            // client.BaseUrl = new Uri("https://api.mailgun.net/v3");
            client.BaseUrl = new Uri("https://api.eu.mailgun.net/v3"); // using the EU region with Mailgun
            client.Authenticator = new HttpBasicAuthenticator("api", cfg.MailgunPrivateKey);
            RestRequest request = new RestRequest();
            request.AddParameter("domain", cfg.MailgunDomain, ParameterType.UrlSegment);
            request.Resource = "{domain}/messages";
            request.AddParameter("from", $"{cfg.EmailFromName} <{cfg.EmailFromAddress}>");
            request.AddParameter("to", toAddress);
            request.AddParameter("subject", subject);
            if (textContent != null && textContent != "") request.AddParameter("text", textContent);
            if (htmlContent != null && htmlContent != "") request.AddParameter("html", htmlContent);
            request.Method = Method.POST;

            return client.Execute(request);
        }
    }
}
