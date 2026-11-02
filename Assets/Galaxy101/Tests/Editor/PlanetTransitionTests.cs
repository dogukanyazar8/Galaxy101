using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public class PlanetTransitionTests
    {
        private sealed class Driver
        {
            private readonly Type type;
            private readonly object state;
            public Driver()
            {
                type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Galaxy101.PlanetTransition")).FirstOrDefault(t=>t!=null);
                Assert.That(type,Is.Not.Null,"The menu needs a native, independently sampled planet transition.");
                state=Activator.CreateInstance(type);
            }
            public void Configure(string name,bool animate,double now)=>type.GetMethod("Configure").Invoke(state,new object[]{name,animate,now});
            public object Sample(double now)=>type.GetMethod("Sample").Invoke(state,new object[]{now});
            public object Get(string name)=>type.GetProperty(name).GetValue(state);
        }
        private static object Get(object value,string name)=>value.GetType().GetField(name).GetValue(value);
        private static float Number(object value,string name)=>(float)Get(value,name);
        private static void Near(object value,string name,float expected)=>Assert.That(Number(value,name),Is.EqualTo(expected).Within(.0002f),name);

        [Test]
        public void InitialConfigurationDisplaysSelectedPlanetWithoutAnEntrance()
        {
            var state=new Driver();state.Configure("mars",true,12);
            var frame=state.Sample(12);
            Assert.That(state.Get("Current"),Is.EqualTo("mars"));
            Assert.That(state.Get("Previous"),Is.Null);
            Assert.That(Get(frame,"Active"),Is.False);
            Near(Get(frame,"Incoming"),"Scale",1);Near(Get(frame,"Incoming"),"Opacity",1);
            Near(frame,"HaloOpacity",1);Near(frame,"RingOpacity",0);
        }

        [Test]
        public void OutgoingUsesGlobalCssEasingBeforeItsTwentyEightPercentKeyframe()
        {
            var state=new Driver();state.Configure("saturn",true,0);state.Configure("mars",true,0);
            var pose=Get(state.Sample(.475),"Outgoing");
            // Independently solved cubic-bezier(.55,0,.75,.35) at wall-clock progress .5.
            Near(pose,"X",7.545442f);Near(pose,"Y",-4.311681f);Near(pose,"Scale",1.037727f);
            Near(pose,"Rotation",1.616880f);Near(pose,"Opacity",1);Near(pose,"Brightness",1.080844f);
        }

        [Test]
        public void OutgoingSnapshotReachesTheReferenceFinalPoseAtNineHundredFiftyMilliseconds()
        {
            var state=new Driver();state.Configure("saturn",true,0);state.Configure("mars",true,0);
            var pose=Get(state.Sample(.95),"Outgoing");
            Near(pose,"X",-560);Near(pose,"Y",300);Near(pose,"Scale",.16f);Near(pose,"Rotation",-75);
            Near(pose,"Opacity",0);Near(pose,"Blur",7);Near(pose,"Brightness",.6f);
        }

        [Test]
        public void IncomingWaitsThenOvershootsAndSettlesWhileTheRingContinues()
        {
            var state=new Driver();state.Configure("saturn",true,0);state.Configure("mars",true,0);
            var initial=Get(state.Sample(.32),"Incoming");
            Near(initial,"X",480);Near(initial,"Y",-320);Near(initial,"Scale",.1f);Near(initial,"Opacity",0);Near(initial,"Blur",10);
            var peak=Get(state.Sample(.5762865131171666),"Incoming");
            Near(peak,"X",-10);Near(peak,"Y",6);Near(peak,"Scale",1.05f);Near(peak,"Opacity",1);Near(peak,"Blur",0);
            var settled=state.Sample(1.47);
            Near(Get(settled,"Incoming"),"X",0);Near(Get(settled,"Incoming"),"Y",0);Near(Get(settled,"Incoming"),"Scale",1);
            Assert.That(Get(settled,"Active"),Is.True);Assert.That(Number(settled,"RingOpacity"),Is.GreaterThan(0));
        }

        [Test]
        public void PulseAndHaloHaveTheirIndependentDelaysAndCssEasing()
        {
            var state=new Driver();state.Configure("saturn",true,0);state.Configure("mars",true,0);
            Near(state.Sample(.5),"HaloOpacity",0);Near(state.Sample(.9),"RingOpacity",0);
            var pulse=state.Sample(.9661993198915626);
            Near(pulse,"RingOpacity",.85f);Near(pulse,"RingScale",.8875f);
            Near(state.Sample(.95),"HaloOpacity",.6846432f);
            var finished=state.Sample(1.8);
            Assert.That(Get(finished,"Active"),Is.False);Near(finished,"RingOpacity",0);Near(finished,"HaloOpacity",1);
            Assert.That(state.Get("Previous"),Is.Null);
        }

        [Test]
        public void RapidThemeReplacementKeepsOnlyTheImmediatelyPreviousPlanet()
        {
            var state=new Driver();state.Configure("saturn",true,0);state.Configure("mars",true,1);
            state.Sample(1.5);state.Configure("neptune",true,1.5);
            Assert.That(state.Get("Previous"),Is.EqualTo("mars"));Assert.That(state.Get("Current"),Is.EqualTo("neptune"));
            var frame=state.Sample(1.5);Near(Get(frame,"Outgoing"),"X",0);Near(Get(frame,"Incoming"),"Opacity",0);
            state.Configure("neptune",true,1.6);
            Near(Get(state.Sample(1.975),"Outgoing"),"X",7.545442f);
        }

        [Test]
        public void DisablingMotionCancelsAnUnfinishedTransitionAndReenablingDoesNotReplayIt()
        {
            var state=new Driver();state.Configure("saturn",true,0);state.Configure("mars",true,0);
            state.Configure("mars",false,.4);
            Assert.That(state.Get("Previous"),Is.Null);Assert.That(Get(state.Sample(.4),"Active"),Is.False);
            Near(Get(state.Sample(.4),"Incoming"),"Opacity",1);
            state.Configure("mars",true,10);Assert.That(Get(state.Sample(10),"Active"),Is.False);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1)]
        public void InvalidTimeCannotCorruptTheAnimationState(double now)
        {
            var state=new Driver();state.Configure("mars",true,0);
            var error=Assert.Throws<TargetInvocationException>(()=>state.Sample(now));
            Assert.That(error.InnerException,Is.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase("UseTableLayout")]
        [TestCase("UseAuthLayout")]
        public void LeavingTheMenuCancelsItsTransitionBeforeDrawingAnotherComposition(string layout)
        {
            var owner=new GameObject("Planet layout transition test");
            try
            {
                var background=owner.AddComponent<GalaxyBackground>();
                background.Configure("saturn",true,"high");background.Configure("mars",true,"high");
                if(layout=="UseTableLayout")background.UseTableLayout(Color.white);
                else background.UseAuthLayout(new Vector4(310,340,1,.4f),Color.white);
                var transition=typeof(GalaxyBackground).GetField("planetTransition",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(background);
                var frame=transition.GetType().GetMethod("Sample").Invoke(transition,new object[]{0.0});
                Assert.That(Get(frame,"Active"),Is.False);
            }
            finally {UnityEngine.Object.DestroyImmediate(owner);}
        }

        [Test]
        public void PulseShaderCompilesWithoutErrors()
        {
            var shader=Resources.Load<Shader>("Galaxy101PlanetTransition");Assert.That(shader,Is.Not.Null);
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(shader),Is.False);
        }
    }
}
