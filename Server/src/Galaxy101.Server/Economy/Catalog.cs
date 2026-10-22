using System.Text.Json;

namespace Galaxy101.Server.Economy;

public sealed record StoreOffer(string Id, long Chips, long Gems, int BonusPercent, string StoreProductId, decimal ReferencePrice, decimal? ReferenceOldPrice, bool Popular);
public sealed record SkinOffer(string Id, long GemPrice, bool Popular);
public sealed record QuestDefinition(string Id, int Target, long Chips, long Gems);
public sealed record GiftDefinition(string Id, long Chips);

/// <summary>
/// Server-owned prices and rewards (embedded <c>catalog.json</c>, generated from the menu reference content).
/// The client renders what it gets from here; it never decides an amount.
/// </summary>
public sealed class Catalog
{
    public IReadOnlyList<StoreOffer> Chips { get; }
    public IReadOnlyList<StoreOffer> Gems { get; }
    public IReadOnlyList<SkinOffer> Skins { get; }
    public StoreOffer Deal { get; }
    public IReadOnlyList<long> Streak { get; }
    public IReadOnlyList<QuestDefinition> Quests { get; }
    public IReadOnlyList<GiftDefinition> Gifts { get; }
    public IReadOnlyList<string> Emotes { get; }

    private sealed record Document(List<StoreOffer> Chips, List<StoreOffer> Gems, List<SkinOffer> Skins, StoreOffer Deal,
        List<long> Streak, List<QuestDefinition> Quests, List<GiftDefinition> Gifts, List<string> Emotes);

    public Catalog()
    {
        using var stream = typeof(Catalog).Assembly.GetManifestResourceStream("catalog.json") ?? throw new InvalidOperationException("catalog.json is not embedded.");
        var d = JsonSerializer.Deserialize<Document>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new InvalidOperationException("Empty catalog.");
        (Chips, Gems, Skins, Deal, Streak, Quests, Gifts, Emotes) = (d.Chips, d.Gems, d.Skins, d.Deal, d.Streak, d.Quests, d.Gifts, d.Emotes);
        if (Streak.Count != 7 || Quests.Count > 31) throw new InvalidOperationException("Catalog: seven streak days and at most 31 quests are required.");
    }

    /// <summary>Real-money offer by store product id (chips, gems or the deal).</summary>
    public StoreOffer? ByProduct(string productId) =>
        Chips.Concat(Gems).Append(Deal).FirstOrDefault(x => x.StoreProductId == productId);
}
