using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    public sealed class MockEconomyService : IEconomyService
    {
        private readonly MenuModel model;
        private readonly JObject content;
        public MockEconomyService(MenuModel model, JObject content) { this.model = model; this.content = content; }
        public JObject Balance => new JObject { ["chips"] = model.Chips, ["gems"] = model.Gems };
        public JArray GetOffers(int tab) => tab >= 0 && tab < 3 ? (JArray)content["offers"][tab].DeepClone() : new JArray();
        public JObject GetDeal() => (JObject)content["deal"].DeepClone();
        public System.Threading.Tasks.Task<string> PurchaseAsync(string id) => System.Threading.Tasks.Task.FromResult(Purchase(id) ? null : "insufficient_funds");
        public bool Purchase(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            long chips = model.Chips, gems = model.Gems;
            string skin = null;
            if (id == "deal")
            {
                var deal = GetDeal();
                if (chips > long.MaxValue - (long)deal["chips"] || gems > long.MaxValue - (long)deal["gems"]) return false;
                chips += (long)deal["chips"]; gems += (long)deal["gems"];
            }
            else
            {
                var offer = ((JArray)content["offers"]).OfType<JArray>().SelectMany(x => x.OfType<JObject>()).FirstOrDefault(x => (string)x["id"] == id);
                if (offer == null) return false;
                switch ((string)offer["kind"])
                {
                    // Amounts displayed in the reference are the delivered totals; bonus labels are descriptive.
                    case "chips": if (chips > long.MaxValue - (long)offer["amount"]) return false; chips += (long)offer["amount"]; break;
                    case "gems": if (gems > long.MaxValue - (long)offer["amount"]) return false; gems += (long)offer["amount"]; break;
                    case "skin":
                        long price = (long)offer["price"];
                        if (gems < price || model.OwnedSkins.Contains(id)) return false;
                        gems -= price; skin = id; break;
                    default: return false;
                }
            }
            model.Chips = chips; model.Gems = gems;
            if (skin != null) model.OwnedSkins.Add(skin);
            model.NotifyChanged();
            return true;
        }
    }
}
