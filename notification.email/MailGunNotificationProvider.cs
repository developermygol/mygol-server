using contracts;
using RestSharp;
using RestSharp.Authenticators;
using System;

namespace notification.email
{
    public class MailGunNotificationProvider
    {
        // DAVE: THIS IS NOT USED. I'm using a class direclty inside the webapi. Too many dependencies with the new organizationManager 
        //(HttpRequest, Orgmanager to get specific org config, cyclic references...)

        public void Notify(string fromAddress, string toAddress, string Subject, string msgBody)
        {
            //RestClient client = new RestClient();
            //client.BaseUrl = new Uri("https://api.mailgun.net/v3");
            //client.Authenticator =
            //    new HttpBasicAuthenticator("api",
            //                                "YOUR_API_KEY");
            //RestRequest request = new RestRequest();
            //request.AddParameter("domain", "YOUR_DOMAIN_NAME", ParameterType.UrlSegment);
            //request.Resource = "{domain}/messages";
            //request.AddParameter("from", "Excited User <mailgun@YOUR_DOMAIN_NAME>");
            //request.AddParameter("to", "bar@example.com");
            //request.AddParameter("to", "YOU@YOUR_DOMAIN_NAME");
            //request.AddParameter("subject", "Hello");
            //request.AddParameter("text", "Testing some Mailgun awesomness!");
            //request.Method = Method.POST;

            //return client.Execute(request);
        }
    }
}
