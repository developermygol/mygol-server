using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using Dapper;
using Dapper.Contrib.Extensions;
using webapi.Models.Db;
using webapi.Controllers;

namespace webapi.test
{
    [TestClass]
    public class PushNotificationTest
    {
        [TestMethod]
        public void BasicPush()
        {
            // Commented out so running the test doesn't spit notifications like crazy. 
            // This was meant to test during development. 

            //var users = new User[]
            //{
            //    new User { DeviceToken = "ExponentPushToken[uyz2YHLsz-f-S56JN6S1xz]" },
            //    new User { DeviceToken = "ExponentPushToken[yIujVSMuD7JLuHaV8xKnxS]" }
            //};

            //var ns = ExpoPushAdapter.GetNotifications(users, "Próximo partido", "En el estadio de siempre");

            //ExpoPushProvider.EnqueueNotifications(ns);

            //ExpoPushProvider.Stop();
        }
    }
}
