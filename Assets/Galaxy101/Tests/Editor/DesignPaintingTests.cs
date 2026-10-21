using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class DesignPaintingTests
    {
        [TestCase(0f,0f)]
        [TestCase(.2f,.764261f)]
        [TestCase(.5f,1.004923f)]
        [TestCase(.8f,1.005904f)]
        [TestCase(1f,1f)]
        public void ModalOpeningUsesTheSourceCssBezierIncludingOvershoot(float fraction,float expected)
        {
            // Independent samples of cubic-bezier(.2,1.2,.4,1), not an ease-out approximation.
            var method=Require("MenuView").GetMethod("ModalProgress",BindingFlags.NonPublic|BindingFlags.Static);
            Assert.That(method,Is.Not.Null,"The modal opening must implement the supplied CSS timing curve.");
            Assert.That((float)method.Invoke(null,new object[]{fraction}),Is.EqualTo(expected).Within(.0001f));
        }
        private static Type Require(string name)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Galaxy101."+name)).FirstOrDefault(t=>t!=null);
            Assert.That(type,Is.Not.Null);return type;
        }

        [Test]
        public void StorefrontScallopUsesSvgSweepFlagAndExactCircleCenter()
        {
            var method=Require("GalaxyIcon").GetMethod("CalculateCircularArc",BindingFlags.NonPublic|BindingFlags.Static);
            var arc=method.Invoke(null,new object[]{new Vector2(21,8.5f),new Vector2(15,8.5f),3f,0f,false,true});
            var type=arc.GetType();var center=(Vector2)type.GetField("Center").GetValue(arc);
            Assert.That(center.x,Is.EqualTo(18).Within(.0001f));Assert.That(center.y,Is.EqualTo(8.5f).Within(.0001f));
            Assert.That((float)type.GetField("Radius").GetValue(arc),Is.EqualTo(3).Within(.0001f));
            Assert.That((float)type.GetField("StartRadians").GetValue(arc),Is.EqualTo(0).Within(.0001f));
            Assert.That((float)type.GetField("EndRadians").GetValue(arc),Is.EqualTo(Mathf.PI).Within(.0001f));
        }

        [Test]
        public void StorefrontBodyCounterclockwiseCornerKeepsItsOnePixelRadius()
        {
            var method=Require("GalaxyIcon").GetMethod("CalculateCircularArc",BindingFlags.NonPublic|BindingFlags.Static);
            var arc=method.Invoke(null,new object[]{new Vector2(4.5f,19.5f),new Vector2(5.5f,20.5f),1f,0f,false,false});
            var type=arc.GetType();var center=(Vector2)type.GetField("Center").GetValue(arc);
            Assert.That(center.x,Is.EqualTo(5.5f).Within(.0001f));Assert.That(center.y,Is.EqualTo(19.5f).Within(.0001f));
            float start=(float)type.GetField("StartRadians").GetValue(arc),end=(float)type.GetField("EndRadians").GetValue(arc);
            Assert.That(end-start,Is.EqualTo(-Mathf.PI/2).Within(.0001f));
        }

        [Test]
        public void LiteralWhiteAwningPaintDoesNotInheritBrownStrokeColorOrItsOpacity()
        {
            var type=Require("GalaxyIcon");var icon=Activator.CreateInstance(type,new object[]{"shop",new Color(.2f,.1f,0,.4f)});
            var actual=(Color)type.GetMethod("Paint",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(icon,new object[]{"rgba(255,255,255,.6)"});
            Assert.That(actual.r,Is.EqualTo(1));Assert.That(actual.g,Is.EqualTo(1));Assert.That(actual.b,Is.EqualTo(1));Assert.That(actual.a,Is.EqualTo(.6f).Within(.0001f));
        }

        [Test]
        public void CoinDashesStayInsideTheTwentyEightPixelBorderBox()
        {
            var type=Require("CssDashedCircle").GetNestedType("Layout",BindingFlags.NonPublic);
            var layout=Activator.CreateInstance(type,new object[]{28f,2f});
            float radius=(float)type.GetField("Radius").GetValue(layout),step=(float)type.GetField("StepRadians").GetValue(layout);
            int count=(int)type.GetField("Count").GetValue(layout);
            Assert.That(radius+1,Is.EqualTo(14));Assert.That(radius-1,Is.EqualTo(12));
            Assert.That(step*count,Is.EqualTo(2*Mathf.PI).Within(.0001f));
            Assert.That(radius*step*.5f,Is.EqualTo(5.834386f).Within(.0001f));
        }
    }
}
