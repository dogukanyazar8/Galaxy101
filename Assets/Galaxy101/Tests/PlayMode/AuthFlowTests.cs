using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Online;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// Açılış ve giriş (spec/18, S01–S05) against a running game server (GALAXY101_SERVER, default localhost:5173).
    /// Without a server the tests are skipped (Inconclusive), never faked.
    /// </summary>
    public sealed class AuthFlowTests
    {
        private GameObject owner;
        private string statePath;

        [UnitySetUp]
        public IEnumerator RequireServer()
        {
            using (var probe = UnityWebRequest.Get(GalaxyApi.ResolveServer() + "/health"))
            {
                probe.timeout = 3; yield return probe.SendWebRequest();
                if (probe.responseCode != 200) Assert.Inconclusive("No game server at " + GalaxyApi.ResolveServer() + " (start Server/ locally to run these).");
            }
            GalaxyOnline.Forget();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MainMenuController.AuthOnStart = null;
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (GalaxyOnline.Instance != null) UnityEngine.Object.Destroy(GalaxyOnline.Instance.gameObject);
            GalaxyOnline.Forget();
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private MainMenuController Menu()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-auth-" + Guid.NewGuid().ToString("N") + ".json");
            MainMenuController.AuthOnStart = true;
            owner = new GameObject("Menu"); owner.SetActive(false);
            owner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(statePath);
            return menu;
        }

        private static void Click(VisualElement root, string name)
        {
            var e = root.Q<VisualElement>(name); Assert.That(e, Is.Not.Null, "element " + name);
            using (var evt = ClickEvent.GetPooled()) { evt.target = e; e.SendEvent(evt); }
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        private static string Dir => Environment.GetEnvironmentVariable("GALAXY_AUTH_CAPTURE_DIR");

        [UnityTest]
        public IEnumerator SplashThenGuestSignInLandsInTheMenuWithTheGuestBalance()
        {
            var menu = Menu(); menu.Auth.SplashSeconds = .8f;
            var root = menu.View.Root;
            Assert.That(menu.Auth.Screen, Is.EqualTo("splash"));
            Assert.That(root.Q<Label>("splash-percent"), Is.Not.Null);
            if (!string.IsNullOrEmpty(Dir)) yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(Dir, "S01_splash.png"));
            yield return Until(() => menu.Auth.Screen == "choice", 10, "the choice panel");
            Assert.That(root.Q<VisualElement>("auth-guest"), Is.Not.Null);
            if (UiDump.Dir != null) { yield return new WaitForSeconds(1f); yield return UiDump.Capture(root, "S02_choice"); }
            if (!string.IsNullOrEmpty(Dir)) yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(Dir, "S02_choice.png"));

            Click(root, "auth-guest"); yield return null;
            Assert.That(menu.Auth.Screen, Is.EqualTo("guest"));
            root.Q<TextField>("auth-nick").value = "Ab"; Click(root, "auth-submit"); yield return null;
            Assert.That(root.Q<Label>("auth-hint-nick"), Is.Not.Null, "3–16 characters, shown as the field's hint");
            root.Q<TextField>("auth-nick").value = "TestMisafir" + UnityEngine.Random.Range(10, 99);
            Click(root, "auth-color-8B5CF6"); yield return null;
            if (UiDump.Dir != null) { yield return UiDump.Capture(root, "S05_guest"); }
            if (!string.IsNullOrEmpty(Dir)) yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(Dir, "S05_guest.png"));
            Click(root, "auth-submit");
            yield return Until(() => !menu.Auth.Visible, 15, "signing in as a guest");
            Assert.That(GalaxyOnline.Instance.SignedIn, Is.True); Assert.That(GalaxyOnline.Instance.Guest, Is.True);
            Assert.That(GalaxyOnline.Instance.Wallet.Chips, Is.EqualTo(10000));
            Assert.That(menu.Model.Chips, Is.EqualTo(10000), "the menu shows the server balance");
            Assert.That(PlayerPrefs.HasKey(GalaxyOnline.SessionKey), Is.True, "a guest session is kept on this device (\"İlerlemen bu cihazda saklanır\")");
            yield return Until(() => menu.View.LastFlash != null, 2, "the welcome flash (500 ms after the menu)");
            Assert.That(menu.View.LastFlash, Does.StartWith("Hoş geldin, TestMisafir").And.Contain("10.000 çip"), "loginAs(): gold greeting with the welcome chips");
            Assert.That(menu.View.Root.Q<VisualElement>("shell-flash"), Is.Not.Null);
            yield return Until(() => GalaxyOnline.Instance.Realtime.Connected, 5, "the realtime socket");
        }

        [UnityTest]
        public IEnumerator RegisterThenLoginWithWrongPasswordsAndRememberedResume()
        {
            var menu = Menu(); menu.Auth.SplashSeconds = .3f;
            var root = menu.View.Root;
            yield return Until(() => menu.Auth.Screen == "choice", 10, "the choice panel");
            Click(root, "auth-register"); yield return null;
            string email = "test" + Guid.NewGuid().ToString("N").Substring(0, 10) + "@example.com";
            root.Q<TextField>("auth-name").value = "Kayitli" + UnityEngine.Random.Range(10, 99);
            root.Q<TextField>("auth-email").value = email;
            root.Q<TextField>("auth-password").value = "gizli123";
            root.Q<TextField>("auth-password2").value = "gizli124";
            Click(root, "auth-submit"); yield return null;
            Assert.That(root.Q<Label>("auth-hint-pass2").text, Does.Contain("aynı değil").Or.Contain("match"));
            root.Q<TextField>("auth-password2").value = "gizli123";
            Click(root, "auth-submit"); yield return null;
            Assert.That(root.Q<Label>("auth-error").text, Does.Contain("koşulları").Or.Contain("terms"));
            Click(root, "auth-terms"); yield return null;
            Assert.That(root.Q<Label>("auth-error"), Is.Null, "setF(): ticking the terms clears the error");
            if (UiDump.Dir != null) { yield return UiDump.Capture(root, "S04_register"); }
            if (!string.IsNullOrEmpty(Dir)) yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(Dir, "S04_register.png"));
            Click(root, "auth-submit");
            yield return Until(() => !menu.Auth.Visible, 15, "registering");
            Assert.That(GalaxyOnline.Instance.Wallet.Chips, Is.EqualTo(25000));
            Assert.That(PlayerPrefs.HasKey(GalaxyOnline.SessionKey), Is.True, "a registered account is remembered");

            // Sign out, then the login screen: a wrong password tells the tries left.
            var signOut = GalaxyOnline.Instance.SignOutAsync(); yield return Until(() => signOut.IsCompleted, 10, "signing out");
            menu.ShowAuth(); menu.Auth.SplashSeconds = .2f;
            yield return Until(() => menu.Auth.Screen == "choice", 10, "the choice panel again");
            Click(root, "auth-login"); yield return null;
            if (UiDump.Dir != null) { yield return UiDump.Capture(root, "S03_login"); }
            root.Q<TextField>("auth-email").value = email; root.Q<TextField>("auth-password").value = "yanlis99";
            Click(root, "auth-submit");
            yield return Until(() => root.Q<Label>("auth-error") != null, 10, "the wrong-password message");
            Assert.That(root.Q<Label>("auth-error").text, Does.Contain("2"));
            if (!string.IsNullOrEmpty(Dir)) yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(Dir, "S03_login_error.png"));
            root.Q<TextField>("auth-password").value = "gizli123";
            Click(root, "auth-submit");
            yield return Until(() => !menu.Auth.Visible, 15, "logging in");
            Assert.That(GalaxyOnline.Instance.Profile.Name, Does.StartWith("Kayitli"));

            // A new run with the remembered account skips the panels.
            UnityEngine.Object.Destroy(GalaxyOnline.Instance.gameObject); yield return null;
            UnityEngine.Object.Destroy(owner); yield return null;
            var again = Menu(); again.Auth.SplashSeconds = .2f;
            yield return Until(() => !again.Auth.Visible, 15, "resuming the remembered account");
            Assert.That(GalaxyOnline.Instance.Profile.Name, Does.StartWith("Kayitli"));
        }
    }
}
