using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Application.Tests
{
    public sealed class ReplayTests
    {
        private string root;
        [SetUp] public void Setup() { root = Path.Combine(Path.GetTempPath(), "okey-replay-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
        [TearDown] public void Cleanup() { Directory.Delete(root, true); }
        [Test] public void RestoresExactFinalStateAndScores()
        {
            var initial = PracticeDeals.Create("Finish");
            var plan = new BotPlanner().PlanTurn(PlayerViewProjector.ForPlayer(initial, 0), CancellationToken.None);
            var command = new CommitTurnCommand(new CommandMeta(initial.RoundId, 0, 1, initial.Version), plan);
            var final = GameEngine.Apply(initial, command, CancellationToken.None).Value;
            var codec = new ReplayCodec(root); var record = new ReplayRecord(initial, new[] { command }, ReplayCodec.Hash(final));
            codec.Write(record, "round.json"); Assert.That(File.Exists(Path.Combine(root, "round.json")), Is.True);
            var read = codec.Read("round.json"); Assert.That(read.Success, Is.True);
            var restored = ReplayCodec.Restore(read.Value, CancellationToken.None); Assert.That(restored.Success, Is.True);
            Assert.That(ReplayCodec.Hash(restored.Value), Is.EqualTo(ReplayCodec.Hash(final)).And.Length.EqualTo(64));
            Assert.That(restored.Value.Result.FinishInfo.Winner, Is.Zero);
            Assert.That(restored.Value.Result.Scores[0].Total, Is.EqualTo(-202));
            Assert.That(restored.Value.Table.SelectMany(m => m.Group.Tiles.Select(t => (t.Id, t.RepresentedFace))),
                Is.EqualTo(final.Table.SelectMany(m => m.Group.Tiles.Select(t => (t.Id, t.RepresentedFace)))));
            Assert.That(restored.Value.Stock, Is.EqualTo(initial.Stock));
            Assert.That(restored.Value.LastAcceptedSequences, Is.EqualTo(new long[] { 1, 0, 0, 0 }));
        }
        [Test] public void ARandomStartKeepsItsDiceThroughAReplay()
        {
            // Seed 71 drew its dealer before the dice: only the stored deal tells the real ones.
            var initial = RoundFactory.Create(71, Guid.NewGuid(), null);
            var codec = new ReplayCodec(root);
            codec.Write(new ReplayRecord(initial, Array.Empty<GameCommand>(), ReplayCodec.Hash(initial)), "dealt.json");
            var restored = ReplayCodec.Restore(codec.Read("dealt.json").Value, CancellationToken.None).Value;
            var dealt = DealCeremony.Describe(initial); var replayed = DealCeremony.Describe(restored);
            Assert.That((replayed.Dealer, replayed.DieA, replayed.DieB, replayed.IndicatorPosition), Is.EqualTo((dealt.Dealer, dealt.DieA, dealt.DieB, dealt.IndicatorPosition)));
            Assert.That(ReplayCodec.Hash(restored), Is.EqualTo(ReplayCodec.Hash(initial)));
        }
        [Test] public void AnimationSettingsDoNotAffectReplay()
        {
            var codec = new ReplayCodec(root); var state = PracticeDeals.Create("Swap");
            codec.Write(new ReplayRecord(state, Array.Empty<GameCommand>(), ReplayCodec.Hash(state)), "base.json");
            var json = File.ReadAllText(Path.Combine(root, "base.json"));
            foreach (var motion in new[] { "true", "false" })
            {
                File.WriteAllText(Path.Combine(root, "motion.json"), "{\"Presentation\":{\"ReducedMotion\":" + motion + "}," + json.Substring(1));
                var read = codec.Read("motion.json"); Assert.That(read.Success, Is.True);
                Assert.That(ReplayCodec.Hash(ReplayCodec.Restore(read.Value, CancellationToken.None).Value), Is.EqualTo(ReplayCodec.Hash(state)));
            }
        }
        [Test] public void RejectsTamperedHashAndUnknownCommand()
        {
            var state = PracticeDeals.Create("Pairs"); var codec = new ReplayCodec(root);
            Assert.Throws<ArgumentException>(() => codec.Write(new ReplayRecord(state, Array.Empty<GameCommand>(), new string('0', 64)), "bad.json"));
            codec.Write(new ReplayRecord(state, Array.Empty<GameCommand>(), ReplayCodec.Hash(state)), "base.json");
            var json = File.ReadAllText(Path.Combine(root, "base.json"));
            File.WriteAllText(Path.Combine(root, "bad.json"), json.Replace("\"Commands\":[]", "\"Commands\":[{\"Kind\":\"System.Process\"}]"));
            Assert.That(codec.Read("bad.json").Success, Is.False);
        }
        [Test] public void HashIsCultureIndependentAndSensitiveToStockOrder()
        {
            var state = RoundFactory.Create(71, Guid.NewGuid(), 0); var before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR"); var a = ReplayCodec.Hash(state);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA"); Assert.That(ReplayCodec.Hash(state), Is.EqualTo(a).And.Length.EqualTo(64));
                var reversed = new RoundState(state.RoundId,state.Version,state.Seed,state.StartingPlayer,state.ActivePlayer,state.TurnNumber,state.Phase,state.Indicator,
                    state.Stock.Reverse().ToArray(),state.Players,state.Table,state.Discards,state.PendingDraw,state.OpeningAtTurnStart,state.SeriesTilesUsed,state.IsInitialTurn,state.LastAcceptedSequences);
                Assert.That(a, Is.Not.EqualTo(ReplayCodec.Hash(reversed)));
            }
            finally { CultureInfo.CurrentCulture = before; }
        }
        [Test] public async Task SessionRecordsOnlyAcceptedCommandsAndResetsAtNewRound()
        {
            using (var session = new LocalSession())
            {
                session.Start(71, 0); var view = session.GetView(0);
                var command = new CommitTurnCommand(new CommandMeta(view.RoundId, 0, 1, 0), new TurnPlan(Array.Empty<TableAction>(), view.OwnHand[0], true));
                Assert.That((await session.SubmitAsync(command, CancellationToken.None)).Accepted, Is.True);
                Assert.That((await session.SubmitAsync(command, CancellationToken.None)).Accepted, Is.False);
                view = session.GetView(1);
                Assert.That((await session.SubmitAsync(new DrawCommand(new CommandMeta(view.RoundId,1,1,view.Version),DrawSource.Stock),CancellationToken.None)).Accepted, Is.True);
                var record = session.CaptureReplay(); Assert.That(record.Commands.Count, Is.EqualTo(2));
                var codec = new ReplayCodec(root); codec.Write(record,"session.json"); record=codec.Read("session.json").Value;
                Assert.That(record.InitialState.Version, Is.Zero); var restored=ReplayCodec.Restore(record, CancellationToken.None).Value;
                Assert.That(restored.Version, Is.EqualTo(2)); Assert.That(restored.PendingDraw.Source, Is.EqualTo(DrawSource.Stock));
                Assert.That(restored.LastAcceptedSequences, Is.EqualTo(new long[]{1,1,0,0}));
                session.Start(72, 0); Assert.That(session.CaptureReplay().Commands, Is.Empty);
            }
        }
        [Test] public void SwapExtensionAndNewGroupSurviveReplayWithFixedBindings()
        {
            TileId Id(TileColor color,int n)=>new TileId(2*(13*(int)color+n-1));
            PlacedTile Normal(TileColor color,int n)=>new PlacedTile(Id(color,n),new TileFace(color,n));
            var draft=new Okey101.TestSupport.RoundTestBuilder().WithIndicator(Id(TileColor.Black,13)).WithOpening(0,OpeningKind.Series).WithOpening(1,OpeningKind.Series)
                .WithHand(0,Id(TileColor.Red,2),Id(TileColor.Yellow,4),Id(TileColor.Blue,7),Id(TileColor.Blue,8),Id(TileColor.Blue,9),Id(TileColor.Red,10),Id(TileColor.Black,12))
                .WithHand(1,Id(TileColor.Red,6)).WithHand(2,Id(TileColor.Red,7)).WithHand(3,Id(TileColor.Red,8))
                .WithTable(new Meld(0,1,new MeldProposal(MeldKind.Run,new[]{Normal(TileColor.Red,1),new PlacedTile(Id(TileColor.Black,1),new TileFace(TileColor.Red,2)),Normal(TileColor.Red,3)})),
                    new Meld(1,1,new MeldProposal(MeldKind.Run,new[]{Normal(TileColor.Yellow,1),Normal(TileColor.Yellow,2),Normal(TileColor.Yellow,3)}))).Build();
            var initial=new RoundState(draft.RoundId,0,1,0,0,1,RoundPhase.PlanningTurn,draft.Indicator,draft.Stock,draft.Players,draft.Table,draft.Discards,
                new DrawReceipt(DrawSource.Stock,Id(TileColor.Red,2),false),OpeningKind.Series,0,false,draft.LastAcceptedSequences);
            var actions=new TableAction[]{new SwapJoker(0,Id(TileColor.Black,1),Id(TileColor.Red,2)),new ExtendMeld(1,false,new[]{Normal(TileColor.Yellow,4)}),
                new AddGroup(new MeldProposal(MeldKind.Run,new[]{Normal(TileColor.Blue,7),Normal(TileColor.Blue,8),Normal(TileColor.Blue,9)}))};
            var command=new CommitTurnCommand(new CommandMeta(initial.RoundId,0,1,0),new TurnPlan(actions,Id(TileColor.Black,12),true));
            var final=GameEngine.Apply(initial,command,CancellationToken.None); Assert.That(final.Success,Is.True,final.Error.ToString());
            var codec=new ReplayCodec(root);codec.Write(new ReplayRecord(initial,new[]{command},ReplayCodec.Hash(final.Value)),"actions.json");
            var record=codec.Read("actions.json");Assert.That(record.Success,Is.True);
            Assert.That(ReplayCodec.Hash(ReplayCodec.Restore(record.Value,CancellationToken.None).Value),Is.EqualTo(ReplayCodec.Hash(final.Value)));
            Assert.That(record.Value.Commands.Cast<CommitTurnCommand>().Single().Plan.Actions.Select(a=>a.GetType()),Is.EqualTo(new[]{typeof(SwapJoker),typeof(ExtendMeld),typeof(AddGroup)}));
        }
        [Test] public void RejectsUnknownActionAndDuplicateRequiredField()
        {
            var initial=PracticeDeals.Create("Series");var plan=new BotPlanner().PlanTurn(PlayerViewProjector.ForPlayer(initial,0),CancellationToken.None);
            var command=new CommitTurnCommand(new CommandMeta(initial.RoundId,0,1,initial.Version),plan);
            var final=GameEngine.Apply(initial,command,CancellationToken.None).Value;var codec=new ReplayCodec(root);
            codec.Write(new ReplayRecord(initial,new[]{command},ReplayCodec.Hash(final)),"source.json");
            var json=File.ReadAllText(Path.Combine(root,"source.json"));
            foreach(var content in new[]{json.Replace("\"Kind\":\"Open\"","\"Kind\":\"Execute\""),"{\"SchemaVersion\":1,"+json.Substring(1),json+"{}"})
            { File.WriteAllText(Path.Combine(root,"invalid.json"),content);Assert.That(codec.Read("invalid.json").Success,Is.False,content==json+"{}"?"trailing root":content.StartsWith("{\"SchemaVersion\":1,{",StringComparison.Ordinal)?"duplicate schema":"unknown action or duplicate required field"); }
        }
        [Test] public async Task InvalidCancelledAndObsoleteReplayCannotReplaceCurrentRound()
        {
            var initial=PracticeDeals.Create("Pairs");var valid=new ReplayRecord(initial,Array.Empty<GameCommand>(),ReplayCodec.Hash(initial));
            using(var session=new LocalSession())
            using(var cancelled=new CancellationTokenSource())
            {
                session.Start(71,0);var before=session.CaptureReplay().ExpectedFinalHash;
                Assert.That((await session.RestoreReplayAsync(new ReplayRecord(initial,Array.Empty<GameCommand>(),new string('0',64)),CancellationToken.None)).Success,Is.False);
                Assert.That(session.CaptureReplay().ExpectedFinalHash,Is.EqualTo(before));
                cancelled.Cancel();Assert.That((await session.RestoreReplayAsync(valid,cancelled.Token)).Success,Is.False);
                Assert.That(session.CaptureReplay().ExpectedFinalHash,Is.EqualTo(before));
                var pending=session.RestoreReplayAsync(valid,CancellationToken.None);session.Start(72,0);var current=session.CaptureReplay().ExpectedFinalHash;
                await pending;Assert.That(session.CaptureReplay().ExpectedFinalHash,Is.EqualTo(current));
                Assert.That(session.CaptureReplay().Commands,Is.Empty);
            }
        }
    }
}
