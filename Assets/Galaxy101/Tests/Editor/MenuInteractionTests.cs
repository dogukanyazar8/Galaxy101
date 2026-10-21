using System;
using System.Linq;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    public sealed class MenuInteractionTests
    {
        private static Type Require(string name)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Galaxy101."+name)).FirstOrDefault(t=>t!=null);
            Assert.That(type,Is.Not.Null,"Missing native menu behavior: "+name);
            return type;
        }

        [Test]
        public void ClosingNestedDialogPreservesUnderlyingPanelAndNextCloseClearsPanel()
        {
            var type=Require("ModalStack");var stack=Activator.CreateInstance(type);
            type.GetMethod("OpenPanel").Invoke(stack,new object[]{"settings"});
            type.GetMethod("OpenDialog").Invoke(stack,new object[]{"profile"});
            type.GetMethod("CloseTop").Invoke(stack,null);
            Assert.That(type.GetProperty("Panel").GetValue(stack),Is.EqualTo("settings"));
            Assert.That(type.GetProperty("Dialog").GetValue(stack),Is.Null);
            type.GetMethod("CloseTop").Invoke(stack,null);
            Assert.That(type.GetProperty("Panel").GetValue(stack),Is.Null);
        }

        [Test]
        public void OpeningAnotherPanelClearsItsPredecessorAndNestedDialog()
        {
            var type=Require("ModalStack");var stack=Activator.CreateInstance(type);
            type.GetMethod("OpenPanel").Invoke(stack,new object[]{"settings"});
            type.GetMethod("OpenDialog").Invoke(stack,new object[]{"delete"});
            type.GetMethod("OpenPanel").Invoke(stack,new object[]{"shop"});
            Assert.That(type.GetProperty("Panel").GetValue(stack),Is.EqualTo("shop"));
            Assert.That(type.GetProperty("Dialog").GetValue(stack),Is.Null);
        }

        [TestCase(112f,100f,246f,1,10,1)]
        [TestCase(235f,112f,246f,1,10,6)]
        [TestCase(358f,112f,246f,1,10,10)]
        [TestCase(-50f,112f,246f,0,100,0)]
        [TestCase(800f,112f,246f,0,100,100)]
        public void SliderUsesInnerTrackCoordinatesAndClampsAtBothEnds(float pointer,float left,float width,int minimum,int maximum,int expected)
        {
            var type=Require("GalaxySlider");
            var actual=type.GetMethod("MapValue").Invoke(null,new object[]{pointer,left,width,minimum,maximum});
            Assert.That(actual,Is.EqualTo(expected));
        }
    }
}
