using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Text;
using webapi.Controllers;
using webapi.Models.Db;

namespace webapi.test
{
    [TestClass]
    public class PinTests
    {
        [TestMethod]
        public void NegativePin()
        {
            // 114  davidbiosca1980@hotmail.com

            var user = new User
            {
                Id = 114,
                Email = "davidbiosca1980@hotmail.com"
            };

            var authManager = new AuthTokenManager(new TokenAuthConfig());

            var pin = UsersController.GetActivationPin(authManager, user);

            Assert.AreEqual("0214", pin);
        }

        [TestMethod]
        public void TestAlgorithmsFull()
        {
            int equal = 0, different = 0;

            for (byte a = 32; a <= 60; ++a)
            {
                for (byte b = 32; b <= 60; ++b)
                {
                    var res = Compare(a, b);

                    if (res)
                        equal++;
                    else
                        different++;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Equal: {equal}, Different: {different}");
        }


        [TestMethod]
        public void TestAlgorithmsNegative()
        {
            Compare(115, 48);
        }

        private static bool Compare(byte t1, byte t2)
        {
            var original = AlgoOriginal(t1, t2);
            var alternate = AlgoAlternate2(t1, t2);

            System.Diagnostics.Debug.WriteLine($"{original} -> {alternate} {(alternate != original ? '!' : ' ')} {(original.StartsWith('-') ? '-' : ' ')}");

            return original == alternate;
        }

        private static string AlgoOriginal(byte t1, byte t2)
        {
            int hash = t2 << 8 + t1;
            if (hash < 0) hash = -hash;
            var pin = hash.ToString("0000").Substring(0, 4);
            return pin;
        }

        private static string AlgoAlternate1(byte t1, byte t2)
        {
            int st2 = ((int)t2) << 8;

            int hash = st2 + t1;
            if (hash < 0) hash = -hash;
            var pin = hash.ToString("0000").Substring(0, 4);
            return pin;
        }

        private static string AlgoAlternate2(byte t1, byte t2)
        {
            int hash = t2 << 8 + t1;
            if (hash < 0) hash = -hash;
            var pin = hash.ToString("0000").Substring(0, 4);
            if (pin.StartsWith('-')) pin = '0' + pin.Substring(1);
            return pin;
        }

    }
}
