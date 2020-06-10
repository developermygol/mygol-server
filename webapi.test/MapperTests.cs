using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using webapi;

namespace webapi.test
{
    [TestClass]
    public class MapperTests
    {
        [TestMethod]
        public void MapBasic()
        {
            var from = new FromTestClass();
            var to = new ToTestClass();

            Mapper.MapExcept(from, to);

            Assert.AreEqual(to.PublicField, 1);
            Assert.AreEqual(to.PublicProp, 1);
            Assert.AreEqual(to.PublicReadOnlyProp, 1);
        }

        [TestMethod]
        public void MapTestExceptions()
        {
            var from = new FromTestClass();
            var to = new ToTestClass();

            Mapper.MapExcept(from, to, "PublicField");

            Assert.AreEqual(to.PublicField, 2);
            Assert.AreEqual(to.PublicProp, 1);
            Assert.AreEqual(to.PublicReadOnlyProp, 1);
        }

        [TestMethod]
        public void MapExplicit()
        {
            var from = new FromTestClass();
            var to = new ToTestClass();

            Mapper.MapExplicit(from, to, new[] { "PublicProp" });

            Assert.AreEqual(to.PublicField, 2);
            Assert.AreEqual(to.PublicProp, 1);
            Assert.AreEqual(to.PublicReadOnlyProp, 2);
        }

        [TestMethod]
        public void RedactBasic()
        {
            var target = new FromTestClass {
                PublicProp = 2,
                PublicProp2 = 2,
                PublicProp3 = 2,
                PublicField = 2, 
                PublicField2 = 2,
                StringField = "HoldTheDoor",
                StringField2 = "HoldTheDoor",
                StringProp = "JJ",
                StringProp2 = "JJ"
            };

            Mapper.RedactExcept(target, new[] { "PublicProp", "PublicField", "StringField", "StringProp" });

            Assert.AreEqual(target.PublicProp, 2);
            Assert.AreEqual(target.PublicProp2, 0);
            Assert.AreEqual(target.PublicProp3, 0);
            Assert.AreEqual(target.PublicField, 2);
            Assert.AreEqual(target.PublicField2, 0);
            Assert.AreEqual(target.PublicField2, 0);
            Assert.AreEqual(target.StringField, "HoldTheDoor");
            Assert.AreEqual(target.StringField2, null);
            Assert.AreEqual(target.StringProp, "JJ");
            Assert.AreEqual(target.StringProp2, null);
        }

        //[TestMethod]
        //public void RedactMultiLevel()
        //{
        //    var target = new MultiLevel
        //    {
        //        StringProp = "HoldTheDoorAgain",
        //        StringProp2 = "HoldTheDoorAgain",
        //        PublicProp = new FromTestClass
        //        {
        //            PublicProp = 2,
        //            PublicProp2 = 2,
        //            PublicProp3 = 2,
        //            PublicField = 2,
        //            PublicField2 = 2,
        //            StringField = "HoldTheDoor",
        //            StringField2 = "HoldTheDoor",
        //            StringProp = "JJ",
        //            StringProp2 = "JJ"
        //        }
        //    };

        //    Mapper.Redact(target, new[] { "StringProp", "PublicProp.PublicProp", "PublicProp.PublicField", "PublicProp.StringProp" });

        //    Assert.AreEqual(target.StringProp, "HoldTheDoorAgain");
        //    Assert.AreEqual(target.StringProp, null);
        //    Assert.AreEqual(target.PublicProp.PublicProp, 2);
        //    Assert.AreEqual(target.PublicProp.PublicProp2, 0);
        //    Assert.AreEqual(target.PublicProp.PublicProp3, 0);
        //    Assert.AreEqual(target.PublicProp.PublicField, 2);
        //    Assert.AreEqual(target.PublicProp.PublicField2, 0);
        //    Assert.AreEqual(target.PublicProp.PublicField2, 0);
        //    Assert.AreEqual(target.PublicProp.StringField, null);
        //    Assert.AreEqual(target.PublicProp.StringField2, null);
        //    Assert.AreEqual(target.PublicProp.StringProp, "JJ");
        //    Assert.AreEqual(target.PublicProp.StringProp2, null);
        //}




        internal class FromTestClass
        {
            public int PublicField = 1;
            public int PublicField2 = 1;
            public int PublicProp { get; set; } = 1;
            public int PublicProp2 { get; set; } = 1;
            public int PublicProp3 { get; set; } = 1;
            public int PublicReadOnlyProp { get; } = 1;

            //private int PrivField = 1;
            private int PrivProperty { get; set; } = 1;

            public string StringField;
            public string StringField2;
            public string StringProp { get; set; }
            public string StringProp2 { get; set; }
        }

        internal class ToTestClass
        {
            public int PublicField = 2;
            public int PublicProp { get; set; } = 2;
            public int PublicReadOnlyProp { get; set; } = 2;
        }

        internal class MultiLevel
        {
            public string StringProp { get; set; }
            public string StringProp2 { get; set; }
            public FromTestClass PublicProp { get; set; }
            public FromTestClass PublicProp2 { get; set; }
        }
    }
}
