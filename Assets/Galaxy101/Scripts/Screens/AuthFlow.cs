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
        private static readonly string[] Colors = { "#FF8A5B", "#FF5E6C", "#E85D9A", "#8B5CF6", "#3D7BFF", "#16B6A8", "#3CCB6E", "#F5A623" };
        private readonly MenuView view;
        private readonly Transform owner;
        private readonly GalaxyOnline online;
        private VisualElement layer, body;
        private GalaxyBackground background;
        private string screen;
        private bool busy;
        private string error;
        // Form fields keep their values across rebuilds (language switch, error redraw).
        private string email = "", password = "", password2 = "", name = "", nick, color;
        private bool remember = true, terms, showPassword;
        private float splashStarted;

        /// <summary>Signed in (or resumed): the menu takes over.</summary>
        public event Action Completed;
        public bool Visible => layer != null;
        public string Screen => screen;
        public VisualElement Layer => layer;
        /// <summary>Minimum splash time (spec/18: the bar fills before the panel); tests shorten it.</summary>
        public float SplashSeconds = 2.4f;

        public AuthFlow(MenuView view, Transform owner, GalaxyOnline online)
        {
            this.view = view; this.owner = owner; this.online = online;
            nick = "Misafir" + UnityEngine.Random.Range(1000, 10000); color = Colors[UnityEngine.Random.Range(0, Colors.Length)];
        }

        private string L(string tr, string en) => view.L(tr, en);

        public async void Show()
        {
            var canvas = view.Root.Q<VisualElement>("galaxy-canvas") ?? view.Root;
            layer = Fill("auth-flow"); layer.pickingMode = PickingMode.Position; canvas.Add(layer);
            var backdrop = Fill("auth-background"); backdrop.pickingMode = PickingMode.Ignore; layer.Add(backdrop);
            background = new GameObject("Auth background").AddComponent<GalaxyBackground>(); background.transform.SetParent(owner, false);
            background.Initialize(backdrop);
            background.Configure(view.Model.Planet, (bool?)view.Model.Settings["motion"] ?? true, (string)view.Model.Settings["quality"] ?? "Yüksek");
            background.SetFocused(UnityEngine.Application.isFocused || UnityEngine.Application.isBatchMode);
            body = Fill("auth-body"); body.pickingMode = PickingMode.Ignore; layer.Add(body);
            screen = "splash"; splashStarted = Time.realtimeSinceStartup; Render();
            var resume = online.ResumeAsync();
            while (Time.realtimeSinceStartup - splashStarted < SplashSeconds || !resume.IsCompleted) await Task.Yield();
            bool resumed = false;
            try { resumed = resume.Result; } catch (Exception e) { Debug.LogWarning("Resume failed: " + e.Message); }
            if (layer == null) return;
            if (resumed) { Finish(L("Tekrar hoş geldin, ", "Welcome back, ") + online.Profile?.Name + "!"); return; }
            Go("choice");
        }

        public void Go(string next) { screen = next; error = null; Render(); }

        private void Finish(string toast)
        {
            Hide();
            if (!string.IsNullOrEmpty(toast)) view.Toast(toast);
            Completed?.Invoke();
        }

        public void Hide()
        {
            ticker?.Pause(); ticker = null; motions.Clear();
            layer?.RemoveFromHierarchy(); layer = null;
            if (background != null) UnityEngine.Object.Destroy(background.gameObject); background = null;
        }

        // ---------- layout ----------
        private void Render()
        {
            if (body == null) return;
            body.Clear(); motions.Clear();
            bool splash = screen == "splash";
            // Planet: centred for the splash (640,330), left for the auth screens (310,340 ×1.05) — spec/18.
            var at = splash ? new Vector4(640, 330, 1f, 0) : new Vector4(310, 340, 1.05f, 0);
            background.UseStaticLayout(new Vector4(640, 360, 1280, 720), new Vector4(at.x, at.y, 330, 1), at, view.C("var(--pg)"));
            if (splash) { BuildSplash(); return; }
            BuildSide();
            switch (screen)
            {
                case "choice": BuildChoice(); break;
                case "login": BuildLogin(); break;
                case "register": BuildRegister(); break;
                case "guest": BuildGuest(); break;
            }
        }

        private void BuildSplash()
        {
            var column = Box(); MenuView.Place(column, 0, 118, 1280, 300); column.style.alignItems = Align.Center; body.Add(column);
            column.Add(Logo(66, 8)); column.Add(Tiles(1f));
            var bar = Box("splash-progress"); MenuView.Place(bar, 420, 478, 440, 40); body.Add(bar);
            var labels = Row(); labels.style.justifyContent = Justify.SpaceBetween;
            var message = Text("", 14, "Baloo2-800", "var(--ic)"); var pct = Text("0%", 14, "Baloo2-800", "var(--a1)"); pct.name = "splash-percent";
            labels.Add(message); labels.Add(pct); bar.Add(labels);
            var track = Box(); track.style.height = 14; track.style.marginTop = 9; track.style.overflow = Overflow.Hidden; view.Decorate(track, "var(--dp)", 7);
            var fill = Box(); fill.style.position = Position.Absolute; fill.style.left = fill.style.top = fill.style.bottom = 0; view.Decorate(fill, "linear-gradient(90deg,var(--a2),var(--a1))", 7);
            GalaxyFilters.Shadow(fill, 0, 0, 7, view.C("var(--gl)")); track.Add(fill); bar.Add(track);
            var messages = new[] { L("Gezegenler hizalanıyor…", "Aligning planets…"), L("Taşlar diziliyor…", "Racking tiles…"), L("Masalar hazırlanıyor…", "Setting up tables…"), L("Neredeyse hazır…", "Almost ready…"), L("Hazır", "Ready") };
            Animate(() =>
            {
                float u = Mathf.Clamp01((Time.realtimeSinceStartup - splashStarted) / Mathf.Max(.01f, SplashSeconds)) * 100;
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
            tip.Add(Text(tips[UnityEngine.Random.Range(0, tips.Length)], 14, "Baloo2-700", "var(--ic)")); body.Add(tip);
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
            column.Add(Logo(60, 7)); column.Add(Tiles(.9f));
            var tag = Text(L("Masalar dolu, taşlar hazır.\nArkadaşlarınla otur, 101 açıp eli bitir.", "Tables are full, tiles are racked.\nSit with your friends, open 101 and finish."), 20, "Baloo2-700", "var(--ic)");
            tag.style.whiteSpace = WhiteSpace.Normal; tag.style.unityTextAlign = TextAnchor.UpperCenter; tag.style.maxWidth = 440; tag.style.marginTop = 120; column.Add(tag);
        }

        private VisualElement Panel(float top, float bottom, int padY, int padX)
        {
            var panel = Box("auth-panel"); panel.pickingMode = PickingMode.Position;
            panel.style.position = Position.Absolute; panel.style.right = 70; panel.style.top = top; panel.style.bottom = bottom; panel.style.width = 470;
            Pad(panel, padY, padX, padY, padX);
            view.Decorate(panel, "var(--pn)", 28, 10, "var(--pnS)"); Border(panel, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(panel, 0, 40, 45, new Color(0, 0, 0, .5f));
            float began = Time.realtimeSinceStartup;
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
            var welcome = Text(L("Hoş geldin", "Welcome"), 32, "Montserrat-700", "#F4F1E8"); welcome.style.marginTop = 10; panel.Add(welcome);
            var how = Text(L("Nasıl devam etmek istersin?", "How would you like to continue?"), 15, "Baloo2-700", "var(--mt)"); how.style.marginTop = 4; how.style.marginBottom = 14; panel.Add(how);

            var guest = Row(); guest.name = "auth-guest"; guest.style.height = 76; Pad(guest, 0, 20, 0, 20); guest.style.overflow = Overflow.Hidden;
            view.Decorate(guest, "linear-gradient(var(--a1),var(--a2))", 20);
            var gIcon = Box(); gIcon.style.width = gIcon.style.height = 44; gIcon.style.alignItems = Align.Center; gIcon.style.justifyContent = Justify.Center; gIcon.style.marginRight = 14;
            view.Decorate(gIcon, "rgba(255,255,255,.35)", 14); gIcon.Add(view.Icon("auth-guest", 24, "var(--aT)")); guest.Add(gIcon);
            var gWords = Box(); gWords.Add(Text(L("MİSAFİR OLARAK OYNA", "PLAY AS GUEST"), 21, "Montserrat-800", "var(--aT)"));
            var gSub = Text(L("Kayıt yok · hemen başla", "No sign-up · start now"), 13, "Baloo2-800", "var(--aT)"); gSub.style.opacity = .8f; gSub.style.marginTop = 3; gWords.Add(gSub); guest.Add(gWords);
            Tap(guest, () => Go("guest")); panel.Add(guest);

            var login = Row(); login.name = "auth-login"; login.style.height = 60; login.style.marginTop = 12; Pad(login, 0, 20, 0, 20); view.Decorate(login, "var(--pnH)", 18, 5, "var(--pnS)");
            var lIcon = view.Icon("auth-login", 22, "var(--ic)"); lIcon.style.marginRight = 14; login.Add(lIcon); login.Add(Text(L("Giriş yap", "Log in"), 18, "Montserrat-700", "#F4F1E8"));
            GalaxyFilters.HoverBrightness(login, 1.2f); Tap(login, () => Go("login")); panel.Add(login);

            var register = Row(); register.name = "auth-register"; register.style.height = 60; register.style.marginTop = 12; Pad(register, 0, 14, 0, 20); view.Decorate(register, "#3D7BFF", 18, 5, "#2148B0");
            var rIcon = view.Icon("auth-register", 22, "#fff"); rIcon.style.marginRight = 14; register.Add(rIcon);
            var rText = Text(L("Kayıt ol", "Sign up"), 18, "Montserrat-700", "#fff"); rText.style.flexGrow = 1; register.Add(rText);
            var gift = Row(); Pad(gift, 5, 10, 5, 10); view.Decorate(gift, "rgba(0,0,0,.22)", 10);
            var coin = Box(); coin.style.width = coin.style.height = 14; coin.style.marginRight = 6; view.Decorate(coin, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", 7); gift.Add(coin);
            gift.Add(Text("+" + view.N(25000), 12, "Baloo2-800", "#FFE08A")); register.Add(gift);
            GalaxyFilters.HoverBrightness(register, 1.1f); Tap(register, () => Go("register")); panel.Add(register);

            panel.Add(Divider(L("ya da", "or")));
            panel.Add(GoogleButton());
            var spacer = Box(); spacer.style.flexGrow = 1; panel.Add(spacer);
            var terms = Text(L("Devam ederek Kullanım Koşulları’nı ve Gizlilik Politikası’nı kabul etmiş olursun. Oyun sanal çiple oynanır; gerçek para kazandırmaz.",
                "By continuing you accept the Terms of Use and the Privacy Policy. The game is played with virtual chips; it pays no real money."), 12, "Baloo2-700", "var(--mt)");
            terms.style.whiteSpace = WhiteSpace.Normal; terms.style.opacity = .8f; panel.Add(terms);
            AddError(panel);
        }

        private VisualElement GoogleButton()
        {
            var google = Row(); google.name = "auth-google"; google.style.height = 56; google.style.justifyContent = Justify.Center; google.style.flexShrink = 0; view.Decorate(google, "#fff", 18, 5, "#A9B4D0");
            var ring = Box(); ring.style.width = ring.style.height = 22; ring.style.marginRight = 12; Radius(ring, 11);
            ring.style.borderTopWidth = ring.style.borderRightWidth = ring.style.borderBottomWidth = ring.style.borderLeftWidth = 4;
            ring.style.borderTopColor = view.C("#3D7BFF"); ring.style.borderRightColor = view.C("#3CCB6E"); ring.style.borderBottomColor = view.C("#FFC94A"); ring.style.borderLeftColor = view.C("#FF5E6C");
            google.Add(ring); google.Add(Text(L("Google ile devam et", "Continue with Google"), 17, "Montserrat-700", "#1B2340"));
            // Google needs the platform sign-in SDK and the server's OAuth client ids; until both are set the server answers invalid_token.
            Tap(google, () => { error = L("Google ile giriş bu sürümde henüz açık değil.", "Google sign-in is not available in this version yet."); Render(); });
            return google;
        }

        // ---------- forms ----------
        private VisualElement FormPanel(string title, string sub)
        {
            var panel = Panel(46, 46, 24, 30);
            var head = Row(); head.style.marginBottom = 4;
            var back = Box("auth-back"); back.style.width = back.style.height = 40; back.style.alignItems = Align.Center; back.style.justifyContent = Justify.Center; back.style.marginRight = 12;
            view.Decorate(back, "var(--pnH)", 12, 3, "var(--pnS)"); back.Add(view.Icon("back", 20, "var(--ic)")); Tap(back, () => Go("choice")); head.Add(back);
            var words = Box(); words.style.flexShrink = 1;
            words.Add(Text(title, 26, "Montserrat-700", "#F4F1E8"));
            var s = Text(sub, 13, "Baloo2-700", "var(--mt)"); s.style.whiteSpace = WhiteSpace.Normal; words.Add(s); head.Add(words);
            panel.Add(head);
            return panel;
        }

        private TextField Field(VisualElement panel, string label, string name, string value, string placeholder, bool secret, Action<string> changed, int maxLength = 120)
        {
            var cap = Text(label, 11, "Baloo2-800", "var(--mt)"); MenuView.Tracking(cap, 1.4f, 11); cap.style.marginTop = 10; panel.Add(cap);
            var field = new TextField { name = name, value = value, maxLength = maxLength, isPasswordField = secret && !showPassword };
            field.style.height = 50; field.style.marginTop = 6; field.style.marginLeft = field.style.marginRight = 0;
            field.textEdition.placeholder = placeholder; field.textEdition.hidePlaceholderOnFocus = false;
            var input = field.Q(className: TextField.inputUssClassName);
            if (input != null)
            {
                view.Decorate(input, "var(--dp)", 14); Border(input, 0, Color.clear); Pad(input, 0, secret ? 46 : 16, 0, 16);
                input.style.unityFont = view.Font("Baloo2-700"); input.style.fontSize = 16; input.style.color = view.C("#F4F1E8");
            }
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            field.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { evt.StopPropagation(); Submit(); } }, TrickleDown.TrickleDown);
            if (secret)
            {
                var holder = Box(); holder.Add(field);
                var eye = Box("auth-eye"); eye.style.position = Position.Absolute; eye.style.right = 12; eye.style.top = 19; eye.style.width = eye.style.height = 26; eye.style.alignItems = Align.Center; eye.style.justifyContent = Justify.Center;
                eye.Add(view.Icon("eye", 18, showPassword ? "var(--a1)" : "var(--mt)")); eye.tooltip = showPassword ? L("Gizle", "Hide") : L("Göster", "Show");
                Tap(eye, () => { showPassword = !showPassword; Render(); }); holder.Add(eye); panel.Add(holder);
            }
            else panel.Add(field);
            return field;
        }

        private VisualElement Toggle(string name, bool on, string text, Action flip)
        {
            var row = Row(); row.name = name;
            var track = Box(); track.style.width = 44; track.style.height = 24; track.style.flexShrink = 0; view.Decorate(track, on ? "#3CCB6E" : "rgba(255,255,255,.14)", 12);
            var knob = Box(); MenuView.Place(knob, on ? 22 : 2, 2, 20, 20); view.Decorate(knob, "#fff", 10); track.Add(knob);
            row.Add(track); var label = Text(text, 13, "Baloo2-800", "#F4F1E8"); label.style.marginLeft = 10; label.style.whiteSpace = WhiteSpace.Normal; label.style.flexShrink = 1; row.Add(label);
            Tap(row, () => { error = null; flip(); }); return row;
        }

        private VisualElement Cta(string name, string text)
        {
            var cta = Text(busy ? "…" : text, 18, "Montserrat-800", "var(--aT)"); cta.name = name; cta.style.height = 56; cta.style.unityTextAlign = TextAnchor.MiddleCenter;
            view.Decorate(cta, "linear-gradient(var(--a1),var(--a2))", 18, 5, "var(--aS)"); cta.style.opacity = busy ? .7f : 1;
            Tap(cta, Submit); return cta;
        }

        private VisualElement Switch(string question, string link, string target)
        {
            var row = Row(); row.style.justifyContent = Justify.Center; row.style.marginTop = 10;
            row.Add(Text(question + " ", 13, "Baloo2-700", "var(--mt)"));
            var go = Text(link, 13, "Baloo2-800", "var(--a1)"); go.name = "auth-switch-" + target; Tap(go, () => Go(target)); row.Add(go);
            return row;
        }

        private void BuildLogin()
        {
            var panel = FormPanel(L("Giriş yap", "Log in"), L("Hesabınla kaldığın yerden devam et", "Pick up where you left off"));
            panel.Add(GoogleButton()); panel.Add(Divider(L("ya da e-postayla", "or with e-mail")));
            Field(panel, L("E-POSTA", "E-MAIL"), "auth-email", email, "ornek@mail.com", false, v => email = v);
            Field(panel, L("ŞİFRE", "PASSWORD"), "auth-password", password, L("En az 6 karakter", "At least 6 characters"), true, v => password = v);
            var options = Row(); options.style.justifyContent = Justify.SpaceBetween; options.style.marginTop = 12;
            options.Add(Toggle("auth-remember", remember, L("Beni hatırla", "Remember me"), () => { remember = !remember; Render(); }));
            var forgot = Text(L("Şifremi unuttum", "Forgot password"), 13, "Baloo2-800", "var(--a1)"); forgot.name = "auth-forgot";
            Tap(forgot, () => { error = string.IsNullOrWhiteSpace(email) ? L("Sıfırlama için önce e-postanı yaz.", "Type your e-mail first to reset.") : L("Şifre sıfırlama bu sürümde henüz yok; destekle iletişime geç.", "Password reset is not available yet; contact support."); Render(); });
            options.Add(forgot); panel.Add(options);
            AddError(panel);
            var spacer = Box(); spacer.style.flexGrow = 1; panel.Add(spacer);
            panel.Add(Cta("auth-submit", L("Giriş yap", "Log in")));
            panel.Add(Switch(L("Hesabın yok mu?", "No account?"), L("Kayıt ol", "Sign up"), "register"));
        }

        private void BuildRegister()
        {
            var panel = FormPanel(L("Kayıt ol", "Sign up"), L("Hesap aç, 25.000 çip hediye", "Open an account, get 25,000 chips"));
            Field(panel, L("KULLANICI ADI", "USERNAME"), "auth-name", name, L("3–16 karakter", "3–16 characters"), false, v => name = v, 16);
            Field(panel, L("E-POSTA", "E-MAIL"), "auth-email", email, "ornek@mail.com", false, v => email = v);
            Field(panel, L("ŞİFRE", "PASSWORD"), "auth-password", password, L("En az 6 karakter", "At least 6 characters"), true, v => { password = v; UpdateStrength(panel); });
            var strength = Text("", 11, "Baloo2-800", "var(--mt)"); strength.name = "auth-strength"; strength.style.marginTop = 4; panel.Add(strength); UpdateStrength(panel);
            Field(panel, L("ŞİFRE TEKRAR", "REPEAT PASSWORD"), "auth-password2", password2, L("Şifreni tekrar yaz", "Type it again"), true, v => password2 = v);
            var agree = Toggle("auth-terms", terms, L("Kullanım Koşulları’nı ve Gizlilik Politikası’nı okudum, kabul ediyorum.", "I have read and accept the Terms of Use and the Privacy Policy."), () => { terms = !terms; Render(); });
            agree.style.marginTop = 12; panel.Add(agree);
            AddError(panel);
            var spacer = Box(); spacer.style.flexGrow = 1; panel.Add(spacer);
            panel.Add(Cta("auth-submit", L("Hesabı oluştur", "Create account")));
            panel.Add(Switch(L("Zaten hesabın var mı?", "Already have an account?"), L("Giriş yap", "Log in"), "login"));
        }

        private void UpdateStrength(VisualElement panel)
        {
            var label = panel.Q<Label>("auth-strength"); if (label == null) return;
            int score = (password.Length >= 6 ? 1 : 0) + (password.Length >= 10 ? 1 : 0) + (password.Any(char.IsDigit) && password.Any(char.IsLetter) ? 1 : 0) + (password.Any(c => !char.IsLetterOrDigit(c)) ? 1 : 0);
            label.text = password.Length == 0 ? "" : score <= 1 ? L("Zayıf", "Weak") : score == 2 ? L("Orta", "Medium") : L("Güçlü", "Strong");
            label.style.color = view.C(score <= 1 ? "#FF8A94" : score == 2 ? "#FFC94A" : "#8EF0B0");
        }

        private void BuildGuest()
        {
            var panel = FormPanel(L("Misafir girişi", "Guest"), L("İlerlemen bu cihazda saklanır", "Your progress stays on this device"));
            Field(panel, L("TAKMA AD", "NICKNAME"), "auth-nick", nick, L("Masada görünecek adın", "Your name at the table"), false, v => nick = v, 16);
            var cap = Text(L("AVATAR RENGİ", "AVATAR COLOUR"), 11, "Baloo2-800", "var(--mt)"); MenuView.Tracking(cap, 1.4f, 11); cap.style.marginTop = 14; panel.Add(cap);
            var colors = Row(); colors.name = "auth-colors"; colors.style.marginTop = 8; colors.style.justifyContent = Justify.SpaceBetween;
            foreach (var c in Colors)
            {
                string pick = c; bool on = c == color;
                var dot = Box("auth-color-" + c.Substring(1)); dot.style.width = dot.style.height = 40; view.Decorate(dot, c, 20);
                Border(dot, 3, on ? view.C("#fff") : Color.clear); if (on) GalaxyFilters.Shadow(dot, 0, 0, 6, view.C(c));
                Tap(dot, () => { color = pick; Render(); }); colors.Add(dot);
            }
            panel.Add(colors);
            var note = Box(); note.style.marginTop = 16; Pad(note, 12, 14, 12, 14); view.Decorate(note, "var(--dp)", 14);
            foreach (var line in new[] { L("Başlangıçta 10.000 çip hesabına yüklenir.", "You start with 10,000 chips."), L("Uygulamayı silersen ya da cihaz değiştirirsen ilerlemen kaybolur.", "Deleting the app or changing device loses your progress.") })
            { var t = Text(line, 13, "Baloo2-700", "var(--ic)"); t.style.whiteSpace = WhiteSpace.Normal; t.style.marginBottom = 4; note.Add(t); }
            panel.Add(note);
            AddError(panel);
            var spacer = Box(); spacer.style.flexGrow = 1; panel.Add(spacer);
            panel.Add(Cta("auth-submit", L("Oynamaya başla", "Start playing")));
            var later = Text(L("Sonra hesaba bağlayabilirsin.", "You can link an account later."), 12, "Baloo2-700", "var(--mt)"); later.style.unityTextAlign = TextAnchor.MiddleCenter; later.style.marginTop = 10; panel.Add(later);
        }

        private void AddError(VisualElement panel)
        {
            if (string.IsNullOrEmpty(error)) return;
            var box = Text(error, 13, "Baloo2-800", "#FFB3BA"); box.name = "auth-error"; box.style.whiteSpace = WhiteSpace.Normal; box.style.marginTop = 10;
            Pad(box, 10, 12, 10, 12); view.Decorate(box, "rgba(255,94,108,.14)", 12); Border(box, 1, new Color(1, .37f, .42f, .4f)); panel.Add(box);
        }

        // ---------- submit ----------
        private static readonly Regex NameRule = new Regex("^[\\p{L}0-9_ ]{3,16}$");

        public async void Submit()
        {
            if (busy) return;
            string problem = Validate();
            if (problem != null) { error = problem; Render(); return; }
            busy = true; error = null; Render();
            try
            {
                if (screen == "login") await online.LoginAsync(email.Trim(), password, remember);
                else if (screen == "register") await online.RegisterAsync(new RegisterRequest { Name = name.Trim(), Email = email.Trim(), Password = password, AcceptTerms = terms, Color = color });
                else if (screen == "guest") await online.GuestAsync(nick.Trim(), color);
                busy = false;
                string who = online.Profile?.Name ?? nick;
                Finish(screen == "login" ? L("Hoş geldin, ", "Welcome, ") + who + "!" : L("Hoş geldin, " + who + "! " + view.N(online.Wallet?.Chips ?? 0) + " çip hesabına eklendi", "Welcome, " + who + "! " + view.N(online.Wallet?.Chips ?? 0) + " chips added"));
            }
            catch (ApiException e)
            {
                busy = false; error = Explain(e); Render();
            }
        }

        private string Validate()
        {
            bool mail = Regex.IsMatch(email.Trim(), "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");
            switch (screen)
            {
                case "login":
                    if (!mail) return L("Geçerli bir e-posta yaz", "Enter a valid e-mail");
                    if (password.Length < 6) return L("En az 6 karakter", "At least 6 characters");
                    return null;
                case "register":
                    if (!NameRule.IsMatch(name.Trim())) return L("3–16 harf, rakam ya da _", "3–16 letters, digits or _");
                    if (!mail) return L("Geçerli bir e-posta yaz", "Enter a valid e-mail");
                    if (password.Length < 6) return L("En az 6 karakter", "At least 6 characters");
                    if (password != password2) return L("Şifreler aynı değil", "Passwords do not match");
                    if (!terms) return L("Devam etmek için koşulları kabul et", "Accept the terms to continue");
                    return null;
                case "guest":
                    if (!NameRule.IsMatch(nick.Trim())) return L("3–16 harf, rakam ya da _", "3–16 letters, digits or _");
                    return null;
            }
            return null;
        }

        private string Explain(ApiException e)
        {
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

        /// <summary>1 0 1 (78×108, 88×122, 78×108, −10° / 2° / 10°): tileIn 1.15 s staggered, then g101Float 6 s (−7 px).</summary>
        private VisualElement Tiles(float scale)
        {
            var row = Row(); row.style.alignItems = Align.FlexEnd; row.style.marginTop = 10 * scale; row.style.height = 136 * scale;
            float began = Time.realtimeSinceStartup;
            int[] w = { 78, 88, 78 }, h = { 108, 122, 108 }; float[] rot = { -10, 2, 10 }; string[] tint = { "t1", "t2", "t3" };
            for (int i = 0; i < 3; i++)
            {
                var tile = Box("auth-tile-" + i); tile.style.width = w[i] * scale; tile.style.height = h[i] * scale; tile.style.rotate = new Rotate(rot[i]);
                tile.style.alignItems = Align.Center; tile.style.justifyContent = Justify.Center; if (i > 0) tile.style.marginLeft = 12 * scale;
                view.Decorate(tile, "linear-gradient(var(--tf1),var(--tf2))", Mathf.RoundToInt((i == 1 ? 13 : 12) * scale), Mathf.RoundToInt((i == 1 ? 6 : 5) * scale), "var(--te2)");
                tile.style.borderBottomWidth = (i == 1 ? 8 : 7) * scale; tile.style.borderBottomColor = view.C("var(--te)");
                var number = view.Text(i == 1 ? "0" : "1", Mathf.RoundToInt((i == 1 ? 74 : 64) * scale), true, "var(--" + tint[i] + ")"); number.style.unityTextAlign = TextAnchor.MiddleCenter; number.pickingMode = PickingMode.Ignore; tile.Add(number);
                var dot = Box(); dot.style.width = dot.style.height = (i == 1 ? 16 : 14) * scale; dot.style.marginTop = 3; view.Decorate(dot, "var(--" + tint[i] + ")", 99); tile.Add(dot);
                float phase = i * 2f, delay = .1f + i * .2f;
                Animate(() =>
                {
                    float t = Time.realtimeSinceStartup - began, u = Mathf.Clamp01((t - delay) / 1.15f), k = 1 - Mathf.Pow(1 - u, 4);
                    float bob = -7 * (0.5f - 0.5f * Mathf.Cos((t + phase) / 6f * Mathf.PI * 2));
                    tile.style.translate = new Translate(0, 80 * (1 - k) + bob); tile.style.opacity = Mathf.Clamp01(u * 3); tile.style.scale = new Scale(Vector2.one * Mathf.Lerp(.78f, 1, k));
                });
                row.Add(tile);
            }
            return row;
        }

        // ---------- animation: one scheduler for the flow's layer ----------
        private readonly System.Collections.Generic.List<Action> motions = new System.Collections.Generic.List<Action>();
        private IVisualElementScheduledItem ticker;
        private void Animate(Action step)
        {
            motions.Add(step);
            if (ticker == null && layer != null) ticker = layer.schedule.Execute(() => { foreach (var m in motions.ToArray()) m(); }).Every(16);
        }

        private VisualElement Divider(string text)
        {
            var row = Row(); row.style.marginTop = 12; row.style.marginBottom = 4;
            var a = Box(); a.style.flexGrow = 1; a.style.height = 1; a.style.backgroundColor = new Color(1, 1, 1, .1f); row.Add(a);
            var label = Text(text, 12, "Baloo2-800", "var(--mt)"); label.style.marginLeft = label.style.marginRight = 12; row.Add(label);
            var b = Box(); b.style.flexGrow = 1; b.style.height = 1; b.style.backgroundColor = new Color(1, 1, 1, .1f); row.Add(b);
            return row;
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
            e.pickingMode = PickingMode.Position; e.focusable = true;
            e.RegisterCallback<ClickEvent>(evt => { action(); evt.StopPropagation(); });
        }
    }
}
