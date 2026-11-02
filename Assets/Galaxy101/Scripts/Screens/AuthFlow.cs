using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Açılış ve giriş (spec/18, S01–S05), drawn over the menu canvas until the player is signed in:
    /// <list type="bullet">
    /// <item>Splash: GALAXY + 1 0 1 tiles on the centred planet, a progress bar with the loading messages, a random tip and
    /// the CDX · version mark. A remembered account signs back in meanwhile and goes straight to the menu.</item>
    /// <item>Choice: the planet moves left under the logo and tagline; on the right the 470 px panel — TR/EN, "Hoş geldin",
    /// MİSAFİR OLARAK OYNA, Giriş yap, Kayıt ol (+25.000), Google, terms.</item>
    /// <item>Forms: Giriş yap (e-posta, şifre with show/hide, Beni hatırla, Şifremi unuttum, tries left / locked),
    /// Kayıt ol (kullanıcı adı, e-posta, şifre + strength, tekrar, terms), Misafir (takma ad, 8 avatar colours, notes).</item>
    /// </list>
    /// </summary>
    public sealed class AuthFlow
    {
        // The game shell's COLORS, in its order (a 4 × 2 grid).
        private static readonly string[] Colors = { "#FF8A5B", "#3D7BFF", "#16B6A8", "#8B5CF6", "#FF6B6B", "#3CCB6E", "#E8A317", "#E05FA8" };
        private readonly MenuView view;
        private readonly Transform owner;
        private readonly GalaxyOnline online;
        private VisualElement layer, body;
        private AuthCursorVisual authCursor;
        private GalaxyBackground background;
        private string screen;
        private bool busy;
        private string error;
        // Form fields keep their values across rebuilds (language switch, error redraw).
        private string email = "", password = "", password2 = "", name = "", nick, color;
        private bool remember = true, terms, showPassword;
        // Şifremi unuttum: the six-digit code from the e-mail, and whether it was sent (step two).
        private string resetCode = "";
        private bool resetSent;
        private float splashStarted;

        /// <summary>Signed in (or resumed): the menu takes over.</summary>
        public event Action Completed;
        public bool Visible => layer != null;
        public string Screen => screen;
        public VisualElement Layer => layer;
        /// <summary>
        /// The prototype's 2800 ms splash gate (runSplash: the bar may reach 100 % once 2.8 s have passed and the heavy module
        /// is in, or after 7 s); every splash time scales with it, so tests shorten it.
        /// </summary>
        public float SplashSeconds = 2.8f;
        // runSplash(): target 18 → 46 fonts ready → 62 planet ready → 84 heavy module (here: the session check) in; every
        // 40 ms sp += 0.9 + rand × 1.8 up to cap = (elapsed > 2800 and target ≥ 84) or elapsed > 7000 ? 100 : min(target, 96).
        private float splashProgress, splashTarget = 18, splashStepAt;
        private bool splashSkipRequested;
        private Mulberry32 splashRandom;
        private float SplashScale => Mathf.Max(.01f, SplashSeconds) / 2.8f;
        /// <summary>Realtime the splash began (the state router times S01 from it, as the prototype's 'fresh' page load).</summary>
        public float SplashStartedAt => splashStarted;

        public AuthFlow(MenuView view, Transform owner, GalaxyOnline online)
        {
            this.view = view; this.owner = owner; this.online = online;
            nick = "Misafir" + UnityEngine.Random.Range(1000, 10000); color = Colors[UnityEngine.Random.Range(0, Colors.Length)];
        }

        private string L(string tr, string en) => view.L(tr, en);

        /// <summary>The guest form's nickname and colour before it is drawn (the state router's S05 path sets f.nick and f.color).</summary>
        public void Prefill(string guestNick, string guestColor) { nick = guestNick; color = guestColor; if (layer != null) Render(); }

        public async void Show()
        {
            var canvas = view.Root.Q<VisualElement>("galaxy-canvas") ?? view.Root;
            layer = Fill("auth-flow"); layer.pickingMode = PickingMode.Position; canvas.Add(layer);
            layer.RegisterCallback<PointerDownEvent>(ClickRipple, TrickleDown.TrickleDown);
            layer.RegisterCallback<ClickEvent>(evt => { if (screen == "splash" && splashProgress > 60) splashSkipRequested = true; });
            var backdrop = Fill("auth-background"); backdrop.pickingMode = PickingMode.Ignore; layer.Add(backdrop);
            background = new GameObject("Auth background").AddComponent<GalaxyBackground>(); background.transform.SetParent(owner, false);
            background.Initialize(backdrop);
            background.Configure(view.Model.Planet, (bool?)view.Model.Settings["motion"] ?? true, (string)view.Model.Settings["quality"] ?? "Yüksek");
            background.SetFocused(UnityEngine.Application.isFocused || UnityEngine.Application.isBatchMode);
            body = Fill("auth-body"); body.pickingMode = PickingMode.Ignore; layer.Add(body);
            authCursor = new AuthCursorVisual(layer, planet: () => view.Model.Planet);
            screen = "splash"; splashStarted = Time.realtimeSinceStartup; splashProgress = 0; splashStepAt = splashStarted; splashSkipRequested = false;
            splashTarget = 62; // the fonts ship with the build and the planet is drawn above: 46 and 62 at once
            splashRandom = GameClock.Stream("splash"); Render();
            var resume = online.ResumeAsync();
            while ((splashProgress < 100 && !splashSkipRequested) || !resume.IsCompleted) { if (layer == null) return; if (resume.IsCompleted) splashTarget = 84; await Task.Yield(); }
            float hold = Time.realtimeSinceStartup + .38f * SplashScale;   // setTimeout(afterSplash, 380)
            while (!splashSkipRequested && Time.realtimeSinceStartup < hold) { if (layer == null) return; await Task.Yield(); }
            bool resumed = false; string banned = null;
            try { resumed = resume.Result; }
            catch (Exception e)
            {
                if (e.InnerException is ApiException api && api.Code == ErrorCodes.Banned) banned = Explain(api);
                else Debug.LogWarning("Resume failed: " + e.Message);
            }
            if (layer == null) return;
            await SplashOut();
            if (layer == null) return;
            // A remembered session goes straight to the menu (menuIn), without a greeting — as the design's afterSplash().
            if (resumed) { Finish(null, null, fromSplash: true); return; }
            if (banned != null) { Go("login"); error = banned; Render(); return; }
            Go("choice");
        }

        public void Go(string next)
        {
            if (next != screen) { tried.Clear(); error = null; }
            float now = Time.realtimeSinceStartup;
            if (next != screen) { if (screen == "splash" || screen == null) sideAt = now; screenAt = now; }
            screen = next; error = null; Render();
        }
        // Entrance animations run from when a screen (or the side art) first appeared, not from each redraw: a toggle,
        // a colour pick or an error line redraws the panel in place without replaying the slide-in.
        private float screenAt, sideAt;

        private void Finish(string greeting, string kind, bool fromSplash = false)
        {
            Hide();
            if (fromSplash) view.PlayMenuIn();
            // loginAs(): the greeting flashes 500 ms after the menu appears — gold with the welcome chips, green otherwise.
            if (!string.IsNullOrEmpty(greeting)) view.Root.schedule.Execute(() => view.Flash(greeting, kind)).ExecuteLater(500);
            Completed?.Invoke();
        }

        public void Hide()
        {
            splashPresentation?.Dispose(); splashPresentation = null;
            ticker?.Pause(); ticker = null; motions.Clear(); planetJob?.Pause(); planetJob = null; planetPlaced = false;
            authCursor?.Dispose(); authCursor = null;
            layer?.RemoveFromHierarchy(); layer = null;
            if (background != null) UnityEngine.Object.Destroy(background.gameObject); background = null;
        }

        // ---------- layout ----------
        private void Render()
        {
            if (body == null) return;
            splashPresentation?.Dispose(); splashPresentation = null;
            Galaxy101.Audio.GalaxyCursor.Lock(layer, busy ? "wait" : null);
            // Keep the caret where it was across the redraw (typing never loses focus).
            var focused = body.panel?.focusController?.focusedElement as VisualElement;
            var focusedField = focused?.GetFirstAncestorOfType<TextField>() ?? focused as TextField;
            string focusName = focusedField?.name;
            int cursor = focusedField?.cursorIndex ?? 0, selection = focusedField?.selectIndex ?? 0;
            body.Clear(); motions.Clear();
            bool splash = screen == "splash";
            // Planet: centred for the splash (640,330), left for the auth screens (310,340 ×1.05) — spec/18.
            PlanetTo(splash ? new Vector4(640, 330, 1f, 0) : new Vector4(310, 340, 1.05f, 0));
            if (splash) { BuildSplash(); return; }
            BuildSide();
            switch (screen)
            {
                case "choice": BuildChoice(); break;
                case "login": BuildLogin(); break;
                case "register": BuildRegister(); break;
                case "guest": BuildGuest(); break;
                case "reset": BuildReset(); break;
            }
            AppVersionLabel.Attach(body);
            if (focusName != null && body.Q<TextField>(focusName) is TextField again)
                again.schedule.Execute(() => { again.Focus(); again.SelectRange(Mathf.Clamp(cursor, 0, again.value.Length), Mathf.Clamp(selection, 0, again.value.Length)); });
        }

        // ---------- planet and splash exit ----------
        // The design's planet element: transition left/top/transform 1s cubic-bezier(.6,0,.2,1), opacity .8s.
        private Vector4 planetFrom, planetTo, planetNow; private float planetAt = -9; private bool planetPlaced; private IVisualElementScheduledItem planetJob;

        private void PlanetTo(Vector4 target)
        {
            if (planetPlaced && target == planetTo) return;
            if (!planetPlaced) { planetPlaced = true; planetFrom = planetTo = planetNow = target; ApplyPlanet(); return; }
            planetFrom = planetNow; planetTo = target; planetAt = Time.realtimeSinceStartup;
            planetJob ??= layer?.schedule.Execute(StepPlanet).Every(0);
            planetJob?.Resume();
        }

        private void StepPlanet()
        {
            float t = Time.realtimeSinceStartup - planetAt, f = Mathf.Clamp01(t / 1f), k = MenuView.CssBezier(.6f, 0, .2f, 1, f);
            float o = MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01(t / .8f));
            planetNow = new Vector4(Mathf.Lerp(planetFrom.x, planetTo.x, k), Mathf.Lerp(planetFrom.y, planetTo.y, k), Mathf.Lerp(planetFrom.z, planetTo.z, k), Mathf.Lerp(planetFrom.w, planetTo.w, o));
            ApplyPlanet();
            if (f >= 1 && t >= .8f) planetJob?.Pause();
        }

        private void ApplyPlanet() =>
            background?.UseAuthLayout(planetNow, view.C("var(--pg)"));

        private VisualElement splashColumn, splashTiles, splashBar;
        private LogoTilePresentation splashPresentation;

        /// <summary>
        /// splashOut .75s cubic-bezier(.7,0,.3,1): the splash fades, shrinks to .96 and blurs 10 px while the tiles rise 36 px
        /// and grow 1.14×, and the planet sweeps to (640, 340) ×1.5 and fades; the next screen follows at 720 ms.
        /// </summary>
        private async Task SplashOut()
        {
            if (splashColumn == null) return;
            PlanetTo(new Vector4(640, 340, 1.5f, 1));
            float began = Time.realtimeSinceStartup;
            var parts = new[] { splashColumn, splashBar };
            while (layer != null && Time.realtimeSinceStartup - began < .72f)
            {
                float f = Mathf.Clamp01((Time.realtimeSinceStartup - began) / .75f), k = MenuView.CssBezier(.7f, 0, .3f, 1, f);
                foreach (var part in parts)
                {
                    if (part == null) continue;
                    part.style.opacity = 1 - k; part.style.scale = new Scale(Vector2.one * (1 - .04f * k));
                    GalaxyFilters.Blur(part, 10 * k);
                }
                if (splashTiles != null) { splashTiles.style.translate = new Translate(0, -36 * k); splashTiles.style.scale = new Scale(Vector2.one * (1 + .14f * k)); }
                await Task.Yield();
            }
            splashColumn = splashTiles = splashBar = null;
        }

        private void BuildSplash()
        {
            var column = Box(); MenuView.Place(column, 0, 118, 1280, 300); column.style.alignItems = Align.Center; body.Add(column); splashColumn = column;
            // The padded logo texture paints inside the source's 66px/1.15 line; its transparent margins do not take flow space.
            var logoLine = Box(); logoLine.style.width = 1280; logoLine.style.height = 66 * 1.15f; logoLine.style.flexShrink = 0; logoLine.style.alignItems = Align.Center;
            var logo = Logo(66, 8); logo.style.position = Position.Absolute; logo.style.top = LogoCapOffset(66, 66 * 1.15f); logo.style.opacity = 0;
            logoLine.Add(logo); column.Add(logoLine); splashTiles = Tiles(1f); column.Add(splashTiles);
            var bar = Box("splash-progress"); MenuView.Place(bar, 420, 478, 440, 40); body.Add(bar); splashBar = bar;
            bar.style.height = StyleKeyword.Auto;
            bar.style.opacity = 0;
            var labels = Row(); labels.style.justifyContent = Justify.SpaceBetween;
            var message = Text("", 14, "Baloo2-800", "var(--ic)"); var pct = Text("0%", 14, "Baloo2-800", "var(--a1)"); pct.name = "splash-percent";
            labels.Add(message); labels.Add(pct); bar.Add(labels);
            var track = Box(); track.style.height = 14; track.style.flexShrink = 0; track.style.marginTop = 9; track.style.overflow = Overflow.Hidden; view.Decorate(track, "var(--dp)", 7);
            var fill = Box(); fill.style.position = Position.Absolute; fill.style.left = fill.style.top = fill.style.bottom = 0; view.Decorate(fill, "linear-gradient(90deg,var(--a2),var(--a1))", 7);
            GalaxyFilters.Shadow(fill, 0, 0, 7, view.C("var(--gl)")); track.Add(fill); bar.Add(track);
            var messages = new[] { L("Gezegenler hizalanıyor…", "Aligning planets…"), L("Taşlar diziliyor…", "Racking tiles…"), L("Masalar hazırlanıyor…", "Setting up tables…"), L("Neredeyse hazır…", "Almost ready…"), L("Hazır", "Ready") };
            Animate(() =>
            {
                float now = Time.realtimeSinceStartup, scale = SplashScale;
                while (now >= splashStepAt && splashProgress < 100)
                {
                    float elapsed = (splashStepAt - splashStarted) / scale;
                    float cap = elapsed > 2.8f && splashTarget >= 84 || elapsed > 7f ? 100 : Mathf.Min(splashTarget, 96);
                    splashProgress = Mathf.Min(cap, splashProgress + .9f + (float)splashRandom.NextDouble() * 1.8f);
                    splashStepAt += .04f * scale;
                }
                float u = splashProgress;
                fill.style.width = Length.Percent(u); pct.text = Mathf.FloorToInt(u) + "%";
                message.text = messages[u >= 100 ? 4 : u > 75 ? 3 : u > 45 ? 2 : u > 20 ? 1 : 0];
            });
            var tips = new[]
            {
                L("İpucu: Göstergenin bir üstü okeydir ve her taşın yerine geçer.", "Tip: The tile one above the indicator is the okey; it stands for any tile."),
                L("İpucu: Elini açmak için 101 puanlık per ya da 5 çift gerekir.", "Tip: Opening needs sets worth 101 or 5 pairs."),
                L("İpucu: Bir pere basılı tutarak toplu taşıyabilirsin.", "Tip: Hold a set to move it as one."),
                L("İpucu: İşlek taşı atarsan +101 ceza yazılır.", "Tip: Throwing a playable tile costs +101."),
            };
            var tip = Row(); tip.name = "splash-tip"; tip.style.position = Position.Absolute; tip.style.bottom = 40; tip.style.left = 640; tip.style.translate = new Translate(Length.Percent(-50), 0);
            Pad(tip, 11, 18, 11, 11); view.Decorate(tip, "rgba(0,0,0,.32)", 18); Border(tip, 1, new Color(1, 1, 1, .08f));
            var bulb = Box(); bulb.style.width = bulb.style.height = 30; bulb.style.alignItems = Align.Center; bulb.style.justifyContent = Justify.Center; bulb.style.marginRight = 12;
            view.Decorate(bulb, "linear-gradient(160deg,var(--a1),var(--a2))", 10); bulb.Add(view.Icon("tip-bulb", 16, "var(--aT)")); tip.Add(bulb);
            tip.Add(Text(tips[(int)(GameClock.Stream("splash-tip").NextDouble() * tips.Length)], 14, "Baloo2-700", "var(--ic)")); body.Add(tip);
            tip.style.opacity = 0;
            Animate(() =>
            {
                float elapsed = GameClock.Frozen ? (float)(GameClock.CanonMs / 1000) : Time.realtimeSinceStartup - splashStarted;
                float logoProgress = CubicBezier(.16f, 1, .3f, 1, Mathf.Clamp01((elapsed - .85f) / 1.1f));
                logo.style.opacity = logoProgress;
                logo.style.translate = new Translate(0, 14 * (1 - logoProgress));
                logo.style.scale = new Scale(Vector2.one * Mathf.Lerp(.94f, 1, logoProgress));
                bar.style.opacity = CubicBezier(.25f, .1f, .25f, 1, Mathf.Clamp01((elapsed - 1.1f) / .5f));
                tip.style.opacity = CubicBezier(.25f, .1f, .25f, 1, Mathf.Clamp01((elapsed - 1.4f) / .5f));
            });
            var mark = Row(); mark.style.position = Position.Absolute; mark.style.right = 20; mark.style.bottom = 14;
            var diamond = Box(); diamond.style.width = diamond.style.height = 9; diamond.style.rotate = new Rotate(45); diamond.style.marginRight = 5; view.Decorate(diamond, "linear-gradient(135deg,var(--a1),var(--a2))", 2); mark.Add(diamond);
            mark.Add(Text("CDX", 12, "Montserrat-800", "#F4F1E8")); var games = Text("GAMES", 11, "Baloo2-700", "var(--mt)"); MenuView.Tracking(games, 1.5f, 11); games.style.marginLeft = 5; mark.Add(games);
            var rule = Box(); rule.style.width = 1; rule.style.height = 10; rule.style.marginLeft = rule.style.marginRight = 8; rule.style.backgroundColor = view.C("var(--mt)"); rule.style.opacity = .4f; mark.Add(rule);
            var version = Text("v" + UnityEngine.Application.version, 11, "Baloo2-700", "var(--mt)"); version.style.opacity = .6f; mark.Add(version);
            body.Add(mark);
        }

        /// <summary>Left of the auth screens: the logo, the tiles and the tagline over the planet.</summary>
        private void BuildSide()
        {
            var column = Box(); MenuView.Place(column, 0, 150, 620, 520); column.style.alignItems = Align.Center; body.Add(column);
            // isAuthSide: top 150, GALAXY 60px/1.15 (a 69 px line), tiles row margin 8 / height 120 / gap 11, tagline 20px/1.45
            // 150 px below the tiles.
            var logoLine = Box(); logoLine.style.width = 620; logoLine.style.height = 69; logoLine.style.alignItems = Align.Center;
            var logo = Logo(60, 7); logo.style.position = Position.Absolute; logo.style.top = LogoCapOffset(60, 69); logoLine.Add(logo); column.Add(logoLine);
            column.Add(SideTiles());
            var tag = Text(L("Masalar dolu, taşlar hazır.\nArkadaşlarınla otur, 101 açıp eli bitir.", "Tables are full, tiles are racked.\nSit with your friends, open 101 and finish."), 20, "Baloo2-700", "var(--ic)");
            // auth.side.tag: 700 20px/1.45 (29 px lines), centred, max-width 440, margin-top 150, pre-line.
            tag.style.unityTextAlign = TextAnchor.UpperCenter; tag.style.width = 440; tag.style.marginTop = 150; MenuView.CssParagraph(tag, 29, false); column.Add(tag);
        }

        private VisualElement Panel(float top, float bottom, int padY, int padX)
        {
            var panel = Box("auth-panel"); panel.pickingMode = PickingMode.Position;
            panel.style.position = Position.Absolute; panel.style.right = 70; panel.style.top = top; panel.style.bottom = bottom; panel.style.width = 470;
            Pad(panel, padY, padX, padY, padX);
            view.Decorate(panel, "var(--pn)", 28, 10, "var(--pnS)"); Border(panel, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.BoxShadow(panel, 0, 40, 45, new Color(0, 0, 0, .5f));
            float began = screenAt;
            if (Time.realtimeSinceStartup - began < .45f)
                Animate(() => { float u = Mathf.Clamp01((Time.realtimeSinceStartup - began) / .45f), k = 1 - Mathf.Pow(1 - u, 3); panel.style.translate = new Translate(30 * (1 - k), 0); panel.style.opacity = u; });
            body.Add(panel); return panel;
        }

        private void BuildChoice()
        {
            var panel = Panel(60, 60, 28, 32);
            var head = Row(); head.style.justifyContent = Justify.SpaceBetween;
            var brand = Text("GALAXY 101", 11, "Baloo2-800", "var(--a1)"); MenuView.Tracking(brand, 2, 11); head.Add(brand);
            var langs = Row(); langs.name = "auth-lang"; Pad(langs, 4, 4, 4, 4); view.Decorate(langs, "var(--dp)", 12);
            foreach (var code in new[] { "tr", "en" })
            {
                bool on = view.Loc.Language == code; string pick = code;
                var chip = Text(code.ToUpperInvariant(), 13, "Montserrat-800", on ? "var(--aT)" : "var(--mt)"); chip.name = "auth-lang-" + code; Pad(chip, 6, 13, 6, 13);
                view.Decorate(chip, on ? "linear-gradient(var(--a1),var(--a2))" : "transparent", 9); if (code == "en") chip.style.marginLeft = 4;
                Tap(chip, () => { view.Services.Settings.SetLanguage(pick); Render(); }); langs.Add(chip);
            }
            head.Add(langs); panel.Add(head);
            var welcome = Line(L("Hoş geldin", "Welcome"), 32, 1.15f, "Montserrat-700", "#F4F1E8"); welcome.style.marginTop = 12 + 6; panel.Add(welcome);
            var how = Text(L("Nasıl devam etmek istersin?", "How would you like to continue?"), 15, "Baloo2-700", "var(--mt)"); how.style.marginTop = 4; how.style.marginBottom = 8 + 12; panel.Add(how);

            var guest = Row(); guest.name = "auth-guest"; guest.style.height = 76; Pad(guest, 0, 20, 0, 20);
            view.Decorate(guest, "linear-gradient(var(--a1),var(--a2))", 20, 6, "var(--aS)");
            var glow = GalaxyFilters.SpreadRing(guest, 14);
            var shineClip = Fill("auth-guest-shine-clip"); shineClip.style.overflow = Overflow.Hidden; Radius(shineClip, 20); guest.Add(shineClip);
            var shine = Box(); shine.style.position = Position.Absolute; shine.style.top = shine.style.bottom = 0; shine.style.width = 56; shine.style.left = 0;
            shine.style.backgroundColor = new Color(1, 1, 1, .4f); SkewX(shine, -20); shineClip.Add(shine);
            var gIcon = Box(); gIcon.style.width = gIcon.style.height = 44; gIcon.style.alignItems = Align.Center; gIcon.style.justifyContent = Justify.Center; gIcon.style.marginRight = 14;
            view.Decorate(gIcon, "rgba(255,255,255,.35)", 14); gIcon.Add(view.Icon("auth-guest", 24, "var(--aT)")); guest.Add(gIcon);
            // shine 3 s: a 56 px white bar sweeping across; pulseG 2.2 s: a glow ring growing to 14 px.
            float shineFrom = sideAt;
            Animate(() =>
            {
                // translateX(-160% → 520%) skewX(-20deg) over the first 60 % of each 3 s cycle (ease).
                float p = Mathf.Repeat((Time.realtimeSinceStartup - shineFrom) / 3f, 1), u = CubicBezier(.25f, .1f, .25f, 1, Mathf.Clamp01(p / .6f));
                shine.style.translate = new Translate(56 * Mathf.Lerp(-1.6f, 5.2f, u), 0);
                float pulse = Mathf.Repeat((Time.realtimeSinceStartup - shineFrom) / 2.2f, 1);
                float progress = CubicBezier(.25f, .1f, .25f, 1, pulse < .5f ? pulse * 2 : (1 - pulse) * 2);
                var tint = view.C("var(--gl)"); tint.a *= 1 - progress; glow(14 * progress, tint);
            });
            var gWords = Box(); gWords.Add(Line(L("MİSAFİR OLARAK OYNA", "PLAY AS GUEST"), 21, 1.1f, "Montserrat-800", "var(--aT)"));
            var gSub = Text(L("Kayıt yok · hemen başla", "No sign-up · start now"), 13, "Baloo2-800", "var(--aT)"); gSub.style.opacity = .8f; gSub.style.marginTop = 3; gWords.Add(gSub); guest.Add(gWords);
            TapRaised(guest, () => Go("guest"), 6, "var(--aS)", true, true); panel.Add(guest);

            var login = Row(); login.name = "auth-login"; login.style.height = 60; login.style.marginTop = 12; Pad(login, 0, 20, 0, 20); view.Decorate(login, "var(--pnH)", 18, 5, "var(--pnS)");
            var lIcon = view.Icon("auth-login", 22, "var(--ic)"); lIcon.style.marginRight = 14; login.Add(lIcon); login.Add(Text(L("Giriş yap", "Log in"), 18, "Montserrat-700", "#F4F1E8"));
            AuthHover(login, Bright("var(--pnH)", 1.2f), 18); TapRaised(login, () => Go("login"), 5, "var(--pnS)"); panel.Add(login);

            var register = Row(); register.name = "auth-register"; register.style.height = 60; register.style.marginTop = 12; Pad(register, 0, 14, 0, 20); view.Decorate(register, "#3D7BFF", 18, 5, "#2148B0");
            var rIcon = view.Icon("auth-register", 22, "#fff"); rIcon.style.marginRight = 14; register.Add(rIcon);
            var rText = Text(L("Kayıt ol", "Sign up"), 18, "Montserrat-700", "#fff"); rText.style.flexGrow = 1; register.Add(rText);
            var gift = Row(); Pad(gift, 5, 10, 5, 10); view.Decorate(gift, "rgba(0,0,0,.22)", 10);
            var coin = view.ChipIcon(14); coin.style.marginRight = 6; gift.Add(coin);
            gift.Add(Text("+" + view.N(25000), 12, "Baloo2-800", "#FFE08A")); register.Add(gift);
            AuthHover(register, Bright("#3D7BFF", 1.1f), 18); TapRaised(register, () => Go("register"), 5, "#2148B0"); panel.Add(register);

            panel.Add(Divider(L("ya da", "or")));
            panel.Add(GoogleButton());
            var spacer = Box(); spacer.style.flexGrow = 1; panel.Add(spacer);
            var terms = Text(L("Devam ederek Kullanım Koşulları’nı ve Gizlilik Politikası’nı kabul etmiş olursun. Oyun sanal çiple oynanır; gerçek para kazandırmaz.",
                "By continuing you accept the Terms of Use and the Privacy Policy. The game is played with virtual chips; it pays no real money."), 12, "Baloo2-700", "var(--mt)");
            terms.style.whiteSpace = WhiteSpace.Normal; terms.style.opacity = .8f; panel.Add(terms);
            AddError(panel);
        }

        /// <summary>Google: 56 px / radius 18 / edge 5 / 22 px ring / 17 px on the choice panel, 50 / 16 / 4 / 20 / 16 on the login form.</summary>
        private VisualElement GoogleButton(int height = 56, int radius = 18, int edge = 5, int ringSize = 22, int font = 17)
        {
            var google = Row(); google.name = "auth-google"; google.style.height = height; google.style.justifyContent = Justify.Center; google.style.flexShrink = 0; view.Decorate(google, "#fff", radius, edge, "#A9B4D0");
            var ring = Box(); ring.style.width = ring.style.height = ringSize; ring.style.marginRight = 12; Radius(ring, ringSize / 2f);
            ring.style.borderTopWidth = ring.style.borderRightWidth = ring.style.borderBottomWidth = ring.style.borderLeftWidth = 4;
            ring.style.borderTopColor = view.C("#3D7BFF"); ring.style.borderRightColor = view.C("#3CCB6E"); ring.style.borderBottomColor = view.C("#FFC94A"); ring.style.borderLeftColor = view.C("#FF5E6C");
            google.Add(ring); google.Add(Text(L("Google ile devam et", "Continue with Google"), font, "Montserrat-700", "#1B2340"));
            // Google needs the platform sign-in SDK and the server's OAuth client ids; until both are set the server answers invalid_token.
            TapRaised(google, () => view.Flash(L("Google ile giriş bu sürümde henüz açık değil.", "Google sign-in is not available in this version yet."), "info", layer), edge, "#A9B4D0");
            return google;
        }

        private void TapRaised(VisualElement element, Action action, float depth, string shadow, bool liftOnHover = false, bool keepShadow = false)
        {
            element.pickingMode = PickingMode.Position; element.focusable = true;
            element.AddToClassList("auth-raised");
            Galaxy101.Audio.CursorRules.Mark(element, Galaxy101.Audio.CursorRules.Pointer);
            bool hovered = false, pressed = false;
            void UpdatePress()
            {
                float offset = pressed ? (keepShadow ? 4 : depth - 1) : hovered && liftOnHover ? -2 : 0;
                element.style.translate = new Translate(0, offset);
                GalaxyFilters.HardShadow(element, pressed && !keepShadow ? 1 : depth, view.C(shadow));
            }
            element.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; UpdatePress(); });
            element.RegisterCallback<PointerLeaveEvent>(_ => { hovered = pressed = false; UpdatePress(); });
            element.RegisterCallback<PointerDownEvent>(evt => { if (evt.button == 0 && element.enabledInHierarchy) { pressed = true; UpdatePress(); } });
            element.RegisterCallback<PointerUpEvent>(_ => { pressed = false; UpdatePress(); });
            element.RegisterCallback<PointerCancelEvent>(_ => { pressed = false; UpdatePress(); });
            element.RegisterCallback<ClickEvent>(evt => { action(); evt.StopPropagation(); });
        }

        // A UI Toolkit tint above 1 did not produce CSS brightness above 100% in the rendered output.
        // Draw a pre-brightened face instead, keeping the existing ledge and content in their original layers.
        private string Bright(string color, float gain)
        {
            Color c = view.C(color);
            c.r = Mathf.Clamp01(c.r * gain); c.g = Mathf.Clamp01(c.g * gain); c.b = Mathf.Clamp01(c.b * gain);
            return "#" + ColorUtility.ToHtmlStringRGBA(c);
        }

        private void AuthHover(VisualElement element, string background, int radius)
        {
            var face = Fill("auth-hover-face"); face.AddToClassList("auth-hover-face"); face.style.opacity = 0;
            view.Decorate(face, background, radius);
            int index = element.childCount > 0 && element[0].name == "ledge" ? 1 : 0;
            element.Insert(index, face);
            element.RegisterCallback<PointerEnterEvent>(_ => face.style.opacity = 1);
            element.RegisterCallback<PointerLeaveEvent>(_ => face.style.opacity = 0);
        }

        private void ClickRipple(PointerDownEvent evt)
        {
            if (evt.button != 0 || evt.pointerType != UnityEngine.UIElements.PointerType.mouse || Galaxy101.Audio.GalaxyCursor.Mode == Galaxy101.Audio.CursorSet.System) return;
            string cursor = Galaxy101.Audio.CursorRules.Resolve(evt.target as VisualElement);
            if (cursor != Galaxy101.Audio.CursorRules.Default && cursor != Galaxy101.Audio.CursorRules.Pointer) return;
            float size = 38 * Galaxy101.Audio.CursorSet.ModeScale(Galaxy101.Audio.GalaxyCursor.Mode);
            Vector2 position = layer.WorldToLocal(new Vector2(evt.position.x, evt.position.y));
            var ripple = Box("auth-click-ripple"); ripple.style.position = Position.Absolute;
            ripple.style.left = position.x - size / 2; ripple.style.top = position.y - size / 2;
            ripple.style.width = ripple.style.height = size; Radius(ripple, size / 2); Border(ripple, 2, view.C("var(--a1)"));
            ripple.style.scale = new Scale(Vector2.one * .25f); ripple.style.opacity = .9f;
            GalaxyFilters.Shadow(ripple, 0, 0, 6, view.C("var(--a1)")); layer.Add(ripple);
            float started = Time.realtimeSinceStartup;
            ripple.schedule.Execute(() =>
            {
                float t = Mathf.Clamp01((Time.realtimeSinceStartup - started) / .45f);
                float ease = CubicBezier(.2f, .8f, .3f, 1, t);
                ripple.style.scale = new Scale(Vector2.one * Mathf.Lerp(.25f, 1, ease));
                ripple.style.opacity = .9f * (1 - ease);
                if (t >= 1) ripple.RemoveFromHierarchy();
            }).Every(0);
        }

        // ---------- forms (isForm: right 70, top/bottom 46, 470 wide, padding 24 30, gap 12) ----------
        private const float Gap = 12;
        private readonly System.Collections.Generic.HashSet<string> tried = new System.Collections.Generic.HashSet<string>();
        private System.Collections.Generic.Dictionary<string, string> Errors => tried.Contains(screen) ? FieldErrors() : new System.Collections.Generic.Dictionary<string, string>();

        private VisualElement FormPanel(string title, string sub)
        {
            var panel = Panel(46, 46, 24, 30);
            var head = Row(); head.style.marginBottom = 4;
            var back = Box("auth-back"); back.style.width = back.style.height = 42; back.style.alignItems = Align.Center; back.style.justifyContent = Justify.Center; back.style.marginRight = 12; back.style.flexShrink = 0;
            view.Decorate(back, "var(--pnH)", 13, 4, "var(--pnS)"); back.Add(view.Icon("back", 18, "var(--ic)")); back.tooltip = L("Geri", "Back");
            AuthHover(back, Bright("var(--pnH)", 1.25f), 13); Tap(back, () => Go("choice")); head.Add(back);
            var words = Box(); words.style.flexShrink = 1;
            words.Add(Line(title, 26, 1.1f, "Montserrat-700", "#F4F1E8"));
            var s = Text(sub, 13, "Baloo2-700", "var(--mt)"); s.style.whiteSpace = WhiteSpace.Normal; s.style.marginTop = 2; words.Add(s); head.Add(words);
            panel.Add(head);
            return panel;
        }

        /// <summary>Adds <paramref name="child"/> to the form with the panel's 12 px flex gap.</summary>
        private static T Add<T>(VisualElement panel, T child) where T : VisualElement
        {
            if (panel.childCount > 0) child.style.marginTop = Gap;
            panel.Add(child); return child;
        }

        /// <summary>
        /// One field (column, gap 6): the caption row (12 px, +0.6 letter-spacing) with its hint on the right (red error /
        /// green "Kullanılabilir"), a 50 px box (padding 0 8 0 14, radius 14) ringed red on error, gold when focused, and
        /// the eye (34 px) or the green tick.
        /// </summary>
        private TextField Field(VisualElement panel, string key, string label, string name, string value, string placeholder, bool secret, Action<string> changed, int maxLength = 64, bool ok = false, string goodHint = null, bool showEye = true)
        {
            var errors = Errors; errors.TryGetValue(key, out var hint);
            var column = Box("auth-field-" + key); column.style.flexShrink = 0;
            // The browser's normal line height for Baloo 2 at 12px is 18 2/3px.
            const float labelHeight = 56f / 3f;
            var top = Row(); top.style.justifyContent = Justify.SpaceBetween; top.style.height = labelHeight;
            var cap = MenuView.CssLine(Text(label, 12, "Baloo2-800", "var(--mt)"), labelHeight); MenuView.Tracking(cap, 0.6f, 12); top.Add(cap);
            string shown = hint ?? goodHint;
            if (shown != null) { var h = MenuView.CssLine(Text(shown, 12, "Baloo2-800", hint != null ? "#FF8A94" : "#8EF0B0"), labelHeight); h.name = "auth-hint-" + key; top.Add(h); }
            column.Add(top);
            var boxRow = Row(); boxRow.name = "auth-box-" + key; boxRow.style.height = 50; boxRow.style.marginTop = 6; Pad(boxRow, 0, 8, 0, 14); view.Decorate(boxRow, "var(--dp)", 14);
            // ring: error inset 2px #FF5E6C; focus inset 2px --a1 + 0 0 0 4px rgba(255,201,74,.12); rest inset 0 2px 0 — no layout.
            var inset = view.InsetRing(boxRow, 14); var halo = GalaxyFilters.SpreadRing(boxRow, 4);
            void Ring(bool focus)
            {
                inset(hint != null ? "bad" : focus ? "focus" : "rest");
                halo(hint == null && focus ? 4 : 0, new Color(1, 201 / 255f, 74 / 255f, .12f));
            }
            Ring(false);
            var field = new TextField { name = name, value = value, maxLength = maxLength, isPasswordField = secret && !showPassword };
            field.style.flexGrow = 1; field.style.height = 50; field.style.marginLeft = field.style.marginRight = field.style.marginTop = field.style.marginBottom = 0;
            field.textEdition.placeholder = placeholder; field.textEdition.hidePlaceholderOnFocus = false;
            var input = field.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                input.style.backgroundColor = Color.clear; Border(input, 0, Color.clear); Pad(input, 0, 0, 0, 0);
                input.style.unityFont = view.Font("Baloo2-700"); input.style.fontSize = 16;
            }
            view.InputLook(field, "#F4F1E8");
            field.RegisterValueChangedCallback(evt =>
            {
                changed(evt.newValue);
                // setF(): any edit clears the banner error; after a tried submit the hints follow the typing (the box
                // redraws only when an error appears or clears).
                if (error != null) { error = null; Render(); }
                else if (tried.Contains(screen) && (FieldErrors().ContainsKey(key) != (hint != null))) Render();
            });
            field.RegisterCallback<FocusInEvent>(_ => Ring(true));
            field.RegisterCallback<FocusOutEvent>(_ => Ring(false));
            field.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { evt.StopPropagation(); Submit(); } }, TrickleDown.TrickleDown);
            boxRow.Add(field);
            if (secret && showEye)
            {
                var eye = Box("auth-eye"); eye.style.width = eye.style.height = 34; eye.style.flexShrink = 0; eye.style.alignItems = Align.Center; eye.style.justifyContent = Justify.Center; Radius(eye, 10);
                eye.style.marginLeft = 10; eye.style.opacity = showPassword ? 1 : .55f; eye.Add(view.Icon("v4/form-field-eye", 18, "var(--ic)")); eye.tooltip = showPassword ? L("Gizle", "Hide") : L("Göster", "Show");
                eye.RegisterCallback<PointerEnterEvent>(_ => eye.style.backgroundColor = view.C("var(--pnH)")); eye.RegisterCallback<PointerLeaveEvent>(_ => eye.style.backgroundColor = Color.clear);
                Tap(eye, () => { showPassword = !showPassword; error = null; Render(); }); boxRow.Add(eye);
            }
            if (ok) boxRow.Add(MatchTick(key));
            column.Add(boxRow);
            Add(panel, column);
            return field;
        }

        private VisualElement MatchTick(string key)
        {
            var tick = Box("auth-ok-" + key); tick.style.width = tick.style.height = 22; tick.style.marginLeft = 10; tick.style.marginRight = 6; tick.style.flexShrink = 0; tick.style.alignItems = Align.Center; tick.style.justifyContent = Justify.Center;
            view.Decorate(tick, "#3CCB6E", 11); tick.Add(view.Icon("tick", 12, "#fff")); return tick;
        }

        /// <summary>Şifre gücü: length ≥ 8, an upper-case letter, a digit, a symbol → Zayıf / Orta / Güçlü on three 5 px bars.</summary>
        private void Meter(VisualElement column)
        {
            if (string.IsNullOrEmpty(password)) return;
            int score = (password.Length >= 8 ? 1 : 0) + (password.Any(c => char.IsUpper(c)) ? 1 : 0) + (password.Any(char.IsDigit) ? 1 : 0) + (password.Any(c => !char.IsLetterOrDigit(c)) ? 1 : 0);
            int level = score <= 1 ? 1 : score <= 2 ? 2 : 3;
            string color = level == 1 ? "#FF5E6C" : level == 2 ? "#FFC94A" : "#3CCB6E";
            var row = Row(); row.name = "auth-strength"; row.style.marginTop = 6;
            for (int b = 1; b <= 3; b++) { var bar = Box(); bar.style.flexGrow = 1; bar.style.height = 5; bar.style.marginRight = 6; view.Decorate(bar, b <= level ? color : "rgba(255,255,255,.1)", 3); row.Add(bar); }
            var label = Text(level == 1 ? L("Zayıf", "Weak") : level == 2 ? L("Orta", "Medium") : L("Güçlü", "Strong"), 11, "Baloo2-800", color); label.name = "auth-strength-label";
            label.style.minWidth = 54; label.style.unityTextAlign = TextAnchor.MiddleRight; row.Add(label);
            column.Add(row);
        }

        /// <summary>Beni hatırla: 42 × 24 switch (knob 18 at 3 / 21 px), green when on.</summary>
        private VisualElement Switch42(string name, bool on, string text, Action flip)
        {
            var row = Row(); row.name = name;
            var track = Box(name + "-track"); track.style.width = 42; track.style.height = 24; track.style.flexShrink = 0; track.AddToClassList("auth-switch-track"); view.Decorate(track, on ? "#3CCB6E" : "var(--dp)", 12);
            var inset = Fill("auth-remember-inset"); Radius(inset, 12); inset.style.borderTopWidth = 2; inset.style.borderTopColor = new Color(0, 0, 0, .25f); track.Add(inset);
            var knob = Box(name + "-thumb"); MenuView.Place(knob, 3, 3, 18, 18); knob.AddToClassList("auth-switch-thumb"); knob.style.translate = new Translate(on ? 18 : 0, 0); view.Decorate(knob, "#fff", 9); track.Add(knob);
            row.Add(track); var label = Text(text, 14, "Baloo2-700", "var(--ic)"); label.style.marginLeft = 10; row.Add(label);
            Tap(row, () => { on = !on; flip(); track.style.backgroundColor = view.C(on ? "#3CCB6E" : "var(--dp)"); knob.style.translate = new Translate(on ? 18 : 0, 0); }); return row;
        }

        private VisualElement Cta(string name, string text)
        {
            var cta = Row(); cta.name = name; cta.style.height = 58; cta.style.justifyContent = Justify.Center; cta.style.overflow = Overflow.Hidden; cta.style.flexShrink = 0;
            view.Decorate(cta, "linear-gradient(var(--a1),var(--a2))", 18, 5, "var(--aS)"); cta.style.opacity = busy ? .85f : 1;
            if (busy)
            {
                var ring = Box(); ring.style.width = ring.style.height = 20; ring.style.marginRight = 10; Radius(ring, 10); Border(ring, 3, view.C("var(--aT)")); ring.style.borderTopColor = Color.clear;
                Animate(() => ring.style.rotate = new Rotate(Mathf.Repeat(Time.realtimeSinceStartup / .8f, 1) * 360)); cta.Add(ring);
            }
            cta.Add(Text(text, 19, "Montserrat-800", "var(--aT)"));
            AuthHover(cta, "linear-gradient(" + Bright("var(--a1)", 1.07f) + "," + Bright("var(--a2)", 1.07f) + ")", 18);
            TapRaised(cta, Submit, 5, "var(--aS)"); return cta;
        }

        private VisualElement Switch(string question, string link, string target)
        {
            var row = Row(); row.style.justifyContent = Justify.Center;
            row.Add(Text(question, 14, "Baloo2-700", "var(--mt)"));
            if (!string.IsNullOrEmpty(link)) { var go = Text(link, 14, "Baloo2-800", "var(--a1)"); go.name = "auth-switch-" + target; go.style.marginLeft = 6; Tap(go, () => Go(target)); row.Add(go); }
            return row;
        }

        private void Spacer(VisualElement panel) { var spacer = Box(); spacer.style.flexGrow = 1; Add(panel, spacer); }
        private static VisualElement Grow() { var g = Box(); g.style.flexGrow = 1; return g; }

        private void BuildLogin()
        {
            var panel = FormPanel(L("Giriş yap", "Log in"), L("Hesabınla kaldığın yerden devam et", "Pick up where you left off"));
            Add(panel, GoogleButton(50, 16, 4, 20, 16));
            var or = Divider(L("ya da e-postayla", "or with e-mail")); or.style.marginBottom = 0; Add(panel, or);
            Field(panel, "email", L("E-POSTA", "EMAIL"), "auth-email", email, "ornek@mail.com", false, v => email = v);
            Field(panel, "pass", L("ŞİFRE", "PASSWORD"), "auth-password", password, L("En az 6 karakter", "At least 6 characters"), true, v => password = v, 40);
            var options = Row();
            options.Add(Switch42("auth-remember", remember, L("Beni hatırla", "Remember me"), () => { remember = !remember; if (error != null) { error = null; Render(); } }));
            options.Add(Grow());
            var forgot = Text(L("Şifremi unuttum", "Forgot password"), 14, "Baloo2-800", "var(--a1)"); forgot.name = "auth-forgot";
            Tap(forgot, () => { resetSent = false; resetCode = ""; password = password2 = ""; Go("reset"); });
            options.Add(forgot); Add(panel, options);
            AddError(panel);
            Spacer(panel);
            Add(panel, Cta("auth-submit", L("Giriş yap", "Log in")));
            Add(panel, Switch(L("Hesabın yok mu?", "No account yet?"), L("Kayıt ol", "Sign up"), "register"));
        }

        /// <summary>
        /// Şifremi unuttum (spec/18 S03): the account's e-mail gets a six-digit code; the code and a new password sign in
        /// (every other device is signed out).
        /// </summary>
        private void BuildReset()
        {
            var panel = FormPanel(L("Şifreni sıfırla", "Reset your password"), resetSent
                ? L("E-postana gelen 6 haneli kodu ve yeni şifreni yaz", "Enter the 6-digit code from your e-mail and a new password")
                : L("Hesabının e-postasını yaz, sıfırlama kodu gönderelim", "Enter your account's e-mail and we'll send a reset code"));
            Field(panel, "email", L("E-POSTA", "EMAIL"), "auth-email", email, "ornek@mail.com", false, v => { email = v; if (resetSent) { resetSent = false; Render(); } });
            if (resetSent)
            {
                Field(panel, "code", L("KOD", "CODE"), "auth-reset-code", resetCode, "123456", false, v => resetCode = v, 6);
                Field(panel, "pass", L("YENİ ŞİFRE", "NEW PASSWORD"), "auth-password", password, L("En az 6 karakter", "At least 6 characters"), true, v => password = v, 40);
                Field(panel, "pass2", L("YENİ ŞİFRE (TEKRAR)", "NEW PASSWORD (AGAIN)"), "auth-password2", password2, L("Aynı şifre", "Same password"), true, v => password2 = v, 40, showEye: false);
                var again = Text(L("Kodu yeniden gönder", "Send the code again"), 14, "Baloo2-800", "var(--a1)"); again.name = "auth-reset-again";
                Tap(again, () => { resetSent = false; Submit(); });
                var row = Row(); row.Add(Grow()); row.Add(again); Add(panel, row);
            }
            AddError(panel);
            Spacer(panel);
            Add(panel, Cta("auth-submit", resetSent ? L("Şifreyi değiştir", "Change password") : L("Kod gönder", "Send code")));
            Add(panel, Switch(L("Şifreni hatırladın mı?", "Remembered it?"), L("Giriş yap", "Log in"), "login"));
        }

        private void BuildRegister()
        {
            var panel = FormPanel(L("Kayıt ol", "Sign up"), L("Hesap aç, 25.000 çip hediye", "Create an account, get 25,000 chips"));
            bool nameOk = NameRule.IsMatch(name.Trim());
            Field(panel, "name", L("KULLANICI ADI", "USERNAME"), "auth-name", name, L("3–16 karakter", "3–16 characters"), false, v => name = v, 16, ok: nameOk, goodHint: nameOk ? L("Kullanılabilir", "Available") : null);
            Field(panel, "email", L("E-POSTA", "EMAIL"), "auth-email", email, "ornek@mail.com", false, v => email = v);
            Field(panel, "pass", L("ŞİFRE", "PASSWORD"), "auth-password", password, L("En az 6 karakter", "At least 6 characters"), true, v => { password = v; UpdateMeter(panel); }, 40);
            Meter(panel.Q("auth-field-pass"));
            Field(panel, "pass2", L("ŞİFRE TEKRAR", "REPEAT PASSWORD"), "auth-password2", password2, L("Şifreni tekrar yaz", "Type your password again"), true, v => { password2 = v; UpdateRepeatTick(panel); }, 40, ok: password2.Length > 0 && password2 == password, showEye: false);
            // Terms: a 22 px checkbox (radius 7), gold when ticked, red ring once a submit was tried without it.
            var agree = Row(); agree.name = "auth-terms"; agree.style.alignItems = Align.FlexStart;
            var check = Box(); check.style.width = check.style.height = 22; check.style.flexShrink = 0; check.style.marginTop = 1; check.style.alignItems = Align.Center; check.style.justifyContent = Justify.Center;
            view.Decorate(check, "var(--dp)", 7);
            var fill = Fill("auth-terms-fill"); fill.AddToClassList("auth-terms-fill"); fill.style.opacity = terms ? 1 : 0;
            fill.style.alignItems = Align.Center; fill.style.justifyContent = Justify.Center; view.Decorate(fill, "linear-gradient(var(--a1),var(--a2))", 7);
            fill.Add(view.Icon("tick", 13, "var(--aT)")); check.Add(fill);
            var ring = Fill("auth-terms-ring"); Radius(ring, 7); Border(ring, 2, view.C(tried.Contains("register") && !terms ? "#FF5E6C" : "var(--bd)")); ring.style.opacity = terms ? 0 : 1; check.Add(ring);
            agree.Add(check);
            var agreeText = Text(L("Kullanım Koşulları’nı ve Gizlilik Politikası’nı okudum, kabul ediyorum.", "I have read and accept the Terms of Use and Privacy Policy."), 13, "Baloo2-700", terms ? "var(--ic)" : "var(--mt)");
            // 700 13px/1.45, text-wrap: pretty (the last line never holds one word alone).
            MenuView.CssParagraph(agreeText, 13 * 1.45f, true); agreeText.style.flexGrow = 1; agreeText.style.flexShrink = 1; agreeText.style.flexBasis = 0; agreeText.style.marginLeft = 10; agree.Add(agreeText);
            Tap(agree, () =>
            {
                terms = !terms;
                if (error != null) { error = null; Render(); return; }
                fill.style.opacity = terms ? 1 : 0; ring.style.opacity = terms ? 0 : 1;
                Border(ring, 2, view.C(tried.Contains("register") && !terms ? "#FF5E6C" : "var(--bd)"));
                agreeText.style.color = view.C(terms ? "var(--ic)" : "var(--mt)");
            }); Add(panel, agree);
            AddError(panel);
            Spacer(panel);
            Add(panel, Cta("auth-submit", L("Hesabı oluştur", "Create account")));
            Add(panel, Switch(L("Zaten hesabın var mı?", "Already have an account?"), L("Giriş yap", "Log in"), "login"));
        }

        private void UpdateMeter(VisualElement panel)
        {
            var column = panel.Q("auth-field-pass"); if (column == null) return;
            column.Q("auth-strength")?.RemoveFromHierarchy(); Meter(column);
            UpdateRepeatTick(panel);
        }

        private void UpdateRepeatTick(VisualElement panel)
        {
            var box = panel.Q<VisualElement>("auth-box-pass2"); if (box == null) return;
            var tick = box.Q<VisualElement>("auth-ok-pass2"); bool same = password2.Length > 0 && password2 == password;
            if (same && tick == null) box.Add(MatchTick("pass2"));
            else if (!same && tick != null) tick.RemoveFromHierarchy();
        }

        private void BuildGuest()
        {
            var panel = FormPanel(L("Misafir girişi", "Guest login"), L("İlerlemen bu cihazda saklanır", "Your progress is saved on this device"));
            Field(panel, "nick", L("TAKMA AD", "NICKNAME"), "auth-nick", nick, L("Masada görünecek adın", "Name shown at the table"), false, v => { nick = v; var ini = panel.Q<Label>("auth-avatar-ini"); if (ini != null) ini.text = LobbyScreen.Initials(nick); }, 16);
            var section = Box(); section.name = "auth-colors";
            var cap = Text(L("AVATAR RENGİ", "AVATAR COLOR"), 12, "Baloo2-800", "var(--mt)"); MenuView.Tracking(cap, 0.6f, 12); section.Add(cap);
            var row = Row(); row.style.marginTop = 8;
            // 74 px content + 3 px gold border (content-box: 80 px), a 3 px --pnS ring and a 22 px --gl glow outside it.
            var holder = Box(); holder.style.width = holder.style.height = 80; holder.style.flexShrink = 0; holder.style.marginRight = 16;
            var halo = Box(); halo.style.position = Position.Absolute; halo.style.left = halo.style.top = -3; halo.style.width = halo.style.height = 86;
            view.Decorate(halo, "var(--pnS)", 43); GalaxyFilters.Shadow(halo, 0, 0, 11, view.C("var(--gl)")); holder.Add(halo);
            var avatar = Box("auth-avatar"); avatar.style.position = Position.Absolute; avatar.style.left = avatar.style.top = 0; avatar.style.width = avatar.style.height = 80; avatar.style.alignItems = Align.Center; avatar.style.justifyContent = Justify.Center;
            view.Decorate(avatar, color, 40); Border(avatar, 3, view.C("var(--a1)"));
            var ini = Text(LobbyScreen.Initials(nick), 26, "Montserrat-700", "#fff"); ini.name = "auth-avatar-ini"; avatar.Add(ini); holder.Add(avatar); row.Add(holder);
            var grid = Box(); grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap; grid.style.width = 4 * 30 + 3 * 10;
            for (int k = 0; k < Colors.Length; k++)
            {
                string pick = Colors[k]; bool on = pick == color;
                var dot = Box("auth-color-" + pick.Substring(1)); dot.pickingMode = PickingMode.Position; dot.style.width = dot.style.height = 30; dot.style.flexShrink = 0;
                if (k % 4 != 3) dot.style.marginRight = 10; if (k >= 4) dot.style.marginTop = 10;
                // box-shadow rings: picked "0 0 0 3px --pn, 0 0 0 5px --a1", others "0 0 0 1px rgba(255,255,255,.15)".
                VisualElement Ring(float spread, string fill) { var r = Box(); r.style.position = Position.Absolute; r.style.left = r.style.top = -spread; r.style.width = r.style.height = 30 + 2 * spread; view.Decorate(r, fill, Mathf.RoundToInt(15 + spread)); return r; }
                if (on) { dot.Add(Ring(5, "var(--a1)")); dot.Add(Ring(3, "var(--pn)")); } else dot.Add(Ring(1, "rgba(255,255,255,.15)"));
                var fill = Box(); fill.style.position = Position.Absolute; fill.style.left = fill.style.top = 0; fill.style.width = fill.style.height = 30; view.Decorate(fill, pick, 15); dot.Add(fill);
                dot.RegisterCallback<PointerEnterEvent>(_ => dot.style.scale = new Scale(Vector2.one * 1.12f)); dot.RegisterCallback<PointerLeaveEvent>(_ => dot.style.scale = new Scale(Vector2.one));
                Tap(dot, () => { color = pick; error = null; Render(); }); grid.Add(dot);
            }
            row.Add(grid); section.Add(row);
            var notes = Box(); notes.style.marginTop = 10 + 8; Pad(notes, 14, 16, 14, 16); view.Decorate(notes, "var(--sd)", 16);
            var chips = Row(); chips.style.alignItems = Align.Center;
            var coin = view.ChipIcon(22); coin.style.marginRight = 10; chips.Add(coin);
            var c1 = Text(L("Başlangıçta 10.000 çip hesabına yüklenir.", "You start with 10,000 chips."), 14, "Baloo2-700", "var(--ic)"); MenuView.CssParagraph(c1, 14 * 1.35f, false); c1.style.flexShrink = 1; chips.Add(c1); notes.Add(chips); // 700 14px/1.35
            var device = Row(); device.style.marginTop = 10;
            var phone = view.Icon("phone", 22, "var(--mt)"); phone.style.marginRight = 10; device.Add(phone);
            var c2 = Text(L("Uygulamayı silersen ya da cihaz değiştirirsen ilerlemen kaybolur.", "If you delete the app or switch devices, your progress is lost."), 14, "Baloo2-700", "var(--ic)"); MenuView.CssParagraph(c2, 14 * 1.35f, false); c2.style.flexShrink = 1; device.Add(c2); // 700 14px/1.35
            notes.Add(device); section.Add(notes);
            Add(panel, section);
            AddError(panel);
            Spacer(panel);
            Add(panel, Cta("auth-submit", L("Oynamaya başla", "Start playing")));
            Add(panel, Switch(L("Sonra hesaba bağlayabilirsin.", "You can link an account later."), null, null));
        }

        /// <summary>Server answer: a red box (padding 11 14, radius 14) with a 22 px "!" — shakes .4 s when it appears.</summary>
        private void AddError(VisualElement panel)
        {
            if (string.IsNullOrEmpty(error)) return;
            var box = Row(); box.name = "auth-error-box"; Pad(box, 11, 14, 11, 14); view.Decorate(box, "rgba(255,94,108,.12)", 14); Border(box, 1, new Color(1, .37f, .42f, .35f));
            var mark = Text("!", 13, "Montserrat-800", "#fff"); mark.style.width = mark.style.height = 22; mark.style.flexShrink = 0; mark.style.unityTextAlign = TextAnchor.MiddleCenter; view.Decorate(mark, "#FF5E6C", 11); box.Add(mark);
            var text = Text(error, 14, "Baloo2-700", "#FFC2C8"); text.name = "auth-error"; text.style.whiteSpace = WhiteSpace.Normal; text.style.flexShrink = 1; text.style.marginLeft = 10; box.Add(text);
            float at = errorAt;
            Animate(() => { float t = (Time.realtimeSinceStartup - at) / .4f; box.style.translate = new Translate(t >= 1 || t < 0 ? 0 : 7 * Mathf.Sin(t * Mathf.PI * 4), 0); });
            Add(panel, box);
        }
        private float errorAt;

        // ---------- submit ----------
        private static readonly Regex NameRule = new Regex("^[\\p{L}0-9_ ]{3,16}$");

        public async void Submit()
        {
            if (busy) return;
            tried.Add(screen);
            var problems = FieldErrors();
            if (problems.Count > 0)
            {
                error = problems.Count == 1 && problems.ContainsKey("terms") ? problems["terms"] : null; errorAt = Time.realtimeSinceStartup;
                Render(); return;
            }
            busy = true; error = null; Render();
            try
            {
                if (screen == "reset" && !resetSent)
                {
                    await online.Api.PostAsync<Newtonsoft.Json.Linq.JObject>("/v1/auth/password/forgot", new PasswordForgotRequest { Email = email.Trim() });
                    busy = false; resetSent = true; resetCode = ""; password = password2 = ""; tried.Remove("reset"); Render();
                    return;
                }
                if (screen == "reset")
                {
                    await online.Api.PostAsync<Newtonsoft.Json.Linq.JObject>("/v1/auth/password/reset", new PasswordResetRequest { Email = email.Trim(), Code = resetCode.Trim(), Password = password });
                    await online.LoginAsync(email.Trim(), password, remember);
                }
                else if (screen == "login") await online.LoginAsync(email.Trim(), password, remember);
                else if (screen == "register") await online.RegisterAsync(new RegisterRequest { Name = name.Trim(), Email = email.Trim(), Password = password, AcceptTerms = terms, Color = color });
                else if (screen == "guest") await online.GuestAsync(nick.Trim(), color);
                busy = false;
                string who = (online.Profile?.Name ?? nick ?? "").Trim().Split(' ')[0];
                if (screen == "reset") Finish(L("Şifren değişti. Hoş geldin, " + who + "!", "Password changed. Welcome, " + who + "!"), "ok");
                else if (screen == "login") Finish(L("Hoş geldin, " + who + "!", "Welcome, " + who + "!"), "ok");
                else Finish(L("Hoş geldin, " + who + "! " + view.N(online.Wallet?.Chips ?? 0) + " çip hesabına eklendi", "Welcome, " + who + "! " + view.N(online.Wallet?.Chips ?? 0) + " chips added"), "gold");
            }
            catch (ApiException e)
            {
                busy = false; error = Explain(e); errorAt = Time.realtimeSinceStartup; Render();
            }
            catch (Exception e)
            {
                // Never a silent failure: whatever went wrong is shown and logged.
                Debug.LogException(e);
                busy = false; error = L("Bir şeyler ters gitti. Tekrar dene.", "Something went wrong. Try again."); Render();
            }
        }

        /// <summary>The design's validate(scr): field → message (shown as the field's hint once a submit was tried).</summary>
        private System.Collections.Generic.Dictionary<string, string> FieldErrors()
        {
            var e = new System.Collections.Generic.Dictionary<string, string>();
            bool mail = Regex.IsMatch(email.Trim(), "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");
            switch (screen)
            {
                case "login":
                    if (!mail) e["email"] = L("Geçerli bir e-posta yaz", "Enter a valid email");
                    if (password.Length < 6) e["pass"] = L("En az 6 karakter", "At least 6 characters");
                    break;
                case "register":
                    if (!NameRule.IsMatch(name.Trim())) e["name"] = L("3–16 harf, rakam ya da _", "3–16 letters, digits or _");
                    if (!mail) e["email"] = L("Geçerli bir e-posta yaz", "Enter a valid email");
                    if (password.Length < 6) e["pass"] = L("En az 6 karakter", "At least 6 characters");
                    if (password2 != password) e["pass2"] = L("Şifreler aynı değil", "Passwords don’t match");
                    if (!terms) e["terms"] = L("Devam etmek için koşulları kabul et", "Accept the terms to continue");
                    break;
                case "guest":
                    if (!NameRule.IsMatch(nick.Trim())) e["nick"] = L("3–16 harf, rakam ya da _", "3–16 letters, digits or _");
                    break;
                case "reset":
                    if (!mail) e["email"] = L("Geçerli bir e-posta yaz", "Enter a valid email");
                    if (!resetSent) break;
                    if (!Regex.IsMatch(resetCode.Trim(), "^[0-9]{6}$")) e["code"] = L("6 haneli kod", "The 6-digit code");
                    if (password.Length < 6) e["pass"] = L("En az 6 karakter", "At least 6 characters");
                    if (password2 != password) e["pass2"] = L("Şifreler aynı değil", "Passwords don’t match");
                    break;
            }
            return e;
        }

        private string Explain(ApiException e)
        {
            if (e.Code == "insecure_server") return L("Sunucu adresi güvenli değil (https gerekli).", "The server address is not secure (https required).");
            if (e.Offline) return L("Sunucuya bağlanılamadı. İnternetini kontrol edip yeniden dene.", "Could not reach the server. Check your connection and try again.");
            switch (e.Code)
            {
                case ErrorCodes.AccountNotFound: return L("Bu e-postayla kayıtlı hesap bulunamadı.", "No account with this e-mail.");
                case ErrorCodes.WrongPassword:
                    int left = (int?)e.Body?["remainingAttempts"] ?? 0;
                    return L("Şifre hatalı. " + left + " deneme hakkın kaldı.", "Wrong password. " + left + " tries left.");
                case ErrorCodes.AccountLocked:
                    int wait = (int?)e.Body?["retryAfter"] ?? 0;
                    return L("Çok fazla deneme. " + Mathf.CeilToInt(wait / 60f) + " dakika sonra tekrar dene.", "Too many tries. Try again in " + Mathf.CeilToInt(wait / 60f) + " minutes.");
                case ErrorCodes.EmailTaken: return L("Bu e-postayla zaten hesap var", "An account with this e-mail already exists");
                case ErrorCodes.TermsRequired: return L("Devam etmek için koşulları kabul et", "Accept the terms to continue");
                case ErrorCodes.Validation: return L("Bilgileri kontrol et: ad 3–16 karakter, şifre en az 6.", "Check the details: name 3–16 characters, password at least 6.");
                case ErrorCodes.RateLimited: return L("Çok hızlı denedin; biraz bekle.", "Too many attempts; wait a moment.");
                case ErrorCodes.CodeInvalid: return L("Kod hatalı ya da süresi doldu. Yeni kod iste.", "The code is wrong or expired. Ask for a new one.");
                case ErrorCodes.EmailUnavailable: return L("Şu an e-posta gönderilemiyor. Biraz sonra tekrar dene.", "E-mail cannot be sent right now. Try again later.");
                case ErrorCodes.Inappropriate: return L("Bu ad uygun değil; başka bir ad seç.", "This name is not allowed; pick another one.");
                case ErrorCodes.Banned:
                    int hours = Mathf.CeilToInt(((int?)e.Body?["retryAfter"] ?? 0) / 3600f);
                    return L("Hesabın askıya alındı (" + hours + " saat).", "Your account is suspended (" + hours + " h).");
                default: return L("Bir şeyler ters gitti (" + e.Code + ").", "Something went wrong (" + e.Code + ").");
            }
        }

        // ---------- little helpers ----------
        /// <summary>The menu's GALAXY artwork for the theme (gradient letters), scaled to the given cap height.</summary>
        private VisualElement Logo(int size, float tracking)
        {
            var ids = new[] { "saturn", "mars", "neptune", "jupiter", "venus" };
            int index = Mathf.Max(0, Array.IndexOf(new[] { "Satürn", "Mars", "Neptün", "Jüpiter", "Venüs" }, view.Model.Planet));
            var art = Resources.Load<Texture2D>("Galaxy101/Artwork/logo-" + ids[index]);
            if (art == null) { var text = Text("GALAXY", size, "Montserrat-700", "var(--a1)"); MenuView.Tracking(text, tracking, size); return text; }
            float scale = size / 66f * .5f;
            var image = new Image { image = art, pickingMode = PickingMode.Ignore, name = "auth-logo" };
            image.style.width = art.width * scale; image.style.height = art.height * scale; image.style.flexShrink = 0;
            return image;
        }

        /// <summary>
        /// Where the GALAXY artwork sits inside a CSS line of <paramref name="lineHeight"/> for a <paramref name="size"/> px
        /// font: the artwork's letters (rows 105–207 of 308 at 2×) start where Montserrat's cap top would.
        /// </summary>
        private static float LogoCapOffset(float size, float lineHeight)
        {
            float contentTop = (lineHeight - size * 1.219f) / 2;
            float capTop = contentTop + (0.968f - 0.700f) * size;
            return capTop - 105 * (size / 66f * .5f);
        }

        /// <summary>
        /// The auth side tiles (isAuthSide): 70×96 / 79×108 / 70×96, −10° / 2° / 10°, gap 11, bottom-aligned in a 120 px row;
        /// g101TileIn 1.1 s (delays .12 / .26 / .40, cubic-bezier(.16,1,.3,1)) then g101Float 6 s (−7 px, phases 0 / −2 / −4 s).
        /// </summary>
        private VisualElement SideTiles()
        {
            var row = Row(); row.style.alignItems = Align.FlexEnd; row.style.height = 120; row.style.marginTop = 8;
            float began = sideAt;
            int[] w = { 70, 79, 70 }, h = { 96, 108, 96 }, font = { 56, 66, 56 }, dot = { 12, 14, 12 }; float[] rot = { -10, 2, 10 }; string[] tint = { "t1", "t2", "t3" };
            for (int i = 0; i < 3; i++)
            {
                var tile = Box("auth-tile-" + i); tile.style.width = w[i]; tile.style.height = h[i]; tile.style.rotate = new Rotate(rot[i]); tile.style.flexShrink = 0;
                tile.style.alignItems = Align.Center; tile.style.paddingTop = 7; if (i > 0) tile.style.marginLeft = 11;
                view.Decorate(tile, "linear-gradient(var(--tf1),var(--tf2))", i == 1 ? 12 : 11, i == 1 ? 6 : 5, "var(--te2)");
                tile.style.borderBottomWidth = i == 1 ? 7 : 6; tile.style.borderBottomColor = view.C("var(--te)");
                // font Npx/1: the glyph's content box centred on the N px line (as the menu's logo tiles).
                var number = view.Text(i == 1 ? "0" : "1", font[i], true, "var(--" + tint[i] + ")"); number.style.unityTextAlign = TextAnchor.MiddleCenter; number.style.height = font[i]; number.pickingMode = PickingMode.Ignore; tile.Add(number);
                var pip = Box(); pip.style.width = pip.style.height = dot[i]; pip.style.marginTop = 3; view.Decorate(pip, "var(--" + tint[i] + ")", 99); tile.Add(pip);
                float delay = .12f + i * .14f, phase = i * 2f;
                Animate(() =>
                {
                    // Frozen (state router): every CSS animation sits at 100000 ms (g101-state.js freeze(), EKSIKLER EK-602).
                    float t = GameClock.Frozen ? (float)(GameClock.CanonMs / 1000) : Time.realtimeSinceStartup - began, u = Mathf.Clamp01((t - delay) / 1.1f), k = CubicBezier(.16f, 1, .3f, 1, u);
                    // g101Float 6 s cubic-bezier(.45,0,.55,1) on each half: translate 0 → -7px (50 %) → 0, delays 0 / -2 / -4 s.
                    float p = Mathf.Repeat(t + phase, 6) / 6, bob = -7 * (p < .5f ? CubicBezier(.45f, 0, .55f, 1, p * 2) : 1 - CubicBezier(.45f, 0, .55f, 1, p * 2 - 1));
                    tile.style.translate = new Translate(0, 46 * (1 - k) + bob); tile.style.opacity = u >= .55f ? 1 : CubicBezier(.16f, 1, .3f, 1, u / .55f); tile.style.scale = new Scale(Vector2.one * Mathf.Lerp(.82f, 1, k));
                });
                row.Add(tile);
            }
            return row;
        }

        /// <summary>skewX(deg) for a plain box: UI Toolkit has no skew, so a parallelogram is approximated by a thin rotated slab.</summary>
        private static void SkewX(VisualElement e, float degrees) { e.style.rotate = new Rotate(-degrees * 0.5f); e.style.scale = new Scale(new Vector2(1.1f, 1.3f)); }

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) evaluated at progress <paramref name="u"/>.</summary>
        public static float CubicBezier(float x1, float y1, float x2, float y2, float u)
        {
            if (u <= 0) return 0; if (u >= 1) return 1;
            float t = u;
            for (int i = 0; i < 8; i++)
            {
                float x = 3 * (1 - t) * (1 - t) * t * x1 + 3 * (1 - t) * t * t * x2 + t * t * t - u;
                float dx = 3 * (1 - t) * (1 - t) * x1 + 6 * (1 - t) * t * (x2 - x1) + 3 * t * t * (1 - x2);
                if (Mathf.Abs(dx) < 1e-5f) break; t = Mathf.Clamp01(t - x / dx);
            }
            return 3 * (1 - t) * (1 - t) * t * y1 + 3 * (1 - t) * t * t * y2 + t * t * t;
        }

        /// <summary>1 0 1 (78×108, 88×122, 78×108, −10° / 2° / 10°): tileIn 1.15 s staggered, then g101Float 6 s (−7 px).</summary>
        private VisualElement Tiles(float scale)
        {
            var row = Row(); row.style.alignItems = Align.FlexEnd; row.style.marginTop = 10 * scale; row.style.height = 136 * scale;
            float began = screen == "splash" ? splashStarted : sideAt;
            int[] w = { 78, 88, 78 }, h = { 108, 122, 108 }; float[] rot = { -10, 2, 10 }; string[] tint = { "t1", "t2", "t3" };
            VisualElement Face(int i)
            {
                var tile = Box("auth-tile-" + i); tile.style.width = w[i] * scale; tile.style.height = h[i] * scale; tile.style.rotate = new Rotate(rot[i]); tile.style.flexShrink = 0;
                tile.style.alignItems = Align.Center; tile.style.paddingTop = 8 * scale;
                view.Decorate(tile, "linear-gradient(var(--tf1),var(--tf2))", Mathf.RoundToInt((i == 1 ? 13 : 12) * scale), Mathf.RoundToInt((i == 1 ? 6 : 5) * scale), "var(--te2)");
                tile.style.borderBottomWidth = (i == 1 ? 8 : 7) * scale; tile.style.borderBottomColor = view.C("var(--te)");
                int numberSize = Mathf.RoundToInt((i == 1 ? 74 : 64) * scale);
                var number = MenuView.CssLine(view.Text(i == 1 ? "0" : "1", numberSize, true, "var(--" + tint[i] + ")"), numberSize); number.style.unityTextAlign = TextAnchor.MiddleCenter; number.pickingMode = PickingMode.Ignore; tile.Add(number);
                var dot = Box(); dot.style.width = dot.style.height = (i == 1 ? 16 : 14) * scale; dot.style.marginTop = 3; view.Decorate(dot, "var(--" + tint[i] + ")", 99); tile.Add(dot);
                return tile;
            }
            var flat = new VisualElement[3];
            for (int i = 0; i < flat.Length; i++) { flat[i] = Face(i); if (i > 0) flat[i].style.marginLeft = 12 * scale; row.Add(flat[i]); }
            VisualElement Spatial(int i)
            {
                var position = SplashTileMotion.Position(i); var size = LogoTileMotion.Size(i);
                var wrapper = Box("splash-tile-entry-" + i); wrapper.usageHints = UsageHints.DynamicTransform;
                MenuView.Place(wrapper, Mathf.Floor(position.x), Mathf.Floor(position.y), size.x, size.y);
                wrapper.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100), 0);
                var face = Face(i); face.name = "splash-tile-face-" + i; wrapper.Add(face); return wrapper;
            }
            void Apply(VisualElement wrapper, int i, float time)
            {
                var pose = SplashTileMotion.Sample(i, time); var position = SplashTileMotion.Position(i);
                wrapper.style.opacity = pose.Opacity;
                wrapper.style.translate = new Translate(position.x - Mathf.Floor(position.x), position.y - Mathf.Floor(position.y) + pose.Lift);
                wrapper.style.scale = new Scale(Vector3.one * pose.Scale); wrapper.style.rotate = new Rotate(Quaternion.AngleAxis(pose.Tilt, Vector3.right));
                wrapper.ElementAt(0).style.translate = new Translate(0, pose.Bob);
            }
            Animate(() =>
            {
                float time = GameClock.Frozen ? (float)(GameClock.CanonMs / 1000) : Time.realtimeSinceStartup - began;
                for (int i = 0; i < flat.Length; i++)
                {
                    var pose = SplashTileMotion.Sample(i, time);
                    flat[i].style.translate = new Translate(0, pose.Lift + pose.Bob); flat[i].style.opacity = pose.Opacity;
                    flat[i].style.scale = new Scale(Vector2.one * pose.Scale);
                }
                if (Application.isPlaying && time < SplashTileMotion.EntryEnd && splashPresentation == null)
                    splashPresentation = new LogoTilePresentation(view, row, flat, Spatial,
                        () => Application.isFocused || Application.isBatchMode,
                        new LogoTilePresentation.Configuration
                        {
                            Crop = new Rect(424, 160, 432, 336), HostOrigin = SplashTileMotion.RowOrigin,
                            PerspectiveOrigin = SplashTileMotion.PerspectiveOrigin, Perspective = SplashTileMotion.Perspective,
                            EntryEnd = SplashTileMotion.EntryEnd, Name = "Galaxy101 splash perspective", ImageName = "splash-perspective", Apply = Apply,
                        });
                splashPresentation?.Step(time);
            });
            return row;
        }

        // ---------- animation: one scheduler for the flow's layer ----------
        private readonly System.Collections.Generic.List<Action> motions = new System.Collections.Generic.List<Action>();
        private IVisualElementScheduledItem ticker;
        private void Animate(Action step)
        {
            motions.Add(step);
            if (ticker == null && layer != null) ticker = layer.schedule.Execute(() => { foreach (var m in motions.ToArray()) m(); }).Every(0);
        }

        private VisualElement Divider(string text)
        {
            // margin 4px 0 inside the panel's 12 px gap.
            var row = Row(); row.style.marginTop = 12 + 4; row.style.marginBottom = 4 + 12;
            var a = Box(); a.style.flexGrow = 1; a.style.height = 1; a.style.backgroundColor = new Color(1, 1, 1, .1f); row.Add(a);
            var label = Text(text, 12, "Baloo2-800", "var(--mt)"); label.style.marginLeft = label.style.marginRight = 12; row.Add(label);
            var b = Box(); b.style.flexGrow = 1; b.style.height = 1; b.style.backgroundColor = new Color(1, 1, 1, .1f); row.Add(b);
            return row;
        }

        /// <summary>A text on a CSS line box of <paramref name="size"/> × <paramref name="lineHeight"/> (glyphs centred in it, as in CSS).</summary>
        private Label Line(string text, int size, float lineHeight, string font, string color)
        {
            var label = Text(text, size, font, color); label.style.height = size * lineHeight; label.style.unityTextAlign = TextAnchor.MiddleLeft; label.style.overflow = Overflow.Visible; return label;
        }

        private Label Text(string text, int size, string font, string color)
        {
            var label = view.Text(text, size, false, color); label.style.unityFont = view.Font(font); label.style.whiteSpace = WhiteSpace.NoWrap; label.pickingMode = PickingMode.Ignore; return label;
        }

        private static VisualElement Box(string name = null) { var e = new VisualElement { pickingMode = PickingMode.Ignore }; if (name != null) e.name = name; return e; }
        private static VisualElement Row() { var r = Box(); r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.flexShrink = 0; return r; }
        private static VisualElement Fill(string name) { var e = Box(name); e.style.position = Position.Absolute; e.style.left = e.style.top = e.style.right = e.style.bottom = 0; return e; }
        private static void Pad(VisualElement e, float t, float r, float b, float l) { e.style.paddingTop = t; e.style.paddingRight = r; e.style.paddingBottom = b; e.style.paddingLeft = l; }
        private static void Radius(VisualElement e, float r) => e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        private static void Border(VisualElement e, float w, Color c)
        {
            e.style.borderTopWidth = e.style.borderRightWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = w;
            e.style.borderTopColor = e.style.borderRightColor = e.style.borderBottomColor = e.style.borderLeftColor = c;
        }
        private static void Tap(VisualElement e, Action action)
        {
            e.pickingMode = PickingMode.Position; e.focusable = true; GalaxyFilters.PressSink(e);
            e.RegisterCallback<ClickEvent>(evt => { action(); evt.StopPropagation(); });
        }
    }
}
