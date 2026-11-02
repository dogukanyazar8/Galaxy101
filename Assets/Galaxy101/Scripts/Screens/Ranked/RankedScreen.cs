using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Dereceli (spec/27, S16; reference G101-Dereceli.dc.html): the ranked home (league card, Solo / Duo / Squad, "Masan" seats,
    /// eşleşme aralığı, MAÇ ARA), İlerleme (league road, season rewards, recent matches), the Duo / Squad invite, the search,
    /// the 10 s accept ("Maç bulundu"), "Masa hazırlanıyor" and the result card with its promotion screen. Drawn over the menu
    /// canvas like the lobby; everything outside the screen comes through <see cref="IRankedBackend"/>.
    /// </summary>
    public sealed partial class RankedScreen
    {
        private readonly MenuView view;
        private readonly IRankedBackend backend;
        private VisualElement layer, body, modalLayer, toastLayer;

        /// <summary>Back to the main menu.</summary>
        public event Action Closed;
        /// <summary>The match starts: the menu opens its table.</summary>
        public event Action<RankedStart> Matched;
        public bool Visible => layer != null;
        public VisualElement Layer => layer;

        // ----- state (the prototype's this.state) -----
        public string View { get; private set; } = "home";
        public string Mode { get; private set; } = "solo";
        private sealed class Member { public RankedPlayer Friend; public bool In; }
        private readonly List<Member> party = new List<Member>();
        public bool InviteOpen { get; private set; }
        private float inviteOpenedAt;
        /// <summary>In the queue (rankedQueue sent, no match yet).</summary>
        public bool Searching { get; private set; }
        private RankedSearch search = new RankedSearch();
        private float searchStartedAt;
        public RankedFound Found { get; private set; }
        private float foundAt;
        private bool acceptedByMe, ready;
        private RankedResult result;
        private string resultStage;
        private bool againAfterResult;
        private float resultShownAt, promoShownAt;
        private float viewShownAt, shownAt;
        private readonly Dictionary<string, float> seatsSeen = new Dictionary<string, float>();
        /// <summary>The last toast (tests).</summary>
        public string LastToast { get; private set; }
        public string ResultStage => resultStage;

        public RankedScreen(MenuView view, IRankedBackend backend) { this.view = view; this.backend = backend; }

        private string L(string tr, string en) => view.L(tr, en);
        private string Lang => view.Loc.Language == "en" ? "en" : "tr";
        private bool En => Lang == "en";

        public void Show()
        {
            if (layer != null) return;
            var canvas = view.Root.Q<VisualElement>("galaxy-canvas") ?? view.Root;
            var main = canvas.Q<VisualElement>("main-layer"); if (main != null) main.style.visibility = Visibility.Hidden;
            layer = Fill("ranked"); layer.pickingMode = PickingMode.Position;
            var modal = canvas.Q<VisualElement>("modal-layer");
            if (modal?.parent != null) modal.parent.Insert(modal.parent.IndexOf(modal), layer); else canvas.Add(layer);
            // div[data-screen-label=Dereceli]: background linear-gradient(180deg,rgba(5,8,22,.15),rgba(5,8,22,.5)).
            view.Decorate(layer, "linear-gradient(180deg,rgba(5,8,22,.15),rgba(5,8,22,.5))");
            body = Fill("ranked-body"); layer.Add(body);
            AppVersionLabel.Attach(layer);
            modalLayer = Fill("ranked-modals"); modalLayer.pickingMode = PickingMode.Ignore; layer.Add(modalLayer);
            toastLayer = Fill("ranked-toasts"); layer.Add(toastLayer);
            Hook();
            // The menu scene unloading (the table opens) takes the panel away without Hide: stop listening then too, so a
            // session-long backend hands its rankChange to the next screen (Pending) instead of this dead one.
            layer.RegisterCallback<DetachFromPanelEvent>(_ => Unhook());
            shownAt = viewShownAt = Time.realtimeSinceStartup;
            Render();
            backend.Refresh();
            // Opened mid-search or on a found match (a party leader queued this player): pick the state up.
            if (backend.Pending != null) OnFound(backend.Pending); else if (backend.Queue != null) OnSearching(backend.Queue);
            if (Pending != null) { var r = Pending; Pending = null; OnRankChanged(r); }
        }

        private bool hooked;
        private void Hook()
        {
            if (hooked) return; hooked = true;
            backend.Changed += OnChanged; backend.PartyAnswered += OnPartyAnswered; backend.Searching += OnSearching; backend.Found += OnFound;
            backend.Accepted += OnAccepted; backend.Cancelled += OnCancelled; backend.Ready += OnReady; backend.Started += OnStarted; backend.RankChanged += OnRankChanged;
            view.Root.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            view.Model.Changed += OnChanged; view.Loc.Changed += OnChanged; // balance, profile, language, theme
        }

        private void Unhook()
        {
            if (!hooked) return; hooked = false;
            backend.Changed -= OnChanged; backend.PartyAnswered -= OnPartyAnswered; backend.Searching -= OnSearching; backend.Found -= OnFound;
            backend.Accepted -= OnAccepted; backend.Cancelled -= OnCancelled; backend.Ready -= OnReady; backend.Started -= OnStarted; backend.RankChanged -= OnRankChanged;
            view.Root.UnregisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
            view.Model.Changed -= OnChanged; view.Loc.Changed -= OnChanged;
        }

        /// <summary>A result that arrived while the table was up (the menu shows it when it opens the screen again).</summary>
        public static RankedResult Pending;

        public void Hide()
        {
            if (layer == null) return;
            Unhook();
            ticker?.Pause(); ticker = null; motions.Clear();
            var canvas = layer.parent; layer.RemoveFromHierarchy(); layer = null; body = modalLayer = toastLayer = null;
            var main = canvas?.Q<VisualElement>("main-layer"); if (main != null) main.style.visibility = Visibility.Visible;
        }

        /// <summary>back(): a running search is cancelled first.</summary>
        private void Back() { if (Searching) Cancel(); Hide(); Closed?.Invoke(); }

        private void OnKey(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Escape || !InviteOpen) return;
            CloseInvite(); evt.StopImmediatePropagation();
        }

        // ---------- actions (the prototype's methods) ----------
        private int Need(string m) => m == "duo" ? 1 : m == "squad" ? 3 : 0;
        private int Need() => Need(Mode);
        private int Joined => party.Count(p => p.In);
        private bool Busy => Searching || Found != null;
        public bool Ready => Joined >= Need() && party.Count >= Need();

        public void SetView(string value)
        {
            if (View == value) return;
            // The view's content mounts again: its fade and the seats' rkSeat play again, as in the prototype.
            View = value; viewShownAt = Time.realtimeSinceStartup; seatsSeen.Clear(); Render();
        }

        public void SetMode(string value)
        {
            if (Busy || Mode == value) return;
            Mode = value; int keep = Need(value); if (party.Count > keep) party.RemoveRange(keep, party.Count - keep);
            Render();
        }

        public void OpenInvite() { InviteOpen = true; inviteOpenedAt = Time.realtimeSinceStartup; Render(); }
        public void CloseInvite() { InviteOpen = false; Render(); }

        private bool InRange(int idx, bool wide = false) => Math.Abs(RankedLadder.LeagueOf(backend.Profile.Idx) - RankedLadder.LeagueOf(idx)) <= (wide ? 2 : 1);

        public async void Invite(RankedPlayer friend)
        {
            if (friend == null || party.Count >= Need() || party.Any(p => p.Friend.Id == friend.Id)) return;
            party.Add(new Member { Friend = friend });
            if (party.Count >= Need()) InviteOpen = false;
            Render();
            string error = await backend.InviteAsync(friend);
            if (error == null || layer == null) return;
            party.RemoveAll(p => p.Friend.Id == friend.Id); FlashError(error); Render();
        }

        private void OnPartyAnswered(string id, bool joined)
        {
            var member = party.FirstOrDefault(p => p.Friend.Id == id); if (member == null) return;
            if (joined) { member.In = true; Flash(En ? member.Friend.Name + " joined your party" : member.Friend.Name + " takıma katıldı", "ok"); }
            else party.Remove(member);
            Render();
        }

        public void Kick(string id) { if (Searching) return; party.RemoveAll(p => p.Friend.Id == id); Render(); }

        /// <summary>MAÇ ARA / MASAYI KUR: rankedQueue with the party; Squad goes straight to the ready check.</summary>
        public async void StartSearch()
        {
            if (Busy || layer == null) return;
            if (Mode != "squad") { Searching = true; search = new RankedSearch(); searchStartedAt = Time.realtimeSinceStartup; }
            Render();
            string error = await backend.QueueAsync(Mode, party.Select(p => p.Friend.Id).ToList());
            if (error == null || layer == null) return;
            Searching = false; FlashError(error); Render();
        }

        public void Cancel()
        {
            if (!Searching) return;
            Searching = false; _ = backend.CancelAsync();
            Flash(L("Arama iptal edildi", "Search cancelled"), "info"); Render();
        }

        public async void Accept()
        {
            if (Found == null || acceptedByMe) return;
            var match = Found; acceptedByMe = true; Render();
            string error = await backend.AcceptAsync(match.MatchId, true);
            if (error == null || layer == null || Found != match) return;
            Found = null; FlashError(error); Render(); // E_MATCH_EXPIRED: the search goes on (rankedSearch)
        }

        public void Decline()
        {
            if (Found == null) return;
            _ = backend.AcceptAsync(Found.MatchId, false);
            Found = null; Searching = false; Flash(L("Maçı reddettin", "You declined the match"), "bad"); Render();
        }

        private void CloseResult(bool again)
        {
            if (result == null) return;
            if (resultStage == "sum" && result.Up) { resultStage = "promo"; againAfterResult = again; promoShownAt = Time.realtimeSinceStartup; Render(); return; }
            bool go = again || againAfterResult;
            result = null; resultStage = null; againAfterResult = false; Render();
            if (go) layer?.schedule.Execute(StartSearch).ExecuteLater(250);
        }

        /// <summary>The result card for a finished match (rankChange); the router and tests call it directly.</summary>
        public void ShowResult(RankedResult r) => OnRankChanged(r);

        // ---------- backend events ----------
        private void OnChanged() => Render();

        /// <summary>
        /// rankedSearch: the search (again). A party member learns the mode here. While a match was waiting for accepts it means
        /// that match fell through (another player timed out or declined) and the server put this party back in the search
        /// (lane B EK-219): E_MATCH_EXPIRED's text "Eşleşme süresi doldu · aramaya dönülüyor".
        /// </summary>
        private void OnSearching(RankedSearch s)
        {
            if (s == null) return;
            if (s.Mode == "solo" || s.Mode == "duo" || s.Mode == "squad") Mode = s.Mode;
            if (Found != null) { Found = null; Flash(L("Eşleşme süresi doldu · aramaya dönülüyor", "Match expired · back to search"), "info"); }
            if (!Searching) { Searching = true; searchStartedAt = Time.realtimeSinceStartup; }
            search = s; Render();
        }

        private void OnFound(RankedFound f)
        {
            if (f == null) return;
            Searching = false; Found = f; foundAt = Time.realtimeSinceStartup; acceptedByMe = false; ready = false; InviteOpen = false;
            Render();
        }

        private void OnAccepted(IReadOnlyList<bool> accepted)
        {
            if (Found == null || accepted == null) return;
            Found.Accepted = accepted.ToList(); Render();
        }

        private void OnCancelled(string reason)
        {
            bool had = Found != null || Searching;
            Found = null; Searching = false;
            // timeout: you did not accept in time; cancel: your party's leader stopped the search; declined: a member of your party
            // declined (your own decline already said "Maçı reddettin"; the package has no text for a partner's, EK-712).
            if (had && reason == "timeout") Flash(L("Maç zamanında kabul edilmedi", "Match not accepted in time"), "bad");
            else if (had && reason == "cancel") Flash(L("Arama iptal edildi", "Search cancelled"), "info");
            Render();
        }

        private void OnReady(int loadingMs) { if (Found == null) return; ready = true; Render(); }

        private void OnStarted(RankedStart start)
        {
            Found = null; Searching = false; Render();
            Matched?.Invoke(start);
        }

        private void OnRankChanged(RankedResult r)
        {
            if (r == null) return;
            result = r; resultStage = "sum"; againAfterResult = false; resultShownAt = Time.realtimeSinceStartup;
            if (View != "home") { View = "home"; viewShownAt = Time.realtimeSinceStartup; }
            Render();
        }

        /// <summary>
        /// The error's toast (strings errors[code]): E_PARTY_RANGE "Aralık dışı", E_ALREADY_SEATED, E_MATCH_EXPIRED; E_ALREADY_QUEUED,
        /// E_MODE, E_PARTY_SIZE and E_NOT_QUEUED are silent (null). Transport failures keep the menu's earlier queue texts.
        /// </summary>
        private string ErrorText(string code)
        {
            switch (code)
            {
                case "E_PARTY_RANGE": return L("Aralık dışı", "Out of range");
                case "E_ALREADY_SEATED": return L("Zaten bir masada oturuyorsun", "You are already seated at a table");
                case "E_MATCH_EXPIRED": return L("Eşleşme süresi doldu · aramaya dönülüyor", "Match expired · back to search");
                case "E_ALREADY_QUEUED": case "E_MODE": case "E_PARTY_SIZE": case "E_NOT_QUEUED": return null;
                case "conflict": return L("Zaten bir masadasın ya da sıradasın", "You are already at a table or in a queue");
                case "offline": case "timeout": return L("Sunucuya ulaşılamadı", "Server unreachable");
                default: return L("Eşleşme başlatılamadı", "Could not start matchmaking");
            }
        }

        private void FlashError(string code) { string text = ErrorText(code); if (text != null) Flash(text, "bad"); }

        // ---------- layout ----------
        public void Refresh() => Render();

        private void Render()
        {
            if (body == null) return;
            body.Clear(); modalLayer.Clear();
            motions.RemoveAll(m => m.Owner == null || m.Owner.panel == null); // the toast's own motion outlives a redraw
            BuildHeader();
            if (View == "progress") BuildProgress(); else BuildHome();
            modalLayer.pickingMode = PickingMode.Ignore;
            if (InviteOpen) BuildInvite();
            if (Found != null) BuildFound();
            if (result != null) BuildResult();
        }

        // ---------- per-frame: the search clock and the accept countdown (texts only, no redraw) ----------
        private Label clockLabel, acceptLabel;
        private VisualElement acceptBar;

        private void Every()
        {
            if (clockLabel != null && clockLabel.panel != null) { string c = ClockText(); if (clockLabel.userData as string != c) { clockLabel.userData = c; clockLabel.text = c; MenuView.TabularDigits(clockLabel); } }
            if (acceptLabel != null && acceptLabel.panel != null && Found != null)
            {
                float t = Time.realtimeSinceStartup - foundAt;
                string a = AcceptText(); if (acceptLabel.text != a) acceptLabel.text = a;
                // width: found.left × 10 % with "transition: width 1s linear": each second's step glides over the next second.
                if (acceptBar != null) acceptBar.style.width = Length.Percent(Mathf.Clamp(Found.Seconds - Mathf.Max(0, t - 1), 0, Found.Seconds) / Found.Seconds * 100);
            }
        }

        private int SearchSeconds => Mathf.FloorToInt(Time.realtimeSinceStartup - searchStartedAt);
        private int SearchSlots => Mode == "duo" ? 2 : 3;
        private int SearchFilled => search?.Opponents?.Count ?? search?.Filled ?? 0;
        private string ClockText()
        {
            int sec = Searching ? SearchSeconds : 0;
            return sec / 60 + ":" + (sec % 60).ToString("00") + " · " + SearchFilled + "/" + SearchSlots + " " + L("bulundu", "found");
        }
        private int AcceptLeft => Found == null ? 0 : Mathf.Max(1, Mathf.CeilToInt(Found.Seconds) - Mathf.FloorToInt(Time.realtimeSinceStartup - foundAt));
        private string AcceptText() => L("Kabul et · ", "Accept · ") + AcceptLeft;

        // ---------- toast (G101-Dereceli flash(): top 20, pill, rkToast .35s, 2.6 s) ----------
        public void Flash(string text, string kind)
        {
            LastToast = text; if (toastLayer == null) return;
            toastLayer.Clear();
            string bg = kind == "ok" ? "#3CCB6E" : kind == "bad" ? "linear-gradient(#FF7A86,#E8455A)" : "rgba(5,8,22,.85)", fg = kind == "ok" ? "#062B14" : kind == "bad" ? "#fff" : "#F4F1E8";
            var toast = Row(); toast.style.position = Position.Absolute; toast.style.top = 20; toast.style.left = 640; Pad(toast, 10, 20, 10, 10);
            view.Decorate(toast, bg, 999); GalaxyFilters.Shadow(toast, 0, 10, 15, new Color(0, 0, 0, .4f));
            var disc = Box(); Square(disc, 26); view.Decorate(disc, "rgba(0,0,0,.18)", 13);
            var diamond = Box(); diamond.style.width = diamond.style.height = 9; diamond.style.rotate = new Rotate(45); view.Decorate(diamond, fg, 2); disc.Add(diamond); toast.Add(disc);
            var label = Text(text, 15, "Baloo2-800", fg); label.style.marginLeft = 10; toast.Add(label);
            toastLayer.Add(toast);
            float began = Time.realtimeSinceStartup;
            Animate(() =>
            {
                float t = Time.realtimeSinceStartup - began, k = Ease(Mathf.Clamp01(t / .35f));
                toast.style.translate = new Translate(Length.Percent(-50), -12 * (1 - k)); toast.style.opacity = k;
                if (t > 2.6f) toast.RemoveFromHierarchy();
            }, toast);
        }
    }
}
