using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class ShopController
    {
        public string[] Tabs(MenuView v) => new[] { v.L("Çip", "Chips"), v.L("Elmas", "Gems"), v.L("Görünüm", "Cosmetics") };

        public VisualElement Build(MenuView v)
        {
            var root = PanelUi.Shell(v, "Shop");
            var deal = root.Q<VisualElement>("deal");
            v.Decorate(deal, "linear-gradient(110deg,var(--a2),var(--a1))", 22, 5, "aS");
            deal.style.paddingTop = deal.style.paddingBottom = 0;
            ShopArtwork.Shine(v, deal);
            var art = ShopArtwork.Deal(v);
            art.style.marginRight = 18;
            deal.Add(art);
            var titles = v.Column(v.Text(v.L("HAFTA SONU FIRSATI · %50", "WEEKEND DEAL · 50% OFF"), 11, false, "aT"), v.Text(v.L("1.000.000 çip + 50 elmas", "1,000,000 chips + 50 gems"), 26, true, "aT"));
            titles[0].style.unityFont = v.Font("Baloo2-800"); MenuView.Tracking(titles[0], 1.5f, 11); titles[0].style.opacity = .75f;
            PanelUi.Grow(titles);
            deal.Add(titles);
            var countdown = v.Text("", 15, true, "aT");
            var timer = v.Row(v.Icon("clock", 16, "aT"), countdown);
            timer[0].style.marginRight = 7;
            PanelUi.Pad(timer, 8, 12);
            v.Decorate(timer, "rgba(0,0,0,.16)", 12);
            timer.style.marginRight = 18;
            deal.Add(timer);
            System.Action tick = () => countdown.text = (v.Services.Rewards.ResetTime - System.DateTime.Now).ToString(@"hh\:mm\:ss");
            tick(); countdown.schedule.Execute(tick).Every(1000);
            var data = v.Services.Economy.GetDeal();
            var old = v.Text(PanelUi.Money(v, (decimal)data["oldPrice"]), 13, false, "aT");
            old.style.opacity = .6f;
            old.style.whiteSpace = WhiteSpace.NoWrap;
            var oldPrice = v.Box(); oldPrice.style.alignSelf = Align.FlexEnd; oldPrice.Add(old);
            var line = v.Box();
            line.style.position = Position.Absolute; line.style.left = 0; line.style.right = 0; line.style.top = 8; line.style.height = 1; line.style.backgroundColor = v.C("aT");
            oldPrice.Add(line);
            var price = v.Column(oldPrice, v.Text(PanelUi.Money(v, (decimal)data["price"]), 26, true, "aT"));
            price.style.alignItems = Align.FlexEnd;
            price.style.marginRight = 18;
            deal.Add(price);
            var buy = PanelUi.Action(v, v.L("Satın al", "Buy"), () => Purchase(v, (string)data["id"]));
            buy.style.height = StyleKeyword.Auto; PanelUi.Pad(buy, 13, 24); buy.style.fontSize = 17; buy.style.unityFont = v.Font("Montserrat-700");
            v.Decorate(buy, "aT", 14, 4, "rgba(0,0,0,.3)"); buy.style.color = v.C("a1");
            deal.Add(buy);

            var tab = v.Tab("shop");
            var products = root.Q<VisualElement>("products");
            products.style.alignItems = Align.Stretch;
            var offers = v.Services.Economy.GetOffers(tab);
            for (var index = 0; index < offers.Count; index++)
            {
                var offer = (JObject)offers[index];
                var card = v.Box("g-shop-product");
                ShopArtwork.ProductRing(v, card, (bool)offer["pop"]);
                card.name = "offer-" + (string)offer["id"];
                if (index < offers.Count - 1) card.style.marginRight = 14;
                if ((bool)offer["pop"])
                {
                    card.AddToClassList("popular");
                    var badge = v.Text(v.L("EN POPÜLER", "MOST POPULAR"), 10, false, "aT");
                    badge.AddToClassList("g-shop-popular"); ShopArtwork.Popular(v, badge); card.Add(badge);
                }
                var bonus = (int?)offer["bonus"] ?? 0;
                if (bonus > 0)
                {
                    var badge = v.Text("+" + v.P(bonus), 11, false, "#fff");
                    PanelUi.Pad(badge, 3, 8); badge.style.whiteSpace = WhiteSpace.NoWrap;
                    badge.AddToClassList("g-shop-bonus"); badge.style.top = badge.style.right = 14; badge.style.unityFont = v.Font("Baloo2-800");
                    badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 8; card.Add(badge);
                }
                var image = v.Box("g-shop-art");
                if (tab == 0) image.Add(ShopArtwork.Coins(v, (float)offer["scale"]));
                else if (tab == 1) image.Add(ShopArtwork.Gems(v, (float)offer["scale"]));
                else image.Add(ShopArtwork.Tile(v, offer));
                image.style.marginBottom = 4;
                card.Add(image);
                var amount = v.Text(tab == 2 ? v.L((string)offer["tr"], (string)offer["en"]) : v.N((long)offer["amount"]), 22, true); amount.style.marginBottom = 4; card.Add(amount);
                var sub = v.Text(tab == 0 ? v.L("çip", "chips") : tab == 1 ? v.L("elmas", "gems") : v.L("Taş görünümü", "Tile skin"), 12, false, "mt"); sub.style.marginBottom = 4; card.Add(sub);
                var spacer = PanelUi.Spacer(v); spacer.style.marginBottom = 4; card.Add(spacer);
                var id = (string)offer["id"];
                var button = v.Button(tab == 2 ? v.N((long)(decimal)offer["price"]) : PanelUi.Money(v, (decimal)offer["price"]), () => Purchase(v, id));
                button.name = "purchase-" + id;
                button.AddToClassList("g-shop-price");
                button.style.flexDirection = FlexDirection.Row; button.style.fontSize = 17; button.style.unityFont = v.Font("Montserrat-700");
                button.style.height = StyleKeyword.Auto; button.style.marginTop = 0; PanelUi.Pad(button, 11, 0);
                if (tab == 2)
                {
                    var gem = ShopArtwork.PriceGem(v); gem.style.marginRight = 7; button.Insert(0, gem);
                }
                card.Add(button); products.Add(card);
            }
            return root;
        }

        private static void Purchase(MenuView v, string id)
        {
            if (v.Model.OwnedSkins.Contains(id)) { v.Toast(v.L("Sahipsin", "Owned")); return; }
            if (v.Services.Economy.Purchase(id)) v.Toast(v.L("İşlem tamam", "Done"));
            else v.Toast(v.L("Yetersiz elmas", "Not enough gems"));
        }

    }
}
