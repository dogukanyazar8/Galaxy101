using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Okey101.Editor
{
    public static class BuildWindows
    {
        internal static bool IncludeDiagnosticContent {get;private set;}
        [Serializable]
        private sealed class BuildEvidence
        {
            public string result, executable, unityVersion, backend, graphicsAPI, completedUtc, configuration, productVersion, ruleVersion, tableRuleVersion, productName;
            public int errors, warnings, captureWindowGuardVersion;
            public long bytes;
            public string[] scenes;
        }
        [MenuItem("Okey 101/Build Windows Development")]
        public static void Development() => Build(true);
        [MenuItem("Okey 101/Build Windows Release")]
        public static void Release() => Build(false);
        public static void Acceptance() => Build(false,true);
        private static string[] GetBuildScenes() => new[]
        {
            ProjectSetup.BootstrapScene,
            ProjectSetup.TableScene,
            Galaxy101.Table.EditorTools.TableAssetBuilder.ScenePath
        };
        private static void Build(bool development,bool acceptance=false)
        {
            var scenes=GetBuildScenes();
            foreach(var scene in scenes)
                if(!File.Exists(scene))
                    throw new BuildFailedException("Required authored scene is missing: "+scene+". Prepare project assets explicitly before building.");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
            PlayerSettings.companyName = "Okey101"; PlayerSettings.productName = "Galaxy 101"; PlayerSettings.bundleVersion="0.5.0";
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            // UnityWebRequest needs this for the local test server (http://localhost); GalaxyApi's ServerAddress check
            // keeps every public address on HTTPS.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            // Shader variants for materials the table creates in code (see RuntimeVariantMaterials).
            Galaxy101.Table.EditorTools.RuntimeVariantMaterials.Ensure();
            Galaxy101.EditorTools.ShipBuildMaterials.RequireCurrent();
            PlayerSettings.enableFrameTimingStats = true;
            var buildRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../artifacts/builds"));
            var output = Argument("-okeyBuildPath") ?? Path.Combine(buildRoot, DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8), "Galaxy101.exe");
            output = Path.GetFullPath(output);
            if (!output.StartsWith(buildRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetExtension(output), ".exe", StringComparison.OrdinalIgnoreCase))
                throw new BuildFailedException("The build must be an exe inside this project's artifacts/builds directory.");
            if (File.Exists(output)) throw new BuildFailedException("Refusing to overwrite an existing game build.");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            IncludeDiagnosticContent=development||acceptance;
            // First frame before the menu applies Ayarlar → Görüntü: a borderless window at the monitor's own size (spec/19),
            // the least disruptive start for any saved görüntü modu.
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow; PlayerSettings.defaultIsNativeResolution = true; PlayerSettings.resizableWindow = true;
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                target = BuildTarget.StandaloneWindows64,
                locationPathName = output,
                extraScriptingDefines = IncludeDiagnosticContent ? new[] { "OKEY_DIAGNOSTICS" } : Array.Empty<string>(),
                options = development ? BuildOptions.Development : BuildOptions.None
            }); } finally {IncludeDiagnosticContent=false;}
            var summary = report.summary;
            var evidence = new BuildEvidence
            {
                result = summary.result.ToString(), executable = output, unityVersion = UnityEngine.Application.unityVersion,
                backend = "Mono", graphicsAPI = "Direct3D11", completedUtc = DateTime.UtcNow.ToString("O"),
                configuration = development ? "Development" : acceptance ? "Acceptance" : "Release",
                captureWindowGuardVersion = development ? Galaxy101.Diagnostics.G101StateRouter.CaptureWindowGuardVersion : 0,
                productVersion = PlayerSettings.bundleVersion, ruleVersion = Okey101.Application.ReplayRecord.RuleVersion, tableRuleVersion = Okey101.Core.V4.G101Rules.RuleVersion, productName=PlayerSettings.productName,
                errors = (int)summary.totalErrors, warnings = (int)summary.totalWarnings, bytes = checked((long)summary.totalSize),scenes=scenes
            };
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output), "build-report.json"), JsonUtility.ToJson(evidence, true));
            if (summary.result != BuildResult.Succeeded || !File.Exists(output)) throw new BuildFailedException("Windows build failed: " + summary.result);
            Debug.Log("WINDOWS_BUILD_SUCCEEDED " + evidence.configuration + " " + output);
        }
        private static string Argument(string key)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++) if (arguments[i] == key) return arguments[i + 1];
            return null;
        }
    }
}
