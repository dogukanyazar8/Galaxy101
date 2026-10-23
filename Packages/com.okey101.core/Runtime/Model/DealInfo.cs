using System;

namespace Okey101.Core.Model
{
    /// <summary>
    /// Public facts of a deal (spec/21) every seat may see: who dealt, the two dice, which stack showed the indicator (and
    /// from which height, 0 = bottom), whether a fake okey came up first, and where the extra tile went. Tile identities of
    /// the stacks stay hidden; each seat learns its own tiles from its hand.
    /// </summary>
    public sealed class DealInfo
    {
        public int Dealer { get; }
        public int DieA { get; }
        public int DieB { get; }
        public int IndicatorStack { get; }
        public int IndicatorPosition { get; }
        public bool FakeOkeyShown { get; }
        public int ExtraStack { get; }
        public int StartingPlayer => (Dealer + 1) % 4;

        public DealInfo(int dealer, int dieA, int dieB, int indicatorStack, int indicatorPosition, bool fakeOkeyShown, int extraStack)
        {
            Immutable.Player(dealer, nameof(dealer));
            if (dieA < 1 || dieA > 6) throw new ArgumentOutOfRangeException(nameof(dieA));
            if (dieB < 1 || dieB > 6) throw new ArgumentOutOfRangeException(nameof(dieB));
            if (indicatorStack < 0 || indicatorStack > 14 || extraStack < 0 || extraStack > 14) throw new ArgumentOutOfRangeException(nameof(indicatorStack));
            if (indicatorPosition < 0 || indicatorPosition > 6) throw new ArgumentOutOfRangeException(nameof(indicatorPosition));
            Dealer = dealer; DieA = dieA; DieB = dieB; IndicatorStack = indicatorStack; IndicatorPosition = indicatorPosition; FakeOkeyShown = fakeOkeyShown; ExtraStack = extraStack;
        }
    }
}
