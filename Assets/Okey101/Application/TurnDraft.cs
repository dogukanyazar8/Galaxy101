using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using Okey101.Core.Turns;

namespace Okey101.Application
{
    public sealed class TurnPreview
    {
        public bool Valid { get; }
        public ErrorCode Error { get; }
        public int OpeningTotal { get; }
        public int PairCount { get; }
        public IReadOnlyDictionary<TileId, TileFace> BindingChoices { get; }
        public IReadOnlyList<TileId> ProjectedOwnHand { get; }
        public IReadOnlyList<Meld> ProjectedTable { get; }
        public int SeriesTilesUsed { get; }
        public int DiscardPenalty { get; }
        public bool ActionsValid { get; }
        public OpeningKind Opening { get; }
        public TileId? Discard { get; }
        public IReadOnlyList<TileId> ReservedTiles { get; }
        internal TurnPreview(ErrorCode error, int total, int pairs, IDictionary<TileId, TileFace> bindings,
            IReadOnlyList<TileId> hand, IReadOnlyList<Meld> table, int used, int penalty,
            bool actionsValid, OpeningKind opening, TileId? discard, IEnumerable<TileId> reserved)
        {
            Error = error; Valid = error == ErrorCode.None; OpeningTotal = total; PairCount = pairs;
            BindingChoices = new ReadOnlyDictionary<TileId, TileFace>(new Dictionary<TileId, TileFace>(bindings));
            ProjectedOwnHand = Array.AsReadOnly(hand.ToArray()); ProjectedTable = Array.AsReadOnly(table.ToArray());
            SeriesTilesUsed = used; DiscardPenalty = penalty; ActionsValid = actionsValid; Opening = opening; Discard = discard;
            ReservedTiles = Array.AsReadOnly(reserved.Distinct().ToArray());
        }
    }

    public sealed class TurnDraft
    {
        private readonly object gate = new object();
        private PlayerView view;
        private readonly List<TableAction> actions = new List<TableAction>();
        private readonly Dictionary<TileId, TileFace> bindings = new Dictionary<TileId, TileFace>();
        private TileId? discard;
        private bool acceptPenalty;
        public IReadOnlyList<TableAction> Actions { get { lock (gate) return Array.AsReadOnly(BoundActions()); } }
        public void Reset(PlayerView value)
        {
            lock (gate)
            {
                view = value ?? throw new ArgumentNullException(nameof(value));
                actions.Clear(); bindings.Clear(); discard = null; acceptPenalty = false;
            }
        }
        public void SetPlan(TurnPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            lock (gate)
            {
                RequireView(); actions.Clear(); actions.AddRange(plan.Actions); bindings.Clear();
                discard = plan.Discard; acceptPenalty = plan.AcceptPenalty;
            }
        }
        public void StageAction(TableAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            lock (gate) { RequireView(); actions.Add(MapPlacements(action, tile => bindings.TryGetValue(tile.Id, out var face) ? new PlacedTile(tile.Id, face) : tile)); acceptPenalty = false; }
        }
        public void StageGroup(MeldProposal group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            lock (gate)
            {
                RequireView(); acceptPenalty = false;
                group = new MeldProposal(group.Kind, group.Tiles.Select(tile => bindings.TryGetValue(tile.Id, out var face) ? new PlacedTile(tile.Id, face) : tile).ToArray());
                if (view.OwnOpening != OpeningKind.None) { actions.Add(new AddGroup(group)); return; }
                var index = actions.FindIndex(a => a is OpenGroups);
                if (index >= 0)
                {
                    var state = PlayerViewProjector.ForEvaluation(view);
                    var prefix = state.Success ? TableEvaluator.Evaluate(state.Value, BoundActions()) : RuleResult<RoundState>.Fail(state.Error);
                    // Once opening succeeds, later groups are ordered actions. In particular,
                    // a group may use a joker recovered by an earlier swap in this draft.
                    if (actions.Count > 1 || (prefix.Success && prefix.Value.Players[view.Actor].Opening != OpeningKind.None))
                    { actions.Add(new AddGroup(group)); return; }
                }
                if (index < 0) actions.Insert(0, new OpenGroups(group.Kind == MeldKind.Pair ? OpeningKind.Pairs : OpeningKind.Series, new[] { group }));
                else
                {
                    var open = (OpenGroups)actions[index];
                    actions[index] = new OpenGroups(open.Opening, open.Groups.Concat(new[] { group }).ToArray());
                }
            }
        }
        public void SelectDiscard(TileId tile)
        {
            // Ownership is checked against the projected hand by Core. A recovered joker
            // is not in the original hand but is a legitimate discard after a swap.
            lock (gate) { RequireView(); discard = tile; acceptPenalty = false; }
        }
        public void ClearDiscard() { lock (gate) { discard = null; acceptPenalty = false; } }
        public void ChooseBinding(TileId joker, TileFace face)
        {
            if (!face.IsValid) throw new ArgumentException("Invalid joker face.", nameof(face));
            lock (gate)
            {
                RequireView();
                var indicator = TileCatalog.Create()[view.Indicator.Value].PrintedFace;
                if (!indicator.HasValue || !TileCatalog.IsRealJoker(joker, indicator.Value))
                    throw new ArgumentException("An explicit binding is only for a real joker.", nameof(joker));
                var editable = view.OwnHand.Contains(joker) || actions.OfType<SwapJoker>().Any(a => a.JokerId == joker);
                if (!editable) throw new InvalidOperationException("A committed table joker's binding is locked.");
                bindings[joker] = face; acceptPenalty = false;
                // Edit only the latest placement that still contains this joker. A later
                // swap ends that placement; assigning the recovered joker must not rewrite
                // the earlier group or change the replacement that made that swap legal.
                for (var index = actions.Count - 1; index >= 0; index--)
                {
                    if (actions[index] is SwapJoker swap && swap.JokerId == joker) break;
                    var found = false;
                    var changed = MapPlacements(actions[index], tile =>
                    {
                        if (tile.Id != joker) return tile;
                        found = true; return new PlacedTile(joker, face);
                    });
                    if (found) { actions[index] = changed; break; }
                }
            }
        }
        public void UndoLast()
        {
            lock (gate)
            {
                if (actions.Count == 0) return;
                var index = actions.Count - 1;
                if (actions[index] is OpenGroups open && open.Groups.Count > 1)
                    actions[index] = new OpenGroups(open.Opening, open.Groups.Take(open.Groups.Count - 1).ToArray());
                else actions.RemoveAt(index);
                acceptPenalty = false;
            }
        }
        public TurnPlan Build()
        {
            lock (gate)
            {
                RequireView();
                if (!discard.HasValue) throw new InvalidOperationException("Select a discard first.");
                return new TurnPlan(BoundActions(), discard.Value, acceptPenalty);
            }
        }
        private void RequireView() { if (view == null) throw new InvalidOperationException("Reset the draft before editing it."); }
        private TableAction[] BoundActions()
        {
            return actions.ToArray();
        }
        private static TableAction MapPlacements(TableAction action, Func<PlacedTile, PlacedTile> bind)
        {
            MeldProposal Group(MeldProposal group) => new MeldProposal(group.Kind, group.Tiles.Select(bind).ToArray());
            return action is OpenGroups open ? (TableAction)new OpenGroups(open.Opening, open.Groups.Select(Group).ToArray())
                : action is AddGroup add ? new AddGroup(Group(add.Group))
                : action is ExtendMeld extend ? new ExtendMeld(extend.MeldId, extend.Prepend, extend.Tiles.Select(bind).ToArray()) : action;
        }
        public Task<TurnPreview> PreviewAsync(CancellationToken token)
        {
            PlayerView snapshot; TableAction[] planned; TileId? selected;
            lock (gate) { RequireView(); snapshot = view; planned = BoundActions(); selected = discard; }
            return Task.Run(() => Preview(snapshot, planned, selected, token));
        }
        private static TurnPreview Preview(PlayerView snapshot, TableAction[] planned, TileId? selected, CancellationToken token)
        {
            var total = 0; var pairs = 0;
            var choices = new Dictionary<TileId, TileFace>();
            var hand = snapshot.OwnHand; var table = snapshot.Table; var used = snapshot.SeriesTilesUsed;
            var opening = snapshot.OwnOpening; var actionsValid = false; var penalty = 0;
            var reserved = planned.SelectMany(action => action is OpenGroups open ? open.Groups.SelectMany(g => g.Tiles.Select(t => t.Id))
                : action is AddGroup add ? add.Group.Tiles.Select(t => t.Id)
                : action is ExtendMeld extend ? extend.Tiles.Select(t => t.Id)
                : action is SwapJoker swap ? new[] { swap.ReplacementId } : Array.Empty<TileId>()).ToArray();
            TurnPreview Result(ErrorCode error) => new TurnPreview(error, total, pairs, choices, hand, table, used, penalty, actionsValid, opening, selected, reserved);
            try
            {
                token.ThrowIfCancellationRequested();
                if (snapshot.Actor != snapshot.ActivePlayer) return Result(ErrorCode.WrongPlayer);
                if (snapshot.Phase != RoundPhase.PlanningTurn || snapshot.Result != null) return Result(ErrorCode.WrongPhase);
                var reconstructed = PlayerViewProjector.ForEvaluation(snapshot);
                if (!reconstructed.Success) return Result(reconstructed.Error);
                var root = reconstructed.Value;
                var indicator = TileCatalog.Create()[snapshot.Indicator.Value].PrintedFace.Value;
                foreach (var action in planned)
                {
                    token.ThrowIfCancellationRequested();
                    var groups = action is OpenGroups open ? open.Groups : action is AddGroup add ? new[] { add.Group } : Array.Empty<MeldProposal>();
                    foreach (var group in groups)
                    {
                        var validation = MeldValidator.Validate(group, indicator);
                        if (action is OpenGroups)
                        {
                            if (validation.Success) total += validation.Value;
                            if (group.Kind == MeldKind.Pair && validation.Success) pairs++;
                        }
                        foreach (var tile in group.Tiles)
                            if (TileCatalog.IsRealJoker(tile.Id, indicator)) choices[tile.Id] = tile.RepresentedFace;
                    }
                    if (action is ExtendMeld extend)
                        foreach (var tile in extend.Tiles)
                            if (TileCatalog.IsRealJoker(tile.Id, indicator)) choices[tile.Id] = tile.RepresentedFace;
                    if (action is SwapJoker swap) choices.Remove(swap.JokerId);
                }
                var evaluated = TableEvaluator.Evaluate(root, planned);
                if (!evaluated.Success) return Result(evaluated.Error);
                var candidate = evaluated.Value; var player = candidate.Players[snapshot.Actor];
                hand = player.Hand; table = candidate.Table; used = candidate.SeriesTilesUsed; opening = player.Opening; actionsValid = true;
                if (!selected.HasValue) return Result(ErrorCode.LastTileRequired);
                if (!hand.Contains(selected.Value)) return Result(ErrorCode.InvalidTile);
                penalty = PenaltyEvaluator.GetDiscardPenalty(candidate, selected.Value, hand.Count == 1);
                // Validation accepts the penalty only to expose a legal preview. The UI still
                // requires explicit confirmation; Build never silently sets AcceptPenalty.
                var turn = TurnEvaluator.Evaluate(root, new TurnPlan(planned, selected.Value, true));
                token.ThrowIfCancellationRequested();
                return Result(turn.Success ? ErrorCode.None : turn.Error);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return Result(ErrorCode.Cancelled); }
        }

    }
}
