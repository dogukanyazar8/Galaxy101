using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>table3d.js:242-261: rack badges must be present on the live scene's tile renderers.</summary>
    public sealed class TileTagRuntimeTests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;

        private TableGame Build(string flow)
        {
            TableGame.Pending = new TableLaunch
            {
                Setup = new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, TurnMs = 60000 },
                Table = TableGame.DefaultTable(), Flow = flow, Clock = () => Time.timeAsDouble * 1000,
            };
            gameOwner = new GameObject("TableGame");
            var game = gameOwner.AddComponent<TableGame>();
            gameOwner.AddComponent<RackInput>();
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-tile-tags-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            return game;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TableGame.Pending = null;
            foreach (var owner in new[] { hudOwner, gameOwner, sceneOwner })
                if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator PlayableRackTilesShowThePrototypesIslekMarker()
        {
            var game = Build("isle");
            yield return Until(() => game.HumanTurn && game.CanDiscard, "the processing scenario");
            var playable = game.Rack.Tiles.Where(id => game.Model.IsIslek(TileIds.ToV4(id)) && !game.Model.IsOkey(TileIds.ToV4(id))).ToArray();
            Assert.That(playable, Is.Not.Empty, "The real scenario must contain a tile that can be laid off.");
            foreach (int id in playable) AssertVisibleTag(game.Board.ViewOf(id), "islek");
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator TileTakenFromTheLeftShowsSideMarkerAndHidesItWhenReturned()
        {
            var game = Build("play");
            yield return Until(() => game.HumanTurn && game.CanDrawSide, "a legal draw from the left");
            var draw = game.Draw(true, null);
            yield return Until(() => draw.IsCompleted, "the side draw");
            Assert.That(draw.Result, Is.True);
            int id = game.PickedTile;
            Assert.That(id, Is.GreaterThanOrEqualTo(0));
            var tile = game.Board.ViewOf(id);
            AssertVisibleTag(tile, "side");
            Assert.That(VisibleTags(tile).Count(), Is.EqualTo(1), "A picked tile has one badge, even when it can also be laid off.");

            game.PutBack();
            yield return Until(() => game.PickedTile < 0 && !game.Rack.TryFind(id, out _), "the tile returning to the left pile");
            Assert.That(game.Board.ViewOf(id), Is.SameAs(tile), "The same physical tile moves back to the pile.");
            Assert.That(VisibleTags(tile), Is.Empty, "Rack badges must disappear when the tile leaves my rack.");
        }

        private static System.Collections.Generic.IEnumerable<MeshRenderer> VisibleTags(TileView tile)
        {
            Assert.That(tile, Is.Not.Null, "The board must have a real tile object.");
            return tile.GetComponentsInChildren<MeshRenderer>(true).Where(renderer =>
                renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sharedMaterial != null &&
                (renderer.sharedMaterial.name == "simge_side" || renderer.sharedMaterial.name == "simge_islek"));
        }

        private static void AssertVisibleTag(TileView tile, string kind)
        {
            var marker = VisibleTags(tile).SingleOrDefault(renderer => renderer.sharedMaterial.name == "simge_" + kind);
            Assert.That(marker, Is.Not.Null, "Missing visible " + kind + " rack badge (table3d.js:242-261).");
            var texture = marker.sharedMaterial.mainTexture;
            Assert.That(texture, Is.Not.Null, "The marker must use the prototype's extracted texture.");
            Assert.That(texture.width, Is.EqualTo(128));
            Assert.That(texture.height, Is.EqualTo(128));
        }

        private static IEnumerator Until(Func<bool> condition, string what)
        {
            float end = Time.realtimeSinceStartup + 10;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }
    }
}
