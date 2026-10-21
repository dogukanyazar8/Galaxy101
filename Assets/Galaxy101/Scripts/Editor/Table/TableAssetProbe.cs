using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Galaxy101.Table.EditorTools
{
    /// <summary>Batch diagnostics: dumps the imported GLB hierarchies (names, transforms, renderers, materials).</summary>
    public static class TableAssetProbe
    {
        public static void Dump()
        {
            var output = new StringBuilder();
            foreach (var path in Directory.GetFiles("Assets/Galaxy101/Table/Models", "*.glb").OrderBy(p => p))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace(Path.DirectorySeparatorChar, '/'));
                output.AppendLine("== " + Path.GetFileName(path) + (root == null ? " (NOT IMPORTED)" : ""));
                if (root == null) continue;
                void Walk(Transform t, int depth)
                {
                    var renderer = t.GetComponent<Renderer>();
                    var mats = renderer ? string.Join(",", renderer.sharedMaterials.Select(m => m ? m.name + "/" + m.shader.name : "null")) : "";
                    output.AppendLine(new string(' ', depth * 2) + t.name + " p" + t.localPosition.ToString("F3") + " r" + t.localEulerAngles.ToString("F1") + " s" + t.localScale.ToString("F2") + (renderer ? " [" + mats + "]" : ""));
                    if (depth < 3) foreach (Transform c in t) Walk(c, depth + 1);
                }
                Walk(root.transform, 0);
            }
            File.WriteAllText(System.Environment.GetEnvironmentVariable("GALAXY_PROBE_OUT") ?? "probe.txt", output.ToString());
        }
    }
}
