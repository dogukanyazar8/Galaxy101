using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Galaxy101.Editor
{
    public static class MenuAssetBuilder
    {
        private const string ResourcesRoot="Assets/Galaxy101/Resources/Galaxy101/UI";
        [MenuItem("Galaxy 101/Update Menu Assets")]
        public static void PrepareAssets()
        {
            Directory.CreateDirectory(ResourcesRoot);
            foreach(string file in Directory.GetFiles("Assets/Galaxy101/UI","*.uxml",SearchOption.AllDirectories))CopyAsset(file,ResourcesRoot+"/"+Path.GetFileName(file));
            foreach(string file in Directory.GetFiles("Assets/Galaxy101/UI/Styles","*.uss"))CopyAsset(file,ResourcesRoot+"/"+Path.GetFileName(file));
            AssetDatabase.Refresh();PlanetTextureImporter.RefreshImports();
            foreach(string file in Directory.GetFiles("Assets/Galaxy101/Resources/Galaxy101/Artwork","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(file);importer.maxTextureSize=4096;importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            LocalizationImporter.Import();
            string panelPath=ResourcesRoot+"/PanelSettings.asset";var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if(panel==null){panel=ScriptableObject.CreateInstance<PanelSettings>();AssetDatabase.CreateAsset(panel,panelPath);}
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1280,720);panel.screenMatchMode=PanelScreenMatchMode.Expand;panel.match=.5f;// Expand: the 1280×720 canvas always fits, any aspect ratio (16:10, 21:9, 4:3) gets bars instead of cropping
            panel.sortingOrder=10;
            // The authored panel never draws: MenuGammaPresenter renders an owned copy into its own target (and clears that).
            // A UIDocument binds this asset for a moment on scene load; a clearing screen panel left empty would wipe the frame.
            panel.clearColor=false;panel.colorClearValue=new Color(0,0,0,0);
            panel.forceGammaRendering=true;
            string settingsPath=ResourcesRoot+"/TextSettings.asset";var text=AssetDatabase.LoadAssetAtPath<PanelTextSettings>(settingsPath);
            if(text==null){text=ScriptableObject.CreateInstance<PanelTextSettings>();AssetDatabase.CreateAsset(text,settingsPath);}
            foreach(string name in new[]{"Montserrat-600","Montserrat-700","Montserrat-800","Montserrat-900","Baloo2-600","Baloo2-700","Baloo2-800","Nunito-900"})
            {
                string fontPath="Assets/Galaxy101/Resources/Galaxy101/Fonts/"+name;var asset=AssetDatabase.LoadAssetAtPath<FontAsset>(fontPath+".asset");
                if(asset==null)
                {
                    var font=AssetDatabase.LoadAssetAtPath<Font>(fontPath+".ttf");asset=FontAsset.CreateFontAsset(font);asset.name=name;AssetDatabase.CreateAsset(asset,fontPath+".asset");
                    foreach(var atlas in asset.atlasTextures)if(atlas!=null)AssetDatabase.AddObjectToAsset(atlas,asset);if(asset.material!=null)AssetDatabase.AddObjectToAsset(asset.material,asset);
                }
                asset.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789çğıİöşüÇĞÖŞÜ₺×·—…",out string missing);
                if(!string.IsNullOrEmpty(missing))throw new InvalidOperationException("Required font glyphs missing from "+name+": "+missing);
                if(name=="Baloo2-700")text.defaultFontAsset=asset;EditorUtility.SetDirty(asset);
            }
            panel.textSettings=text;EditorUtility.SetDirty(text);EditorUtility.SetDirty(panel);AssetDatabase.SaveAssets();Debug.Log("GALAXY101_MENU_ASSETS_READY");
        }
        [MenuItem("Galaxy 101/Install Menu In Bootstrap")]
        public static void InstallMenu()
        {
            PrepareAssets();
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save open scenes before installing Galaxy menu. No scene was discarded.");
            const string scenePath="Assets/Okey101/Scenes/Bootstrap.unity";
            string backup="artifacts/galaxy101/source-backup/Bootstrap-before-galaxy.unity";Directory.CreateDirectory(Path.GetDirectoryName(backup));if(!File.Exists(backup))File.Copy(scenePath,backup);
            var scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.GetComponent("Okey101.Presentation.MainMenu")!=null||root.GetComponent<MainMenuController>()!=null)UnityEngine.Object.DestroyImmediate(root);
            }
            var menu=new GameObject("Galaxy 101 Main Menu",typeof(UIDocument),typeof(MainMenuController));
            var document=menu.GetComponent<UIDocument>();document.panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>(ResourcesRoot+"/PanelSettings.asset");document.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ResourcesRoot+"/MainMenu.uxml");
            if(!EditorSceneManager.SaveScene(scene,scenePath))throw new IOException("Cannot save Galaxy101 Bootstrap scene.");
            PlayerSettings.productName="Galaxy 101";PlayerSettings.bundleVersion="0.4.0";
            // Bootstrap leads the build; every other listed scene (the 3D table GalaxyTable.unity included) stays.
            var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=scenePath).ToList();scenes.Insert(0,new EditorBuildSettingsScene(scenePath,true));
            if(!scenes.Any(s=>s.path=="Assets/Okey101/Scenes/Table.unity"))scenes.Add(new EditorBuildSettingsScene("Assets/Okey101/Scenes/Table.unity",true));
            EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();Debug.Log("GALAXY101_BOOTSTRAP_READY");
        }
        private static void CopyAsset(string source,string destination)
        {if(File.Exists(destination)&&File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(destination)))return;File.Copy(source,destination,true);}
    }
}
