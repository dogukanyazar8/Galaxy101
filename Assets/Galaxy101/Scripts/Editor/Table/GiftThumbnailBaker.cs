using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101.Table.EditorTools
{
    /// <summary>
    /// Gift panel pictures (spec/13 §1): every gift model rendered once to a 220×220 transparent PNG, like table3d.js
    /// thumbs(): 30° FOV camera at centre + size·(0.7, 0.95, 1.5) looking at the centre, hemisphere light
    /// #ffffff/#30344a 1.1 and a #fff4e2 key light 2.4 from (1, 2, 1.6). Output: Resources/Galaxy101/Table/GiftThumbs.
    /// </summary>
    public static class GiftThumbnailBaker
    {
        public const string Folder = "Assets/Galaxy101/Resources/Galaxy101/Table/GiftThumbs";
        private const int Size = 220;

        public static void Bake()
        {
            var catalog = TableAssetCatalog.Load();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Folder);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.white * 1.1f; RenderSettings.ambientEquatorColor = Color.Lerp(Color.white, Hex("#30344a"), .5f) * 1.1f; RenderSettings.ambientGroundColor = Hex("#30344a") * 1.1f;
            var key = new GameObject("key").AddComponent<Light>(); key.type = LightType.Directional; key.color = Hex("#fff4e2"); key.intensity = 2.4f;
            key.transform.rotation = Quaternion.LookRotation(-ThreeSpace.ToUnity(new Vector3(1, 2, 1.6f)).normalized);
            var cam = new GameObject("thumb camera").AddComponent<Camera>();
            cam.fieldOfView = 30; cam.nearClipPlane = 0.01f; cam.farClipPlane = 10; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear; cam.allowHDR = false;
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.aspect = 1;
            foreach (var kind in TableAssetCatalog.Gifts)
            {
                var prefab = TableAssetCatalog.Find(catalog.GiftModels, kind);
                var gift = UnityEngine.Object.Instantiate(prefab); gift.name = kind;
                foreach (var lod in gift.GetComponentsInChildren<LODGroup>(true)) lod.ForceLOD(0);
                // Flight-only parts stay out of the still picture (the rocket flame).
                foreach (var t in gift.GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("alev", StringComparison.Ordinal)) t.gameObject.SetActive(false);
                var renderers = gift.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.name.EndsWith("_LOD1", StringComparison.Ordinal)).ToArray();
                var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                float size = bounds.size.magnitude; var centre = bounds.center;
                cam.transform.position = centre + ThreeSpace.ToUnity(new Vector3(0.7f, 0.95f, 1.5f)) * size;
                cam.transform.LookAt(centre);
                // Glass and liquids blend with whatever is behind them, so the alpha a single render leaves is wrong. Render
                // on black and on white: alpha = 1 − (white − black), colour = black / alpha (straight alpha).
                var onBlack = Shot(cam, rt, Color.black); var onWhite = Shot(cam, rt, Color.white);
                var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                var pixels = new Color[Size * Size];
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color b = onBlack[i], w = onWhite[i];
                    float alpha = Mathf.Clamp01(1 - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3);
                    pixels[i] = alpha < 0.004f ? Color.clear : new Color(Mathf.Clamp01(b.r / alpha), Mathf.Clamp01(b.g / alpha), Mathf.Clamp01(b.b / alpha), alpha);
                }
                image.SetPixels(pixels); image.Apply();
                string path = Folder + "/" + kind + ".png";
                File.WriteAllBytes(path, image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(gift);
                Debug.Log("GALAXY101_GIFT_THUMB " + path);
            }
            rt.Release();
            AssetDatabase.Refresh();
            foreach (var kind in TableAssetCatalog.Gifts)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/" + kind + ".png");
                importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
                importer.sRGBTexture = true; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        private static Color[] Shot(Camera cam, RenderTexture rt, Color background)
        {
            cam.backgroundColor = background;
            for (int pass = 0; pass < 3; pass++) RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest { destination = rt });
            var resolved = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(rt, resolved);
            var previous = RenderTexture.active; RenderTexture.active = resolved;
            var read = new Texture2D(Size, Size, TextureFormat.RGBA32, false); read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); read.Apply();
            RenderTexture.active = previous; resolved.Release();
            var colors = read.GetPixels(); UnityEngine.Object.DestroyImmediate(read); return colors;
        }

        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    }
}
