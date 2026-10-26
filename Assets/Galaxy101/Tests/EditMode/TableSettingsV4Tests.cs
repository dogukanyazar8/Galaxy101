using System;
using System.Reflection;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class TableSettingsV4Tests
    {
        [Test]
        public void MidViewCanBeSelectedAndInvalidViewsLeaveTheSelectionUntouched()
        {
            var settings = new TableSettings(null, false);
            int changed = 0;
            settings.Changed += key => { Assert.That(key, Is.EqualTo("view")); changed++; };
            settings.Set("view", "mid");
            Assert.That(settings.View, Is.EqualTo("mid"));
            settings.Set("view", "mid");
            Assert.That(changed, Is.EqualTo(1), "Selecting the same view does not restart its transition.");
            Assert.Throws<ArgumentException>(() => settings.Set("view", "unknown"));
            Assert.That(settings.View, Is.EqualTo("mid"));
        }

        [Test]
        public void AStoredMidViewSurvivesLoadingAlongsideTheExistingTopChoice()
        {
            bool hadValue = PlayerPrefs.HasKey(TableSettings.PrefsKey);
            string previous = PlayerPrefs.GetString(TableSettings.PrefsKey);
            bool designDefaults = TableSettings.DesignDefaultsOnly;
            try
            {
                TableSettings.DesignDefaultsOnly = false;
                foreach (var choice in new[] { "mid", "top" })
                {
                    PlayerPrefs.SetString(TableSettings.PrefsKey, "{\"view\":\"" + choice + "\"}");
                    Assert.That(new TableSettings(null).View, Is.EqualTo(choice));
                }
            }
            finally
            {
                TableSettings.DesignDefaultsOnly = designDefaults;
                if (hadValue) PlayerPrefs.SetString(TableSettings.PrefsKey, previous);
                else PlayerPrefs.DeleteKey(TableSettings.PrefsKey);
            }
        }

        [Test]
        public void MidCameraUsesThePackagesPoseWithoutChangingExistingEnumValues()
        {
            Assert.That((int)TableCameraRig.View.Top, Is.EqualTo(1));
            Assert.That(Enum.TryParse("Mid", out TableCameraRig.View mid), Is.True);
            var method = typeof(TableCameraRig).GetMethod("ViewOf", BindingFlags.NonPublic | BindingFlags.Static);
            var pose = (TableConstants.CameraView)method.Invoke(null, new object[] { mid });
            Assert.That(pose.Position, Is.EqualTo(new Vector3(0, 2.08f, 1.62f)));
            Assert.That(pose.Target, Is.EqualTo(new Vector3(0, .6f, -.06f)));
            Assert.That(TableConstants.FieldOfView, Is.EqualTo(40));
        }
    }
}
