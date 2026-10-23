namespace Okey101.Core.Model
{
    /// <summary>
    /// Table rules a room can switch off (G101-Lobi "Kural özelleştirme"): the +101 for discarding the okey and the
    /// +101 for discarding a tile that fits a meld on the table (işlek taş). Both on by default.
    /// </summary>
    public sealed class RuleOptions
    {
        public bool OkeyDiscardPenalty { get; }
        public bool PlayableDiscardPenalty { get; }
        public RuleOptions(bool okeyDiscardPenalty, bool playableDiscardPenalty) { OkeyDiscardPenalty = okeyDiscardPenalty; PlayableDiscardPenalty = playableDiscardPenalty; }
        public static readonly RuleOptions Standard = new RuleOptions(true, true);
        public bool IsStandard => OkeyDiscardPenalty && PlayableDiscardPenalty;
    }
}
