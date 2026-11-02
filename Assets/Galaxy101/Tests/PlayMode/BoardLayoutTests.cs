using Galaxy101.Table;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    /// <summary>Coordinates captured from the active HTML layoutBoard, including its retained physical-tile anchors.</summary>
    public sealed class BoardLayoutTests
    {
        [Test]
        public void RunsAndGroupsUseTheSourceMainZoneAndOpeningRows()
        {
            var board = new BoardLayout();
            Assert.That(board.Place(1, 2, true, 3, 3), Is.EqualTo((0, 2)), "first opener: rows 0–1; 3-4-5 at columns 2–4");
            Assert.That(board.Place(2, 2, false, 7, 3), Is.EqualTo((0, 9)), "groups prefer column 9 and reserve four cells");
            Assert.That(board.Place(3, 0, true, 1, 4), Is.EqualTo((2, 0)), "second opener: rows 2–3");
            Assert.That(board.OpeningOrder, Is.EqualTo(new[] { 2, 0 }));
        }

        [Test]
        public void NewRunsSearchColumnsWithinTheirOwnersRowBeforeTheNextRow()
        {
            var board = new BoardLayout();
            board.Place(1, 0, true, 3, 3);
            Assert.That(board.Place(2, 0, true, 7, 3), Is.EqualTo((0, 8)), "both runs reserve their extendable ends plus one empty cell");
            Assert.That(board.Place(2, 0, true, 6, 4), Is.EqualTo((0, 7)), "a left extension follows the retained anchor, not the printed number's absolute column");
            Assert.That(board.TryGet(1, out var first) && first.Row == 0 && first.Column == 2, Is.True);
        }

        [Test]
        public void PairsPreferThePairZoneInEveryRowBeforeSpillingIntoTheMainZone()
        {
            var board = new BoardLayout();
            Assert.That(board.Place(1, 0, false, 1, 2), Is.EqualTo((0, 15)));
            Assert.That(board.Place(2, 0, false, 1, 2), Is.EqualTo((0, 18)));
            Assert.That(board.Place(3, 0, false, 1, 2), Is.EqualTo((1, 15)));
        }

        [Test]
        public void TwoTileMeldsArePairsEvenWhenTheCallerClassifiesAJokerAsARun()
        {
            var board = new BoardLayout();
            Assert.That(board.Place(1, 0, true, 5, 2), Is.EqualTo((0, 15)));
            Assert.That(board.HasPairs, Is.True);
        }

        [Test]
        public void ThreeTileGroupsReserveFourColumnsBeforeAnotherGroupIsPlaced()
        {
            var board = new BoardLayout();
            Assert.That(board.Place(1, 0, false, 1, 3), Is.EqualTo((0, 9)));
            Assert.That(board.Place(2, 0, false, 1, 3), Is.EqualTo((0, 0)));
            Assert.That(board.Place(3, 0, false, 1, 3), Is.EqualTo((1, 9)));
        }

        [Test]
        public void ReconciliationKeepsAllOldAnchorsBeforePlacingAnEarlierNewInputMeld()
        {
            var board = new BoardLayout();
            var old = new BoardLayout.Meld(1, 0, true, 3, 3, new[] { 10, 11, 12 });
            board.Reconcile(new[] { old });
            board.Reconcile(new[] { new BoardLayout.Meld(2, 0, true, 7, 3, new[] { 20, 21, 22 }), old });
            Assert.That(board.TryGet(1, out var kept) && kept.Row == 0 && kept.Column == 2, Is.True);
            Assert.That(board.TryGet(2, out var added) && added.Row == 0 && added.Column == 8, Is.True);
        }

        [Test]
        public void ADisplacedOldRunCannotStealANewerMeldsRetainedRowDuringReconciliation()
        {
            var board = new BoardLayout();
            board.Place(1, 0, true, 3, 3);
            board.Place(2, 0, true, 7, 3);
            Assert.That(board.Place(3, 0, true, 1, 13), Is.EqualTo((1, 0)));
            // Run 2 grows left into run 1. Keep run 3 first, then move run 2 into row 2.
            Assert.That(board.Place(2, 0, true, 3, 7), Is.EqualTo((2, 2)));
            Assert.That(board.TryGet(3, out var third) && third.Row == 1 && third.Column == 0, Is.True);
        }

        [Test]
        public void APhysicalMeldKeepsItsAnchorAfterItsServerIdChanges()
        {
            var board = new BoardLayout();
            board.Reconcile(new[] { new BoardLayout.Meld(1, 2, true, 3, 3, new[] { 10, 11, 12 }) });
            board.Reconcile(new[] {
                new BoardLayout.Meld(3, 0, true, 1, 4, new[] { 20, 21, 22, 23 }),
                new BoardLayout.Meld(99, 2, true, 3, 3, new[] { 10, 11, 12 }) });
            Assert.That(board.TryGet(99, out var kept) && kept.Row == 0 && kept.Column == 2, Is.True);
            Assert.That(board.TryGet(3, out var added) && added.Row == 2 && added.Column == 0, Is.True);
            Assert.That(board.TryGet(1, out _), Is.False);
            Assert.That(board.OpeningOrder, Is.EqualTo(new[] { 2, 0 }));
        }

        [Test]
        public void AnOkeyTakenFromAGroupAndLaidInANewPairStaysInThePairZone()
        {
            var board = new BoardLayout();
            board.Reconcile(new[] { new BoardLayout.Meld(1, 2, false, 2, 4, new[] { 10, 11, 12, 99 }) });
            // The okey (99) left the group for my rack; I lay it with a 13 as a pair in the same update.
            board.Reconcile(new[] {
                new BoardLayout.Meld(5, 0, false, 13, 2, new[] { 50, 99 }),
                new BoardLayout.Meld(1, 2, false, 2, 3, new[] { 10, 11, 12 }) });
            Assert.That(board.TryGet(5, out var pair) && pair.Column >= 15, Is.True, "pairs never inherit a group's main-zone anchor");
            Assert.That(board.TryGet(1, out var group) && group.Row == 0 && group.Column == 9, Is.True, "the group keeps its place");
        }

        [Test]
        public void AStaleTileAnchorIsForgottenOnceTheTileLeavesTheBoard()
        {
            var board = new BoardLayout();
            board.Reconcile(new[] { new BoardLayout.Meld(1, 2, true, 3, 4, new[] { 10, 11, 12, 99 }) });
            board.Reconcile(new[] { new BoardLayout.Meld(1, 2, true, 3, 3, new[] { 10, 11, 12 }) });
            board.Reconcile(new[] {
                new BoardLayout.Meld(1, 2, true, 3, 3, new[] { 10, 11, 12 }),
                new BoardLayout.Meld(7, 0, true, 8, 3, new[] { 99, 60, 61 }) });
            Assert.That(board.TryGet(1, out var run) && run.Row == 0 && run.Column == 2, Is.True);
            Assert.That(board.TryGet(7, out var other) && other.Row == 2, Is.True, "a new run opens in its owner's rows");
        }

        [Test]
        public void ABoardWithEntirelyNewPhysicalTilesStartsANewOpeningOrder()
        {
            var board = new BoardLayout();
            board.Reconcile(new[] { new BoardLayout.Meld(1, 2, true, 3, 3, new[] { 10, 11, 12 }) });
            board.Reconcile(new[] { new BoardLayout.Meld(2, 0, true, 3, 3, new[] { 20, 21, 22 }) });
            Assert.That(board.OpeningOrder, Is.EqualTo(new[] { 0 }));
            Assert.That(board.TryGet(2, out var added) && added.Row == 0, Is.True);
        }

        [Test]
        public void ALeftExtensionMovesTheFirstColumnAndUndoForgetsMelds()
        {
            var board = new BoardLayout();
            board.Place(1, 1, true, 5, 3);
            Assert.That(board.Place(1, 1, true, 4, 4), Is.EqualTo((0, 3)));
            board.Retain(new int[0]);
            Assert.That(board.Count, Is.EqualTo(0)); Assert.That(board.OpeningOrder, Is.Empty);
        }

        [Test]
        public void ADeepSnapshotRestoresRelocatedAnchorsAndCanBeReusedAfterFurtherGrowth()
        {
            var board = new BoardLayout();
            board.Place(1, 0, true, 3, 3); board.Place(2, 0, true, 7, 3); board.Place(3, 0, true, 1, 13);
            var snapshot = board.Capture();
            Assert.That(board.Place(2, 0, true, 4, 6), Is.EqualTo((2, 3)));
            board.Restore(snapshot);
            Assert.That(board.TryGet(2, out var restored) && restored.Row == 0 && restored.Column == 8, Is.True);
            Assert.That(board.Place(2, 0, true, 6, 4), Is.EqualTo((0, 7)), "the original base value and column offset survive");
            board.Restore(snapshot);
            Assert.That(board.TryGet(2, out restored) && restored.Row == 0 && restored.Column == 8, Is.True, "restoring must not share mutable anchors with the saved state");
            Assert.That(board.Count, Is.EqualTo(3)); Assert.That(board.OpeningOrder, Is.EqualTo(new[] { 0 }));
        }
    }
}
