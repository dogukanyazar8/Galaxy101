using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Okey101.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Okey101.PlayMode.Tests
{
    public sealed class WindowsSoakTests
    {
        private GameObject root;
        private string directory;
        [Serializable] private sealed class Report
        {public bool completed;public int profileSamples;public double elapsedSeconds,profileElapsedSeconds;public string failure;}
        [SetUp] public void Setup()
        {root=new GameObject("Soak test");directory=Path.Combine(Path.GetTempPath(),"Okey101-soak-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);}
        [TearDown] public void Cleanup()
        {UnityEngine.Object.DestroyImmediate(root);Directory.Delete(directory,true);}
        [Test] public void RefusesOccupiedOutputAndInvalidDurations()
        {
            var game=root.AddComponent<GameBootstrap>();game.Initialize(71,0);
            var probe=root.AddComponent<WindowsSoakProbe>();
            Assert.Throws<ArgumentOutOfRangeException>(()=>probe.Initialize(game,directory,0,2,1));
            File.WriteAllText(Path.Combine(directory,"keep.txt"),"existing evidence");
            Assert.Throws<IOException>(()=>probe.Initialize(game,directory,0,.1,.3));
            Assert.That(File.ReadAllText(Path.Combine(directory,"keep.txt")),Is.EqualTo("existing evidence"));
        }
        [Test] public void BackgroundAcceptanceIgnoresFocusLossButExplicitPauseStillWorks()
        {
            var game=root.AddComponent<GameBootstrap>();game.Initialize(71,0);
            game.EnableBackgroundAcceptance();game.HandleFocus(false);
            Assert.That(game.Session.IsPaused,Is.False);
            game.SetPaused(true);Assert.That(game.Session.IsPaused,Is.True);
            game.SetPaused(false);Assert.That(game.Session.IsPaused,Is.False);
        }
        [UnityTest] public IEnumerator ShortRunWritesMeasuredWindowAndClosesStreams()
        {
            var game=root.AddComponent<GameBootstrap>();game.Initialize(71,0);
            root.AddComponent<WindowsSoakProbe>().Initialize(game,directory,0,.15,.3);
            var path=Path.Combine(directory,"soak.json");var deadline=Time.realtimeSinceStartup+10;
            while(!File.Exists(path)){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(path));
            Assert.That(report.completed,Is.True,report.failure);Assert.That(report.elapsedSeconds,Is.GreaterThanOrEqualTo(.3));
            Assert.That(report.profileElapsedSeconds,Is.GreaterThanOrEqualTo(.15));Assert.That(report.profileSamples,Is.GreaterThan(2));
            using(var file=File.Open(Path.Combine(directory,"frames.csv"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                Assert.That(file.Length,Is.GreaterThan(100));
            Assert.That(File.ReadAllText(Path.Combine(directory,"memory.csv")),Does.Contain("managedBytes"));
        }
        [UnityTest] public IEnumerator DeadlineWritesFailureEvenWhenAcceptedMotionIsPaused()
        {
            var game=root.AddComponent<GameBootstrap>();game.Initialize(Okey101.Application.PracticeDeals.Create("Penalty"));
            root.AddComponent<WindowsSoakProbe>().Initialize(game,directory,0,.1,1);
            var deadline=Time.realtimeSinceStartup+5;
            while(!game.Presenter.Motion.IsPlaying){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            game.HandleFocus(false);
            var path=Path.Combine(directory,"soak.json");
            while(!File.Exists(path)){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(path));
            Assert.That(report.completed,Is.False);Assert.That(report.failure,Does.Contain("Pending work"));
            using(var file=File.Open(Path.Combine(directory,"frames.csv"),FileMode.Open,FileAccess.ReadWrite,FileShare.None))
                Assert.That(file.Length,Is.GreaterThan(100));
        }
        [UnityTest] public IEnumerator DeadlineDrainsAnUnpausedAcceptedMotionBeforeReporting()
        {
            var game=root.AddComponent<GameBootstrap>();game.Initialize(Okey101.Application.PracticeDeals.Create("Penalty"));
            var probe=root.AddComponent<WindowsSoakProbe>();probe.Initialize(game,directory,0,.02,.03);probe.SendMessage("Update");
            var path=Path.Combine(directory,"soak.json");var deadline=Time.realtimeSinceStartup+5;
            while(!File.Exists(path)){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            var report=JsonUtility.FromJson<Report>(File.ReadAllText(path));
            Assert.That(report.completed,Is.True,report.failure);
            Assert.That(report.elapsedSeconds,Is.GreaterThanOrEqualTo(.03));
        }
        [TestCase(1800d,1700d,100,2,10,true)]
        [TestCase(1800d,1440d,4,1,1,true)]
        [TestCase(1800d,1439d,100,2,10,false)]
        [TestCase(1800d,1700d,0,2,10,false)]
        [TestCase(1800d,1700d,100,0,10,false)]
        [TestCase(1800d,1700d,100,2,0,false)]
        public void IdleOrUnexercisedWorkloadCannotPass(double elapsed,double active,int commands,int rounds,int pauses,bool expected)
        {
            var method=typeof(WindowsSoakProbe).GetMethod("HasRequiredWorkload",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            Assert.That(method,Is.Not.Null);
            Assert.That(method.Invoke(null,new object[]{elapsed,active,commands,rounds,pauses}),Is.EqualTo(expected));
        }
    }
}
