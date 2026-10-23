using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class MenuServicesTests
    {
        private MenuModel model;
        private MenuServices services;
        private string savePath;

        [SetUp] public void SetUp()
        {
            savePath = Path.Combine(Path.GetTempPath(), "galaxy101-" + Guid.NewGuid().ToString("N") + ".json");
            model = NewModel();
            services = new MenuServices(model);
        }

        private MenuModel NewModel() => new MenuModel(
            File.ReadAllText("Design/galaxy101_unity_handoff/spec/data/content.json"),
            File.ReadAllText("Design/galaxy101_unity_handoff/spec/data/themes.json"), savePath);

        [TearDown] public void TearDown()
        {
            if (File.Exists(savePath)) File.Delete(savePath);
        }

        [Test] public void DefaultsMatchSuppliedDesign()
        {
            Assert.That(model.Chips, Is.EqualTo(125400));
            Assert.That(model.Gems, Is.EqualTo(340));
            Assert.That(model.Planet, Is.EqualTo("Satürn"));
            Assert.That((string)model.Settings["lang"], Is.EqualTo("tr"));
            Assert.That((string)model.Settings["wmode"], Is.EqualTo("fullscreen"));
            Assert.That((string)model.Settings["res"], Is.EqualTo(GameDisplay.Native));
            Assert.That(model.QuestsClaimed, Does.Contain(2));
        }

        [Test] public void RewardsAreCreditedOnceAndIncompleteQuestsCannotBeClaimed()
        {
            Assert.That(services.Rewards.ClaimDay(), Is.True);
            Assert.That(services.Rewards.ClaimDay(), Is.False);
            Assert.That(services.Rewards.ClaimQuest(0), Is.False);
            Assert.That(services.Rewards.ClaimQuest(1), Is.True);
            Assert.That(services.Rewards.ClaimQuest(1), Is.False);
            Assert.That(services.Rewards.ClaimQuest(2), Is.False);
            Assert.That(model.Chips, Is.EqualTo(131900));
        }

        [Test] public void FriendSearchNormalizesCodesAndTurkishNames()
        {
            Assert.That(services.Friends.GetFriends(false, "#AY 2187").Count, Is.EqualTo(1));
            Assert.That(services.Friends.GetFriends(false, "AYŞE").Count, Is.EqualTo(1));
            Assert.That((string)services.Friends.Search(" mk3321 ")["code"], Is.EqualTo("MK-3321"));
            Assert.That((bool)services.Friends.Search("#AY-2187")["isFriend"], Is.True);
            Assert.That(services.Friends.SendRequest("MK3321"), Is.True);
            Assert.That(services.Friends.SendRequest("#mk-3321"), Is.False);
            Assert.That(services.Friends.SendRequest("XX-9999"), Is.False);
        }

        [Test] public void AcceptedRequestBecomesFriendAndCannotBeHandledTwice()
        {
            Assert.That(services.Friends.Accept("kd"), Is.True);
            Assert.That(services.Friends.GetRequests().Count, Is.EqualTo(1));
            Assert.That(services.Friends.GetFriends().Count, Is.EqualTo(7));
            Assert.That(services.Friends.Decline("kd"), Is.False);
            Assert.That((bool)services.Friends.Search("KD1290")["isFriend"], Is.True);
        }

        [Test] public void ProfileRejectsInvalidNameColorAndLockedFrame()
        {
            Assert.That(services.Profile.Save("ab", "#FF8A5B", "", 0), Is.False);
            Assert.That(services.Profile.Save("Valid Name", "red", "", 0), Is.False);
            Assert.That(services.Profile.Save("Valid Name", "#FF8A5B", "", 2), Is.False);
            Assert.That(services.Profile.Save("  İpek Işık  ", "#3D7BFF", "Merhaba", 1), Is.True);
            Assert.That((string)model.Profile["name"], Is.EqualTo("İpek Işık"));
            Assert.That(ProfileValidation.Initials("ipek ışık"), Is.EqualTo("İI"));
            Assert.That(ProfileValidation.Initials(""), Is.EqualTo("?"));
        }

        [Test] public void SettingsDraftDoesNotMutateSavedStateAndInvalidValuesAreRejected()
        {
            JObject draft = services.Settings.Load();
            draft["master"] = 11;
            Assert.That((int)model.Settings["master"], Is.EqualTo(80));
            draft["warn"] = -1;
            Assert.That(services.Settings.Save(draft), Is.False);
            draft["warn"] = 10;
            Assert.That(services.Settings.Save(draft), Is.True);
            Assert.That((int)NewModel().Settings["master"], Is.EqualTo(11));
        }

        [Test] public void PurchaseCreditsMockCurrencyAndChecksSkinAffordability()
        {
            Assert.That(services.Economy.Purchase("skin-0"), Is.False);
            Assert.That(services.Economy.Purchase("gems-2"), Is.True);
            Assert.That(model.Gems, Is.EqualTo(1540));
            Assert.That(services.Economy.Purchase("skin-0"), Is.True);
            Assert.That(model.Gems, Is.EqualTo(740));
            Assert.That(services.Economy.Purchase("skin-0"), Is.False);
            Assert.That(services.Economy.Purchase("unknown"), Is.False);
        }

        [Test] public void PersistenceRestoresValidatedStateAndIgnoresCorruption()
        {
            services.Settings.SetPlanet("Mars");
            services.Settings.SetLanguage("en");
            model.SelectedMode = 4;
            Assert.That(model.Persist(), Is.True);
            var restored = NewModel();
            Assert.That(restored.Planet, Is.EqualTo("Mars"));
            Assert.That(restored.SelectedMode, Is.EqualTo(4));
            Assert.That((string)restored.Settings["lang"], Is.EqualTo("en"));
            File.WriteAllText(savePath, "{not-json");
            Assert.That(NewModel().Planet, Is.EqualTo("Satürn"));
            File.WriteAllText(savePath, "{\"Settings\":{\"warn\":-10,\"master\":999},\"Profile\":{\"name\":\"x\",\"frame\":2},\"SelectedMode\":99,\"Planet\":\"Pluto\"}");
            restored = NewModel();
            Assert.That((int)restored.Settings["warn"], Is.EqualTo(5));
            Assert.That((string)restored.Profile["name"], Is.EqualTo("Doğukan Y."));
            Assert.That(restored.SelectedMode, Is.EqualTo(0));
        }

        [Test] public void LocalizationPreservesDuplicateKeysAndContextualTranslations()
        {
            string csv = File.ReadAllText("Design/galaxy101_unity_handoff/spec/data/strings_tr_en.csv");
            var rows = LocalizationCatalog.Parse(csv);
            Assert.That(rows.Count, Is.EqualTo(194));
            Assert.That(rows.Select(x => x.Key).Distinct().Count(), Is.EqualTo(rows.Count));
            var loc = new LocalizationBridge(model);
            loc.SetLanguage("en");
            Assert.That(loc.Text("HEMEN OYNA"), Is.EqualTo("PLAY NOW"));
            Assert.That(loc.Text("SIRA", "PLACE"), Is.EqualTo("PLACE"));
            Assert.That(loc.Text("SIRA", "RANK"), Is.EqualTo("RANK"));
            Assert.That(NumberFormat.Number(125400, "tr"), Is.EqualTo("125.400"));
            Assert.That(NumberFormat.Number(125400, "en"), Is.EqualTo("125,400"));
            Assert.That(NumberFormat.Percent(41, "tr"), Is.EqualTo("%41"));
            Assert.That(NumberFormat.Money(249.99m, "tr"), Is.EqualTo("₺249,99"));
        }

        [Test] public void OversizedPersistedIntegersCannotCrashStartup()
        {
            File.WriteAllText(savePath, "{\"Settings\":{\"master\":9999999999999999999999999999},\"Profile\":{\"name\":\"Valid Name\",\"color\":\"#FF8A5B\",\"status\":\"\",\"frame\":9999999999999999999999999999},\"SelectedMode\":9999999999999999999999999999}");
            Assert.DoesNotThrow(() => model = NewModel());
            Assert.That((int)model.Settings["master"], Is.EqualTo(80));
            Assert.That((int)model.Profile["frame"], Is.EqualTo(0));
            Assert.That(model.SelectedMode, Is.EqualTo(0));
        }

        [Test] public void PersistenceFailureKeepsPreviouslySavedProfileAndSettings()
        {
            string directory = Path.Combine(Path.GetTempPath(), "galaxy101-blocked-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var blocked = new MenuModel(model.Content.ToString(), model.Themes.ToString(), directory);
                var blockedServices = new MenuServices(blocked);
                Assert.That(blockedServices.Profile.Save("Valid Name", "#3D7BFF", "", 1), Is.False);
                Assert.That((string)blocked.Profile["name"], Is.EqualTo("Doğukan Y."));
                Assert.That(blockedServices.Settings.SetPlanet("Mars"), Is.False);
                Assert.That(blocked.Planet, Is.EqualTo("Satürn"));
                Assert.That(blocked.LastPersistenceError, Is.Not.Empty);
            }
            finally { Directory.Delete(directory); }
        }

        [Test] public void LeaderboardUsesReferenceWeeklyRoundingAndEditedProfile()
        {
            Assert.That((long)services.Leaderboard.Get("weekly")["podium"][0]["points"], Is.EqualTo(1542));
            Assert.That((int)services.Leaderboard.Get("season")["me"]["rank"], Is.EqualTo(1204));
            Assert.That(services.Profile.Save("İpek Işık", "#3D7BFF", "", 1), Is.True);
            Assert.That((string)services.Leaderboard.Get("friends")["podium"][2]["name"], Is.EqualTo("İpek Işık"));
        }

        [Test] public void EconomyAndRewardsRejectOverflowWithoutMutatingBalances()
        {
            model.Chips = long.MaxValue;
            model.Gems = long.MaxValue;
            Assert.That(services.Rewards.ClaimDay(), Is.False);
            Assert.That(model.DayClaimed, Is.False);
            Assert.That(services.Rewards.ClaimQuest(1), Is.False);
            Assert.That(model.QuestsClaimed.Contains(1), Is.False);
            Assert.That(services.Economy.Purchase("deal"), Is.False);
            Assert.That(model.Chips, Is.EqualTo(long.MaxValue));
        }

        [Test] public void CsvQuotedCommasQuotesAndNewlinesArePreserved()
        {
            var rows = LocalizationCatalog.Parse("\"key\",\"tr\",\"en\"\r\n\"same\",\"Bir, iki\",\"One, two\"\r\n\"same\",\"Bir\n\\iki\",\"A \"\"quote\"\"\"\r\n");
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0].Tr, Is.EqualTo("Bir, iki"));
            Assert.That(rows[1].En, Is.EqualTo("A \"quote\""));
            Assert.That(rows[0].Key, Is.Not.EqualTo(rows[1].Key));
            Assert.Throws<FormatException>(() => LocalizationCatalog.Parse("key,tr,en\nx,\"unfinished,en"));
        }

        [Test] public void DefaultResetReturnsDraftAndImmediateLanguageDoesPersist()
        {
            Assert.That(services.Settings.SetLanguage("en"), Is.True);
            var defaults = services.Settings.ResetToDefaults();
            Assert.That((string)defaults["lang"], Is.EqualTo("tr"));
            Assert.That((string)model.Settings["lang"], Is.EqualTo("en"));
            Assert.That((string)NewModel().Settings["lang"], Is.EqualTo("en"));
        }

        [Test] public void EveryOfferProvidesPresentationFieldsAcrossAllTabs()
        {
            for (int tab = 0; tab < 3; tab++)
                foreach (JObject offer in services.Economy.GetOffers(tab))
                {
                    Assert.That((int)offer["bonus"], Is.GreaterThanOrEqualTo(0));
                    Assert.That((float)offer["scale"], Is.GreaterThan(0));
                    Assert.That((bool)offer["pop"], Is.TypeOf<bool>());
                }
        }
    }
}

