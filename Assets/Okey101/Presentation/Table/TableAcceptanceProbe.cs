#if UNITY_EDITOR || OKEY_DIAGNOSTICS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Okey101.Presentation
{
    // Explicit developer opt-in. Captures this game's framebuffer, never the desktop.
    public sealed class TableAcceptanceProbe : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {
            public string unity, gpu, cpu, graphics, completedUtc;
            public bool development, frameTimingSupported;
            public int width, height, samples, gpuSamples, tiles, transitions, audioFrames;
            public double cpuMeanMs, cpuP95Ms, gpuMeanMs, gpuP95Ms, frameMeanMs, frameP95Ms;
            public double mainThreadMeanMs, mainThreadP95Ms, renderThreadMeanMs, presentWaitMeanMs, audioPeak, audioRms;
        }
        private GameBootstrap game;
        private string directory;
        public void Initialize(GameBootstrap value, string output)
        {
            game = value; directory = Path.GetFullPath(output);
            if (!Directory.Exists(directory) || Directory.EnumerateFileSystemEntries(directory).Any())
                throw new IOException("A3 evidence requires an existing empty output directory.");
            StartCoroutine(Capture());
        }
        private IEnumerator Capture()
        {
            game.SetBotsEnabled(false);
            foreach (var size in new[] { new Vector2Int(1280,720), new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(2560,1080) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                for (var i = 0; i < 30; i++) yield return null;
                yield return new WaitForEndOfFrame();
                var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                try
                {
                    texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply(false);
                    if (texture.width != size.x || texture.height != size.y) throw new InvalidOperationException("A3 capture resolution differs from requested size.");
                    using (var file = new FileStream(Path.Combine(directory, size.x + "x" + size.y + ".png"), FileMode.CreateNew))
                    { var png = texture.EncodeToPNG(); file.Write(png, 0, png.Length); }
                    var minimum = GetComponentsInChildren<TileView>().Where(t => t.Role == TileRole.Hand).Min(t => t.NumberPixelHeight(game.Presenter.TableCamera));
                    Debug.Log($"A3_CAPTURE resolution={size.x}x{size.y} tiles={game.Presenter.VisibleOwnTileIds.Count} minNumberPixels={minimum:F2}");
                }
                finally { Destroy(texture); }
            }
            Screen.SetResolution(1920,1080,FullScreenMode.Windowed);
            for (var i = 0; i < 120; i++) yield return null;
            var cpu = new List<double>(); var gpu = new List<double>(); var frames = new List<double>();
            var main = new List<double>(); var render = new List<double>(); var wait = new List<double>();
            var audio = new float[1024]; double audioPeak = 0, audioEnergy = 0; var audioFrames = 0;
            var timing = new FrameTiming[1]; Task step = null; var transitions = 0;
            for (var i = 0; i < 600; i++)
            {
                if (step != null && step.IsFaulted) { Debug.LogException(step.Exception); yield break; }
                if (game.CanControl && (step == null || step.IsCompleted) && game.Session.GetView(0).Result == null)
                { step = game.StepDiagnosticBotAsync(); transitions++; }
                FrameTimingManager.CaptureFrameTimings(); yield return null;
                AudioListener.GetOutputData(audio, 0); var audible = false;
                foreach (var sample in audio)
                { audioPeak = Math.Max(audioPeak, Math.Abs(sample)); audioEnergy += sample * sample; if (Math.Abs(sample) > .0001f) audible = true; }
                if (audible) audioFrames++;
                frames.Add(Time.unscaledDeltaTime * 1000);
                if (FrameTimingManager.GetLatestTimings(1, timing) > 0)
                {
                    if (timing[0].cpuFrameTime > 0) cpu.Add(timing[0].cpuFrameTime);
                    if (timing[0].gpuFrameTime > 0) gpu.Add(timing[0].gpuFrameTime);
                    main.Add(timing[0].cpuMainThreadFrameTime); render.Add(timing[0].cpuRenderThreadFrameTime); wait.Add(timing[0].cpuMainThreadPresentWaitTime);
                }
            }
            while (step != null && !step.IsCompleted) yield return null;
            if (step != null && step.IsFaulted) { Debug.LogException(step.Exception); yield break; }
            var evidence = new Evidence
            {
                unity = UnityEngine.Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType,
                graphics = SystemInfo.graphicsDeviceType.ToString(), development = Debug.isDebugBuild,
                completedUtc = DateTime.UtcNow.ToString("O"), frameTimingSupported = FrameTimingManager.IsFeatureEnabled(),
                width = Screen.width, height = Screen.height, samples = cpu.Count, gpuSamples = gpu.Count,
                tiles = game.Presenter.VisibleOwnTileIds.Count, transitions = transitions,
                cpuMeanMs = Mean(cpu), cpuP95Ms = P95(cpu), gpuMeanMs = Mean(gpu), gpuP95Ms = P95(gpu),
                frameMeanMs = Mean(frames), frameP95Ms = P95(frames),
                mainThreadMeanMs = Mean(main), mainThreadP95Ms = P95(main), renderThreadMeanMs = Mean(render), presentWaitMeanMs = Mean(wait),
                audioPeak = audioPeak, audioRms = Math.Sqrt(audioEnergy / (600 * audio.Length)), audioFrames = audioFrames
            };
            File.WriteAllText(Path.Combine(directory, "profile.json"), JsonUtility.ToJson(evidence,true));
            Debug.Log("A3_PROFILE_READY " + directory);
        }
        private static double Mean(List<double> values) => values.Count > 0 ? values.Average() : 0;
        private static double P95(List<double> values) => values.Count > 0 ? values.OrderBy(v => v).ElementAt((int)((values.Count - 1) * .95)) : 0;
    }
}
#endif
