using System;
using System.IO;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    public sealed class GalaxyPreferencesBridgeTests
    {
        private string path;
        [SetUp] public void SetUp() { path=Path.Combine(Path.GetTempPath(),"galaxy-shared-volume-"+Guid.NewGuid().ToString("N")+".json"); }
        [TearDown] public void TearDown() { if(File.Exists(path))File.Delete(path); }

        [Test] public void AbsentNativePreferencesRemainAbsentAfterLegacyVolumeSave()
        {
            Assert.That(GalaxyPreferencesBridge.TryLoadMasterVolume(out _,out var error,path),Is.False);
            Assert.That(error,Is.Null);
            GalaxyPreferencesBridge.SaveMasterVolume(.25f,path);
            Assert.That(File.Exists(path),Is.False);
        }

        [Test] public void TableVolumeRoundTripsWithoutOverwritingProfileThemeOrOtherNativeSettings()
        {
            var model=MenuModel.LoadResources(path);
            var service=new MenuServices(model);
            Assert.That(service.Profile.Save("İpek Işık","#3D7BFF","Merhaba",1),Is.True);
            Assert.That(service.Settings.SetPlanet("Mars"),Is.True);
            var draft=service.Settings.Load();draft["master"]=25;draft["music"]=32;
            Assert.That(service.Settings.Save(draft),Is.True);
            Assert.That(GalaxyPreferencesBridge.TryLoadMasterVolume(out var volume,out var error,path),Is.True);
            Assert.That(error,Is.Null);Assert.That(volume,Is.EqualTo(.25f));
            GalaxyPreferencesBridge.SaveMasterVolume(.47f,path);
            var restored=MenuModel.LoadResources(path);
            Assert.That((int)restored.Settings["master"],Is.EqualTo(47));
            Assert.That((int)restored.Settings["music"],Is.EqualTo(32));
            Assert.That((string)restored.Profile["name"],Is.EqualTo("İpek Işık"));
            Assert.That((int)restored.Profile["frame"],Is.EqualTo(1));
            Assert.That(restored.Planet,Is.EqualTo("Mars"));
        }

        [Test] public void CorruptNativePreferencesAreReportedAndNeverOverwrittenByLegacySave()
        {
            File.WriteAllText(path,"{broken");
            Assert.That(GalaxyPreferencesBridge.TryLoadMasterVolume(out _,out var error,path),Is.False);
            Assert.That(error,Is.Not.Empty);
            Assert.Throws<IOException>(()=>GalaxyPreferencesBridge.SaveMasterVolume(.3f,path));
            Assert.That(File.ReadAllText(path),Is.EqualTo("{broken"));
        }

        [Test] public void LegacyVolumeValidationRejectsNaNInfinityAndOutOfRange()
        {
            foreach(var volume in new[]{float.NaN,float.PositiveInfinity,-.1f,1.1f})
                Assert.Throws<ArgumentOutOfRangeException>(()=>GalaxyPreferencesBridge.SaveMasterVolume(volume,path));
        }
    }
}
