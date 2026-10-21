using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class NativePanelTests
    {
        private MenuModel model;
        private MenuView view;
        private LocalizationBridge localization;
        private string save;
        private GameObject host;
        private PanelSettings panelSettings;
        private RenderTexture target;

        [SetUp]
        public void SetUp()
        {
            save = Path.Combine(Path.GetTempPath(), "galaxy-panel-" + Guid.NewGuid().ToString("N") + ".json");
            model = new MenuModel(File.ReadAllText("Design/galaxy101_unity_handoff/spec/data/content.json"), File.ReadAllText("Design/galaxy101_unity_handoff/spec/data/themes.json"), save);
            localization = new LocalizationBridge(model);
            target = new RenderTexture(1280, 720, 0);
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.targetTexture = target;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(1280, 720);
            host = new GameObject("Native panel input test", typeof(UIDocument));
            var document = host.GetComponent<UIDocument>(); document.panelSettings = panelSettings;
            view = new MenuView(document.rootVisualElement, model, new MenuServices(model), localization);
            view.BuildMain();
            Assert.That(view.Root.panel, Is.Not.Null, "Input tests require an actual native UI Toolkit panel.");
        }

        [TearDown]
        public void TearDown()
        {
            view?.Dispose(); localization?.Dispose();
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (panelSettings != null) UnityEngine.Object.DestroyImmediate(panelSettings);
            if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            if (File.Exists(save)) File.Delete(save);
        }

        [Test]
        public void ProfileEditingKeepsDraftOutOfSavedModelAndRejectsInvalidName()
        {
            var controller = new EditProfileController(); controller.OnOpen(view);
            var body = controller.Build(view);
            view.Root.Add(body);
            var name = body.Q<TextField>("profile-name");
            name.value = "ab";
            Assert.That(body.Q<Button>("profile-save").enabledSelf, Is.False);
            Assert.That(body.Q<Label>("profile-name-hint").text, Does.StartWith("3–16"));
            name.value = "İpek_32";
            Assert.That(body.Q<Button>("profile-save").enabledSelf, Is.True);
            Assert.That((string)model.Profile["name"], Is.EqualTo("Doğukan Y."));
            Assert.That(body.Q<VisualElement>("profile-colors").childCount, Is.EqualTo(8));
            Assert.That(body.Q<Button>("draft-frame-2").enabledSelf, Is.False);
        }

        [TestCase("tr", "SİL")]
        [TestCase("en", "DELETE")]
        public void DeleteButtonRequiresLocalizedConfirmation(string language, string word)
        {
            view.Services.Settings.SetLanguage(language);
            var body = new ConfirmController { Kind = "delete" }.Build(view);
            view.Root.Add(body);
            var button = body.Q<Button>("confirm-account-action");
            var input = body.Q<TextField>("delete-confirmation");
            Assert.That(button.enabledSelf, Is.False);
            input.value = word + "!";
            Assert.That(button.enabledSelf, Is.False);
            input.value = " " + word + " ";
            Assert.That(button.enabledSelf, Is.True);
        }

        [Test]
        public void FriendFilterUpdatesOnlyListAndRetainsInputInstance()
        {
            var body = new FriendsController().Build(view);
            view.Root.Add(body);
            var input = body.Q<TextField>("friend-query");
            input.value = "#AY-2187";
            Assert.That(body.Q<TextField>("friend-query"), Is.SameAs(input));
            var names = body.Q<ScrollView>("friend-list").Query<Label>().ToList().Select(label => label.text).ToArray();
            Assert.That(names, Does.Contain("Ayşe Y."));
            Assert.That(names, Does.Not.Contain("Burak C."));
        }

        [Test]
        public void PodiumKeepsSecondFirstThirdVisualOrder()
        {
            var body = new LeaderboardController().Build(view);
            var podium = body.Q<VisualElement>("podium");
            var names = podium.Query<Label>().ToList().Select(label => label.text).ToArray();
            Assert.That(Array.IndexOf(names, "Okeyci_34"), Is.LessThan(Array.IndexOf(names, "Selin A.")));
            Assert.That(Array.IndexOf(names, "Selin A."), Is.LessThan(Array.IndexOf(names, "Hakan T.")));
        }

        [Test]
        public void ShareOffersExactlyFiveNativeTargets()
        {
            var body = new ShareController().Build(view);
            Assert.That(body.Q<VisualElement>("share-targets").childCount, Is.EqualTo(5));
        }

        [TestCase("shop", 3)]
        [TestCase("friends", 3)]
        [TestCase("leader", 3)]
        [TestCase("daily", 1)]
        [TestCase("rules", 6)]
        [TestCase("me", 3)]
        [TestCase("settings", 5)]
        public void EveryNativePanelTabBuildsInBothLanguages(string key, int count)
        {
            foreach (var language in new[] { "tr", "en" })
            {
                view.Services.Settings.SetLanguage(language);
                for (var tab = 0; tab < count; tab++)
                {
                    view.OpenPanel(key, tab);
                    VisualElement body = null;
                    Assert.DoesNotThrow(() => body = BuildPanel(key), key + ":" + tab + ":" + language);
                    Assert.That(body, Is.Not.Null);
                    view.Root.Add(body);
                    Assert.That(body.panel, Is.SameAs(view.Root.panel));
                    body.RemoveFromHierarchy();
                }
                view.CloseTop();
            }
        }

        private VisualElement BuildPanel(string key)
        {
            switch (key)
            {
                case "shop": return new ShopController().Build(view);
                case "friends": return new FriendsController().Build(view);
                case "leader": return new LeaderboardController().Build(view);
                case "daily": return new DailyRewardController().Build(view);
                case "rules": return new RulesController().Build(view);
                case "me": return new ProfileController().Build(view);
                case "settings": return new SettingsController().Build(view);
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }
    }
}
