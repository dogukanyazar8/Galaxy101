using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Galaxy101.Table.EditorTools
{
    /// <summary>
    /// Adds <see cref="ThreeToneMappingFeature"/> to every URP renderer in the project (PC and Mobile), once. The feature
    /// only acts on cameras carrying <see cref="ThreeToneMapping"/>, so the menu and UI cameras are untouched.
    /// Batch: -executeMethod Galaxy101.Table.EditorTools.ThreeToneMappingInstaller.Install
    /// </summary>
    public static class ThreeToneMappingInstaller
    {
        [MenuItem("Galaxy101/Install Table Tone Mapping")]
        public static void Install()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                if (!path.StartsWith("Assets/") || data == null || data.rendererFeatures.Any(f => f is ThreeToneMappingFeature)) continue;
                var feature = ScriptableObject.CreateInstance<ThreeToneMappingFeature>(); feature.name = "ThreeToneMapping";
                AssetDatabase.AddObjectToAsset(feature, data);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
                var so = new SerializedObject(data);
                var features = so.FindProperty("m_RendererFeatures"); var map = so.FindProperty("m_RendererFeatureMap");
                features.arraySize++; features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
                map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
                Debug.Log("Galaxy101: three.js tone mapping added to " + path);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
