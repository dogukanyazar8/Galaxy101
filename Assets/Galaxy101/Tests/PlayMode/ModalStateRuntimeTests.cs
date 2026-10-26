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
    public sealed class ModalStateRuntimeTests
    {
        private GameObject host;
        private PanelSettings settings;
        private RenderTexture target;
        private MenuModel model;
        private MenuView view;
        private LocalizationBridge localization;
        private string savePath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savePath = Path.Combine(Path.GetTempPath(), "galaxy-modal-state-" + Guid.NewGuid().ToString("N") + ".json");
            model = new MenuModel(Resources.Load<TextAsset>("Galaxy101/content").text, Resources.Load<TextAsset>("Galaxy101/themes").text, savePath);
            localization = new LocalizationBridge(model);
            var authored = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            Assert.That(authored, Is.Not.Null);
            settings = UnityEngine.Object.Instantiate(authored);
            target = new RenderTexture(1280, 720, 0);
            settings.targetTexture = target;
            host = new GameObject("Native modal state regression"); host.SetActive(false);
            var document = host.AddComponent<UIDocument>(); document.panelSettings = settings;
            host.SetActive(true);
            view = new MenuView(document.rootVisualElement, model, new MenuServices(model), localization);
            view.BuildMain();
            yield return Until(() => view.Root.panel != null && view.Root.Q<Button>("menu.openSettings").worldBound.width > 0, "Native runtime panel did not attach and lay out.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            view?.Dispose(); localization?.Dispose();
            if (host != null) UnityEngine.Object.Destroy(host);
            if (settings != null) UnityEngine.Object.Destroy(settings);
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            yield return null;
            if (File.Exists(savePath)) File.Delete(savePath);
        }

        private static IEnumerator Until(Func<bool> predicate, string message)
        {
            var deadline = Time.realtimeSinceStartupAsDouble + 2;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, message);
        }

        private bool FocusedWithin(VisualElement element)
        {
            var focused = view.Root.panel?.focusController?.focusedElement as VisualElement;
            return focused == element || focused != null && element.Contains(focused);
        }

        private bool ModalReady(bool nested)
        {
            var layer = view.Root.Q<VisualElement>(nested ? "dialog-layer" : "modal-layer");
            var card = layer?.Q<VisualElement>(className: nested ? "dialog-card" : "panel-card");
            return card != null && card.resolvedStyle.opacity >= .999f;
        }

        private static void Submit(VisualElement element)
        {
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = element; element.SendEvent(submit); }
        }

        [UnityTest]
        public IEnumerator SwitchingEveryPanelTabKeepsTheOpenShellAndMainSceneVisible()
        {
            var keys = new[] { "settings", "shop", "friends", "leader", "rules", "me" };
            var counts = new[] { 5, 3, 3, 3, 6, 3 };
            var main = view.Root.Q<Button>("menu.play");
            for (int p = 0; p < keys.Length; p++)
            {
                view.OpenPanel(keys[p], 0);
                yield return Until(() => ModalReady(false), keys[p] + " did not finish opening.");
                var layer = view.Root.Q<VisualElement>("modal-layer");
                var card = layer.Q<VisualElement>(className: "panel-card");
                var backdrop = layer.Q<VisualElement>("panel-backdrop");
                for (int tab = 1; tab < counts[p]; tab++)
                {
                    view.SetTab(keys[p], tab);
                    yield return null; yield return null; yield return null;
                    Assert.That(layer.Q<VisualElement>(className: "panel-card"), Is.SameAs(card), keys[p] + " recreated its open window on tab " + tab);
                    Assert.That(layer.Q<VisualElement>("panel-backdrop"), Is.SameAs(backdrop), keys[p] + " recreated its backdrop on tab " + tab);
                    Assert.That(card.resolvedStyle.opacity, Is.GreaterThanOrEqualTo(.999f), keys[p] + " replayed the opening fade on tab " + tab);
                    Assert.That(view.Root.Q<Button>("menu.play"), Is.SameAs(main), keys[p] + " rebuilt the main scene behind the window.");
                }
                view.CloseTop();
                yield return Until(() => view.Root.Q<VisualElement>(className: "closing-ghost") == null, "Closing animation did not finish.");
            }
        }

        [UnityTest]
        public IEnumerator OpeningAndRefreshingNestedDialogDoesNotReplayTheUnderlyingPanel()
        {
            view.OpenPanel("settings", 4);
            yield return Until(() => ModalReady(false), "Settings did not finish opening.");
            var layer = view.Root.Q<VisualElement>("modal-layer");
            var card = layer.Q<VisualElement>(className: "panel-card");
            view.OpenDialog("delete");
            yield return Until(() => ModalReady(true), "Delete dialog did not finish opening.");
            Assert.That(layer.Q<VisualElement>(className: "panel-card"), Is.SameAs(card));
            var dialog = view.Root.Q<VisualElement>("dialog-layer").Q<VisualElement>(className: "dialog-card");
            model.NotifyChanged();
            yield return null; yield return null; yield return null;
            Assert.That(layer.Q<VisualElement>(className: "panel-card"), Is.SameAs(card));
            Assert.That(card.resolvedStyle.opacity, Is.GreaterThanOrEqualTo(.999f));
            Assert.That(view.Root.Q<VisualElement>("dialog-layer").Q<VisualElement>(className: "dialog-card"), Is.SameAs(dialog));
            Assert.That(dialog.resolvedStyle.opacity, Is.GreaterThanOrEqualTo(.999f));
        }

        [UnityTest]
        public IEnumerator KeyboardOpeningMovesFocusInsideEachNewModal()
        {
            var opener = view.Root.Q<Button>("menu.openSettings"); opener.Focus(); yield return null;
            Submit(opener);
            yield return Until(() => ModalReady(false), "Settings did not finish opening.");
            var focused = view.Root.panel.focusController.focusedElement as VisualElement;
            Assert.That(focused != null && view.Root.Q<VisualElement>("modal-layer").Contains(focused), Is.True, "Keyboard opening left focus outside settings.");
            view.OpenDialog("profile");
            yield return Until(() => ModalReady(true), "Profile dialog did not finish opening.");
            focused = view.Root.panel.focusController.focusedElement as VisualElement;
            Assert.That(focused != null && view.Root.Q<VisualElement>("dialog-layer").Contains(focused), Is.True, "Nested opening left focus on the underlying panel.");
        }

        [UnityTest]
        public IEnumerator DeleteConfirmationDraftSurvivesRefreshAfterFocusMovesToAButton()
        {
            view.OpenPanel("settings", 4); view.OpenDialog("delete");
            yield return Until(() => ModalReady(true), "Delete dialog did not finish opening.");
            view.Root.Q<TextField>("menu.cf.onType").value = "SİL";
            var confirmButton = view.Root.Q<Button>("menu.cf.go"); confirmButton.Focus(); yield return null;
            model.NotifyChanged(); yield return null; yield return null; yield return null;
            Assert.That(view.Root.Q<TextField>("menu.cf.onType").value, Is.EqualTo("SİL"));
            Assert.That(view.Root.Q<Button>("menu.cf.go").enabledSelf, Is.True);
            view.CloseTop(); yield return null; yield return null;
            view.OpenDialog("delete"); yield return Until(() => ModalReady(true), "Reopened delete dialog did not finish opening.");
            Assert.That(view.Root.Q<TextField>("menu.cf.onType").value, Is.Empty, "A new destructive-action dialog must require a fresh confirmation.");
            Assert.That(view.Root.Q<Button>("menu.cf.go").enabledSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ClosingSettingsReturnsFocusToRetainedNamedMainTrigger()
        {
            var opener = view.Root.Q<Button>("menu.openSettings"); opener.Focus();
            yield return Until(() => FocusedWithin(opener), "Main settings trigger did not receive focus.");
            Submit(opener);
            yield return Until(() => view.Root.Q<Button>("menu.closeSettings#2") != null, "Settings did not open.");
            yield return Until(() => ModalReady(false), "Settings opening animation did not complete.");
            var close = view.Root.Q<Button>("menu.closeSettings#2"); close.Focus();
            yield return Until(() => FocusedWithin(close), "Settings close did not receive focus.");
            yield return null;
            Submit(close);
            yield return Until(() => view.Modals.Panel == null && FocusedWithin(view.Root.Q<Button>("menu.openSettings")), "Closing settings did not restore focus to the live settings trigger.");
            Assert.That(view.Root.Q<Button>("menu.openSettings"), Is.SameAs(opener), "Opening settings must preserve the main scene and its original trigger.");
            Assert.That(view.Root.Q<Button>("menu.openSettings").enabledInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator ClosingNestedDialogReturnsFocusToNamedSettingsControl()
        {
            view.OpenPanel("settings");
            yield return Until(() => view.Root.Q<Button>("menu.closeSettings#2") != null, "Settings did not open.");
            yield return Until(() => ModalReady(false), "Settings opening animation did not complete.");
            var origin = view.Root.Q<Button>("menu.closeSettings#2"); origin.Focus();
            yield return Until(() => FocusedWithin(origin), "Settings control did not receive focus.");
            yield return null;
            view.OpenDialog("share");
            yield return Until(() => view.Root.Q<Button>("menu.sh.copy") != null, "Nested share dialog did not open.");
            yield return Until(() => ModalReady(true), "Share dialog opening animation did not complete.");
            var activeClose = view.Root.Q<VisualElement>("dialog-layer").Q<Button>("menu.closeDlg#6"); activeClose.Focus();
            yield return Until(() => FocusedWithin(activeClose), "Nested close control did not receive focus.");
            yield return null;
            Submit(activeClose);
            yield return Until(() => view.Modals.Dialog == null && FocusedWithin(view.Root.Q<Button>("menu.closeSettings#2")), "Nested close did not restore focus to the live named settings control.");
            Assert.That(view.Modals.Panel, Is.EqualTo("settings"));
            Assert.That(view.Root.Q<Button>("menu.closeSettings#2"), Is.Not.SameAs(origin));
        }

        [UnityTest]
        public IEnumerator ModelRefreshPreservesFocusedProfileDraftCaretAndSelection()
        {
            view.OpenPanel("me"); view.OpenDialog("profile");
            yield return Until(() => view.Root.Q<TextField>("menu.pf.onName") != null, "Profile form did not open.");
            yield return Until(() => ModalReady(true), "Profile dialog opening animation did not complete.");
            var original = view.Root.Q<TextField>("menu.pf.onName"); original.value = "İpek Test"; original.Focus();
            yield return Until(() => FocusedWithin(original), "Profile name did not receive focus.");
            yield return null;
            original.SelectRange(2, 6);
            var detail = "value=" + original.value + "; text=" + original.text + "; focused=" + (view.Root.panel.focusController.focusedElement as VisualElement)?.name;
            Assert.That(original.cursorIndex, Is.EqualTo(2), detail); Assert.That(original.selectIndex, Is.EqualTo(6), detail);
            model.NotifyChanged();
            yield return Until(() => view.Root.Q<TextField>("menu.pf.onName") != original, "Model notification did not rebuild the field.");
            var replacement = view.Root.Q<TextField>("menu.pf.onName");
            yield return Until(() => FocusedWithin(replacement) && replacement.cursorIndex == 2 && replacement.selectIndex == 6, "Model refresh lost the text field focus or selected range.");
            Assert.That(replacement.value, Is.EqualTo("İpek Test"));
            Assert.That((string)model.Profile["name"], Is.EqualTo("Doğukan Y."), "Refreshing a draft must not save it.");
        }

        [UnityTest]
        public IEnumerator NestedDialogBlocksNavigationSubmitOnUnderlyingSave()
        {
            view.OpenPanel("settings", 1);
            yield return Until(() => view.Root.Q<GalaxySlider>("setting-master") != null, "Sound settings did not open.");
            yield return Until(() => ModalReady(false), "Sound settings opening animation did not complete.");
            view.Root.Q<GalaxySlider>("setting-master").Value = 12;
            Assert.That((int)model.Settings["master"], Is.EqualTo(80));
            view.OpenDialog("profile");
            yield return Until(() => view.Root.Q<TextField>("menu.pf.onName") != null, "Nested profile form did not open.");
            yield return Until(() => ModalReady(true), "Nested profile opening animation did not complete.");
            var underlyingSave = view.Root.Q<VisualElement>("modal-layer").Q<Button>("menu.closeSettings#4");
            Assert.That(underlyingSave, Is.Not.Null);
            Submit(underlyingSave);
            yield return null;
            Assert.That((int)model.Settings["master"], Is.EqualTo(80), "Underlying submit saved a settings draft through the modal.");
            Assert.That(view.Modals.Dialog, Is.EqualTo("profile"));
            Assert.That(view.Modals.Panel, Is.EqualTo("settings"));
        }

        [UnityTest]
        public IEnumerator FocusedDeleteConfirmationSurvivesADataRefresh()
        {
            view.OpenPanel("settings",4);view.OpenDialog("delete");yield return Until(()=>ModalReady(true),"Delete dialog was not ready.");
            var field=view.Root.Q<TextField>("menu.cf.onType");field.Focus();yield return null;field.value="SİL";
            Assert.That(view.Root.Q<Button>("menu.cf.go").enabledSelf,Is.True);model.NotifyChanged();
            yield return Until(()=>view.Root.Q<TextField>("menu.cf.onType")!=field,"Delete field was not refreshed.");
            yield return Until(()=>view.Root.Q<TextField>("menu.cf.onType").value=="SİL"&&view.Root.Q<Button>("menu.cf.go").enabledSelf,"Data refresh lost typed deletion confirmation.");
        }
        [UnityTest]
        public IEnumerator ClosingGhostCannotFocusOrSubmitAnOldFriendAction()
        {
            view.OpenPanel("friends");
            yield return Until(() => view.Root.Q<Button>("invite-ay") != null, "Online friends did not open.");
            yield return Until(() => ModalReady(false), "Friends opening animation did not complete.");
            var invite = view.Root.Q<Button>("invite-ay");
            view.CloseTop();
            var ghost = view.Root.Q<VisualElement>(className: "closing-ghost");
            Assert.That(ghost, Is.Not.Null, "The regression must inspect the active close animation.");
            Assert.That(ghost.enabledSelf, Is.False);
            Assert.That(ghost.Query<VisualElement>().ToList().All(element => !element.focusable && element.tabIndex < 0), Is.True, "Closing controls remained in keyboard navigation.");
            Assert.That(invite.enabledInHierarchy, Is.False);
            Submit(invite);
            Assert.That(model.Invited, Does.Not.Contain("ay"), "A closing friend action still dispatched an invitation.");
            yield return Until(() => view.Root.Q<VisualElement>(className: "closing-ghost") == null, "Close animation did not release the ghost tree.");
        }
    }
}
