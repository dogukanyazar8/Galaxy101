#if UNITY_EDITOR || OKEY_DIAGNOSTICS
using System;
using System.Collections;
using System.IO;
using Galaxy101.Table;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Opt-in player probe: <c>-galaxyTableProbe &lt;absolute.png&gt;</c> opens "Botlara karşı" from the main menu, lets the
    /// table play for a few seconds and saves the real presented frame (the back buffer at end of frame, i.e. after every
    /// camera and screen-space UI panel) before quitting. <c>-galaxyLegacyPanelClear</c> re-enables clearing on the authored
    /// PanelSettings asset to reproduce the black-screen regression.
    /// </summary>
    public sealed class GalaxyTableProbe : MonoBehaviour
    {
        private string output;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-galaxyTableProbe");
            if (index < 0) return;
            if (index + 1 >= args.Length || !Path.IsPathFullyQualified(args[index + 1])) { Debug.LogError("-galaxyTableProbe requires an absolute .png path."); Application.Quit(2); return; }
            if (Array.IndexOf(args, "-galaxyLegacyPanelClear") >= 0)
            {
                var panel = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
                panel.clearColor = true; panel.colorClearValue = new Color(0, 0, 0, 0);
                Debug.Log("GALAXY101_TABLE_PROBE legacy panel clear enabled");
            }
            var owner = new GameObject("Galaxy101 table probe"); DontDestroyOnLoad(owner);
            owner.AddComponent<GalaxyTableProbe>().output = args[index + 1];
        }

        private IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 120;
            MainMenuController menu = null;
            while ((menu = FindAnyObjectByType<MainMenuController>()) == null || menu.View == null) { if (Time.realtimeSinceStartup > deadline) { Fail("menu"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(1);
            var open = menu.StartLocalAsync(MainMenuController.GalaxyTableScene);
            while (!open.IsCompleted) yield return null;
            if (open.IsFaulted) { Fail("table load: " + open.Exception?.GetBaseException().Message); yield break; }
            TableGame game = null;
            while ((game = FindAnyObjectByType<TableGame>()) == null || game.Current == TableGame.Phase.Waiting) { if (Time.realtimeSinceStartup > deadline) { Fail("table"); yield break; } yield return null; }
            yield return new WaitForSecondsRealtime(6);
            // End of frame = after every camera and the screen-space UI: exactly what the window presents.
            yield return new WaitForEndOfFrame();
            var frame = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            RenderTexture.active = null; frame.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); frame.Apply();
            File.WriteAllBytes(output, frame.EncodeToPNG()); Destroy(frame);
            Debug.Log("GALAXY101_TABLE_PROBE captured " + output + " phase=" + game.Current + " screen=" + Screen.width + "x" + Screen.height);
            Application.Quit(File.Exists(output) ? 0 : 3);
        }

        private static void Fail(string what) { Debug.LogError("GALAXY101_TABLE_PROBE failed waiting for " + what); Application.Quit(4); }
    }
}
#endif
