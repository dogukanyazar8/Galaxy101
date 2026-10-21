using Okey101.Presentation;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Okey101.Editor
{
    public sealed class DiagnosticsBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if(report==null || (report.summary.options & BuildOptions.Development)!=0 || BuildWindows.IncludeDiagnosticContent) return;
            var count=0;
            foreach(var root in scene.GetRootGameObjects())
                foreach(var panel in root.GetComponentsInChildren<DebugPanel>(true)) { Object.DestroyImmediate(panel);count++; }
            Debug.Log("T10_RELEASE_DIAGNOSTICS_STRIPPED scene="+scene.name+" panels="+count);
        }
    }
}
