using System;
using System.IO;
using System.Reflection;
using Galaxy101;
using NUnit.Framework;
using Okey101.Application;
using Okey101.Presentation;

namespace Okey101.PlayMode.Tests
{
    public sealed class SharedMenuPreferencesTests
    {
        private string directory,legacyPath,nativePath;
        [SetUp] public void SetUp()
        {
            directory=Path.Combine(Path.GetTempPath(),"galaxy-table-volume-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            legacyPath=Path.Combine(directory,"table.json");nativePath=Path.Combine(directory,"menu.json");
        }
        [TearDown] public void TearDown(){if(Directory.Exists(directory))Directory.Delete(directory,true);}

        private static object Call(string name,params object[] args)
        {
            var type=typeof(GameBootstrap).Assembly.GetType("Okey101.Presentation.MenuUI");
            return type.GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
        }
        private void NativeVolume(float value)
        {
            Assert.That(MenuModel.LoadResources(nativePath).Persist(),Is.True);
            GalaxyPreferencesBridge.SaveMasterVolume(value,nativePath);
        }
        [Test] public void TableLoadsNativeMasterAndKeepsLegacyFullscreenAndReducedMotion()
        {
            new LocalSettings{MasterVolume=.9f,Fullscreen=true,ReducedMotion=true}.Save(legacyPath);NativeVolume(.25f);
            var loaded=(LocalSettings)Call("LoadSettings",legacyPath,nativePath);
            Assert.That(loaded.MasterVolume,Is.EqualTo(.25f));Assert.That(loaded.Fullscreen,Is.True);Assert.That(loaded.ReducedMotion,Is.True);
        }
        [Test] public void TableSaveSynchronizesBothVolumesAndKeepsLegacyFlags()
        {
            NativeVolume(.25f);
            Call("SaveSettings",new LocalSettings{MasterVolume=.47f,Fullscreen=true,ReducedMotion=true},legacyPath,nativePath);
            Assert.That(GalaxyPreferencesBridge.TryLoadMasterVolume(out var value,out var error,nativePath),Is.True);
            Assert.That(value,Is.EqualTo(.47f));Assert.That(error,Is.Null);
            var legacy=LocalSettings.Load(legacyPath);
            Assert.That(legacy.MasterVolume,Is.EqualTo(.47f));Assert.That(legacy.Fullscreen,Is.True);Assert.That(legacy.ReducedMotion,Is.True);
        }
        [Test] public void FailedLegacySaveRollsBackTheNativeVolume()
        {
            NativeVolume(.25f);string blocked=Path.Combine(directory,"blocked");Directory.CreateDirectory(blocked);
            var failure=Assert.Throws<TargetInvocationException>(()=>Call("SaveSettings",new LocalSettings{MasterVolume=.47f},blocked,nativePath));
            Assert.That(failure.InnerException,Is.InstanceOf<IOException>());
            Assert.That(GalaxyPreferencesBridge.TryLoadMasterVolume(out var value,out _,nativePath),Is.True);
            Assert.That(value,Is.EqualTo(.25f));
        }
        [Test] public void LegacyOnlySaveDoesNotCreateNativePreferences()
        {
            Call("SaveSettings",new LocalSettings{MasterVolume=.32f},legacyPath,nativePath);
            Assert.That(File.Exists(nativePath),Is.False);Assert.That(LocalSettings.Load(legacyPath).MasterVolume,Is.EqualTo(.32f));
        }
    }
}
