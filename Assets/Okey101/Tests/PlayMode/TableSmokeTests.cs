using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Galaxy101;
using UnityEngine.UIElements;

namespace Okey101.PlayMode.Tests
{
    public sealed class TableSmokeTests
    {
        private GameObject root;
        [TearDown] public void Cleanup() { if (root != null) UnityEngine.Object.DestroyImmediate(root); }

        [UnityTest]
        public IEnumerator DrawDiscardAndReorder()
        {
            root = new GameObject("Smoke Table");
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0);
            yield return null;
            var initial = game.Session.GetView(0);
            Assert.That(game.Presenter.VisibleOwnTileIds.Count, Is.EqualTo(22));
            game.SelectTile(initial.OwnHand[0]); game.MoveSelectedTo(23); game.SortHand();
            Assert.That(game.Session.GetView(0).Version, Is.EqualTo(initial.Version));
            Assert.That(game.Presenter.VisibleOwnTileIds, Is.EquivalentTo(initial.OwnHand));
            var commit = game.DiscardSelectedAsync(true);
            yield return Await(commit);
            Assert.That(commit.Result.Accepted, Is.True);
            game.SetPerspective(1);
            var waitingVersion = game.Session.GetView(1).Version;
            var draw = game.DrawAsync(DrawSource.Stock); yield return Await(draw);
            Assert.That(draw.Result.Accepted, Is.True);
            Assert.That(game.Presenter.VisibleOwnTileIds.Count, Is.EqualTo(22));
            game.SelectTile(game.Session.GetView(1).OwnHand[0]);
            commit = game.DiscardSelectedAsync(true); yield return Await(commit);
            Assert.That(commit.Result.Accepted, Is.True);
            Assert.That(game.Session.GetView(1).Version, Is.EqualTo(waitingVersion + 2));
            Assert.That(game.Presenter.VisibleOwnTileIds.Count, Is.EqualTo(21));
        }

        [UnityTest]
        public IEnumerator RejectedCommandKeepsAllTilesVisible()
        {
            root = new GameObject("Rejected Table");
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0);
            yield return null;
            var ids = game.Presenter.VisibleOwnTileIds.ToArray();
            Assert.That(ids.Length, Is.EqualTo(22));
            var rejected = game.DrawAsync(DrawSource.Stock); yield return Await(rejected);
            Assert.That(rejected.Result.Error, Is.EqualTo(ErrorCode.WrongPhase));
            Assert.That(game.Session.GetView(0).Version, Is.Zero);
            Assert.That(game.Presenter.VisibleOwnTileIds, Is.EquivalentTo(ids));
            Assert.That(game.Presenter.VisibleOwnTileIds.Distinct().Count(), Is.EqualTo(22));
        }

        private static IEnumerator Await(Task task)
        {
            var deadline = Time.realtimeSinceStartup + 15;
            while (!task.IsCompleted) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; }
            task.GetAwaiter().GetResult();
        }

        [UnityTest]
        public IEnumerator BootstrapLoadsOwnedPrefabTable()
        {
            // Exercise the real build entry point, rather than only the fallback factories.
            var bootstrap = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Additive);
            while (!bootstrap.isDone) yield return null;
            yield return null;
            var deadline = Time.realtimeSinceStartup + 15;
            while(UnityEngine.Object.FindAnyObjectByType<MainMenuController>()?.View==null)
            {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
            Assert.That(menu,Is.Not.Null);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<MainMenu>(),Is.Null);
            // Select bot mode in memory so authored-scene acceptance never changes user preferences.
            menu.Model.SelectedMode=4;menu.View.Refresh();yield return null;yield return null;
            var play=menu.View.Root.Q<Button>("play-now");
            Assert.That(play,Is.Not.Null);Assert.That(play.enabledInHierarchy,Is.True);
            // "Botlara karşı" now opens the 3D Galaxy table; this acceptance covers the classic table entry.
            var open=menu.StartLocalAsync(MainMenuController.LegacyTableScene);
            GameBootstrap game;
            do
            {
                yield return null;
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
                game = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            } while (game == null || game.Session == null);
            root = game.gameObject;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Table"));
            Assert.That(game.BotsEnabled,Is.True);
            Assert.That(game.Session.GetView(0).Actor,Is.Zero);
            Assert.That(game.Session.GetView(0).Players.Count,Is.EqualTo(4));
            game.SetPaused(true);
            var expectedHand=game.Session.GetView(0).OwnHand;
            Assert.That(expectedHand.Count,Is.InRange(21,22));
            Assert.That(game.Presenter.VisibleOwnTileIds,Is.EquivalentTo(expectedHand));
            Assert.That(game.Presenter.Rack.transform.parent, Is.EqualTo(root.transform));
            Assert.That(root.GetComponentsInChildren<Camera>().Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<AudioListener>().Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<TileView>().Count(t => t.Role == TileRole.Hand), Is.EqualTo(expectedHand.Count));
            // The single-scene loader legitimately unloads the test scene. Restore a clean scene
            // so subsequent tests do not inherit this table's cameras or Input System objects.
            var loaded = SceneManager.GetActiveScene();
            var clean = SceneManager.CreateScene("After A0 scene smoke"); SceneManager.SetActiveScene(clean);
            var unload = SceneManager.UnloadSceneAsync(loaded);
            while (!unload.isDone) yield return null;
        }

        [UnityTest]
        public IEnumerator EveryTileIsInsidePlayableViewportAt720And1080()
        {
            root = new GameObject("Viewport Table");
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0);
            yield return null;
            var camera = game.Presenter.TableCamera;
            camera.aspect = 16f / 9f;
            foreach (var height in new[] { 720, 1080 })
            {
                camera.pixelRect = new Rect(0, 0, height * 16f / 9f, height);
                yield return null;
                var tiles = root.GetComponentsInChildren<TileView>().Where(t => t.Role == TileRole.Hand).ToArray();
                Assert.That(tiles.Length, Is.EqualTo(22));
                foreach (var tile in tiles)
                {
                    var collider = tile.GetComponentInChildren<BoxCollider>();
                    var bounds = collider.bounds;
                    foreach (var x in new[] { bounds.min.x, bounds.max.x })
                        foreach (var y in new[] { bounds.min.y, bounds.max.y })
                            foreach (var z in new[] { bounds.min.z, bounds.max.z })
                            {
                                var point = camera.WorldToViewportPoint(new Vector3(x, y, z));
                                Assert.That(point.x, Is.InRange(.21f, .79f), "Rack must not overlap player panels.");
                                Assert.That(point.y, Is.InRange(.085f, .335f), "Rack must clear footer and status.");
                            }
                    var number = tile.GetComponentsInChildren<TextMesh>().Single(t => t.name == "Number");
                    var local = number.GetComponent<MeshRenderer>().localBounds;
                    var bottom = camera.WorldToScreenPoint(number.transform.TransformPoint(local.center - Vector3.up * local.extents.y));
                    var top = camera.WorldToScreenPoint(number.transform.TransformPoint(local.center + Vector3.up * local.extents.y));
                    Assert.That(top.y - bottom.y, Is.GreaterThanOrEqualTo(24), "Tile number must be at least 24 pixels high.");
                }
            }
        }

        [UnityTest]
        public IEnumerator PauseBlocksCommandsAndRackSwapPreservesIdentity()
        {
            root = new GameObject("Paused Table");
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0);
            yield return null;
            var view = game.Session.GetView(0);
            var first = view.OwnHand[0]; var second = view.OwnHand[1];
            game.SelectTile(first); game.ClickTile(second);
            Assert.That(game.Presenter.Rack.IndexOf(first), Is.EqualTo(1));
            Assert.That(game.Presenter.Rack.IndexOf(second), Is.Zero);
            game.TogglePause();
            var rejected = game.DiscardSelectedAsync(true); yield return Await(rejected);
            Assert.That(rejected.Result.Error, Is.EqualTo(ErrorCode.WrongPhase));
            Assert.That(game.Session.GetView(0).Version, Is.Zero);
            Assert.That(game.Presenter.VisibleOwnTileIds, Is.EquivalentTo(view.OwnHand));
            game.TogglePause();
            var accepted = game.DiscardSelectedAsync(true); yield return Await(accepted);
            Assert.That(accepted.Result.Accepted, Is.True);
            Assert.That(game.Session.GetView(0).Version, Is.EqualTo(1));
        }
    }
}
