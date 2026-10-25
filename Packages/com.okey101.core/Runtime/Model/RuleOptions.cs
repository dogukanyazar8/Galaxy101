namespace Okey101.Core.Model
{
    /// <summary>
    /// Table rules a room can switch off (G101-Lobi "Kural özelleştirme"): the +101 for discarding the okey, the +101 for
    /// discarding a tile that fits a meld on the table (işlek taş), and işleme itself — laying tiles onto melds already on
    /// the table (without it nothing is işlek, so neither is its penalty). All on by default.
    /// Katlamalı (<see cref="Doubling"/>, docs/rules/101-v1.md): within a hand each later opener must beat the previous
    /// opener's series total or pair count; in pairs mode <see cref="PartnerOpeningsRaise"/> off leaves the partner's
    /// (the opposite seat's) openings out of the bar.
    /// </summary>
    public sealed class RuleOptions
    {
        public bool OkeyDiscardPenalty { get; }
        public bool PlayableDiscardPenalty { get; }
        public bool Processing { get; }
        public bool Doubling { get; }
        public bool PartnerOpeningsRaise { get; }
        public RuleOptions(bool okeyDiscardPenalty, bool playableDiscardPenalty, bool processing = true, bool doubling = false, bool partnerOpeningsRaise = true)
        {
            OkeyDiscardPenalty = okeyDiscardPenalty; PlayableDiscardPenalty = playableDiscardPenalty; Processing = processing;
            Doubling = doubling; PartnerOpeningsRaise = partnerOpeningsRaise;
        }
        public static readonly RuleOptions Standard = new RuleOptions(true, true);
        public bool IsStandard => OkeyDiscardPenalty && PlayableDiscardPenalty && Processing && !Doubling;
    }
}
