using Galaxy101.Contracts;
using Okey101.Core.Model;

namespace Galaxy101.Server.Games;

/// <summary>
/// Translation between the rules engine and the wire contracts. Inbound translation throws
/// <see cref="ArgumentException"/> on anything malformed; the realtime router reports it as a validation error.
/// </summary>
public static class Wire
{
    public static GameCommand ToCommand(MatchCommandMessage m, CommandMeta meta) => m.Kind switch
    {
        "draw" => new DrawCommand(meta, m.Source switch { "stock" => DrawSource.Stock, "discard" => DrawSource.PreviousDiscard, _ => throw new ArgumentException("source") }),
        "return" => new ReturnDiscardCommand(meta),
        "shortOpening" => new ShortOpeningCommand(meta, Opening(m.Opening)),
        "commit" => new CommitTurnCommand(meta, ToPlan(m.Plan ?? throw new ArgumentException("plan"))),
        _ => throw new ArgumentException("kind"),
    };

    public static MatchCommandMessage ToMessage(GameCommand command) => command switch
    {
        DrawCommand d => new MatchCommandMessage { Kind = "draw", Source = d.Source == DrawSource.Stock ? "stock" : "discard" },
        ReturnDiscardCommand => new MatchCommandMessage { Kind = "return" },
        ShortOpeningCommand s => new MatchCommandMessage { Kind = "shortOpening", Opening = Name(s.Opening) },
        CommitTurnCommand c => new MatchCommandMessage { Kind = "commit", Plan = ToDto(c.Plan) },
        _ => throw new ArgumentException("Unknown command."),
    };

    public static TurnPlan ToPlan(TurnPlanDto dto)
    {
        return new TurnPlan([.. Bounded(dto.Actions, 32).Select(ToAction)], new TileId(dto.Discard), dto.AcceptPenalty);
    }

    private static List<T> Bounded<T>(List<T>? items, int max) =>
        items is not null && items.Count <= max && items.All(x => x is not null) ? items : throw new ArgumentException("Missing or oversized list.");

    private static TableAction ToAction(TableActionDto a) => a.Type switch
    {
        "open" => new OpenGroups(Opening(a.Opening), [.. Bounded(a.Groups, 16).Select(ToProposal)]),
        "add" => new AddGroup(ToProposal(a.Group ?? throw new ArgumentException("group"))),
        "extend" => new ExtendMeld(a.MeldId, a.Prepend, [.. Bounded(a.Tiles, 13).Select(ToPlaced)]),
        "swap" => new SwapJoker(a.MeldId, new TileId(a.JokerId), new TileId(a.ReplacementId)),
        _ => throw new ArgumentException("type"),
    };

    private static MeldProposal ToProposal(MeldDto m) => new(m.Kind switch
    {
        "run" => MeldKind.Run, "set" => MeldKind.Set, "pair" => MeldKind.Pair, _ => throw new ArgumentException("kind"),
    }, [.. Bounded(m.Tiles, 13).Select(ToPlaced)]);

    private static PlacedTile ToPlaced(PlacedTileDto t) => new(new TileId(t.Id), new TileFace(Color(t.Color), t.Number));

    private static OpeningKind Opening(string? value) => value switch
    {
        "series" => OpeningKind.Series, "pairs" => OpeningKind.Pairs, _ => throw new ArgumentException("opening"),
    };

    private static TileColor Color(string? value) => value switch
    {
        "red" => TileColor.Red, "yellow" => TileColor.Yellow, "blue" => TileColor.Blue, "black" => TileColor.Black, _ => throw new ArgumentException("color"),
    };

    public static TurnPlanDto ToDto(TurnPlan plan) => new()
    {
        Discard = plan.Discard.Value, AcceptPenalty = plan.AcceptPenalty,
        Actions = [.. plan.Actions.Select(a => a switch
        {
            OpenGroups o => new TableActionDto { Type = "open", Opening = Name(o.Opening), Groups = [.. o.Groups.Select(g => ToDto(g))] },
            AddGroup g => new TableActionDto { Type = "add", Group = ToDto(g.Group) },
            ExtendMeld e => new TableActionDto { Type = "extend", MeldId = e.MeldId, Prepend = e.Prepend, Tiles = [.. e.Tiles.Select(ToDto)] },
            SwapJoker s => new TableActionDto { Type = "swap", MeldId = s.MeldId, JokerId = s.JokerId.Value, ReplacementId = s.ReplacementId.Value },
            _ => throw new ArgumentException("Unknown action."),
        })],
    };

    public static MeldDto ToDto(MeldProposal g, int id = 0, int owner = 0) => new()
    {
        Id = id, Owner = owner, Kind = g.Kind switch { MeldKind.Run => "run", MeldKind.Set => "set", _ => "pair" }, Tiles = [.. g.Tiles.Select(ToDto)],
    };

    private static PlacedTileDto ToDto(PlacedTile t) => new() { Id = t.Id.Value, Color = Name(t.RepresentedFace.Color), Number = t.RepresentedFace.Number };

    public static PlayerViewDto ToDto(PlayerView v) => new()
    {
        RoundId = v.RoundId, Version = v.Version, Seat = v.Actor, ActiveSeat = v.ActivePlayer, Phase = Name(v.Phase),
        Hand = [.. v.OwnHand.Select(t => t.Value)], Opening = Name(v.OwnOpening), Penalties = v.OwnPenalties,
        Indicator = v.Indicator.Value, StockCount = v.StockCount,
        Players = [.. v.Players.Select(p => new PublicPlayerDto { HandCount = p.HandCount, Opening = Name(p.Opening), Penalties = p.Penalties })],
        Table = [.. v.Table.Select(m => ToDto(m.Group, m.Id, m.Owner))],
        Discards = [.. v.Discards.Select(d => d.Select(t => t.Value).ToList())],
        PendingDraw = v.PendingDraw is { } d ? new DrawReceiptDto { Source = d.Source == DrawSource.Stock ? "stock" : "discard", Tile = d.Tile.Value, WasLastStock = d.WasLastStock } : null,
        SeriesTilesUsed = v.SeriesTilesUsed, Result = v.Result is { } r ? ToDto(r) : null,
        Deal = v.Deal is { } deal ? new DealDto
        {
            Dealer = deal.Dealer, DieA = deal.DieA, DieB = deal.DieB, IndicatorStack = deal.IndicatorStack,
            IndicatorPosition = deal.IndicatorPosition, FakeOkeyShown = deal.FakeOkeyShown, ExtraStack = deal.ExtraStack,
        } : null,
    };

    public static RoundResultDto ToDto(RoundResult r) => new()
    {
        Reason = r.FinishInfo.Reason switch { FinishReason.DiscardWin => "discardWin", FinishReason.StockExhausted => "stockExhausted", _ => "allPairs" },
        Winner = r.FinishInfo.Winner, DiscardedRealJoker = r.FinishInfo.DiscardedRealJoker,
        WinnerOpening = Name(r.FinishInfo.WinnerOpening), WinnerOpenedThisTurn = r.FinishInfo.WinnerOpenedThisTurn,
        Scores = [.. r.Scores.Select(s => new ScoreDto { HasScore = s.HasScore, N = s.N, J = s.J, P = s.P, B = s.B, M = s.M, Total = s.Total })],
    };

    public static string Name(OpeningKind o) => o switch { OpeningKind.Series => "series", OpeningKind.Pairs => "pairs", _ => "none" };
    private static string Name(TileColor c) => c switch { TileColor.Red => "red", TileColor.Yellow => "yellow", TileColor.Blue => "blue", _ => "black" };
    private static string Name(RoundPhase p) => p switch
    {
        RoundPhase.AwaitingDraw => "awaitingDraw", RoundPhase.PlanningTurn => "planningTurn", RoundPhase.Finished => "finished", _ => "cancelled",
    };
}
