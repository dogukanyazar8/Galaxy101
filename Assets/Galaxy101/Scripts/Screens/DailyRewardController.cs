using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class DailyRewardController
    {
        public VisualElement Build(MenuView v)
        {
            var root = PanelUi.Shell(v, "DailyReward");
            var days = root.Q<VisualElement>("days"); days.style.marginBottom = 14;
            foreach (JObject day in v.Services.Rewards.GetStreak())
            {
                var index = (int)day["day"];
                var claimed = (bool)day["claimed"];
                // The server keeps Today true after collection. In the prototype, the green card
                // and its button represent an available reward, not an already claimed day.
                var today = (bool)day["today"] && !claimed;
                var card = v.Box("g-day"); card.name = "streak-day-" + index;
                // CSS grid fractions apply to the whole card. A padded Yoga item retains its 16 px padding at basis 0;
                // put the fractions on an unpadded slot so the seventh card receives 1.5 outer widths too.
                var slot = v.Box(); slot.style.flexDirection = FlexDirection.Row;
                slot.style.flexBasis = 0; slot.style.flexGrow = index == 7 ? 1.5f : 1; slot.style.minWidth = 0;
                card.style.flexBasis = 0; card.style.flexGrow = 1; card.style.minWidth = 0;
                if (index < 7)
                {
                    slot.style.marginRight = 10;
                    if (!today) card.Add(PanelUi.Ring(v, 1, "rgba(255,255,255,.07)", 18));
                }
                else
                {
                    // big: inset 0 0 0 2px var(--a1), 0 0 20px var(--gl) — an overlay ring, no layout (a border moved the label and amount 2 px).
                    v.Decorate(card, "linear-gradient(160deg,var(--pnH),var(--sd))", 18);
                    card.Add(PanelUi.Ring(v, 2, "a1", 18)); GalaxyFilters.Shadow(card, 0, 0, 10, v.C("gl"));
                }
                if (index == 6) card.style.opacity = .6f;
                if (today)
                {
                    v.Decorate(card, "linear-gradient(160deg,#4FDB80,#1FA35A)", 18, 5, "#146B3C"); GalaxyFilters.Shadow(card, 0, 0, 13, v.C("rgba(60,203,110,.55)")); // 0 5px 0 #146B3C, 0 0 26px
                    PanelUi.Bob(v, card);
                }
                card.Add(PanelUi.Caption(v, v.L(index + ". Gün", "Day " + index), 11, .5f, today ? "#fff" : "mt"));
                card.Add(index == 7 ? PanelUi.Chest(v) : PanelUi.Coin(v, 34));
                if (today)
                {
                    var claim = v.Button(v.L("Topla", "Collect"), () => v.Services.Rewards.ClaimDay(), "secondary");
                    claim.name = "claim-day"; claim.style.alignSelf = Align.Stretch; claim.style.height = 30; claim.style.fontSize = 14;
                    claim.style.paddingTop = claim.style.paddingBottom = 6; claim.style.paddingLeft = claim.style.paddingRight = 0;
                    v.Decorate(claim, "#fff", 10, 3, "#0E5A31"); claim.style.color = v.C("#146B3C"); card.Add(claim);
                }
                else card.Add(MenuView.CssLine(v.Text(v.N((int)day["chips"]), 15, true, index == 7 ? "a1" : "#F4F1E8"), 15)); // 700 15px/1
                if (claimed)
                {
                    var cover = v.Box(); cover.name = "day-claimed-cover"; cover.style.position = Position.Absolute;
                    cover.style.left = cover.style.right = cover.style.top = cover.style.bottom = 0;
                    cover.style.alignItems = Align.Center; cover.style.justifyContent = Justify.Center;
                    v.Decorate(cover, "rgba(0,0,0,.38)", 18);
                    var check = PanelUi.Circle(v, 36, "#3CCB6E"); check.name = "day-claimed-check";
                    GalaxyFilters.HardShadow(check, 3, v.C("#22914A"));
                    check.Add(v.Icon("tick", 18, "#fff")); cover.Add(check); card.Add(cover);
                }
                slot.Add(card); days.Add(slot);
            }
            var quests = v.Services.Rewards.GetQuests();
            var heading = root.Q<VisualElement>("quests-heading"); heading.style.marginTop = 2; heading.style.marginBottom = 14;
            var count = quests.OfType<JObject>().Count(q => (int)q["cur"] >= (int)q["max"]);
            heading.Add(MenuView.CssLine(v.Text(v.L("Günlük görevler", "Daily quests"), 20, true), 24)); // 700 20px/1.2
            // doneCount: 800 12px Baloo 2, padding 4px 10px, radius 8 — a 27.2 px pill that sets the heading's height.
            var badge = PanelUi.Badge(v, v.L(count + "/" + quests.Count + " tamamlandı", count + "/" + quests.Count + " completed"), "dp", "a1", 12);
            PanelUi.Pad(badge, 4, 10); v.Decorate(badge, "dp", 8); var badgeText = badge.Q<Label>(); badgeText.style.unityFont = v.Font("Baloo2-800"); MenuView.CssLine(badgeText, 19.2f);
            badge.style.marginLeft = 12; heading.Add(badge); heading.Add(PanelUi.Spacer(v));
            var reset = v.Text("", 13, false, "mt"); var clock = v.Icon("clock", 15, "mt"); clock.style.marginRight = 6; heading.Add(clock); heading.Add(reset);
            Action tick = () => { var left = v.Services.Rewards.ResetTime - DateTime.Now; reset.text = v.L("Yenilenmeye " + Math.Max(0, (int)left.TotalHours) + " sa " + left.Minutes + " dk", "Resets in " + Math.Max(0, (int)left.TotalHours) + "h " + left.Minutes + "m"); };
            tick(); reset.schedule.Execute(tick).Every(1000);
            // overflow-y auto with a stable gutter and padding-right 4 between the rows and the bar.
            var list = v.Scroll(); list.contentContainer.style.paddingRight = 4; root.Q<VisualElement>("quests").Add(list);
            for (var index = 0; index < quests.Count; index++)
            {
                var q = (JObject)quests[index]; var i = index;
                var complete = (int)q["cur"] >= (int)q["max"];
                var claimed = (bool)q["claimed"];
                var row = v.Row(); row.AddToClassList("g-quest"); row.name = "quest-" + index;
                if (claimed) row.style.opacity = .6f;
                var number = PanelUi.Circle(v, 36, complete ? "#3CCB6E" : "dp", complete ? null : (index + 1).ToString(), "mt");
                if (complete) number.Add(v.Icon("tick", 17, "#fff")); number.style.marginRight = 14; row.Add(number);
                var progressText = v.Text(q["cur"] + "/" + q["max"], 13, false, complete ? "#8EF0B0" : "a1"); progressText.style.unityFont = v.Font("Baloo2-800");
                var text = v.Row(v.Text(v.L((string)q["tr"], (string)q["en"]), 15, true), PanelUi.Spacer(v), progressText);
                var progress = v.Progress((float)q["cur"] / (float)q["max"], complete ? "#3CCB6E" : null); progress.style.height = 8; progress.style.marginTop = 6;
                var details = v.Column(text, progress); PanelUi.Grow(details); row.Add(details);
                // Reference reward: 18px radial coin, gap 6, 700 15px; optional 12px gem (radius 3) after a 6px extra margin.
                var amount = v.Text(v.N((int)q["rw"]), 15, true);
                var reward = v.Row(PanelUi.Coin(v, 18), amount);
                reward.style.width = 150; reward.style.justifyContent = Justify.FlexEnd; reward.style.marginLeft = 14; reward.style.marginRight = 14; PanelUi.Gap(reward, 6);
                if ((int?)q["gem"] > 0) { amount.style.marginRight = 6; var gem = v.GemIcon(12); gem.style.marginLeft = 6; gem.style.marginRight = 6; reward.Add(gem); reward.Add(v.Text(q["gem"].ToString(), 15, true)); }
                row.Add(reward);
                VisualElement state;
                if (complete && !claimed)
                {
                    state = PanelUi.CssButton(v, v.L("Al", "Claim"), () => v.Services.Rewards.ClaimQuest(i), "success", 9, 0, 15, 12, 3); state.name = "claim-quest-" + i; state.style.width = 118;
                    GalaxyFilters.Shadow(state, 0, 0, 8, v.C("rgba(60,203,110,.45)"));
                }
                else
                {
                    if (claimed)
                    {
                        var claimedIcon = v.Icon("tick", 15, "#8EF0B0"); claimedIcon.style.marginRight = 6;
                        state = v.Row(claimedIcon, v.Text(v.L("Alındı", "Claimed"), 14, true, "#8EF0B0")); state.style.justifyContent = Justify.Center;
                    }
                    else
                    {
                        state = v.Box(); state.style.alignItems = Align.Center; v.Decorate(state, "dp", 12); state.Add(v.Text(v.L("Devam ediyor", "In progress"), 13, false, "mt"));
                    }
                    state.style.width = 118; state.style.flexShrink = 0; state.style.paddingTop = state.style.paddingBottom = 9;
                }
                row.Add(state); list.Add(row);
            }
            return root;
        }
    }
}
