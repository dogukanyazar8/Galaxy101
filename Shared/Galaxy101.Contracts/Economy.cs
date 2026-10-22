using System;
using System.Collections.Generic;

namespace Galaxy101.Contracts
{
    public sealed class WalletResponse
    {
        public long Chips { get; set; }
        public long Gems { get; set; }
        public List<string> OwnedItems { get; set; } = new List<string>();
    }

    /// <summary>
    /// <c>kind</c>: "chips" / "gems" (real-money store products), "deal" (store bundle), "skin" (paid with gems).
    /// Real-money offers carry <c>storeProductId</c>; the displayed price comes from the platform store.
    /// </summary>
    public sealed class OfferResponse
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public long Chips { get; set; }
        public long Gems { get; set; }
        public int BonusPercent { get; set; }
        public long GemPrice { get; set; }
        public string StoreProductId { get; set; }
        public decimal ReferencePrice { get; set; }
        public decimal? ReferenceOldPrice { get; set; }
        public bool Popular { get; set; }
        public bool Owned { get; set; }
    }

    public sealed class CatalogResponse
    {
        public List<OfferResponse> Chips { get; set; } = new List<OfferResponse>();
        public List<OfferResponse> Gems { get; set; } = new List<OfferResponse>();
        public List<OfferResponse> Skins { get; set; } = new List<OfferResponse>();
        public OfferResponse Deal { get; set; }
    }

    public sealed class GemPurchaseRequest
    {
        public string OfferId { get; set; }
    }

    /// <summary>
    /// <c>platform</c>: "google" (receipt = purchase token) or "apple" (receipt = StoreKit 2 signed transaction JWS).
    /// </summary>
    public sealed class StorePurchaseRequest
    {
        public string Platform { get; set; }
        public string ProductId { get; set; }
        public string Receipt { get; set; }
    }

    public sealed class PurchaseResponse
    {
        public WalletResponse Wallet { get; set; }
        public string OfferId { get; set; }
        /// <summary>True when the receipt had already been redeemed by this account; nothing new was granted.</summary>
        public bool AlreadyProcessed { get; set; }
    }

    public sealed class StreakDayResponse
    {
        public int Day { get; set; }
        public long Chips { get; set; }
        public bool Claimed { get; set; }
        public bool Today { get; set; }
    }

    public sealed class QuestResponse
    {
        public string Id { get; set; }
        public int Progress { get; set; }
        public int Target { get; set; }
        public long Chips { get; set; }
        public long Gems { get; set; }
        public bool Complete { get; set; }
        public bool Claimed { get; set; }
    }

    public sealed class DailyResponse
    {
        public List<StreakDayResponse> Streak { get; set; } = new List<StreakDayResponse>();
        public bool CanClaimStreak { get; set; }
        public List<QuestResponse> Quests { get; set; } = new List<QuestResponse>();
        public DateTimeOffset NextReset { get; set; }
        public WalletResponse Wallet { get; set; }
    }
}
