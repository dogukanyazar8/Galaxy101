using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// Dereceli (spec/27, S16; G101-Dereceli.dc.html): the ladder arithmetic, the home against the S16 reference boxes, and the
    /// search → found → accept → start, invite, decline / timeout and result flows against a scripted backend.
    /// </summary>
    public sealed class RankedTests
    {
        private GameObject owner;
        private string statePath;
        private FakeRanked fake;

        private sealed class FakeRanked : IRankedBackend
        {
            public RankedProfile Profile { get; set; } = new RankedProfile { Idx = 7, Lp = 72, Peak = 7, Season = 3, DaysLeft = 18 };
            public string MeId => "me";
            public List<RankedPlayer> List = new List<RankedPlayer>
            {
                new RankedPlayer { Id = "ay", Name = "Ayşe Y.", Color = "#3D7BFF", Idx = 6 }, new RankedPlayer { Id = "sa", Name = "Selin A.", Color = "#FF6B6B", Idx = 13 },
                new RankedPlayer { Id = "ht", Name = "Hakan T.", Color = "#22B8CF", Idx = 7, Online = false }
            };
            public IReadOnlyList<RankedPlayer> Friends => List;
            public RankedSearch Queue { get; set; }
            public RankedFound Pending { get; set; }
            public int Refreshes;
            public void Refresh() => Refreshes++;
            public string QueueAnswer;
            public string QueuedMode; public List<string> QueuedParty; public int Cancels; public readonly List<bool> Answers = new List<bool>(); public readonly List<string> Invited = new List<string>();
            public event Action Changed;
            public event Action<string, bool> PartyAnswered;
            public event Action<RankedSearch> Searching;
            public event Action<RankedFound> Found;
            public event Action<IReadOnlyList<bool>> Accepted;
            public event Action<string> Cancelled;
            public event Action<int> Ready;
            public event Action<RankedStart> Started;
            public event Action<RankedResult> RankChanged;
            public Task<string> InviteAsync(RankedPlayer friend) { Invited.Add(friend.Id); return Task.FromResult<string>(null); }
            public Task<string> QueueAsync(string mode, IReadOnlyList<string> party) { QueuedMode = mode; QueuedParty = party.ToList(); return Task.FromResult(QueueAnswer); }
            public Task CancelAsync() { Cancels++; return Task.CompletedTask; }
            public Task<string> AcceptAsync(string matchId, bool accept) { Answers.Add(accept); return Task.FromResult<string>(null); }
            public void Answer(string id, bool joined) => PartyAnswered?.Invoke(id, joined);
            public void Search(RankedSearch s) => Searching?.Invoke(s);
            public void Find(RankedFound f) => Found?.Invoke(f);
            public void Accept(params bool[] a) => Accepted?.Invoke(a);
            public void Cancel(string reason) => Cancelled?.Invoke(reason);
            public void Load() => Ready?.Invoke(1800);
            public void Start(RankedStart s) => Started?.Invoke(s);
            public void Result(RankedResult r) => RankChanged?.Invoke(r);
            public void Touch() => Changed?.Invoke();
        }

        [SetUp]
        public void SetUp()
        {
            fake = new FakeRanked();
            MainMenuController.RankedBackend = () => fake; MainMenuController.OpenMatchTable = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MainMenuController.RankedBackend = null; MainMenuController.AuthOnStart = null; MainMenuController.OpenMatchTable = true; RankedScreen.Pending = null;
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (Galaxy101.Online.GalaxyOnline.Instance != null) UnityEngine.Object.Destroy(Galaxy101.Online.GalaxyOnline.Instance.gameObject);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private MainMenuController Menu()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-ranked-" + Guid.NewGuid().ToString("N") + ".json");
            MainMenuController.AuthOnStart = false;
            owner = new GameObject("Menu"); owner.SetActive(false);
            owner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(statePath);
            menu.Model.Profile["name"] = "Doğukan Y."; menu.Model.Profile["color"] = "#FF8A5B"; menu.Model.Chips = 10000;
            return menu;
        }

        private static void Click(VisualElement root, string name)
        {
            var e = root.Q<VisualElement>(name); Assert.That(e, Is.Not.Null, "element " + name);
            using (var evt = ClickEvent.GetPooled()) { evt.target = e; e.SendEvent(evt); }
        }

        private static string TextOf(VisualElement root, string name)
        {
            var label = root.Q<Label>(name); Assert.That(label, Is.Not.Null, "label " + name);
            return System.Text.RegularExpressions.Regex.Replace(label.text ?? "", "<[^>]+>", "");
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        /// <summary>An element's box in the menu canvas's 1280×720 design units (G101Dump).</summary>
        private static Rect Box(VisualElement root, string name)
        {
            var e = root.Q<VisualElement>(name); Assert.That(e, Is.Not.Null, "element " + name);
            var canvas = root.Q<VisualElement>("galaxy-canvas") ?? root; var frame = canvas.worldBound; float k = 1280f / Mathf.Max(1, frame.width);
            var b = e.worldBound; return new Rect((b.x - frame.x) * k, (b.y - frame.y) * k, b.width * k, b.height * k);
        }

        [Test]
        public void LadderFollowsTheRankScript()
        {
            Assert.That(RankedLadder.Label(7, "tr"), Is.EqualTo("Altın II"));
            Assert.That(RankedLadder.Label(0, "en"), Is.EqualTo("Bronze III"));
            Assert.That(RankedLadder.Label(14, "tr"), Is.EqualTo("Usta I"));
            Assert.That(RankedLadder.Label(15, "tr"), Is.EqualTo("Galaksi"));
            Assert.That(RankedLadder.Code(7), Is.EqualTo("g2")); Assert.That(RankedLadder.Code(15), Is.EqualTo("gx"));
            for (int i = 0; i <= 15; i++) Assert.That(RankedLadder.FromCode(RankedLadder.Code(i)), Is.EqualTo(i));
            Assert.That(RankedLadder.Deltas(8), Is.EqualTo(new[] { 30, 10, -10, -20 }));
            Assert.That(RankedLadder.Deltas(9), Is.EqualTo(new[] { 25, 5, -12, -22 }));
            Assert.That(RankedLadder.Leagues.Select(l => l.RewardChips), Is.EqualTo(new long[] { 5000, 15000, 40000, 100000, 250000, 1000000 }));
            Assert.That(RankedLadder.Emblem("gold", 2, 150), Is.Not.Null, "lane D's gold_II_160@2x");
            Assert.That(RankedLadder.Emblem("galaxy", 0, 24), Is.Not.Null);
        }

        [Test]
        public void LpRulesMatchApplyRanked()
        {
            // Altın II 72 LP, 1st: +30 → Altın I 2 LP (tier).
            var p = new RankedProfile { Idx = 7, Lp = 72, Peak = 7 };
            var r = RankedRules.Apply(p, 1, "solo");
            Assert.That((r.Event, r.ToIdx, r.ToLp, r.Delta), Is.EqualTo(("tier", 8, 2, 30)));
            Assert.That((p.Games, p.Top, p.Peak, p.History.Count), Is.EqualTo((1, 1, 8, 1)));
            // Altın I 90 LP, 1st: Elmas III, 3 protected matches (D-30).
            p = new RankedProfile { Idx = 8, Lp = 90 }; r = RankedRules.Apply(p, 1, "solo");
            Assert.That((r.Event, r.ToIdx, r.ToLp, p.Protected), Is.EqualTo(("league", 9, 20, 3)));
            // Elmas III 5 LP with protection, 4th: stays at 0 and spends one protected match.
            r = RankedRules.Apply(new RankedProfile { Idx = 9, Lp = 5, Protected = 2 }, 4, "solo");
            Assert.That((r.Event, r.ToIdx, r.ToLp, r.Protected), Is.EqualTo(("protected", 9, 0, 1)));
            // Without protection: down to Altın I (league).
            r = RankedRules.Apply(new RankedProfile { Idx = 9, Lp = 5 }, 4, "solo");
            Assert.That((r.Event, r.ToIdx, r.ToLp), Is.EqualTo(("downLeague", 8, 83)));
            // Usta I → Galaksi.
            r = RankedRules.Apply(new RankedProfile { Idx = 14, Lp = 90 }, 1, "solo");
            Assert.That((r.Event, r.ToIdx, r.ToLp), Is.EqualTo(("galaxy", 15, 15)));
            // Bronz III never goes below 0.
            r = RankedRules.Apply(new RankedProfile { Idx = 0, Lp = 5 }, 4, "solo");
            Assert.That((r.Event, r.ToIdx, r.ToLp), Is.EqualTo(((string)null, 0, 0)));
        }

        /// <summary>HEMEN OYNA on Dereceli opens the screen; its boxes and texts sit where S16.layout.json / S16.text.json put them.</summary>
        [UnityTest]
        public IEnumerator HemenOynaOpensTheDereceliHomeAsS16()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.Model.SelectedMode = 3;
            var play = root.Q<Button>("play-now"); using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = play; play.SendEvent(evt); }
            yield return null; yield return null;
            Assert.That(menu.Ranked, Is.Not.Null); Assert.That(menu.Ranked.Visible, Is.True);
            Assert.That(root.Q<VisualElement>("main-layer").resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the menu hides under the ranked screen");
            Assert.That(fake.QueuedMode, Is.Null, "the queue starts only with MAÇ ARA");
            yield return new WaitForSeconds(.6f);

            Assert.That(TextOf(root, "ranked.head.title"), Is.EqualTo("Dereceli"));
            Assert.That(TextOf(root, "ranked.head.sub"), Is.EqualTo("Sezon 3 · 18 gün kaldı · Solo / Duo / Squad"));
            Assert.That(TextOf(root, "ranked.chipsLabel"), Is.EqualTo("10.000"));
            Assert.That(TextOf(root, "ranked.text.seasonTag"), Is.EqualTo("SEZON 3 · DERECELİ"));
            Assert.That(TextOf(root, "ranked.me.label"), Is.EqualTo("Altın II"));
            Assert.That(TextOf(root, "ranked.me.lpText"), Is.EqualTo("72 / 100 LP"));
            Assert.That(TextOf(root, "ranked.me.next"), Is.EqualTo("Altın I'e 28 LP"));
            Assert.That(TextOf(root, "ranked.s.v[2]"), Is.EqualTo("—"));
            Assert.That(TextOf(root, "ranked.z.name[0]"), Is.EqualTo("Doğukan Y."));
            Assert.That(root.Q<VisualElement>("ranked.z.ini[0]").Q<Label>().text, Is.EqualTo("DY"));
            Assert.That(TextOf(root, "ranked.range.label"), Is.EqualTo("Eşleşme aralığı · Gümüş – Elmas (±1)"));
            Assert.That(TextOf(root, "ranked.range.short[1]"), Is.EqualTo("Gümüş – Elmas"));
            Assert.That(TextOf(root, "ranked.cta.label"), Is.EqualTo("MAÇ ARA"));
            Assert.That(TextOf(root, "ranked.cta.sub"), Is.EqualTo("Dereceli · 101 · 5 el"));

            // S16.layout.json boxes (1280×720 units).
            void Near(string name, float x, float y, float w, float h, float tolerance = 1.5f)
            {
                var b = Box(root, name);
                Assert.That(b.x, Is.EqualTo(x).Within(tolerance), name + " x"); Assert.That(b.y, Is.EqualTo(y).Within(tolerance), name + " y");
                if (w > 0) Assert.That(b.width, Is.EqualTo(w).Within(tolerance), name + " w");
                if (h > 0) Assert.That(b.height, Is.EqualTo(h).Within(tolerance), name + " h");
            }
            Near("ranked.back", 28, 20, 46, 46);
            Near("ranked.head.title", 154, 17.2f, 0, 28.6f);
            Near("ranked.v.pick[0]", 1054.31f, 24.5f, 93.14f, 37, 3);
            Near("ranked.text.seasonTag", 145.78f, 118, 0, 18, 4);
            Near("ranked.me.label", 155.13f, 306, 0, 33, 4);
            Near("ranked.me.lpText", 50, 369, 0, 21);
            Near("ranked.s.v[0]", 93, 567, 0, 24, 3);
            Near("ranked.toProgress", 50, 631, 316, 43);
            Near("ranked.m.pick[0]", 408, 92, 272, 112);
            Near("ranked.m.pick[1]", 694, 96, 272, 112);
            Near("ranked.m.pick[2]", 980, 96, 272, 112);
            Near("ranked.m.name[0]", 424, 144.92f, 0, 25.2f);
            Near("ranked.table.title", 428, 246, 0, 23);
            Near("ranked.z.tag[0]", 438, 296, 0, 16, 2);
            Near("ranked.z.ini[0]", 492, 381.91f, 64, 64);
            Near("ranked.z.oppLabel[1]", 705.55f, 455.5f, 0, 19, 3);
            Near("ranked.range.label", 470, 625, 0, 18);
            Near("ranked.cta.go", 932, 612, 300, 64);
            Near("ranked.cta.label", 1019.48f, 621, 0, 24, 4);

            Click(root, "ranked.back"); yield return null;
            Assert.That(menu.Ranked.Visible, Is.False);
            Assert.That(root.Q<VisualElement>("main-layer").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        }

        [UnityTest]
        public IEnumerator SoloSearchFoundAcceptAndStart()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenRanked(); yield return null;
            RankedStart started = null; menu.Ranked.Matched += s => started = s;
            Click(root, "ranked.cta.go"); yield return null;
            Assert.That(fake.QueuedMode, Is.EqualTo("solo")); Assert.That(fake.QueuedParty, Is.Empty);
            Assert.That(menu.Ranked.Searching, Is.True);
            Assert.That(root.Q<VisualElement>("ranked.cancel"), Is.Not.Null); Assert.That(root.Q<VisualElement>("ranked.cta.go"), Is.Null);
            Assert.That(TextOf(root, "ranked.clock"), Does.EndWith("0/3 bulundu"));

            var kerem = new RankedPlayer { Id = "k", Name = "Kerem D.", Color = "#5B8CFF", Idx = 8 };
            fake.Search(new RankedSearch { Range = 1, Opponents = new List<RankedPlayer> { kerem } }); yield return null;
            Assert.That(TextOf(root, "ranked.z.name[1]"), Is.EqualTo("Kerem D."));
            fake.Search(new RankedSearch { Range = 2, Opponents = new List<RankedPlayer> { kerem } }); yield return null;
            Assert.That(TextOf(root, "ranked.range.label"), Is.EqualTo("Eşleşme aralığı · Bronz – Usta (±2)"));
            Assert.That(TextOf(root, "ranked.range.hint"), Is.EqualTo("30 sn geçti · arama ±2 lige genişledi"));

            var players = new List<RankedPlayer> { new RankedPlayer { Id = "me", Name = "Doğukan Y.", Color = "#FF8A5B", Idx = 7, Me = true }, kerem,
                new RankedPlayer { Id = "z", Name = "Zeynep A.", Color = "#E85D9A", Idx = 6 }, new RankedPlayer { Id = "m", Name = "Mert Ç.", Color = "#16B6A8", Idx = 7 } };
            fake.Find(new RankedFound { MatchId = "m1", Players = players, Accepted = new List<bool> { false, false, false, false } }); yield return null;
            Assert.That(menu.Ranked.Searching, Is.False);
            Assert.That(TextOf(root, "ranked.found.kicker"), Is.EqualTo("MAÇ BULUNDU"));
            Assert.That(TextOf(root, "ranked.found.acceptLabel"), Is.EqualTo("Kabul et · 10"));
            Assert.That(TextOf(root, "ranked.p.team[0]"), Is.EqualTo("SEN"));
            Click(root, "ranked.accept"); yield return null;
            Assert.That(fake.Answers, Is.EqualTo(new[] { true }));
            Assert.That(root.Q<VisualElement>("ranked.accept"), Is.Null, "accepted: the waiting line replaces the buttons");
            fake.Accept(true, true, true, false); yield return null;
            fake.Load(); yield return null;
            fake.Start(new RankedStart { Mode = "solo", Players = players }); yield return null;
            Assert.That(started, Is.Not.Null); Assert.That(menu.Ranked.Found, Is.Null);
        }

        [UnityTest]
        public IEnumerator DeclineTimeoutAndCancelFlash()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenRanked(); yield return null;
            var players = new List<RankedPlayer> { new RankedPlayer { Id = "me", Name = "Doğukan Y.", Idx = 7, Me = true }, new RankedPlayer { Id = "a", Name = "A B", Idx = 7 },
                new RankedPlayer { Id = "b", Name = "C D", Idx = 7 }, new RankedPlayer { Id = "c", Name = "E F", Idx = 7 } };
            Click(root, "ranked.cta.go"); yield return null;
            Click(root, "ranked.cancel"); yield return null;
            Assert.That(fake.Cancels, Is.EqualTo(1)); Assert.That(menu.Ranked.LastToast, Is.EqualTo("Arama iptal edildi"));

            Click(root, "ranked.cta.go"); yield return null;
            fake.Find(new RankedFound { MatchId = "m2", Players = players, Accepted = new List<bool> { false, false, false, false } }); yield return null;
            Click(root, "ranked.decline"); yield return null;
            Assert.That(fake.Answers, Is.EqualTo(new[] { false })); Assert.That(menu.Ranked.LastToast, Is.EqualTo("Maçı reddettin"));
            Assert.That(menu.Ranked.Found, Is.Null);

            Click(root, "ranked.cta.go"); yield return null;
            fake.Find(new RankedFound { MatchId = "m3", Players = players, Accepted = new List<bool> { false, false, false, false } }); yield return null;
            fake.Cancel("timeout"); yield return null;
            Assert.That(menu.Ranked.LastToast, Is.EqualTo("Maç zamanında kabul edilmedi"));
            Assert.That(root.Q<VisualElement>("ranked.found.kicker"), Is.Null);
        }

        [UnityTest]
        public IEnumerator DuoNeedsAFriendInRange()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenRanked(); yield return null;
            Click(root, "ranked.m.pick[1]"); yield return null;
            Assert.That(menu.Ranked.Mode, Is.EqualTo("duo"));
            Assert.That(TextOf(root, "ranked.table.title"), Is.EqualTo("Duo masan"));
            Assert.That(TextOf(root, "ranked.cta.sub"), Is.EqualTo("Duo için 1 arkadaş davet et"));
            Click(root, "ranked.cta.go"); yield return null;
            Assert.That(fake.QueuedMode, Is.Null, "not ready: MAÇ ARA opens the invite");
            Assert.That(menu.Ranked.InviteOpen, Is.True);
            Assert.That(TextOf(root, "ranked.f.go[1]"), Is.EqualTo("Aralık dışı"), "Usta III is two leagues from Altın");
            Assert.That(TextOf(root, "ranked.f.go[2]"), Is.EqualTo("Çevrimdışı"));
            Click(root, "ranked.f.go[1]"); yield return null; Assert.That(fake.Invited, Is.Empty);
            Click(root, "ranked.f.go[0]"); yield return null;
            Assert.That(fake.Invited, Is.EqualTo(new[] { "ay" })); Assert.That(menu.Ranked.InviteOpen, Is.False, "the only Duo seat is taken");
            Assert.That(TextOf(root, "ranked.text.pending[1]"), Is.EqualTo("Davet bekleniyor"));
            fake.Answer("ay", true); yield return null;
            Assert.That(menu.Ranked.LastToast, Is.EqualTo("Ayşe Y. takıma katıldı"));
            Assert.That(menu.Ranked.Ready, Is.True); Assert.That(TextOf(root, "ranked.z.tag[0]"), Is.EqualTo("TAKIM"));
            Click(root, "ranked.cta.go"); yield return null;
            Assert.That(fake.QueuedMode, Is.EqualTo("duo")); Assert.That(fake.QueuedParty, Is.EqualTo(new[] { "ay" }));
        }

        [UnityTest]
        public IEnumerator ResultCardThenPromotion()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenRanked(); yield return null;
            Click(root, "ranked.v.pick[1]"); yield return null; Assert.That(menu.Ranked.View, Is.EqualTo("progress"));
            Assert.That(TextOf(root, "ranked.text.noHist"), Does.StartWith("Henüz dereceli maç oynamadın."));
            Assert.That(TextOf(root, "ranked.lpRule"), Is.EqualTo("1. +30 · 2. +10 · 3. −10 · 4. −20"));
            Assert.That(TextOf(root, "ranked.w.st[2]"), Is.EqualTo("Kazanacağın"));
            var r = RankedRules.Apply(fake.Profile, 1, "solo"); fake.Touch();
            fake.Result(r); yield return null;
            Assert.That(menu.Ranked.View, Is.EqualTo("home"), "the result shows over the home");
            Assert.That(TextOf(root, "ranked.res.kicker"), Is.EqualTo("DERECELİ · SOLO"));
            Assert.That(TextOf(root, "ranked.res.title"), Is.EqualTo("Galibiyet!"));
            Assert.That(TextOf(root, "ranked.res.d"), Is.EqualTo("+30 LP"));
            Assert.That(TextOf(root, "ranked.res.label"), Is.EqualTo("Altın I"));
            Click(root, "ranked.res.close"); yield return null;
            Assert.That(menu.Ranked.ResultStage, Is.EqualTo("promo"), "a tier up shows the promotion screen next");
            Assert.That(TextOf(root, "ranked.res.pKicker"), Is.EqualTo("KADEME ATLADIN"));
            Assert.That(TextOf(root, "ranked.res.pSub"), Is.EqualTo("Altın II → Altın I"));
            Click(root, "ranked.res.close#2"); yield return null;
            Assert.That(menu.Ranked.ResultStage, Is.Null);
        }

        [UnityTest]
        public IEnumerator OpeningOnAFoundMatchAndFallingBackToTheSearch()
        {
            // A party leader queued this player: the screen opens on the match waiting for accepts (rankedFound).
            fake.Pending = new RankedFound { MatchId = "r9", Players = new List<RankedPlayer> { new RankedPlayer { Id = "me", Name = "Doğukan Y.", Idx = 7, Me = true },
                new RankedPlayer { Id = "x", Name = "Kerem D.", Idx = 8 }, new RankedPlayer { Id = "ay", Name = "Ayşe Y.", Idx = 6 }, new RankedPlayer { Id = "y", Name = "Zeynep A.", Idx = 7 } },
                Accepted = new List<bool> { false, false, true, false } };
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenRanked(); yield return null;
            Assert.That(fake.Refreshes, Is.EqualTo(1), "the screen asks for the standing and the friends");
            Assert.That(menu.Ranked.Found, Is.Not.Null); Assert.That(TextOf(root, "ranked.found.kicker"), Is.EqualTo("MAÇ BULUNDU"));
            Click(root, "ranked.accept"); yield return null;
            // Another player timed out: the server puts the party back in the search (lane B EK-219).
            fake.Search(new RankedSearch { Mode = "duo", Range = 1 }); yield return null;
            Assert.That(menu.Ranked.Found, Is.Null); Assert.That(menu.Ranked.Searching, Is.True); Assert.That(menu.Ranked.Mode, Is.EqualTo("duo"));
            Assert.That(menu.Ranked.LastToast, Is.EqualTo("Eşleşme süresi doldu · aramaya dönülüyor"));
            Assert.That(TextOf(root, "ranked.clock"), Does.EndWith("0/2 bulundu"));
            fake.Cancel("cancel"); yield return null;
            Assert.That(menu.Ranked.LastToast, Is.EqualTo("Arama iptal edildi"), "the party leader stopped the search");
        }

        private static void Inbound(Galaxy101.Online.GalaxyRealtime realtime, string type, string json)
        {
            var field = typeof(Galaxy101.Online.GalaxyRealtime).GetField("inbox", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var inbox = (System.Collections.Concurrent.ConcurrentQueue<(string, Newtonsoft.Json.Linq.JToken)>)field.GetValue(realtime);
            inbox.Enqueue((type, Newtonsoft.Json.Linq.JToken.Parse(json.Replace('\'', '"'))));
            realtime.Pump();
        }

        /// <summary>The v4 ranked messages (protocol.json) as the screen's events: search, found (10 s), accepted, ready, matchStart, rankChange.</summary>
        [UnityTest]
        public IEnumerator OnlineBackendSpeaksTheV4RankedMessages()
        {
            var online = Galaxy101.Online.GalaxyOnline.Ensure("http://127.0.0.1:9");
            var backend = RankedOnlineBackend.For(online);
            Assert.That(RankedOnlineBackend.For(online), Is.SameAs(backend), "one backend per session");
            RankedFound found = null; IReadOnlyList<bool> accepted = null; int ready = 0, incoming = 0; RankedStart start = null; RankedResult result = null; string cancelled = null;
            backend.Incoming += () => incoming++;
            Inbound(online.Realtime, "rankedSearch", "{'mode':'duo','since':1000,'range':2,'filled':2,'at':1000}");
            Assert.That(incoming, Is.EqualTo(1), "nobody listened: the menu opens the screen");
            Assert.That(backend.Queue.Mode, Is.EqualTo("duo")); Assert.That(backend.Queue.Range, Is.EqualTo(2)); Assert.That(backend.Queue.Filled, Is.EqualTo(0), "the server's filled is this party's own seats, not opponents");
            backend.Found += f => found = f; backend.Accepted += a => accepted = a; backend.Ready += ms => ready = ms;
            backend.Started += s => start = s; backend.RankChanged += r => result = r; backend.Cancelled += r => cancelled = r;
            Inbound(online.Realtime, "rankedFound", "{'found':{'matchId':'r1','players':[{'user':{'id':'me','name':'Doğukan Y.','color':'#FF8A5B','rankIdx':7}},{'user':{'id':'b','name':'Kerem D.','rankIdx':8}},"
                + "{'user':{'id':'c','name':'Ayşe Y.','rankIdx':6}},{'user':{'id':'d','name':'Zeynep A.','rankIdx':5}}],'expiresAt':11000,'accepted':[false,false,false,false]},'at':1000}");
            Assert.That(found.MatchId, Is.EqualTo("r1")); Assert.That(found.Seconds, Is.EqualTo(10).Within(.01f)); Assert.That(found.Players[0].Me, Is.True);
            Assert.That(found.Players.Select(p => p.Idx), Is.EqualTo(new[] { 7, 8, 6, 5 })); Assert.That(backend.Pending, Is.SameAs(found)); Assert.That(backend.Queue, Is.Null);
            Inbound(online.Realtime, "rankedAccepted", "{'index':1,'accepted':[false,true,false,false],'at':1500}");
            Assert.That(accepted, Is.EqualTo(new[] { false, true, false, false }));
            Inbound(online.Realtime, "rankedReady", "{'table':{'id':'t1','ranked':true},'loadingMs':1800,'at':2000}");
            Assert.That(ready, Is.EqualTo(1800));
            Inbound(online.Realtime, "matchStart", "{'table':{'id':'other'},'countdownMs':3000}");
            Assert.That(start, Is.Null, "another table's matchStart is not the ranked match");
            Inbound(online.Realtime, "matchStart", "{'table':{'id':'t1','seats':[{'user':{'id':'b','name':'Kerem D.'}},{'user':{'id':'me','name':'Doğukan Y.'}},null,{'user':{'id':'d','name':'Zeynep A.'}}]},'countdownMs':3000}");
            Assert.That(start, Is.Not.Null); Assert.That(start.Mode, Is.EqualTo("duo")); Assert.That(start.Seat, Is.EqualTo(1)); Assert.That(start.Match, Is.InstanceOf<Galaxy101.Contracts.V4MatchStart>());
            Assert.That(backend.Pending, Is.Null);
            Inbound(online.Realtime, "rankChange", "{'before':{'idx':7,'lp':72},'after':{'idx':8,'lp':2},'delta':30,'place':1,'tierUp':true,'leagueUp':false,'down':false,'protectedLeft':0}");
            Assert.That((result.Event, result.FromIdx, result.ToIdx, result.ToLp, result.Delta, result.Mode), Is.EqualTo(("tier", 7, 8, 2, 30, "duo")));
            Inbound(online.Realtime, "rankedCancelled", "{'reason':'timeout'}");
            Assert.That(cancelled, Is.EqualTo("timeout"));
            var queued = backend.QueueAsync("solo", new List<string>()); yield return new WaitUntil(() => queued.IsCompleted);
            Assert.That(queued.Result, Is.EqualTo("offline"), "no socket: the ack says offline");
        }
    }
}
