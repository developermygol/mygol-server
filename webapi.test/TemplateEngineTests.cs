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
    public class TemplateEngineTests
    {
        [TestMethod]
        public void BasicTemplate()
        {
            const string template = "My oh my {{name}}";
            var data = new { name = "Daniel" };

            var result = TemplateEngine.Process(template, data);

            Assert.AreEqual("My oh my Daniel", result);
        }

        [TestMethod]
        public void NestedTemplate()
        {
            const string template = "My oh my {{user.name}}";
            var data = new { user = new { name = "Daniel" } };

            var result = TemplateEngine.Process(template, data);

            Assert.AreEqual("My oh my Daniel", result);
        }

        [TestMethod]
        public void PinGenerator()
        {
            var user = new User { Id = 19, Email = "dave@myhouse.com" };

            var tm = new AuthTokenManager(new TokenAuthConfig());

            var pin1 = UsersController.GetActivationPin(tm, user);
            var pin2 = UsersController.GetActivationPin(tm, user);

            Assert.AreEqual(pin1, pin2);
        }
    }
}
