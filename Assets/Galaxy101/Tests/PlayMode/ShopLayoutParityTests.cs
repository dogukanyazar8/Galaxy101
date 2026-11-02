using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class ShopLayoutParityTests
    {
        private GameObject owner;
        private PanelSettings panel;
        private MainMenuController menu;
        private string path;
        private bool? oldAuth;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldAuth = MainMenuController.AuthOnStart; MainMenuController.AuthOnStart = false;
            path = Path.Combine(Path.GetTempPath(), "g101-shop-layout-" + Guid.NewGuid().ToString("N") + ".json");
            owner = new GameObject("HTML shop layout parity"); owner.SetActive(false);
            panel = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            owner.AddComponent<UIDocument>().panelSettings = panel;
            menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(path);
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator AllThreeTabsKeepTheProductArtworkAndCaptionCenteredAboveThePrice()
        {
            for (int tab = 0; tab < 3; tab++)
            {
                menu.View.OpenPanel("shop", tab); yield return null; yield return null;
                var canvas = menu.View.Root.Q<VisualElement>("galaxy-canvas");
                var firstOffer = menu.View.Services.Economy.GetOffers(tab)[0];
                string firstCard = "offer-" + (string)firstOffer["id"];
                float until = Time.realtimeSinceStartup + 2;
                while (menu.View.Root.Q<VisualElement>(firstCard) == null && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(menu.View.Root.Q<VisualElement>(firstCard), Is.Not.Null, "The requested tab must finish mounting before its layout is measured.");
                yield return null;
                var panelCard = menu.View.Root.Q<VisualElement>("panel-shop");
                DumpCard(canvas, panelCard, tab, "mounted");
                // The HTML capture is frozen after modalIn. Two frames can still be inside the native .28s entry.
                bool AtRest()
                {
                    if (panelCard == null || panelCard.panel == null) return false;
                    var scale = panelCard.resolvedStyle.scale.value;
                    return Mathf.Abs(scale.x - 1) < .0001f && Mathf.Abs(scale.y - 1) < .0001f && Mathf.Abs(panelCard.resolvedStyle.translate.y) < .001f;
                }
                until = Time.realtimeSinceStartup + 2;
                while (!AtRest() && Time.realtimeSinceStartup < until) yield return null;
                DumpCard(canvas, panelCard, tab, "after-entry-wait");
                Assert.That(AtRest(), Is.True, "The modal must complete its authored entry before comparison.");
                yield return null;
                DumpCard(canvas, panelCard, tab, "rest");
                var texts = Diagnostics.G101Dump.Texts();
                for (int index = 0; index < 4; index++)
                {
                    var amount = menu.View.Root.Q<Label>("menu.i.amount[" + index + "]");
                    Assert.That(amount, Is.Not.Null);
                    float top = canvas.WorldToLocal(amount.worldBound.position).y;
                    // Chrome element box is y458.96875 / h25.3; its text Range starts at y458.
                    // Measuring the native label against the text Range obscures a real flex-stack height error.
                    string context = "HTML states 30/31/32: tab=" + tab + ", product=" + index;
                    Assert.That(top, Is.EqualTo(458.96875f).Within(1), context + " caption line box");
                    Assert.That(amount.style.height.value.value, Is.EqualTo(25.3f).Within(.001f), context + " explicit 22px/1.15 line height");
                    Assert.That(amount.resolvedStyle.height, Is.EqualTo(25.3f).Within(1 / amount.scaledPixelsPerPoint), context + " line box snapped to the panel's physical pixel grid");
                    var spacers = amount.parent.Query<VisualElement>(className: "g-panel-spacer").ToList();
                    Assert.That(spacers.Count, Is.EqualTo(2), context + " equal flex space above art and below caption");
                    Assert.That(spacers[0].layout.height, Is.EqualTo(spacers[1].layout.height).Within(.1f), context);
                    foreach (var (name, textTop) in new[] { ("amount", 458), ("sub", 488), ("price", 595) })
                    {
                        string id = "menu.i." + name + "[" + index + "]";
                        var glyph = texts.Single(item => (string)item[8] == id);
                        Assert.That((float)glyph[2], Is.EqualTo(textTop).Within(2), context + " " + name + " text Range");
                    }
                    var price = menu.View.Root.Q<Label>("menu.i.price[" + index + "]");
                    Assert.That(price.resolvedStyle.color, Is.EqualTo(menu.View.C("aT")), context + " theme price colour");
                }
                menu.View.Modals.ClosePanel(); yield return null;
            }
        }

        private void DumpCard(VisualElement canvas, VisualElement panelCard, int tab, string stage)
        {
            var inlineScale = panelCard.style.scale.value.value;
            var resolvedScale = panelCard.resolvedStyle.scale.value;
            TestContext.WriteLine("SHOP_LAYOUT " + stage + " tab=" + tab
                + " attached=" + (panelCard.panel != null) + " parent=" + panelCard.parent?.name
                + " currentNode=" + ReferenceEquals(panelCard, menu.View.Root.Q<VisualElement>("panel-shop"))
                + " inlineScale=" + inlineScale.x.ToString("R") + "," + inlineScale.y.ToString("R") + ":" + panelCard.style.scale.keyword
                + " resolvedScale=" + resolvedScale.x.ToString("R") + "," + resolvedScale.y.ToString("R")
                + " inlineTranslate=" + panelCard.style.translate.value.y.value.ToString("R") + ":" + panelCard.style.translate.keyword
                + " resolvedTranslate=" + panelCard.resolvedStyle.translate.y.ToString("R"));
            var amount = menu.View.Root.Q<Label>("menu.i.amount[0]");
            var offer = amount.parent;
            var parts = new[] { offer, offer.Q<VisualElement>(className: "g-shop-art"), amount, menu.View.Root.Q<Label>("menu.i.sub[0]"), menu.View.Root.Q<Label>("menu.i.price[0]") };
            foreach (var part in parts.Concat(offer.Query<VisualElement>(className: "g-panel-spacer").ToList()))
            {
                var style = part.resolvedStyle;
                string metrics = "";
                if (part is Label label)
                {
                    var measured = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                    metrics = " font=" + style.fontSize + " measured=" + measured + " pixelsPerPoint=" + part.scaledPixelsPerPoint + " authoredHeight=" + part.style.height.value.value;
                }
                TestContext.WriteLine("  " + (string.IsNullOrEmpty(part.name) ? string.Join("/", part.GetClasses()) : part.name)
                    + " y=" + canvas.WorldToLocal(part.worldBound.position).y + " height=" + part.layout.height
                    + " margins=" + style.marginTop + "," + style.marginBottom + " padding=" + style.paddingTop + "," + style.paddingBottom + metrics);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (panel != null) UnityEngine.Object.Destroy(panel);
            MainMenuController.AuthOnStart = oldAuth;
            yield return null;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
