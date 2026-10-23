#if UNITY_EDITOR || OKEY_DIAGNOSTICS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Player-only display soak (<c>-galaxyDisplaySoak &lt;absolute dir&gt;</c>, diagnostics builds): switches görüntü modu,
    /// çözünürlük and kare hızı for real — in the menu, with a panel open, and in the middle of a 3D table hand — and
    /// checks after every switch that the screen took the request, the 1280×720 canvas is whole and centred, every
    /// render target follows the new size, the measured frame rate fits the setting, and no error was logged.
    /// Writes <c>display_soak.json</c> and one PNG per step, then quits (exit code 1 on a failed check).
    /// </summary>
    public sealed class GalaxyDisplaySoak : MonoBehaviour
    {
        private string output;
        private readonly JArray steps = new JArray();
        private readonly List<string> errors = new List<string>();
        private bool failed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-galaxyDisplaySoak");
            if (i < 0 || i + 1 >= args.Length || !Path.IsPathFullyQualified(args[i + 1])) return;
            var go = new GameObject("Galaxy101 display soak"); DontDestroyOnLoad(go);
            var soak = go.AddComponent<GalaxyDisplaySoak>(); soak.output = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(soak.output);
        }

        private void OnEnable() => Application.logMessageReceived += OnLog;
        private void OnDisable() => Application.logMessageReceived -= OnLog;
        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors.Add(type + ": " + message);
        }

        private IEnumerator Start()
        {
            MainMenuController menu = null;
            float until = Time.realtimeSinceStartup + 30;
            while ((menu = FindAnyObjectByType<MainMenuController>()) == null || menu.View == null) { if (Time.realtimeSinceStartup > until) { Fail("menu never appeared"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(2);
            // The soak switches through the real settings service: put the player's own choices back at the end.
            var saved = ((string)menu.Model.Settings["wmode"], (string)menu.Model.Settings["res"], (int)menu.Model.Settings["fps"]);
            var native = GameDisplay.NativeSize; int hz = Mathf.RoundToInt((float)FramePacing.MaxHz);
            string mid = GameDisplay.Options(native).Count > 1 ? GameDisplay.Format(GameDisplay.Options(native)[GameDisplay.Options(native).Count - 2]) : GameDisplay.Native;
            var plan = new List<(string Where, string Mode, string Res, int Fps)>
            {
                ("menu", "windowed", "1280x720", FramePacing.Auto),
                ("menu", "windowed", "1600x900", 60),
                ("menu", "borderless", GameDisplay.Native, FramePacing.Auto),
                ("settings", "fullscreen", mid, hz),
                ("settings", "fullscreen", GameDisplay.Native, FramePacing.Auto),
                ("menu", "windowed", GameDisplay.Native, 30),
                ("menu", "borderless", "1280x720", 120),
            };
            foreach (var s in plan) yield return Step(menu, s.Where, s.Mode, s.Res, s.Fps);
            menu.View.CloseTop();

            // A real hand on the 3D table, switching while it plays.
            var open = menu.StartLocalAsync();
            until = Time.realtimeSinceStartup + 60;
            Galaxy101.Table.TableGame game = null;
            while ((game = FindAnyObjectByType<Galaxy101.Table.TableGame>()) == null || game.Current == Galaxy101.Table.TableGame.Phase.Waiting || game.Current == Galaxy101.Table.TableGame.Phase.Seating || game.Current == Galaxy101.Table.TableGame.Phase.Deal)
            { if (Time.realtimeSinceStartup > until) { Fail("table never started: " + (open.Exception?.GetBaseException().Message ?? "timeout")); break; } yield return null; }
            if (game != null)
            {
                var tablePlan = new List<(string Mode, string Res, int Fps)>
                {
                    ("borderless", GameDisplay.Native, FramePacing.Auto), ("windowed", "1280x720", 60), ("fullscreen", mid, hz),
                    ("windowed", "1600x900", FramePacing.Auto), ("fullscreen", GameDisplay.Native, FramePacing.Auto), ("borderless", GameDisplay.Native, 144),
                };
                foreach (var s in tablePlan) yield return TableStep(game, s.Mode, s.Res, s.Fps);
            }
            var model = FindAnyObjectByType<Galaxy101.Table.TableHud>()?.Model ?? menu.Model;
            var services = new MenuServices(model).Settings;
            services.SetDisplay(saved.Item1, saved.Item2); services.SetFrameRate(saved.Item3);
            GameDisplay.Apply(saved.Item1, saved.Item2); FramePacing.Apply(saved.Item3);
            var report = new JObject { ["native"] = GameDisplay.Format(native), ["maxHz"] = FramePacing.MaxHz, ["steps"] = steps, ["errors"] = new JArray(errors.Distinct().Take(50)), ["passed"] = !failed && errors.Count == 0 };
            File.WriteAllText(Path.Combine(output, "display_soak.json"), report.ToString());
            Debug.Log("GALAXY101_DISPLAY_SOAK " + (report["passed"].Value<bool>() ? "PASSED" : "FAILED") + " " + output);
            yield return new WaitForSecondsRealtime(.5f);
            Application.Quit(report["passed"].Value<bool>() ? 0 : 1);
        }

        private IEnumerator Step(MainMenuController menu, string where, string mode, string res, int fps)
        {
            if (where == "settings") menu.View.OpenPanel("settings", 2); else menu.View.CloseTop();
            // Through the real settings service, as the Görüntü row does.
            menu.View.Services.Settings.SetDisplay(mode, res); menu.View.Services.Settings.SetFrameRate(fps);
            yield return Settle();
            var entry = Measure(where, mode, res, fps, menu.View.Root, FindAnyObjectByType<MenuGammaPresenter>()?.Output);
            yield return Shot(entry);
        }

        private IEnumerator TableStep(Galaxy101.Table.TableGame game, string mode, string res, int fps)
        {
            var hud = FindAnyObjectByType<Galaxy101.Table.TableHud>();
            GameDisplay.Apply(mode, res); hud.Settings.Set("fps", fps);
            yield return Settle();
            var entry = Measure("table", mode, res, fps, hud.GetComponent<UIDocument>().rootVisualElement, null);
            entry["phase"] = game.Current.ToString();
            entry["tilesOnRack"] = game.Rack.Count;
            yield return Shot(entry);
        }

        private static IEnumerator Settle()
        {
            for (int i = 0; i < 10; i++) yield return null;
            yield return new WaitForSecondsRealtime(2.5f); // exclusive mode switches take a moment
        }

        private JObject Measure(string where, string mode, string res, int fps, VisualElement root, RenderTexture presenter)
        {
            var request = GameDisplay.LastRequest;
            // Frame rate over 1.5 s (measured on the main thread between frames).
            var entry = new JObject
            {
                ["where"] = where, ["mode"] = mode, ["res"] = res, ["fps"] = fps,
                ["requested"] = request.Width + "x" + request.Height + " " + request.Mode,
                ["screen"] = Screen.width + "x" + Screen.height + " " + Screen.fullScreenMode,
                ["hz"] = Math.Round(Screen.currentResolution.refreshRateRatio.value, 2),
                ["vSync"] = QualitySettings.vSyncCount, ["targetFrameRate"] = Application.targetFrameRate,
            };
            var problems = new List<string>();
            if (Screen.fullScreenMode != request.Mode) problems.Add("mode " + Screen.fullScreenMode + " != " + request.Mode);
            if (Math.Abs(Screen.width - request.Width) > 2 || Math.Abs(Screen.height - request.Height) > 2) problems.Add("size " + Screen.width + "x" + Screen.height + " != " + request.Width + "x" + request.Height);
            var canvas = root.Q<VisualElement>("galaxy-canvas") ?? root.Q<VisualElement>("table-canvas");
            if (canvas == null) problems.Add("no canvas");
            else
            {
                var s = root.worldBound; var c = canvas.worldBound;
                if (c.xMin < s.xMin - 1 || c.xMax > s.xMax + 1 || c.yMin < s.yMin - 1 || c.yMax > s.yMax + 1) problems.Add("canvas cropped " + c + " in " + s);
                if (Math.Abs(c.center.x - s.center.x) > 1 || Math.Abs(c.center.y - s.center.y) > 1) problems.Add("canvas off centre");
            }
            if (presenter != null && (presenter.width != Screen.width || presenter.height != Screen.height)) problems.Add("menu target " + presenter.width + "x" + presenter.height);
            entry["problems"] = new JArray(problems);
            if (problems.Count > 0) failed = true;
            pending = entry; return entry;
        }

        private JObject pending;

        private IEnumerator Shot(JObject entry)
        {
            // Measure the frame rate, then grab the back buffer at the end of a frame.
            int frames = 0; float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 1.5f) { frames++; yield return null; }
            double measured = frames / (Time.realtimeSinceStartup - start);
            entry["measuredFps"] = Math.Round(measured, 1);
            int fps = entry["fps"].Value<int>(); double hz = entry["hz"].Value<double>();
            double expected = fps == FramePacing.Auto ? hz : fps;
            // Above the target is a pacing bug; far below may be the GPU (reported, not failed).
            if (measured > expected * 1.12 + 2) { ((JArray)entry["problems"]).Add("measured " + measured.ToString("0") + " > " + expected); failed = true; }
            entry["reachedTarget"] = measured >= expected * .85;
            yield return new WaitForEndOfFrame();
            var image = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); image.Apply();
            string name = steps.Count.ToString("00") + "_" + entry["where"] + "_" + entry["mode"] + "_" + entry["res"] + "_" + entry["fps"] + ".png";
            File.WriteAllBytes(Path.Combine(output, name), image.EncodeToPNG()); Destroy(image);
            entry["image"] = name; steps.Add(entry);
            Debug.Log("GALAXY101_DISPLAY_SOAK_STEP " + entry.ToString(Newtonsoft.Json.Formatting.None));
        }

        private void Fail(string why) { failed = true; errors.Add(why); }
    }
}
#endif
