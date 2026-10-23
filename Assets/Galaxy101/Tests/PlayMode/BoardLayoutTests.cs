using Galaxy101.Table;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    /// <summary>spec/23 centre board: two rows per opener, runs on their numbers, sets from column 14, no overlaps.</summary>
    public sealed class BoardLayoutTests
    {
        [Test]
        public void RunsSitOnTheirNumbersAndSetsStartAtColumnFourteen()
        {
            var board = new BoardLayout();
            Assert.That(board.Place(1, 2, true, 3, 3), Is.EqualTo((0, 2)), "first opener: rows 0–1; 3-4-5 at columns 2–4");
            Assert.That(board.Place(2, 2, false, 7, 3), Is.EqualTo((0, 14)), "a set from column 14");
            Assert.That(board.Place(3, 0, true, 1, 4), Is.EqualTo((2, 0)), "second opener: rows 2–3");
            Assert.That(board.OpeningOrder, Is.EqualTo(new[] { 2, 0 }));
        }

        [Test]
        public void MeldsKeepAFreeColumnAndAGrowingRunMovesAlone()
        {
            var board = new BoardLayout();
            board.Place(1, 0, true, 3, 3);                       // columns 2–4
            var probe = new BoardLayout(); probe.Place(1, 0, true, 3, 3);
            Assert.That(probe.Place(9, 0, true, 6, 3), Is.EqualTo((1, 5)), "touching 3-5 would leave no laying-off room");
            Assert.That(board.Place(2, 0, true, 7, 3), Is.EqualTo((0, 6)), "one free column between them");
            // 3-5 grows to 3-7: it would run into 7-9, so it moves to the owner's other row.
            Assert.That(board.Place(1, 0, true, 3, 5), Is.EqualTo((1, 2)));
            Assert.That(board.TryGet(2, out var other) && other.Row == 0 && other.Column == 6, Is.True, "the neighbour stays put");
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
    }
}
