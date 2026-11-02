using System.Runtime.CompilerServices;
using Galaxy101.Contracts;

namespace Galaxy101
{
    /// <summary>Source opener display flag, independent of the protocol's combined penalty setting.</summary>
    internal static class LobbyRulePresentation
    {
        private sealed class OpeningPenalty { public bool Enabled; }
        private static readonly ConditionalWeakTable<TableSummary, OpeningPenalty> openingPenalties = new ConditionalWeakTable<TableSummary, OpeningPenalty>();

        // G101-Lobi: an absent opener flag is enabled; the V4 adapter explicitly supplies its authoritative umbrella flag.
        public static bool OpeningTilePenalty(TableSummary table) => !openingPenalties.TryGetValue(table, out var state) || state.Enabled;

        public static TableSummary WithOpeningTilePenalty(TableSummary table, bool enabled)
        {
            openingPenalties.GetValue(table, _ => new OpeningPenalty()).Enabled = enabled;
            return table;
        }
    }
}
