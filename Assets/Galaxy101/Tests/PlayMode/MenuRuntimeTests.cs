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
    public sealed class MenuRuntimeTests
    {
        private GameObject owner;
        private PanelSettings panel;
        private MainMenuController controller;
        private string statePath;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            statePath=Path.Combine(Path.GetTempPath(),"galaxy-runtime-"+Guid.NewGuid().ToString("N")+".json");
            owner=new GameObject("Native menu runtime test");owner.SetActive(false);
            var document=owner.AddComponent<UIDocument>();panel=UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));document.panelSettings=panel;
            controller=owner.AddComponent<MainMenuController>();owner.SetActive(true);controller.Initialize(statePath);
            yield return null;yield return null;
            Assert.That(controller.View.Root.panel,Is.Not.Null,"Input coverage requires an attached native runtime panel.");
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Object.Destroy(owner);UnityEngine.Object.Destroy(panel);yield return null;
            if(File.Exists(statePath))File.Delete(statePath);
        }
        private static void Press(VisualElement element,KeyCode key,bool shift=false)
        {
            var source=new Event {type=EventType.KeyDown,keyCode=key,modifiers=shift?EventModifiers.Shift:EventModifiers.None};
            using(var evt=KeyDownEvent.GetPooled(source))element.SendEvent(evt);
        }
        [UnityTest]
        public IEnumerator InactiveModeCardMatchesTheSourceSaturationAndBrightnessWithoutExtraDimming()
        {
            controller.View.SetFocused(false);var root=controller.View.Root;
            var card=root.Q<Button>("mode-3");var bounds=card.worldBound;
            var target=owner.GetComponent<MenuGammaPresenter>().Output;
            yield return null;yield return null;
            var pixels=ReadFrame(target);
            try
            {
                var position=new Vector2(bounds.xMin+8,bounds.yMin+42);var rootBounds=root.worldBound;
                int x=Mathf.RoundToInt((position.x-rootBounds.xMin)/rootBounds.width*target.width);
                int y=target.height-1-Mathf.RoundToInt((position.y-rootBounds.yMin)/rootBounds.height*target.height);
                var color=(Color32)pixels.GetPixel(x,y);
                // CSS saturate(.7) brightness(.78) over the solid #16B6A8 Dereceli card.
                Assert.That((int)color.r,Is.EqualTo(46).Within(2));
                Assert.That((int)color.g,Is.EqualTo(134).Within(2));
                Assert.That((int)color.b,Is.EqualTo(126).Within(2));
            }
            finally{UnityEngine.Object.Destroy(pixels);}
        }
        [UnityTest]
        public IEnumerator MenuOwnsGammaTargetAndMapsScreenPixelsToTheSameNativeControls()
        {
            var document=controller.GetComponent<UIDocument>();var target=document.panelSettings.targetTexture;
            Assert.That(target,Is.Not.Null,"The displayed menu must use its own gamma UI target, independent of the platform backbuffer.");
            Assert.That(target.sRGB,Is.False,"Encoded CSS colors need a UNorm UI target.");
            Assert.That(document.panelSettings.forceGammaRendering,Is.True);
            Assert.That(target.width,Is.EqualTo(Screen.width));Assert.That(target.height,Is.EqualTo(Screen.height));
            var root=controller.View.Root;var bounds=root.worldBound;
            var topLeft=RuntimePanelUtils.ScreenToPanel(root.panel,Vector2.zero);
            var bottomRight=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(Screen.width,Screen.height));
            Assert.That(Vector2.Distance(topLeft,bounds.min),Is.LessThan(.1f),"Input mapping added an extra offset or Y flip.");
            Assert.That(Vector2.Distance(bottomRight,bounds.max),Is.LessThan(.1f),"Input mapping applied scaling twice.");
            var play=root.Q<Button>("play-now");var logicalPoint=play.worldBound.center;
            var physicalPoint=new Vector2(logicalPoint.x/bounds.width*Screen.width,logicalPoint.y/bounds.height*Screen.height);
            var picked=root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel,physicalPoint));
            Assert.That(picked==play||picked!=null&&play.Contains(picked),Is.True,"Displayed pixels and native button picking disagree.");
            yield return null;
        }
        [UnityTest]
        public IEnumerator DestroyingPresenterReleasesItsFramebufferAndRestoresOnlyItsPanelAssignment()
        {
            var document=controller.GetComponent<UIDocument>();var presenter=owner.GetComponent<MenuGammaPresenter>();
            var ownedTarget=presenter.Output;var ownedPanel=document.panelSettings;
            Assert.That(ownedPanel,Is.Not.SameAs(panel));Assert.That(ownedTarget.IsCreated(),Is.True);
            UnityEngine.Object.Destroy(presenter);yield return null;yield return null;
            Assert.That(document.panelSettings,Is.SameAs(panel),"Presenter teardown changed the original panel ownership.");
            Assert.That(ownedTarget==null,Is.True,"The menu framebuffer outlived its presenter.");
            Assert.That(ownedPanel==null,Is.True,"The cloned native panel outlived its presenter.");
            Assert.That(owner.GetComponentsInChildren<Camera>(true),Is.Empty,"The menu's presentation camera leaked into the next scene.");
        }
        [UnityTest]
        public IEnumerator PresentationCameraPreservesUiColorsAndVerticalOrientation()
        {
            controller.View.SetFocused(false);var background=owner.GetComponentInChildren<GalaxyBackground>();background.SetFocused(false);
            var source=owner.GetComponent<MenuGammaPresenter>().Output;background.RenderOnce(source.width,source.height);
            var camera=owner.GetComponentInChildren<Camera>();var oldTarget=camera.targetTexture;
            var display=new RenderTexture(source.width,source.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            Texture2D expected=null,actual=null;
            try
            {
                display.Create();camera.targetTexture=display;
                yield return null;yield return null;yield return null;yield return null;
                expected=ReadFrame(source);actual=ReadFrame(display);
                float low=3,high=0;
                foreach(var point in new[]{new Vector2(.04f,.93f),new Vector2(.04f,.06f),new Vector2(.5f,.5f),new Vector2(.98f,.96f),new Vector2(.9f,.1f)})
                {
                    int x=Mathf.RoundToInt(point.x*(source.width-1)),y=Mathf.RoundToInt(point.y*(source.height-1));
                    var want=(Color32)expected.GetPixel(x,y);var got=(Color32)actual.GetPixel(x,y);
                    low=Mathf.Min(low,(want.r+want.g+want.b)/255f);high=Mathf.Max(high,(want.r+want.g+want.b)/255f);
                    Assert.That((int)got.r,Is.EqualTo((int)want.r).Within(2),"Presentation red at "+point);
                    Assert.That((int)got.g,Is.EqualTo((int)want.g).Within(2),"Presentation green at "+point);
                    Assert.That((int)got.b,Is.EqualTo((int)want.b).Within(2),"Presentation blue at "+point);
                }
                Assert.That(high-low,Is.GreaterThan(.1f),"A blank source must not satisfy the presentation round-trip.");
            }
            finally
            {
                camera.targetTexture=oldTarget;display.Release();UnityEngine.Object.Destroy(display);
                if(expected!=null)UnityEngine.Object.Destroy(expected);if(actual!=null)UnityEngine.Object.Destroy(actual);
            }
        }
        private static Texture2D ReadFrame(RenderTexture target)
        {
            var previous=RenderTexture.active;
            try
            {
                RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();return pixels;
            }
            finally{RenderTexture.active=previous;}
        }
        [UnityTest]
        public IEnumerator NativeSliderSupportsPointerCaptureDragReleaseAndKeyboardWithoutSavingDraft()
        {
            var view=controller.View;view.OpenPanel("settings",1);yield return null;yield return null;
            var slider=view.Root.Q<GalaxySlider>("setting-master");var track=slider.Q<VisualElement>(className:"slider-track");
            yield return null;Assert.That(track.worldBound.width,Is.GreaterThan(200));
            var middle=track.worldBound.center;
            using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=middle}))slider.SendEvent(down);
            Assert.That(slider.IsDragging,Is.True);Assert.That(slider.Value,Is.InRange(49,51));yield return null;
            var end=new Vector2(track.worldBound.xMax,track.worldBound.center.y);
            using(var move=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseDrag,button=0,mousePosition=end}))slider.SendEvent(move);
            Assert.That(slider.Value,Is.EqualTo(100));
            using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=end}))slider.SendEvent(up);
            Assert.That(slider.IsDragging,Is.False);Press(slider,KeyCode.LeftArrow);Assert.That(slider.Value,Is.EqualTo(99));
            Assert.That((int)controller.Model.Settings["master"],Is.EqualTo(80),"Dragging a draft must not persist settings.");
        }
        [UnityTest]
        public IEnumerator DecoratedButtonMeasuresItsCaptionAndUpdatesFeedbackText()
        {
            var button=controller.View.Button("Cancel",()=>{},"secondary");button.name="caption-measure";button.style.position=Position.Absolute;button.style.left=100;button.style.top=100;
            controller.View.Root.Q<VisualElement>("main-layer").Add(button);yield return null;yield return null;
            var caption=button.Q<Label>("button-caption");Assert.That(caption,Is.Not.Null);
            Assert.That(button.worldBound.width,Is.GreaterThanOrEqualTo(caption.worldBound.width+32),"Decorative children must not remove the text's intrinsic width or padding.");
            Assert.That(button.style.filter.value.Any(f=>f.type==FilterFunctionType.DropShadow&&f.GetParameter(2).floatValue==0),Is.True);
            button.text="Copied successfully";yield return null;yield return null;
            Assert.That(caption.text,Is.EqualTo("Copied successfully"));Assert.That(button.worldBound.width,Is.GreaterThanOrEqualTo(caption.worldBound.width+32));
        }
        [UnityTest]
        public IEnumerator NestedDialogBlocksUnderlyingActionsAndEscapeKeepsPanel()
        {
            var view=controller.View;view.OpenPanel("settings");yield return null;yield return null;
            view.OpenDialog("profile");yield return null;yield return null;
            var active=view.Root.Q<VisualElement>("dialog-layer");
            var button=active.Query<Button>().ToList().First(b=>b.enabledInHierarchy);button.Focus();
            Press(button,KeyCode.Escape);yield return null;yield return null;
            Assert.That(view.Modals.Dialog,Is.Null);Assert.That(view.Modals.Panel,Is.EqualTo("settings"));
            Press(view.Root,KeyCode.Escape);yield return null;
            Assert.That(view.Modals.Panel,Is.Null);Assert.That(view.Root.Q<Button>("play-now").enabledInHierarchy,Is.True);
        }
        [UnityTest]
        public IEnumerator TabAndShiftTabStayWithinTopDialog()
        {
            var view=controller.View;view.OpenPanel("settings");yield return null;view.OpenDialog("share");yield return null;yield return null;
            var layer=view.Root.Q<VisualElement>("dialog-layer");
            var controls=layer.Query<VisualElement>().ToList().Where(e=>e.focusable&&e.tabIndex>=0&&e.enabledInHierarchy&&!e.ClassListContains("dialog-card")).ToList();
            Assert.That(controls.Count,Is.GreaterThan(2));
            controls[controls.Count-1].Focus();Press(controls[controls.Count-1],KeyCode.Tab);yield return null;
            var focused=view.Root.panel.focusController.focusedElement as VisualElement;
            Assert.That(focused,Is.SameAs(controls[0]));
            Press(focused,KeyCode.Tab,true);yield return null;
            Assert.That(view.Root.panel.focusController.focusedElement,Is.SameAs(controls[controls.Count-1]));
        }
        [UnityTest]
        public IEnumerator MainButtonsEmitSelectedModeAndRefreshWithoutLosingNativeLayout()
        {
            var view=controller.View;int? played=null;controller.PlayRequested+=mode=>played=mode;
            // v3 (S06): Dereceli is YAKINDA — selectable, but HEMEN OYNA only says so; Klasik plays.
            var soon=view.Root.Q<Button>("mode-3");soon.Focus();yield return null;using(var evt=NavigationSubmitEvent.GetPooled())soon.SendEvent(evt);yield return null;yield return null;
            Assert.That(controller.Model.SelectedMode,Is.EqualTo(3));Assert.That(view.Root.Q<Label>("mode-soon-3"),Is.Not.Null);
            var play=view.Root.Q<Button>("play-now");Assert.That(play.worldBound.width,Is.GreaterThan(300));
            play.Focus();yield return null;using(var evt=NavigationSubmitEvent.GetPooled())play.SendEvent(evt);yield return null;
            Assert.That(played,Is.Null,"a coming-soon mode does not start");
            var classic=view.Root.Q<Button>("mode-0");using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=classic;classic.SendEvent(evt);}yield return null;yield return null;
            play=view.Root.Q<Button>("play-now");using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=play;play.SendEvent(evt);}yield return null;
            Assert.That(played,Is.EqualTo(0));
            Assert.That(view.Root.Query<Button>(className:"mode-card").ToList().Count,Is.EqualTo(3));
            Assert.That(controller.View,Is.Not.Null);
        }
    }
}
