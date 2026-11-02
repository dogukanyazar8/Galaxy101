using System.Collections;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Galaxy101.Tests
{
    /// <summary>G101-Masa pushDisc: each discard pile shows its last four tiles, each one level higher; older tiles are gone.</summary>
    public sealed class DiscardPileRuntimeTests
    {
        private GameObject owner;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) Object.Destroy(owner);
            yield return null;
        }

        [UnityTest]
        public IEnumerator APileKeepsItsLastFourTilesOnSeparateLevelsAndNeverRevivesAnOldOne()
        {
            owner = new GameObject("Discard pile scene"); var scene = owner.AddComponent<TableScene>();
            yield return null;
            var board = new TableBoard(scene);
            var rack = new RackGrid(); rack.Deal(new int[0]);
            BoardState State(params int[] pile)
            {
                var s = new BoardState { Rack = rack, Stock = 20 }; s.Discards[3].AddRange(pile); return s;
            }
            board.Sync(State(1, 2, 3, 4, 5, 6, 7), true);
            Assert.That(new[] { 1, 2, 3 }.All(id => board.ViewOf(id) == null), Is.True, "the three oldest tiles are gone");
            var heights = new[] { 4, 5, 6, 7 }.Select(id => board.ViewOf(id).Target.position.y).ToArray();
            for (int i = 1; i < heights.Length; i++) Assert.That(heights[i], Is.GreaterThan(heights[i - 1] + 0.001f), "one tile per level");
            // The top is taken: three remain, and the buried tile 3 does not come back.
            board.Sync(State(1, 2, 3, 4, 5, 6), true);
            Assert.That(board.ViewOf(3), Is.Null);
            Assert.That(new[] { 4, 5, 6 }.All(id => board.ViewOf(id) != null), Is.True);
            // A new hand: the pile starts empty, so the same ids show again.
            board.Sync(State(), true); board.Sync(State(1, 2), true);
            Assert.That(board.ViewOf(1), Is.Not.Null);
            board.Clear();
        }
    }
}
