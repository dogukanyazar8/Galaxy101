#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Galaxy101.Diagnostics
{
    /// <summary>
    /// The Unity side of the v4 state router (prototype g101-state.js, KARARLAR K-06): opens a catalogue state by id the way
    /// the prototype's ?state=ID does, waits ft ms, freezes the shared clock (D-61) and writes a 1280×720 frame with the
    /// layout and text dumps next to it, so artifacts/v4/ref/&lt;ID&gt; and the Unity frame can be compared element by element.
    /// <code>Galaxy101.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -g101State=G13 -g101Seed=42 -g101Theme=saturn -g101Lang=tr -g101Ft=1200 -g101Out=C:\out</code>
    /// -g101State takes one id, a comma list or "all"; without -g101Ft each state uses its catalogue hold. Development
    /// builds and the editor only (the router reaches into the screens like the prototype's router does).
    /// </summary>
    public sealed partial class G101StateRouter : MonoBehaviour
    {
        public sealed class Options
        {
            public List<string> States = new List<string>();
            public uint Seed = 42;
            public string Theme, Lang, Out;
            public int? Ft;
            /// <summary>-g101Size=WxH: the window the frames are captured in (Faz 3 resolutions); default 1280×720.</summary>
            public int Width = 1280, Height = 720;
            /// <summary>-g101Fps=seconds: before freezing, measure the frame times for this long (Faz 3, 60 FPS at 1080p).</summary>
            public float FpsSeconds;
        }

        public static readonly IReadOnlyDictionary<string, string> Planets = new Dictionary<string, string>
        { ["saturn"] = "Satürn", ["mars"] = "Mars", ["neptun"] = "Neptün", ["jupiter"] = "Jüpiter", ["venus"] = "Venüs" };

        /// <summary>The state run account (g101-state.js step acct: guest:state "Doğukan Y.", 10.000 chips; R-96: 20 gems).</summary>
        public const string AccountName = "Doğukan Y.", AccountColor = "#FF8A5B";
        public const long AccountChips = 10000, AccountGems = 20;

        public static Options Current { get; private set; }
        /// <summary>The state being opened (null outside a router run); screens read it to skip greetings and test chips like the prototype (DECISIONS 2026-10-25).</summary>
        public static string ActiveState { get; private set; }

        private Options options;
        private JArray catalogue;
        private readonly JArray manifest = new JArray();
        private string persistence;

        public static Options Parse(string[] args)
        {
            string Value(string key)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].StartsWith("-" + key + "=", StringComparison.Ordinal)) return args[i].Substring(key.Length + 2);
                    if (args[i] == "-" + key && i + 1 < args.Length) return args[i + 1];
                }
                return null;
            }
            string state = Value("g101State");
            if (string.IsNullOrEmpty(state)) return null;
            var o = new Options { Theme = Value("g101Theme"), Lang = Value("g101Lang"), Out = Value("g101Out") };
            if (uint.TryParse(Value("g101Seed"), NumberStyles.None, CultureInfo.InvariantCulture, out uint seed)) o.Seed = seed;
            if (int.TryParse(Value("g101Ft"), NumberStyles.None, CultureInfo.InvariantCulture, out int ft)) o.Ft = ft;
            var size = Value("g101Size");
            if (size != null)
            {
                var wh = size.Split('x', 'X', '×');
                if (wh.Length != 2 || !int.TryParse(wh[0], out o.Width) || !int.TryParse(wh[1], out o.Height) || o.Width < 640 || o.Height < 360) throw new ArgumentException("-g101Size must be WxH, e.g. 1920x1080.");
            }
            if (float.TryParse(Value("g101Fps"), NumberStyles.Float, CultureInfo.InvariantCulture, out float fps)) o.FpsSeconds = fps;
            o.States.AddRange(state.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            if (o.Theme != null && !Planets.ContainsKey(o.Theme)) throw new ArgumentException("-g101Theme must be saturn, mars, neptun, jupiter or venus.");
            if (o.Lang != null && o.Lang != "tr" && o.Lang != "en") throw new ArgumentException("-g101Lang must be tr or en.");
            return o;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            Options o;
            try { o = Parse(Environment.GetCommandLineArgs()); }
            catch (ArgumentException e) { Debug.LogError(e.Message); UnityEngine.Application.Quit(2); return; }
            if (o == null) return;
            Run(o);
        }

        /// <summary>Start a router run (command line or a PlayMode test).</summary>
        public static G101StateRouter Run(Options o)
        {
            Current = o;
            MainMenuController.AuthOnStart = false;
            var owner = new GameObject("G101 state router"); DontDestroyOnLoad(owner);
            var router = owner.AddComponent<G101StateRouter>(); router.options = o;
            return router;
        }

        public bool Finished { get; private set; }

        private IEnumerator Start()
        {
            var text = Resources.Load<TextAsset>("G101/states");
            if (text == null) { Fail("states catalogue missing (Resources/G101/states.json)"); yield break; }
            catalogue = (JArray)JObject.Parse(text.text)["states"];
            var ids = options.States.Count == 1 && options.States[0] == "all"
                ? catalogue.Where(s => (bool?)s["removed"] != true).Select(s => (string)s["id"]).ToList() : options.States;
            if (string.IsNullOrEmpty(options.Out)) options.Out = Path.Combine(Path.GetTempPath(), "g101-states-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(options.Out);
            persistence = Path.Combine(options.Out, "_state", "menu.json");
            foreach (var id in ids) yield return RunState(id);
            ActiveState = null; Finished = true;
            File.WriteAllText(Path.Combine(options.Out, "manifest.json"), new JObject { ["states"] = manifest, ["seed"] = options.Seed }.ToString(Formatting.Indented));
            Debug.Log("G101_STATES_COMPLETE count=" + manifest.Count + " out=" + options.Out);
            if (!UnityEngine.Application.isEditor) UnityEngine.Application.Quit(0);
        }

        /// <summary>Set by a step when the state's hold counts from an earlier moment than the end of its path.</summary>
        private float? holdOrigin;

        private JObject Entry(string id) => (JObject)catalogue.FirstOrDefault(s => (string)s["id"] == id);

        private IEnumerator RunState(string id)
        {
            var entry = Entry(id);
            var record = new JObject { ["id"] = id };
            manifest.Add(record);
            if (entry == null) { record["error"] = "unknown"; yield break; }
            if ((bool?)entry["removed"] == true) { record["error"] = "removed"; yield break; }
            ActiveState = id;
            string theme = options.Theme ?? (string)entry["theme"] ?? "saturn", lang = options.Lang ?? (string)entry["lang"] ?? "tr";
            // The catalogue state's own theme or language wins over the run's (KARARLAR K-07).
            if (entry["theme"] != null) theme = (string)entry["theme"];
            if (entry["lang"] != null) lang = (string)entry["lang"];
            int hold = options.Ft ?? (int?)entry["hold"] ?? 600;
            record["theme"] = theme; record["lang"] = lang; record["ft"] = hold;
            GameClock.Reset(options.Seed);
            if (File.Exists(persistence)) File.Delete(persistence);
            Directory.CreateDirectory(Path.GetDirectoryName(persistence));
            GalaxyPreferencesBridge.OverridePath = persistence;
            string error = null;
            IEnumerator steps;
            try { steps = Steps(id, theme, lang); }
            catch (Exception e) { error = e.Message; steps = null; }
            if (steps != null) yield return Guard(steps, e => error = e.Message);
            if (error != null) { record["error"] = error; Debug.LogWarning("G101_STATE_PATH_ERROR " + id + " " + error); }
            // The hold runs from the end of the path, or from the moment a step names (S01: the splash's own start).
            float holdUntil = (holdOrigin ?? Time.realtimeSinceStartup) + hold / 1000f; holdOrigin = null;
            while (Time.realtimeSinceStartup < holdUntil) yield return null;
            // A settings screen may apply its display draft; the frame is always the 1280×720 window.
            if (!UnityEngine.Application.isEditor && (Screen.width != options.Width || Screen.height != options.Height))
            {
                Screen.SetResolution(options.Width, options.Height, FullScreenMode.Windowed);
                for (int i = 0; i < 30 && (Screen.width != options.Width || Screen.height != options.Height); i++) yield return null;
                yield return Settle(.3f);
            }
            // g101-state.js run(): G101Clock.freeze() without an argument — every reference frame sits at the canonical
            // 100000 ms (DECISIONS D-61 says 100000 + ft; the code that made the references does not: EKSIKLER EK-005).
            if (options.FpsSeconds > 0) yield return MeasureFrames(record);
            GameClock.Freeze(0);
            yield return null; yield return new WaitForEndOfFrame();
            yield return Capture(id, record);
            Time.timeScale = 1;
        }

        /// <summary>Frame times over FpsSeconds of real time with the state live: average FPS, 95th percentile and worst frame (ms).</summary>
        private IEnumerator MeasureFrames(JObject record)
        {
            var times = new List<float>(); float until = Time.realtimeSinceStartup + options.FpsSeconds;
            yield return null;
            while (Time.realtimeSinceStartup < until) { times.Add(Time.unscaledDeltaTime * 1000f); yield return null; }
            if (times.Count == 0) yield break;
            times.Sort(); float total = 0; foreach (var t in times) total += t;
            record["fps"] = new JObject
            {
                ["frames"] = times.Count, ["avgFps"] = Math.Round(1000.0 * times.Count / total, 1),
                ["p95Ms"] = Math.Round(times[(int)(times.Count * .95f)], 2), ["worstMs"] = Math.Round(times[times.Count - 1], 2),
                ["screen"] = Screen.width + "x" + Screen.height, ["vSync"] = QualitySettings.vSyncCount, ["targetFrameRate"] = UnityEngine.Application.targetFrameRate
            };
        }

        /// <summary>Run a nested coroutine and report its exception instead of stopping the router.</summary>
        private static IEnumerator Guard(IEnumerator routine, Action<Exception> failed)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(routine);
            while (stack.Count > 0)
            {
                object current;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    current = stack.Peek().Current;
                    if (current is IEnumerator nested) { stack.Push(nested); continue; }
                }
                catch (Exception e) { failed(e); yield break; }
                yield return current;
            }
        }

        private IEnumerator Capture(string id, JObject record)
        {
            yield return new WaitForEndOfFrame();
            // The composed back buffer at the end of the frame (3D cameras + every UI document), as the player shows it.
            var shot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); shot.Apply();
            try
            {
                record["width"] = shot.width; record["height"] = shot.height;
                File.WriteAllBytes(Path.Combine(options.Out, id + ".png"), shot.EncodeToPNG());
            }
            finally { Destroy(shot); }
            File.WriteAllText(Path.Combine(options.Out, id + ".layout.json"), G101Dump.Layout(id, options.Seed, GameClock.NowMs).ToString(Formatting.Indented));
            File.WriteAllText(Path.Combine(options.Out, id + ".text.json"), G101Dump.Texts().ToString(Formatting.None));
            Debug.Log("G101_STATE_CAPTURED " + id);
        }

        private void Fail(string message)
        {
            Debug.LogError("G101_STATES_FAILED " + message); Finished = true;
            if (!UnityEngine.Application.isEditor) UnityEngine.Application.Quit(3);
        }

        // ----- shared steps -------------------------------------------------------------------------------------------

        /// <summary>The steps of one state (g101-state.js path), per catalogue group.</summary>
        private IEnumerator Steps(string id, string theme, string lang)
        {
            if (MenuStates.Contains(id)) return MenuState(id, theme, lang);
            if (id.StartsWith("S", StringComparison.Ordinal)) return ShellState(id, theme, lang);
            if (IsTableState(id)) return TableState(id, theme, lang);
            throw new NotSupportedException("no Unity path for this state yet");
        }

        /// <summary>Load the bootstrap scene fresh and wait for the menu (steps acct + go menu + waitFor Menu).</summary>
        private IEnumerator OpenMenu(string theme, string lang, bool auth)
        {
            MainMenuController.AuthOnStart = auth;
            SceneManager.LoadScene(0, LoadSceneMode.Single);
            yield return null;
            MainMenuController menu = null;
            float until = Time.realtimeSinceStartup + 20;
            while ((menu = FindAnyObjectByType<MainMenuController>()) == null || menu.View == null)
            {
                if (Time.realtimeSinceStartup > until) throw new TimeoutException("menu did not open");
                yield return null;
            }
            Menu = menu;
            var model = menu.Model;
            // The window the frame is captured from: 1280×720, windowed (the isolated settings start from the defaults).
            model.Settings["wmode"] = "windowed"; model.Settings["res"] = "1280x720"; model.Settings["lang"] = lang;
            model.Planet = Planets[theme];
            model.Profile["name"] = AccountName; model.Profile["color"] = AccountColor; model.Profile["status"] = ""; model.Profile["frame"] = 0;
            model.Chips = AccountChips; model.Gems = AccountGems;
            model.NotifyChanged(); menu.View.Refresh();
            yield return Settle();
        }

        public MainMenuController Menu { get; private set; }

        private static IEnumerator Settle(float seconds = .25f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) yield return null;
        }

        private TextField Field(string name) => Menu.View.Root.Q<TextField>(name) ?? throw new InvalidOperationException("no text field " + name);
        private Button Button(string name) => Menu.View.Root.Q<Button>(name) ?? throw new InvalidOperationException("no button " + name);

        private IEnumerator Click(string name)
        {
            var button = Button(name);
            if (!button.enabledInHierarchy) throw new InvalidOperationException("disabled: " + name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
            yield return Settle(.16f);
        }
    }
}
#endif
