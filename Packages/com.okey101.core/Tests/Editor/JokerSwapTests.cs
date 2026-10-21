using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using static Okey101.Core.Tests.TableTest;

namespace Okey101.Core.Tests
{
    public sealed class JokerSwapTests
    {
        [TestCase(MeldKind.Run)]
        [TestCase(MeldKind.Set)]
        [TestCase(MeldKind.Pair)]
        public void BindingAndIdentityArePreserved(MeldKind kind)
        {
            var group = Target(kind);
            var joker = group.Tiles.Single(t => t.Id == Id(TileColor.Black, 1));
            var replacement = Id(joker.RepresentedFace.Color, joker.RepresentedFace.Number);
            var state = Builder(new[] { replacement, Spare }, OpeningKind.Series)
                .WithOpening(1, kind == MeldKind.Pair ? OpeningKind.Pairs : OpeningKind.Series)
                .WithTable(new Meld(4, 1, group)).Build();
            var next = Accept(state, new SwapJoker(4, joker.Id, replacement));
            Assert.That(next.Players[0].Hand, Is.EqualTo(new[] { Spare, joker.Id }));
            Assert.That(next.Table[0].Id, Is.EqualTo(4));
            Assert.That(next.Table[0].Owner, Is.EqualTo(1));
            Assert.That(next.Table[0].Group.Tiles.Select(t => t.RepresentedFace), Is.EqualTo(group.Tiles.Select(t => t.RepresentedFace)));
            Assert.That(next.Table[0].Group.Tiles.Any(t => t.Id == replacement), Is.True);
            Assert.That(next.Table[0].Group.Tiles.Any(t => t.Id == joker.Id), Is.False);
        }

        [TestCase(MeldKind.Run)]
        [TestCase(MeldKind.Set)]
        public void SwapConsumesOneBudgetAndCanKeepJoker(MeldKind kind)
        {
            var replacement = Id(TileColor.Red, kind == MeldKind.Run ? 2 : 7);
            var state = Builder(new[] { replacement, Spare }, OpeningKind.Pairs)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Target(kind))).Build();
            var next = Accept(state, new SwapJoker(0, Id(TileColor.Black, 1), replacement));
            Assert.That(next.SeriesTilesUsed, Is.EqualTo(1));
            Assert.That(next.Players[0].Hand.Contains(Id(TileColor.Black, 1)), Is.True);
            Reject(Context(state, 2), ErrorCode.ProcessingLimit,
                new SwapJoker(0, Id(TileColor.Black, 1), replacement));
        }

        [Test]
        public void OpenThenSwapInSamePlan()
        {
            var groups = OpeningGroups(101);
            var replacement = Id(TileColor.Red, 2);
            var state = Builder(Ids(groups).Concat(new[] { replacement, Spare }).ToArray())
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Target(MeldKind.Run))).Build();
            var open = new OpenGroups(OpeningKind.Series, groups);
            var swap = new SwapJoker(0, Id(TileColor.Black, 1), replacement);
            var next = Accept(state, open, swap);
            Assert.That(next.Players[0].Opening, Is.EqualTo(OpeningKind.Series));
            Assert.That(next.Players[0].Hand, Is.EqualTo(new[] { Spare, Id(TileColor.Black, 1) }));
            Reject(state, ErrorCode.OpeningRequired, swap, open);
        }

        [Test]
        public void BothJokersInPairCanBeRecoveredByTheirPhysicalIdentity()
        {
            var group = new MeldProposal(MeldKind.Pair,
                new[] { Joker(0, TileColor.Red, 7), Joker(1, TileColor.Red, 7) });
            var state = Context(Builder(new[] { Id(TileColor.Red, 7), Id(TileColor.Red, 7, 1), Spare }, OpeningKind.Pairs)
                .WithOpening(1, OpeningKind.Pairs).WithTable(new Meld(0, 1, group)).Build(), 2);
            var next = Accept(state, new SwapJoker(0, Id(TileColor.Black, 1, 1), Id(TileColor.Red, 7)),
                new SwapJoker(0, Id(TileColor.Black, 1), Id(TileColor.Red, 7, 1)));
            Assert.That(next.Players[0].Hand, Is.EqualTo(new[] { Spare, Id(TileColor.Black, 1, 1), Id(TileColor.Black, 1) }));
            Assert.That(next.Table[0].Group.Tiles.Select(t => t.Id), Is.EqualTo(new[] { Id(TileColor.Red, 7, 1), Id(TileColor.Red, 7) }));
            Assert.That(next.SeriesTilesUsed, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FakeReplacementAndPairSwap(bool pair)
        {
            var fake = new PlacedTile(new TileId(105), new TileFace(TileColor.Black, 1));
            var group = pair
                ? new MeldProposal(MeldKind.Pair, new[] { Joker(0, TileColor.Black, 1), fake })
                : new MeldProposal(MeldKind.Run, new[] { Joker(0, TileColor.Black, 1), Normal(TileColor.Black, 2), Normal(TileColor.Black, 3) });
            var state = Builder(new[] { new TileId(104), Spare }, OpeningKind.Pairs)
                .WithOpening(1, pair ? OpeningKind.Pairs : OpeningKind.Series).WithTable(new Meld(0, 1, group)).Build();
            var next = Accept(pair ? Context(state, 2) : state,
                new SwapJoker(0, Id(TileColor.Black, 1), new TileId(104)));
            Assert.That(next.SeriesTilesUsed, Is.EqualTo(pair ? 2 : 1));
            Assert.That(next.Table[0].Group.Tiles[0].Id, Is.EqualTo(new TileId(104)));
            Assert.That(next.Table[0].Group.Tiles[0].RepresentedFace, Is.EqualTo(new TileFace(TileColor.Black, 1)));
        }

        [Test]
        public void FakeReplacementMustMatchItsFixedFace()
        {
            var state = Builder(new[] { new TileId(104), Spare }, OpeningKind.Series)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Target(MeldKind.Run))).Build();
            Reject(state, ErrorCode.InvalidReplacement, new SwapJoker(0, Id(TileColor.Black, 1), new TileId(104)));
        }

        [TestCase(0, ErrorCode.InvalidReplacement)]
        [TestCase(1, ErrorCode.InvalidReplacement)]
        [TestCase(2, ErrorCode.InvalidReplacement)]
        [TestCase(3, ErrorCode.InvalidTile)]
        public void WrongReplacementIsAtomic(int mistake, ErrorCode expected)
        {
            var replacement = mistake == 0 ? Id(TileColor.Blue, 2) : mistake == 1 ? Id(TileColor.Red, 4)
                : mistake == 2 ? Id(TileColor.Black, 1, 1) : Id(TileColor.Red, 2);
            var pair = Pair(TileColor.Yellow, 5);
            var hand = Ids(pair).Concat(mistake == 3 ? new[] { Spare } : new[] { replacement, Spare }).ToArray();
            var builder = Builder(hand, OpeningKind.Series).WithOpening(1, OpeningKind.Pairs)
                .WithTable(new Meld(0, 1, Target(MeldKind.Run)));
            if (mistake == 3) builder.WithHand(1, replacement);
            Reject(builder.Build(), expected, new AddGroup(pair),
                new SwapJoker(0, Id(TileColor.Black, 1), replacement));
        }

        [Test]
        public void OpeningIsRequiredBeforeSwap()
        {
            var state = Builder(new[] { Id(TileColor.Red, 2), Spare })
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Target(MeldKind.Run))).Build();
            Reject(state, ErrorCode.OpeningRequired, new SwapJoker(0, Id(TileColor.Black, 1), Id(TileColor.Red, 2)));
        }

        [Test]
        public void TargetMustBeRealJokerInRequestedGroup()
        {
            var state = Builder(new[] { Id(TileColor.Red, 2), Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Target(MeldKind.Run))).Build();
            Reject(state, ErrorCode.InvalidReplacement, new SwapJoker(0, Id(TileColor.Red, 1), Id(TileColor.Red, 2)));
            Reject(state, ErrorCode.InvalidReplacement, new SwapJoker(0, Id(TileColor.Black, 1, 1), Id(TileColor.Red, 2)));
            Reject(state, ErrorCode.InvalidGroup, new SwapJoker(99, Id(TileColor.Black, 1), Id(TileColor.Red, 2)));
        }

        [Test]
        public void RecoveredJokerCanBeReboundAndProcessedInSamePlan()
        {
            var state = Builder(new[] { Id(TileColor.Red, 2), Id(TileColor.Blue, 5), Spare }, OpeningKind.Pairs)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Target(MeldKind.Run)),
                    new Meld(1, 1, Run(TileColor.Blue, 1, 2, 3))).Build();
            var swap = new SwapJoker(0, Id(TileColor.Black, 1), Id(TileColor.Red, 2));
            var process = new ExtendMeld(1, false, new[] { Joker(0, TileColor.Blue, 4) });
            var next = Accept(state, swap, process);
            Assert.That(next.SeriesTilesUsed, Is.EqualTo(2));
            Assert.That(next.Table[0].Group.Tiles[1].RepresentedFace, Is.EqualTo(new TileFace(TileColor.Red, 2)));
            Assert.That(next.Table[1].Group.Tiles[3].Id, Is.EqualTo(Id(TileColor.Black, 1)));
            Assert.That(next.Table[1].Group.Tiles[3].RepresentedFace, Is.EqualTo(new TileFace(TileColor.Blue, 4)));
            Reject(state, ErrorCode.ProcessingLimit, swap, process,
                new ExtendMeld(1, false, new[] { Normal(TileColor.Blue, 5) }));
        }

        [Test]
        public void ExtendingGroupCannotMoveOrRebindExistingJoker()
        {
            var group = Target(MeldKind.Run);
            var state = Builder(new[] { Id(TileColor.Red, 4), Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, group)).Build();
            var next = Accept(state, new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 4) }));
            Assert.That(next.Table[0].Group.Tiles[1].RepresentedFace, Is.EqualTo(new TileFace(TileColor.Red, 2)));
            Reject(state, ErrorCode.InvalidTile, new ExtendMeld(0, false, new[] { Joker(0, TileColor.Red, 4) }));
        }

        private static MeldProposal Target(MeldKind kind)
        {
            if (kind == MeldKind.Pair)
                return new MeldProposal(kind, new[] { Joker(0, TileColor.Red, 7), Normal(TileColor.Red, 7, 1) });
            if (kind == MeldKind.Set)
                return new MeldProposal(kind, new[] { Joker(0, TileColor.Red, 7), Normal(TileColor.Blue, 7), Normal(TileColor.Yellow, 7) });
            return new MeldProposal(kind, new[] { Normal(TileColor.Red, 1), Joker(0, TileColor.Red, 2), Normal(TileColor.Red, 3) });
        }
    }
}
