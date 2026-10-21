using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class MenuColorRenderingTests
    {
        [UnityTest]
        public IEnumerator AuthoredMenuCompositesTranslucentWhiteInCssColorSpace()
        {
            var panel=Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            var target=new RenderTexture(64,64,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var host=new GameObject("Native CSS alpha fixture");host.SetActive(false);
            var document=host.AddComponent<UIDocument>();panel.targetTexture=target;document.panelSettings=panel;host.SetActive(true);
            var root=document.rootVisualElement;
            var background=new VisualElement();background.style.position=Position.Absolute;background.style.left=background.style.right=background.style.top=background.style.bottom=0;
            background.style.backgroundColor=(Color)new Color32(16,19,58,255);root.Add(background);
            var overlay=new VisualElement();overlay.style.position=Position.Absolute;overlay.style.left=Length.Percent(50);overlay.style.right=overlay.style.top=overlay.style.bottom=0;
            overlay.style.backgroundColor=new Color(1,1,1,.07f);root.Add(overlay);
            Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                yield return null;yield return null;yield return null;
                RenderTexture.active=target;pixels=new Texture2D(64,64,TextureFormat.RGBA32,false);
                pixels.ReadPixels(new Rect(0,0,64,64),0,0);pixels.Apply();
                var mixed=(Color32)pixels.GetPixel(48,32);
                // Browser CSS: .93 * (16,19,58) + .07 * (255,255,255).
                Assert.That((int)mixed.r,Is.EqualTo(33).Within(1),"7% white must not become a bright linear-space ring.");
                Assert.That((int)mixed.g,Is.EqualTo(36).Within(1));Assert.That((int)mixed.b,Is.EqualTo(72).Within(1));
                var solid=(Color32)pixels.GetPixel(16,32);
                Assert.That((int)solid.r,Is.EqualTo(16).Within(1));Assert.That((int)solid.g,Is.EqualTo(19).Within(1));Assert.That((int)solid.b,Is.EqualTo(58).Within(1));
            }
            finally
            {
                RenderTexture.active=previous;
                if(pixels!=null)Object.Destroy(pixels);Object.Destroy(host);Object.Destroy(panel);target.Release();Object.Destroy(target);
            }
            yield return null;
        }
    }
}
