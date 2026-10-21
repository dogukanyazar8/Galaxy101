using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Galaxy101.Tests
{
    public sealed class WindowsBuildSceneTests
    {
        private static string[] WindowsScenes()
        {
            var builder=AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly=>assembly.GetType("Okey101.Editor.BuildWindows"))
                .First(type=>type!=null);
            return (string[])builder.GetMethod("GetBuildScenes",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        }

        [Test]
        public void WindowsBuildContainsEverySceneTheMenuCanOpen()
        {
            var names=WindowsScenes().Select(Path.GetFileNameWithoutExtension).ToArray();
            Assert.That(names[0],Is.EqualTo("Bootstrap"));
            Assert.That(names,Does.Contain(MainMenuController.GalaxyTableScene),"The production bot button must work in the packaged player.");
            Assert.That(names,Does.Contain(MainMenuController.LegacyTableScene),"Existing diagnostic table entry points must remain available.");
        }

        [Test]
        public void WindowsBuildUsesExistingDistinctAuthoredScenes()
        {
            var scenes=WindowsScenes();
            Assert.That(scenes.Distinct(StringComparer.Ordinal).Count(),Is.EqualTo(scenes.Length));
            foreach(var path in scenes)
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),Is.Not.Null,"Missing authored scene: "+path);
        }
    }
}
