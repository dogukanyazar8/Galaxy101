using System;
using System.Collections;
using System.IO;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class TableLoadingFailureTests
    {
        private GameObject owner;
        private PanelSettings panel;
        private RenderTexture target;
        private MenuView view;
        private LocalizationBridge localization;
        private TableLoadingOverlay loading;
        private string savePath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            loading?.Hide(); view?.Dispose(); localization?.Dispose();
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (panel != null) UnityEngine.Object.Destroy(panel);
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            yield return null;
            if (savePath != null && File.Exists(savePath)) File.Delete(savePath);
        }

        [UnityTest]
        public IEnumerator FailureKeepsLoadingVisibleUnderTheDimAndBothActionsWork()
        {
            savePath = Path.Combine(Path.GetTempPath(), "g101-loading-failure-" + Guid.NewGuid().ToString("N") + ".json");
            var model = MenuModel.LoadResources(savePath); model.Settings["lang"] = "tr";
            localization = new LocalizationBridge(model);
            panel = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            target = new RenderTexture(1280, 720, 0); panel.targetTexture = target;
            owner = new GameObject("Loading failure test"); owner.SetActive(false);
            var document = owner.AddComponent<UIDocument>(); document.panelSettings = panel; owner.SetActive(true);
            view = new MenuView(document.rootVisualElement, model, new MenuServices(model), localization);
            var canvas = new VisualElement { name = "galaxy-canvas" }; canvas.AddToClassList("galaxy-canvas"); view.Root.Add(canvas);
            loading = new TableLoadingOverlay(view, owner.transform); loading.Show(5);
            loading.SetProgress(.08f);
            bool lobby = false, retry = false;
            loading.ShowError(() => lobby = true, () => retry = true);
            yield return Until(() => canvas.Q<VisualElement>("load.fail")?.worldBound.height > 0);
            var card = canvas.Q<VisualElement>("load.fail");
            Assert.That(canvas.Q<VisualElement>("load.logo").worldBound.width, Is.GreaterThan(0), "T51 must keep the loading content behind the dim");
            Assert.That(canvas.Q<Label>("load.pct").text, Is.EqualTo("8%"));
            Assert.That(card.worldBound.width, Is.EqualTo(460).Within(1));
            Assert.That(card.worldBound.center.x, Is.EqualTo(640).Within(1));
            Assert.That(card.worldBound.center.y, Is.EqualTo(360).Within(1));
            Assert.That(canvas.Q<VisualElement>("load.fail.body").resolvedStyle.fontSize, Is.EqualTo(15));
            Submit(canvas.Q<VisualElement>("load.fail.lobby")); Assert.That(lobby, Is.True);
            Submit(canvas.Q<VisualElement>("load.fail.retry"));
            Assert.That(retry, Is.True); Assert.That(loading.ErrorVisible, Is.False);
            Assert.That(canvas.Q<VisualElement>("load.logo").worldBound.width, Is.GreaterThan(0));
        }

        private static IEnumerator Until(Func<bool> predicate)
        {
            float until = Time.realtimeSinceStartup + 3;
            while (!predicate() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(predicate(), Is.True, "Loading failure layout did not attach");
        }

        private static void Submit(VisualElement element)
        {
            using var submit = NavigationSubmitEvent.GetPooled(); submit.target = element; element.SendEvent(submit);
        }
    }
}
