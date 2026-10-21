using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Okey101.PlayMode.Tests
{
    public sealed class TablePresentationTests
    {
        private GameObject root;
        private RenderTexture renderTarget;
        [SetUp] public void Setup() { root = new GameObject("Presentation test"); }
        [TearDown] public void Cleanup()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (renderTarget != null) { renderTarget.Release(); UnityEngine.Object.DestroyImmediate(renderTarget); }
        }
        private TileMotion Motion(out Transform tile)
        {
            tile = new GameObject("Moving tile").transform; tile.SetParent(root.transform);
            var motion = root.AddComponent<TileMotion>();
            motion.Prepare(new[] { new TilePlacement(tile, Vector3.zero, Vector3.right * 2) });
            return motion;
        }
        private static IEnumerator Await(Task task)
        {
            var deadline = Time.realtimeSinceStartup + 10;
            while (!task.IsCompleted) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            task.GetAwaiter().GetResult();
        }
        [UnityTest] public IEnumerator RepeatedClickDuringMotionCommitsOnce()
        {
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0); yield return null;
            var id = game.Session.GetView(0).OwnHand[0]; game.SelectTile(id);
            var first = game.DiscardSelectedAsync(true);
            var second = game.DiscardSelectedAsync(true);
            yield return Await(first); yield return Await(second);
            Assert.That(first.Result.Accepted, Is.True); Assert.That(second.Result.Accepted, Is.False);
            Assert.That(game.Session.GetView(0).Version, Is.EqualTo(1));
            Assert.That(game.Session.GetView(0).Discards[0], Is.EqualTo(new[] { id }));
            Assert.That(game.Presenter.VisibleOwnTileIds.Contains(id), Is.False);
        }
        [UnityTest] public IEnumerator ReducedMotionHasSameFinalPlacement()
        {
            var motion = Motion(out var tile);
            var normal = motion.PlayAsync(Array.Empty<VisibleEvent>(), false, CancellationToken.None);
            Assert.That(motion.IsPlaying, Is.True);
            yield return Await(normal); var final = tile.position;
            motion.Prepare(new[] { new TilePlacement(tile, Vector3.zero, Vector3.right * 2) });
            var reduced = motion.PlayAsync(Array.Empty<VisibleEvent>(), true, CancellationToken.None);
            Assert.That(reduced.IsCompleted, Is.True); Assert.That(tile.position, Is.EqualTo(final));
            Assert.That(final, Is.EqualTo(Vector3.right * 2)); Assert.That(motion.IsPlaying, Is.False);
            var normalRoot = new GameObject("Normal accepted placement"); normalRoot.transform.SetParent(root.transform);
            var normalGame = normalRoot.AddComponent<GameBootstrap>(); normalGame.Initialize(71, 0); yield return null;
            var id = normalGame.Session.GetView(0).OwnHand[0]; normalGame.SelectTile(id);
            yield return Await(normalGame.DiscardSelectedAsync(true));
            var expected = normalRoot.GetComponentsInChildren<TileView>().Where(t => t.Role == TileRole.Hand || t.Role == TileRole.Discard)
                .ToDictionary(t => t.Id, t => t.transform.position);
            var reducedRoot = new GameObject("Reduced accepted placement"); reducedRoot.transform.SetParent(root.transform);
            var reducedGame = reducedRoot.AddComponent<GameBootstrap>(); reducedGame.Initialize(71, 0); reducedGame.Presenter.Motion.ReducedMotion = true;
            yield return null; reducedGame.SelectTile(id); yield return Await(reducedGame.DiscardSelectedAsync(true));
            var actual = reducedRoot.GetComponentsInChildren<TileView>().Where(t => t.Role == TileRole.Hand || t.Role == TileRole.Discard)
                .ToDictionary(t => t.Id, t => t.transform.position);
            Assert.That(actual.Keys, Is.EquivalentTo(expected.Keys));
            foreach (var entry in expected) Assert.That(actual[entry.Key], Is.EqualTo(entry.Value));
        }
        [UnityTest] public IEnumerator PauseDoesNotAdvanceOrLoseAnimation()
        {
            var motion = Motion(out var tile);
            var task = motion.PlayAsync(Array.Empty<VisibleEvent>(), false, CancellationToken.None);
            yield return null; motion.SetPaused(true); var paused = tile.position;
            for (var i = 0; i < 8; i++) yield return null;
            Assert.That(task.IsCompleted, Is.False); Assert.That(tile.position, Is.EqualTo(paused));
            motion.SetPaused(false); yield return Await(task);
            Assert.That(tile.position, Is.EqualTo(Vector3.right * 2));
        }
        [UnityTest] public IEnumerator AcceptedCommandPausesAndResumesWithoutChangingAuthorityTwice()
        {
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0); yield return null;
            var id = game.Session.GetView(0).OwnHand[0]; game.SelectTile(id);
            var task = game.DiscardSelectedAsync(true);
            var deadline = Time.realtimeSinceStartup + 5;
            while (!game.Presenter.Motion.IsPlaying)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); Assert.That(task.IsCompleted, Is.False); yield return null; }
            Assert.That(game.Session.GetView(0).Version, Is.EqualTo(1)); Assert.That(game.CanControl, Is.False);
            var moving = root.GetComponentsInChildren<TileView>().Single(t => t.Role == TileRole.Discard && t.Id == id);
            game.TogglePause(); var position = moving.transform.position;
            for (var i = 0; i < 8; i++) yield return null;
            Assert.That(task.IsCompleted, Is.False); Assert.That(moving.transform.position, Is.EqualTo(position));
            game.TogglePause(); yield return Await(task);
            Assert.That(task.Result.Accepted, Is.True); Assert.That(game.Session.GetView(0).Version, Is.EqualTo(1));
            Assert.That(game.CanControl, Is.True);
        }
        [UnityTest] public IEnumerator CallerCancellationSnapsFinalPlacementAndReleasesMotion()
        {
            var motion = Motion(out var tile);
            using (var token = new CancellationTokenSource())
            {
                var task = motion.PlayAsync(Array.Empty<VisibleEvent>(), false, token.Token);
                token.Cancel(); yield return null; yield return null;
                Assert.That(task.IsCanceled, Is.True); Assert.That(motion.IsPlaying, Is.False);
                Assert.That(tile.position, Is.EqualTo(Vector3.right * 2));
            }
        }
        [UnityTest] public IEnumerator SceneUnloadCancelsWithoutTouchingDestroyedObjects()
        {
            var scene = SceneManager.CreateScene("Motion lifetime"); SceneManager.MoveGameObjectToScene(root, scene);
            var motion = Motion(out _);
            var task = motion.PlayAsync(Array.Empty<VisibleEvent>(), false, CancellationToken.None);
            var unload = SceneManager.UnloadSceneAsync(scene);
            while (!unload.isDone) yield return null;
            Assert.That(task.IsCompleted, Is.True); Assert.That(task.IsCanceled, Is.True);
            LogAssert.NoUnexpectedReceived(); root = null;
        }
        [UnityTest] public IEnumerator SceneUnloadDuringRealBotMotionLeavesNoErrorsOrTableWork()
        {
            var scene = SceneManager.CreateScene("Bot motion lifetime"); SceneManager.MoveGameObjectToScene(root, scene);
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 1, true);
            var deadline = Time.realtimeSinceStartup + 10;
            while (!game.Presenter.Motion.IsPlaying) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            var unload = SceneManager.UnloadSceneAsync(scene);
            while (!unload.isDone) yield return null;
            yield return null; yield return null;
            Assert.That(game == null, Is.True);
            Assert.That(UnityEngine.Object.FindObjectsByType<TileMotion>(FindObjectsSortMode.None), Is.Empty);
            root = null;
        }
        [UnityTest] public IEnumerator GroupBrowserClosesWhenPausedAndCannotOpenDuringMotion()
        {
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0); yield return null;
            game.Presenter.Viewport.OpenGroups();
            Assert.That(root.GetComponentInChildren<ScrollRect>(), Is.Not.Null);
            game.TogglePause();
            Assert.That(root.GetComponentInChildren<ScrollRect>(), Is.Null, "Pause must close the group browser.");
            game.TogglePause(); game.SelectTile(game.Session.GetView(0).OwnHand[0]);
            var task = game.DiscardSelectedAsync(true);
            while (!game.Presenter.Motion.IsPlaying && !task.IsCompleted) yield return null;
            Assert.That(game.Presenter.Motion.IsPlaying, Is.True);
            game.Presenter.Viewport.OpenGroups();
            Assert.That(root.GetComponentInChildren<ScrollRect>(), Is.Null);
            yield return Await(task);
        }
        [UnityTest] public IEnumerator ManyGroupsRemainSelectable()
        {
            var presenter = root.AddComponent<TablePresenter>(); presenter.Initialize();
            var groups = Enumerable.Range(0, 21).Select(i => new Meld(i * 3 + 2, i % 4,
                new MeldProposal(MeldKind.Run, Enumerable.Range(1 + i % 3 * 3, 3).Select(n =>
                {
                    var color = (TileColor)(i / 6); var id = new TileId(2 * ((int)color * 13 + n - 1) + i / 3 % 2);
                    return new PlacedTile(id, new TileFace(color, n));
                }).ToArray()))).ToArray();
            presenter.Melds.Render(groups, new TileFace(TileColor.Black, 13));
            var viewport = root.AddComponent<TableViewport>(); viewport.Initialize(presenter.Melds);
            var rotation = presenter.TableCamera.transform.rotation;
            foreach (var group in groups.Reverse())
            {
                viewport.FocusGroup(group.Id);
                Assert.That(presenter.Melds.SelectedMeldId, Is.EqualTo(group.Id));
                Assert.That(root.GetComponentsInChildren<TileView>().Where(t => t.Role == TileRole.Meld).Select(t => t.Id),
                    Is.EquivalentTo(group.Group.Tiles.Select(t => t.Id)));
                Assert.That(presenter.TableCamera.transform.rotation, Is.EqualTo(rotation));
            }
            viewport.FocusGroup(-1); Assert.That(presenter.Melds.SelectedMeldId, Is.EqualTo(groups[0].Id));
            viewport.ResetFocus(); yield return null;
            var canvas = new GameObject("Group picker canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(root.transform); canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            viewport.Build(canvas.transform); viewport.OpenGroups(); Canvas.ForceUpdateCanvases();
            var scroll = root.GetComponentInChildren<ScrollRect>();
            Assert.That(scroll.content.childCount, Is.EqualTo(21));
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            scroll.content.GetChild(20).GetComponent<Button>().onClick.Invoke();
            Assert.That(presenter.Melds.SelectedMeldId, Is.EqualTo(groups[20].Id));
        }
        [UnityTest] public IEnumerator ThirteenTileRunStaysClearOfInterfaceLabels()
        {
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0);
            yield return Await(game.RecomputePreviewAsync());
            var group = new Meld(0, 1, new MeldProposal(MeldKind.Run, Enumerable.Range(1,13)
                .Select(n => new PlacedTile(new TileId(2*(n-1)), new TileFace(TileColor.Red,n))).ToArray()));
            game.Presenter.Melds.Render(new[] { group }, new TileFace(TileColor.Black,13));
            Canvas.ForceUpdateCanvases();
            var areaLabels = root.GetComponentsInChildren<Text>().Where(t => t.name == "SERİ / SAYI" || t.name == "ÇİFT").ToArray();
            Assert.That(areaLabels.Length, Is.EqualTo(2));
            foreach (var focused in new[] { false, true })
            {
                if (focused) game.Presenter.Viewport.FocusGroup(group.Id);
                Physics.SyncTransforms();
                Assert.That(root.GetComponentsInChildren<TileView>().Count(t=>t.Role==TileRole.Meld), Is.EqualTo(13));
                foreach (var label in areaLabels)
                {
                    var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                    var labelRect = Rect.MinMaxRect(corners.Min(p=>p.x), corners.Min(p=>p.y), corners.Max(p=>p.x), corners.Max(p=>p.y));
                    foreach (var tile in root.GetComponentsInChildren<TileView>().Where(t=>t.Role==TileRole.Meld))
                    {
                        var bounds = tile.GetComponentInChildren<BoxCollider>().bounds;
                        var projected = (from x in new[] { bounds.min.x, bounds.max.x }
                                         from y in new[] { bounds.min.y, bounds.max.y }
                                         from z in new[] { bounds.min.z, bounds.max.z }
                                         select game.Presenter.TableCamera.WorldToScreenPoint(new Vector3(x,y,z))).ToArray();
                        var tileRect = Rect.MinMaxRect(projected.Min(p=>p.x),projected.Min(p=>p.y),projected.Max(p=>p.x),projected.Max(p=>p.y));
                        Assert.That(labelRect.Overlaps(tileRect), Is.False, $"{label.name} overlaps physical tile {tile.Id.Value}, focus={focused}.");
                    }
                }
            }
        }
#if UNITY_EDITOR
        [UnityTest] public IEnumerator ImportedTilesShareAtlasAndKeepReadableNumbersAtFourResolutions()
        {
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Okey101/Prefabs/Tiles/Tile.prefab");
            var tile = UnityEngine.Object.Instantiate(prefab, root.transform).GetComponent<TileView>();
            var camera = new GameObject("Asset camera", typeof(Camera)).GetComponent<Camera>(); camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(0, 14, -6); camera.transform.LookAt(Vector3.zero); camera.orthographic = true; camera.orthographicSize = 5.4f;
            // Batch GameView is only 640x480; a real render target prevents pixelRect clamping.
            renderTarget = new RenderTexture(2560, 1440, 16); camera.targetTexture = renderTarget;
            tile.Show(new TileId(24), new TileFace(TileColor.Black, 13), TileRole.Hand, camera);
            var body = tile.transform.Find("Bevelled ivory body").GetComponentInChildren<MeshRenderer>();
            Assert.That(body.bounds.size.x, Is.EqualTo(.72f).Within(.001f));
            Assert.That(body.bounds.size.y, Is.EqualTo(.16f).Within(.001f));
            Assert.That(body.bounds.size.z, Is.EqualTo(.92f).Within(.001f));
            var second = UnityEngine.Object.Instantiate(prefab, root.transform).GetComponent<TileView>();
            second.Show(new TileId(51), new TileFace(TileColor.Black, 13), TileRole.Hand, camera);
            var face = tile.transform.Find("Shared atlas face").GetComponent<MeshRenderer>();
            var iconBottom = face.transform.TransformPoint(new Vector3(0, -.445f, 0));
            Assert.That(iconBottom.y, Is.GreaterThan(body.bounds.max.y), "The color icon must clear the solid tile body.");
            Assert.That(face.sharedMaterial, Is.SameAs(second.transform.Find("Shared atlas face").GetComponent<MeshRenderer>().sharedMaterial));
            var properties = new MaterialPropertyBlock(); face.GetPropertyBlock(properties);
            Assert.That(properties.GetVector("_BaseMap_ST"), Is.EqualTo(new Vector4(1f/16, 1f/4, 12f/16, 3f/4)));
            foreach (var size in new[] { new Vector2(1280,720), new Vector2(1920,1080), new Vector2(2560,1440), new Vector2(2560,1080) })
            {
                camera.pixelRect = new Rect(Vector2.zero,size);
                camera.aspect = size.x / size.y;
                Assert.That(tile.NumberPixelHeight(camera), Is.GreaterThanOrEqualTo(24));
            }
            var texture = new Texture2D(1, 1);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes("Assets/Okey101/Art/Textures/TileFaces.png"));
                var minX = 256; var maxX = 0;
                for (var x = 0; x < 256; x++) for (var y = 204; y < 246; y++)
                    if (texture.GetPixel(x, texture.height - 1 - y).a > .5f) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); }
                camera.pixelRect = new Rect(0, 0, 1280, 720);
                camera.aspect = 16f / 9;
                var left = camera.WorldToScreenPoint(face.transform.TransformPoint(new Vector3(minX / 256f - .5f, 0, 0)));
                var right = camera.WorldToScreenPoint(face.transform.TransformPoint(new Vector3((maxX + 1) / 256f - .5f, 0, 0)));
                Assert.That(right.x - left.x, Is.GreaterThanOrEqualTo(4), $"Color symbol pixels [{minX},{maxX}], texture {texture.width}x{texture.height}, camera {camera.pixelRect}, ortho {camera.orthographicSize}, aspect {camera.aspect}, faceScale {face.transform.lossyScale}.");
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            yield return null;
        }
#endif
    }
}
