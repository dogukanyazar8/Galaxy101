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
        private sealed class DailyClaimFixture : IRewardsService
        {
            private readonly IRewardsService source; private readonly MenuModel model;
            public bool Claimed; public int Claims;
            public DailyClaimFixture(IRewardsService source,MenuModel model){this.source=source;this.model=model;}
            public Newtonsoft.Json.Linq.JArray GetStreak()
            {
                var result=source.GetStreak();
                for(int i=0;i<result.Count;i++){result[i]["today"]=i==0;result[i]["claimed"]=i==0&&Claimed;}
                return result;
            }
            public bool ClaimDay(){if(Claimed)return false;Claims++;Claimed=true;model.NotifyChanged();return true;}
            public Newtonsoft.Json.Linq.JArray GetQuests()=>source.GetQuests();
            public bool ClaimQuest(int index)=>source.ClaimQuest(index);
            public DateTime ResetTime=>source.ResetTime;
        }
        [UnityTest]
        public IEnumerator CollectedDailyRewardKeepsTheReferenceCardAndCannotBeClaimedAgain()
        {
            var view=controller.View;var rewards=new DailyClaimFixture(view.Services.Rewards,view.Model);
            typeof(MenuServices).GetField("<Rewards>k__BackingField",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(view.Services,rewards);
            view.OpenPanel("daily");yield return new WaitForSecondsRealtime(.5f);
            var claim=view.Root.Q<Button>("claim-day");Assert.That(claim,Is.Not.Null);
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=claim;claim.SendEvent(evt);}
            yield return new WaitForSecondsRealtime(.5f);
            var card=view.Root.Q<VisualElement>("streak-day-1");var cover=card.Q<VisualElement>("day-claimed-cover");var check=card.Q<VisualElement>("day-claimed-check");
            Assert.That(rewards.Claims,Is.EqualTo(1));Assert.That(cover,Is.Not.Null);Assert.That(check,Is.Not.Null);
            Assert.That(check.resolvedStyle.width,Is.EqualTo(36).Within(.1f));Assert.That(check.resolvedStyle.height,Is.EqualTo(36).Within(.1f));
            Assert.That(GalaxyFilters.LedgeDepth(check),Is.EqualTo(3));
            claim=card.Q<Button>("claim-day");Assert.That(claim,Is.Null,"a claimed day must no longer show the collection button");
            Assert.That(card.resolvedStyle.backgroundColor,Is.EqualTo(view.C("var(--sd)")),"a claimed day uses the normal dark card");
            Assert.That(GalaxyFilters.LedgeDepth(card),Is.EqualTo(0),"the green raised treatment is only for an unclaimed day");
            Assert.That(card.Query<Label>().ToList().Any(label=>label.text==view.N((int)rewards.GetStreak()[0]["chips"])),Is.True,"the claimed amount stays visible below the check");
            Assert.That(rewards.Claims,Is.EqualTo(1));
            var output=Environment.GetEnvironmentVariable("GALAXY_DAILY_CAPTURE_DIR");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(),1280,720,Path.Combine(output,"daily-claimed.png"));}
        }
        [UnityTest]
        public IEnumerator CollectingFifthDayShowsFiveCompletedCardsLikeThePrototype()
        {
            var view=controller.View;long before=view.Model.Chips;
            view.OpenPanel("daily");yield return new WaitForSecondsRealtime(.5f);
            var claim=view.Root.Q<Button>("claim-day");Assert.That(claim,Is.Not.Null);
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=claim;claim.SendEvent(evt);}
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(view.Model.DayClaimed,Is.True);Assert.That(view.Model.Chips,Is.EqualTo(before+5000));
            for(int day=1;day<=5;day++)
            {
                var card=view.Root.Q<VisualElement>("streak-day-"+day);
                Assert.That(card.Q<VisualElement>("day-claimed-check"),Is.Not.Null,"day "+day);
                Assert.That(card.Q<Button>("claim-day"),Is.Null,"day "+day);
                Assert.That(card.Query<Label>().ToList().Any(label=>label.text==view.N(day*1000)),Is.True,"day "+day);
            }
            Assert.That(view.Root.Q<VisualElement>("streak-day-6").Q<VisualElement>("day-claimed-check"),Is.Null);
            Assert.That(view.Services.Rewards.ClaimDay(),Is.False);Assert.That(view.Model.Chips,Is.EqualTo(before+5000));
            var output=Environment.GetEnvironmentVariable("GALAXY_DAILY_CAPTURE_DIR");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(),1280,720,Path.Combine(output,"daily-fifth-claimed.png"));}
        }
        [UnityTest]
        public IEnumerator SplashCanBeSkippedAfterSixtyPercent()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.SplashSeconds=10;
            var root=auth.Layer;float deadline=Time.realtimeSinceStartup+9;bool ready=false;
            while(Time.realtimeSinceStartup<deadline)
            {
                var label=root.Q<Label>("splash-percent");
                if(label!=null&&int.TryParse(label.text.TrimEnd('%'),out int percent)&&percent>60){ready=true;break;}
                yield return null;
            }
            Assert.That(ready,Is.True,"splash must reach 60 percent before testing the click");
            using(var click=ClickEvent.GetPooled()){click.target=root;root.SendEvent(click);}
            deadline=Time.realtimeSinceStartup+3;
            while(auth.Screen=="splash"&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(auth.Screen,Is.EqualTo("choice"),"a click after 60 percent skips the remaining splash delay");
            auth.Hide();
        }
        [UnityTest]
        public IEnumerator SplashLogoProgressAndTipAppearAfterTheirReferenceDelays()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.SplashSeconds=10;
            var logo=auth.Layer.Q<VisualElement>("auth-logo");var bar=auth.Layer.Q<VisualElement>("splash-progress");var tip=auth.Layer.Q<VisualElement>("splash-tip");
            Assert.That(logo.style.opacity.value,Is.EqualTo(0));Assert.That(bar.style.opacity.value,Is.EqualTo(0));Assert.That(tip.style.opacity.value,Is.EqualTo(0));
            yield return new WaitForSecondsRealtime(2.1f);
            Assert.That(logo.resolvedStyle.opacity,Is.EqualTo(1).Within(.01f));Assert.That(bar.resolvedStyle.opacity,Is.EqualTo(1).Within(.01f));Assert.That(tip.resolvedStyle.opacity,Is.EqualTo(1).Within(.01f));
            auth.Hide();
        }
        [UnityTest]
        public IEnumerator AuthScreensRenderAtReferenceResolution()
        {
            controller.ShowAuth();var auth=controller.Auth;
            var directory=Environment.GetEnvironmentVariable("GALAXY_AUTH_CAPTURE_DIR");
            if(!string.IsNullOrEmpty(directory))Directory.CreateDirectory(directory);
            foreach(var screen in new[]{"choice","login","register","guest"})
            {
                auth.Go(screen);
                var path=string.IsNullOrEmpty(directory)?null:Path.Combine(directory,"clean-"+screen+".png");
                yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(),1280,720,path,root=>
                {
                    var frame=auth.Layer.Q<VisualElement>("auth-panel");
                    Assert.That(frame.layout.x,Is.EqualTo(740).Within(1),screen);
                    Assert.That(frame.layout.y,Is.EqualTo(screen=="choice"?60:46).Within(1),screen);
                    Assert.That(frame.layout.width,Is.EqualTo(470).Within(1),screen);
                    Assert.That(frame.layout.height,Is.EqualTo(screen=="choice"?600:628).Within(1),screen);
                    Assert.That(frame.resolvedStyle.borderTopWidth,Is.EqualTo(1).Within(.1f),screen);
                    Assert.That(frame.resolvedStyle.borderTopLeftRadius,Is.EqualTo(28).Within(.1f),screen);
                    Assert.That(GalaxyFilters.LedgeDepth(frame),Is.EqualTo(10),screen);
                });
                Assert.That(auth.Layer.Q<VisualElement>("auth-panel"),Is.Not.Null,screen);
            }
            auth.Hide();
        }
        [UnityTest]
        public IEnumerator TextboxFrameTracksFocusAndValidationWithoutMovingInput()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.Go("login");yield return new WaitForSecondsRealtime(.6f);
            var root=auth.Layer;var field=root.Q<TextField>("auth-email");var box=root.Q<VisualElement>("auth-box-email");
            var ring=box.Q<VisualElement>("inset-ring");var before=field.layout;
            Assert.That(ring.resolvedStyle.borderLeftWidth,Is.EqualTo(0));
            field.Focus();yield return null;
            Assert.That(ring.resolvedStyle.borderLeftWidth,Is.EqualTo(2));
            Assert.That(field.layout,Is.EqualTo(before));
            auth.Submit();yield return null;
            ring=root.Q<VisualElement>("auth-box-email").Q<VisualElement>("inset-ring");
            Assert.That(ring.resolvedStyle.borderLeftWidth,Is.EqualTo(2));
            Assert.That(ring.resolvedStyle.borderLeftColor.r,Is.EqualTo(1).Within(.01f));
            Assert.That(ring.resolvedStyle.borderLeftColor.g,Is.EqualTo(94f/255).Within(.01f));
            auth.Hide();
        }
        [UnityTest]
        public IEnumerator StoreDiscountBadgeUsesReferenceWeightAndFitsItsLabel()
        {
            yield return null;yield return null;
            var badge=controller.View.Root.Q<Label>(className:"sale-badge");
            Assert.That(badge,Is.Not.Null);Assert.That(badge.text,Is.EqualTo("%50"));
            Assert.That(badge.resolvedStyle.unityFont.name,Is.EqualTo("Nunito-900"));
            Assert.That(badge.resolvedStyle.height,Is.EqualTo(18).Within(.1f));
            Assert.That(badge.resolvedStyle.width,Is.GreaterThan(12));
            var directory=Environment.GetEnvironmentVariable("GALAXY_AUTH_CAPTURE_DIR");
            if(!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(),1280,720,Path.Combine(directory,"clean-menu.png"));
            }
        }
        [UnityTest]
        public IEnumerator GoogleNoticeKeepsChoiceAndLoginLayoutsAndInputState()
        {
            controller.ShowAuth();
            var auth=controller.Auth;
            foreach(var screen in new[]{"choice","login"})
            {
                auth.Go(screen);yield return null;yield return null;
                var captureDirectory=Environment.GetEnvironmentVariable("GALAXY_AUTH_CAPTURE_DIR");
                if(!string.IsNullOrEmpty(captureDirectory))
                {
                    Directory.CreateDirectory(captureDirectory);
                    yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(),1280,720,Path.Combine(captureDirectory,screen+".png"));
                }
                var root=controller.View.Root;var panel=root.Q<VisualElement>("auth-panel");
                var google=root.Q<VisualElement>("auth-google");
                var email=root.Q<TextField>("auth-email");
                if(email!=null)email.value="ornek@example.com";
                var before=panel.layout;
                using(var evt=ClickEvent.GetPooled()){evt.target=google;google.SendEvent(evt);}
                yield return null;yield return null;
                Assert.That(root.Q<VisualElement>("auth-panel"),Is.SameAs(panel),screen+" must not rebuild the form");
                Assert.That(panel.layout,Is.EqualTo(before),screen+" panel layout changed");
                Assert.That(auth.Layer.Q<VisualElement>("shell-flash"),Is.Not.Null,"notice must be above the auth panel");
                Assert.That(controller.View.LastFlash,Does.Contain("henüz"));
                if(email!=null)
                {
                    Assert.That(email.value,Is.EqualTo("ornek@example.com"));
                    Assert.That(Galaxy101.Audio.CursorRules.Resolve(email),Is.EqualTo("text"));
                    email.Focus();email.SelectRange(2,5);auth.Submit();yield return null;yield return null;
                    var restored=root.Q<TextField>("auth-email");
                    Assert.That(restored.cursorIndex,Is.EqualTo(2));Assert.That(restored.selectIndex,Is.EqualTo(5));
                }
            }
            var output=Environment.GetEnvironmentVariable("GALAXY_AUTH_CAPTURE_DIR");
            if(!string.IsNullOrEmpty(output))foreach(var screen in new[]{"register","guest"})
            {
                auth.Go(screen);
                yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(),1280,720,Path.Combine(output,screen+".png"));
            }
            auth.Hide();
        }
        [UnityTest]
        public IEnumerator FirstTextFieldClickImmediatelyHidesNativeCursor()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.Go("login");yield return new WaitForSecondsRealtime(.6f);
            var mode=typeof(Galaxy101.Audio.GalaxyCursor).GetField("mode",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var previous=mode.GetValue(null);bool previousVisible=UnityEngine.Cursor.visible;
            try
            {
                mode.SetValue(null,Galaxy101.Audio.CursorSet.Normal);UnityEngine.Cursor.visible=true;
                var field=auth.Layer.Q<TextField>("auth-email");
                using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=field.worldBound.center}))field.SendEvent(down);
                Assert.That(UnityEngine.Cursor.visible,Is.False,"hardware cursor must be hidden during the first text-field press");
                Assert.That(auth.Layer.Q<VisualElement>("auth-cursor-text").ClassListContains("auth-cursor-active"),Is.True);
            }
            finally { auth.Hide();mode.SetValue(null,previous);UnityEngine.Cursor.visible=previousVisible; }
        }
        [UnityTest]
        public IEnumerator RememberSwitchKeepsFormAndAnimatesItsThumb()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.Go("login");yield return new WaitForSecondsRealtime(.6f);
            var root=auth.Layer;var panel=root.Q<VisualElement>("auth-panel");var row=root.Q<VisualElement>("auth-remember");
            var track=root.Q<VisualElement>("auth-remember-track");var thumb=root.Q<VisualElement>("auth-remember-thumb");
            Assert.That(track,Is.Not.Null);Assert.That(thumb,Is.Not.Null);
            var email=root.Q<TextField>("auth-email");email.value="ornek@example.com";
            using(var click=ClickEvent.GetPooled()){click.target=row;row.SendEvent(click);}
            Assert.That(root.Q<VisualElement>("auth-panel"),Is.SameAs(panel));
            Assert.That(root.Q<TextField>("auth-email"),Is.SameAs(email));Assert.That(email.value,Is.EqualTo("ornek@example.com"));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(thumb.resolvedStyle.translate.x,Is.EqualTo(0).Within(.2f));
            using(var click=ClickEvent.GetPooled()){click.target=row;row.SendEvent(click);}
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(thumb.resolvedStyle.translate.x,Is.EqualTo(18).Within(.2f));
            auth.Hide();
        }
        [UnityTest]
        public IEnumerator PasswordEyeTogglesWithoutLosingEnteredValues()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.Go("login");yield return null;yield return null;
            var root=auth.Layer;var email=root.Q<TextField>("auth-email");var password=root.Q<TextField>("auth-password");
            email.value="ornek@example.com";password.value="test1234";Assert.That(password.isPasswordField,Is.True);
            var eye=root.Q<VisualElement>("auth-eye");
            using(var click=ClickEvent.GetPooled()){click.target=eye;eye.SendEvent(click);}yield return null;
            password=root.Q<TextField>("auth-password");Assert.That(password.isPasswordField,Is.False);
            Assert.That(password.value,Is.EqualTo("test1234"));Assert.That(root.Q<TextField>("auth-email").value,Is.EqualTo("ornek@example.com"));
            eye=root.Q<VisualElement>("auth-eye");using(var click=ClickEvent.GetPooled()){click.target=eye;eye.SendEvent(click);}yield return null;
            Assert.That(root.Q<TextField>("auth-password").isPasswordField,Is.True);auth.Hide();
        }
        [UnityTest]
        public IEnumerator RegistrationRepeatPasswordShowsMatchWithoutExtraEye()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.Go("register");yield return null;yield return null;
            var root=auth.Layer;var repeatColumn=root.Q<VisualElement>("auth-field-pass2");
            Assert.That(repeatColumn.Q<VisualElement>("auth-eye"),Is.Null,"reference shows an eye only on the first password");
            root.Q<TextField>("auth-password").value="gizli123";root.Q<TextField>("auth-password2").value="gizli123";yield return null;
            Assert.That(root.Q<VisualElement>("auth-ok-pass2"),Is.Not.Null,"matching text immediately shows the tick");
            root.Q<TextField>("auth-password2").value="gizli124";yield return null;
            Assert.That(root.Q<VisualElement>("auth-ok-pass2"),Is.Null,"a mismatch removes the tick immediately");
            auth.Hide();
        }
        [UnityTest]
        public IEnumerator RegistrationCheckboxKeepsFormAndFieldsWhileToggling()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.Go("register");yield return new WaitForSecondsRealtime(.6f);
            var root=auth.Layer;var panel=root.Q<VisualElement>("auth-panel");var field=root.Q<TextField>("auth-name");field.value="Oyuncu";
            var terms=root.Q<VisualElement>("auth-terms");var fill=root.Q<VisualElement>("auth-terms-fill");
            Assert.That(fill,Is.Not.Null);Assert.That(fill.style.opacity.value,Is.EqualTo(0));
            using(var click=ClickEvent.GetPooled()){click.target=terms;terms.SendEvent(click);}
            Assert.That(root.Q<VisualElement>("auth-panel"),Is.SameAs(panel));Assert.That(root.Q<TextField>("auth-name"),Is.SameAs(field));
            Assert.That(field.value,Is.EqualTo("Oyuncu"));Assert.That(fill.style.opacity.value,Is.EqualTo(1));
            using(var click=ClickEvent.GetPooled()){click.target=terms;terms.SendEvent(click);}
            Assert.That(fill.style.opacity.value,Is.EqualTo(0));auth.Hide();
        }
        [UnityTest]
        public IEnumerator CursorPickerFindsWelcomeButtonOnFirstQuery()
        {
            controller.ShowAuth();controller.Auth.Go("choice");yield return new WaitForSecondsRealtime(.6f);
            var root=controller.View.Root;var button=controller.Auth.Layer.Q<VisualElement>("auth-login");
            var bounds=root.worldBound;var point=button.worldBound.center;
            var screen=new Vector2((point.x-bounds.xMin)/bounds.width*Screen.width,
                Screen.height-(point.y-bounds.yMin)/bounds.height*Screen.height);
            var type=typeof(Galaxy101.Audio.GalaxyCursor).Assembly.GetType("Galaxy101.Audio.UiPicker");
            var picker=Activator.CreateInstance(type,true);var picked=(VisualElement)type.GetMethod("Pick").Invoke(picker,new object[]{screen});
            Assert.That(picked,Is.Not.Null,"the first query must scan active UI documents");
            Assert.That(Galaxy101.Audio.CursorRules.Resolve(picked),Is.EqualTo("pointer"));
            controller.Auth.Hide();
        }
        [UnityTest]
        public IEnumerator WelcomeCursorAnimatesPointerPressAndSwitchesToText()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.Go("choice");yield return new WaitForSecondsRealtime(.6f);
            var root=controller.View.Root;var login=auth.Layer.Q<VisualElement>("auth-login");
            using(var move=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseMove,mousePosition=login.worldBound.center}))root.panel.visualTree.SendEvent(move);
            yield return null;
            var cursor=auth.Layer.Q<VisualElement>("auth-cursor");Assert.That(cursor,Is.Not.Null);
            var pointer=cursor.Q<VisualElement>("auth-cursor-pointer");
            Assert.That(pointer.ClassListContains("auth-cursor-active"),Is.True);
            Assert.That(pointer.ClassListContains("auth-cursor-pressed"),Is.False);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(pointer.resolvedStyle.scale.value.x,Is.EqualTo(1.06f).Within(.02f));
            Assert.That(pointer.resolvedStyle.rotate.angle.value,Is.EqualTo(-6).Within(.2f));
            using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=login.worldBound.center}))login.SendEvent(down);
            Assert.That(pointer.ClassListContains("auth-cursor-pressed"),Is.True);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(pointer.resolvedStyle.scale.value.x,Is.EqualTo(.84f).Within(.02f));
            Assert.That(pointer.resolvedStyle.rotate.angle.value,Is.EqualTo(-8).Within(.2f));
            using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=login.worldBound.center}))login.SendEvent(up);
            Assert.That(pointer.ClassListContains("auth-cursor-pressed"),Is.False);
            auth.Go("choice");yield return null;yield return null;
            login=auth.Layer.Q<VisualElement>("auth-login");Galaxy101.Audio.GalaxyCursor.Lock(auth.Layer,"wait");
            using(var move=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseMove,mousePosition=login.worldBound.center}))root.panel.visualTree.SendEvent(move);
            yield return new WaitForSecondsRealtime(.1f);
            var wait=cursor.Q<VisualElement>("auth-cursor-wait");Assert.That(wait.ClassListContains("auth-cursor-active"),Is.True);
            var angle=wait.style.rotate.value.angle.value;yield return new WaitForSecondsRealtime(.15f);
            Assert.That(wait.style.rotate.value.angle.value,Is.Not.EqualTo(angle),"busy cursor should spin");
            Galaxy101.Audio.GalaxyCursor.Lock(auth.Layer,null);
            auth.Go("login");yield return null;yield return null;
            var input=auth.Layer.Q<TextField>("auth-email");
            using(var move=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseMove,mousePosition=input.worldBound.center}))root.panel.visualTree.SendEvent(move);
            yield return null;
            Assert.That(cursor.Q<VisualElement>("auth-cursor-text").ClassListContains("auth-cursor-active"),Is.True);
            Assert.That(pointer.ClassListContains("auth-cursor-active"),Is.False);
            auth.Hide();Assert.That(cursor.panel,Is.Null);
        }
        [UnityTest]
        public IEnumerator WelcomeCursorRestoresHardwareForSystemModeAndOnClose()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.Go("choice");yield return null;
            var assembly=typeof(Galaxy101.Audio.GalaxyCursor).Assembly;
            var mode=typeof(Galaxy101.Audio.GalaxyCursor).GetField("mode",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            var oldMode=mode.GetValue(null);bool oldVisible=UnityEngine.Cursor.visible;
            var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            try
            {
                mouse.MakeCurrent();
                var picker=Activator.CreateInstance(assembly.GetType("Galaxy101.Audio.UiPicker"),true);
                var driverType=assembly.GetType("Galaxy101.Audio.CursorDriver");
                var driver=Activator.CreateInstance(driverType,Galaxy101.Audio.CursorSet.Load(),picker);
                var update=driverType.GetMethod("Update");
                mode.SetValue(null,Galaxy101.Audio.CursorSet.Normal);
                var button=auth.Layer.Q<VisualElement>("auth-login");
                using(var move=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseMove,mousePosition=button.worldBound.center}))controller.View.Root.panel.visualTree.SendEvent(move);
                update.Invoke(driver,null);
                Assert.That(UnityEngine.Cursor.visible,Is.False,"animated cursor owns the cursor during sign-in");
                UnityEngine.Cursor.visible=true;update.Invoke(driver,null);
                Assert.That(UnityEngine.Cursor.visible,Is.False,"an OS cursor made visible by a text control must be hidden again");
                mode.SetValue(null,Galaxy101.Audio.CursorSet.System);update.Invoke(driver,null);
                Assert.That(UnityEngine.Cursor.visible,Is.True,"system mode must restore the hardware cursor");
                mode.SetValue(null,Galaxy101.Audio.CursorSet.Normal);update.Invoke(driver,null);
                Assert.That(UnityEngine.Cursor.visible,Is.False);
                auth.Hide();update.Invoke(driver,null);
                Assert.That(UnityEngine.Cursor.visible,Is.True,"closing sign-in must restore the hardware cursor");
            }
            finally
            {
                auth.Hide();mode.SetValue(null,oldMode);UnityEngine.Cursor.visible=oldVisible;
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);
            }
        }
        [UnityTest]
        public IEnumerator WelcomeTextCaretBlinksWhileFieldRemainsFocused()
        {
            controller.ShowAuth();var auth=controller.Auth;auth.Go("login");yield return new WaitForSecondsRealtime(.6f);
            var root=controller.View.Root;var field=auth.Layer.Q<TextField>("auth-email");field.value="deneme";field.Focus();field.SelectRange(3,3);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(root.panel.focusController.focusedElement,Is.Not.Null);
            var target=owner.GetComponent<MenuGammaPresenter>().Output;var bounds=field.worldBound;var rb=root.worldBound;
            int left=Mathf.Clamp(Mathf.FloorToInt((bounds.xMin-rb.xMin)/rb.width*target.width),0,target.width-1);
            int right=Mathf.Clamp(Mathf.CeilToInt((bounds.xMax-rb.xMin)/rb.width*target.width),left+1,target.width);
            int top=Mathf.Clamp(Mathf.FloorToInt((bounds.yMin-rb.yMin)/rb.height*target.height),0,target.height-1);
            int bottom=Mathf.Clamp(Mathf.CeilToInt((bounds.yMax-rb.yMin)/rb.height*target.height),top+1,target.height);
            Color32[] previous=null;int maxChanged=0;
            for(int frame=0;frame<10;frame++)
            {
                var pixels=ReadFrame(target);var current=pixels.GetPixels32();UnityEngine.Object.Destroy(pixels);
                if(previous!=null)
                {
                    int changed=0;
                    for(int y=top+5;y<bottom-5;y++)for(int x=left+5;x<right-5;x++)
                    {
                        int i=(target.height-1-y)*target.width+x;
                        if(Math.Abs(current[i].r-previous[i].r)>60&&Math.Abs(current[i].g-previous[i].g)>60&&Math.Abs(current[i].b-previous[i].b)>60)changed++;
                    }
                    maxChanged=Math.Max(maxChanged,changed);
                }
                previous=current;yield return new WaitForSecondsRealtime(.2f);
            }
            Assert.That(maxChanged,Is.GreaterThan(3),"focused caret should appear and disappear in the rendered field");
            Assert.That(field.cursorIndex,Is.EqualTo(3));
            float deadline=Time.realtimeSinceStartup+2;
            while(field.textSelection.cursorColor.a>.1f&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.That(field.textSelection.cursorColor.a,Is.LessThan(.1f));
            Press(field,KeyCode.A);Assert.That(field.textSelection.cursorColor.a,Is.EqualTo(1).Within(.01f),"keyboard input restarts the blink");
            auth.Hide();
        }
        [UnityTest]
        public IEnumerator WelcomeControlsExposeReferenceCursorsAndPressedDepth()
        {
            controller.ShowAuth();controller.Auth.Go("choice");yield return null;yield return null;
            var root=controller.View.Root;
            foreach(var name in new[]{"auth-guest","auth-login","auth-register","auth-google"})
            {
                controller.Auth.Go("choice");yield return null;yield return null;
                var control=controller.Auth.Layer.Q<VisualElement>(name);
                Assert.That(Galaxy101.Audio.CursorRules.Resolve(control),Is.EqualTo("pointer"),name);
                float depth=GalaxyFilters.LedgeDepth(control);
                using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0,mousePosition=control.worldBound.center}))
                {
                    Assert.That(down.button,Is.EqualTo(0),"test pointer button");control.SendEvent(down);
                }
                if(Galaxy101.Audio.GalaxyCursor.Mode!=Galaxy101.Audio.CursorSet.System)
                    Assert.That(controller.Auth.Layer.Q<VisualElement>("auth-click-ripple"),Is.Not.Null,name+" click ripple");
                Assert.That(control.style.translate.value.y.value,Is.EqualTo(name=="auth-guest"?4:depth-1),name);
                Assert.That(GalaxyFilters.LedgeDepth(control),Is.EqualTo(name=="auth-guest"?depth:1),name);
                using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,button=0,mousePosition=control.worldBound.center}))control.SendEvent(up);
                Assert.That(GalaxyFilters.LedgeDepth(control),Is.EqualTo(depth),name);
            }
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(controller.Auth.Layer.Q<VisualElement>("auth-click-ripple"),Is.Null,"click ripples must expire");
            controller.Auth.Hide();
        }
        [UnityTest]
        public IEnumerator WelcomeButtonsBrightenWhenPointerEntersAndRestoreOnExit()
        {
            controller.ShowAuth();var root=controller.View.Root;
            foreach(var item in new[]{("choice","auth-login"),("choice","auth-register"),("login","auth-back"),("login","auth-submit"),("register","auth-submit"),("guest","auth-submit")})
            {
                controller.Auth.Go(item.Item1);yield return new WaitForSecondsRealtime(.6f);
                var button=controller.Auth.Layer.Q<VisualElement>(item.Item2);var bounds=button.worldBound;var rootBounds=root.worldBound;
                var target=owner.GetComponent<MenuGammaPresenter>().Output;
                int x=Mathf.RoundToInt(((bounds.xMin+bounds.width*.83f)-rootBounds.xMin)/rootBounds.width*target.width);
                int y=target.height-1-Mathf.RoundToInt((bounds.center.y-rootBounds.yMin)/rootBounds.height*target.height);
                Color Pixel(){var pixels=ReadFrame(target);var color=pixels.GetPixel(x,y);UnityEngine.Object.Destroy(pixels);return color;}
                using(var move=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseMove,mousePosition=rootBounds.min+new Vector2(10,10)}))root.panel.visualTree.SendEvent(move);
                yield return new WaitForSecondsRealtime(.2f);var normal=Pixel();
                using(var move=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseMove,mousePosition=bounds.center}))root.panel.visualTree.SendEvent(move);
                yield return new WaitForSecondsRealtime(.3f);var hover=Pixel();
                using(var move=PointerMoveEvent.GetPooled(new Event{type=EventType.MouseMove,mousePosition=rootBounds.min+new Vector2(10,10)}))root.panel.visualTree.SendEvent(move);
                yield return new WaitForSecondsRealtime(.2f);var restored=Pixel();
                Assert.That(hover.r+hover.g+hover.b,Is.GreaterThan(normal.r+normal.g+normal.b+.025f),$"{item}: normal={normal}, hover={hover}");
                Assert.That(restored.r+restored.g+restored.b,Is.EqualTo(normal.r+normal.g+normal.b).Within(.025f),item+" hover must clear on exit");
            }
            controller.Auth.Hide();
        }
        [UnityTest]
        public IEnumerator ModeCardsRenderTheirFivePixelLedgeOutsideTheGlyphClip()
        {
            controller.View.SetFocused(false);var root=controller.View.Root;
            // At 0.5 DPR the last 5 px shadow sample straddles a physical pixel. Match the source's
            // 1280 x 720 framebuffer so these opaque-shade assertions never sample that antialiased border.
            var document=owner.GetComponent<UIDocument>();var previousPanel=document.panelSettings;
            var capturePanel=UnityEngine.Object.Instantiate(previousPanel);
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            target.Create();capturePanel.targetTexture=target;document.panelSettings=capturePanel;
            Texture2D pixels=null;
            try
            {
                yield return null;yield return null;yield return null;
                pixels=ReadFrame(target);
                Color32[] shades={new Color32(33,72,176,255),new Color32(12,127,117,255),new Color32(34,145,74,255)};
                var rootBounds=root.worldBound;
                Color32 Pixel(Vector2 position)
                {
                    int x=Mathf.RoundToInt((position.x-rootBounds.xMin)/rootBounds.width*target.width);
                    int y=target.height-1-Mathf.RoundToInt((position.y-rootBounds.yMin)/rootBounds.height*target.height);
                    return pixels.GetPixel(x,y);
                }
                for(int i=0;i<3;i++)
                {
                    var card=root.Q<Button>("menu.m.pick["+i+"]");var bounds=card.worldBound;
                    Assert.That(card.scaledPixelsPerPoint,Is.EqualTo(1).Within(.001f),"Five CSS points must occupy five framebuffer pixels.");
                    var clip=card.Q<VisualElement>(className:"mode-clip");
                    Assert.That(clip.style.overflow.value,Is.EqualTo(Overflow.Hidden),"The oversized mode glyph and unselected tick must remain clipped.");
                    Assert.That(GalaxyFilters.LedgeDepth(card),Is.EqualTo(5));
                    bool selected=controller.Model.SelectedMode==MenuView.V3Modes[i];
                    Color source=shades[i];
                    // G101-Menu renderVals: unselected cards use saturate(.7) brightness(.78), including their shadow.
                    float luma=source.r*.2126f+source.g*.7152f+source.b*.0722f;
                    Color32 expected=selected?source:new Color((source.r*.7f+luma*.3f)*.78f,(source.g*.7f+luma*.3f)*.78f,(source.b*.7f+luma*.3f)*.78f,1);
                    float unit=bounds.height/card.layout.height;
                    TestContext.WriteLine($"MODE_LEDGE card={i} selected={selected} bounds={bounds} root={rootBounds} target={target.width}x{target.height} DPR={card.scaledPixelsPerPoint} source={expected}");
                    foreach(float xFraction in new[]{.25f,.5f,.75f})foreach(float below in new[]{1f,2.5f,4f})
                    {
                        var actual=Pixel(new Vector2(Mathf.Lerp(bounds.xMin,bounds.xMax,xFraction),bounds.yMax+below*unit));
                        string where=$"Mode {i}, {below}px below the face: {actual}; source shade {expected}";
                        Assert.That((int)actual.r,Is.EqualTo((int)expected.r).Within(3),where+" red");
                        Assert.That((int)actual.g,Is.EqualTo((int)expected.g).Within(3),where+" green");
                        Assert.That((int)actual.b,Is.EqualTo((int)expected.b).Within(3),where+" blue");
                    }
                    var outside=Pixel(new Vector2(bounds.center.x,bounds.yMax+8*unit));
                    Assert.That(Mathf.Max(Mathf.Abs(outside.r-expected.r),Mathf.Abs(outside.g-expected.g),Mathf.Abs(outside.b-expected.b)),Is.GreaterThan(8),"The five-pixel ledge must end below the card.");
                }
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                document.panelSettings=previousPanel;capturePanel.targetTexture=null;
                if(pixels!=null)UnityEngine.Object.Destroy(pixels);
                UnityEngine.Object.Destroy(capturePanel);target.Release();UnityEngine.Object.Destroy(target);
            }
        }
        [UnityTest]
        public IEnumerator InactiveModeCardMatchesTheSourceSaturationAndBrightnessWithoutExtraDimming()
        {
            controller.View.SetFocused(false);var root=controller.View.Root;
            var card=root.Q<Button>("menu.m.pick[1]");var bounds=card.worldBound;
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
            var play=root.Q<Button>("menu.play");var logicalPoint=play.worldBound.center;
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
        public IEnumerator NativeSliderSupportsPointerCaptureDragReleaseAndKeyboardWithImmediateSound()
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
            Assert.That((int)controller.Model.Settings["master"],Is.EqualTo(99),"Sound changes apply immediately; only display settings use a draft.");
        }
        [UnityTest]
        public IEnumerator DecoratedButtonMeasuresItsCaptionAndUpdatesFeedbackText()
        {
            var button=controller.View.Button("Cancel",()=>{},"secondary");button.name="caption-measure";button.style.position=Position.Absolute;button.style.left=100;button.style.top=100;
            controller.View.Root.Q<VisualElement>("main-layer").Add(button);yield return null;yield return null;
            var caption=button.Q<Label>("button-caption");Assert.That(caption,Is.Not.Null);
            Assert.That(button.worldBound.width,Is.GreaterThanOrEqualTo(caption.worldBound.width+32),"Decorative children must not remove the text's intrinsic width or padding.");
            Assert.That(GalaxyFilters.LedgeDepth(button),Is.GreaterThan(0),"the raised ledge");
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
            Assert.That(view.Modals.Panel,Is.Null);Assert.That(view.Root.Q<Button>("menu.play").enabledInHierarchy,Is.True);
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
            Assert.That(view.Root.Q<Label>("menu.t.soon[2]"),Is.Null);Assert.That(view.Root.Q<Label>("menu.t.soon[1]"),Is.Null);
            var play=view.Root.Q<Button>("menu.play");Assert.That(play.worldBound.width,Is.GreaterThan(300));
            var classic=view.Root.Q<Button>("menu.m.pick[0]");using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=classic;classic.SendEvent(evt);}yield return null;yield return null;
            play=view.Root.Q<Button>("menu.play");using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=play;play.SendEvent(evt);}yield return null;
            Assert.That(played,Is.EqualTo(0));
            Assert.That(view.Root.Query<Button>(className:"mode-card").ToList().Count,Is.EqualTo(3));
            // v4 (G101-Menu modes[].soon = false): Botlara karşı is a playable mode with its own subtitle, no YAKINDA mark.
            var bots=view.Root.Q<Button>("menu.m.pick[2]");using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=bots;bots.SendEvent(evt);}yield return null;yield return null;
            Assert.That(controller.Model.SelectedMode,Is.EqualTo(4));
            Assert.That(view.Root.Q<Label>("menu.playSub").text,Is.EqualTo("Botlara karşı · antrenman · bahis yok"));
            Assert.That(controller.View,Is.Not.Null);
        }
    }
}
