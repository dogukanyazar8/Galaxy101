using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class LeaderboardController
    {
        public string[] Tabs(MenuView v) => new[] { v.L("Haftalık", "Weekly"), v.L("Sezon", "Season"), v.L("Arkadaşlar", "Friends") };

        public VisualElement Build(MenuView v)
        {
            var root = PanelUi.Shell(v, "Leaderboard");
            root.style.alignItems = Align.Stretch;
            var data = v.Services.Leaderboard.Get(new[] { "weekly", "season", "friends" }[v.Tab("leader")]);
            var podium = root.Q<VisualElement>("podium");
            podium.style.width = 370; podium.style.alignSelf = Align.Stretch; podium.style.flexShrink = 0;
            podium.style.flexDirection = FlexDirection.Row; podium.style.alignItems = Align.FlexEnd; podium.style.justifyContent = Justify.Center;
            podium.style.marginRight = 18;
            PanelUi.Pad(podium, 0, 18);
            podium.style.paddingTop = 20;
            podium.style.overflow = Overflow.Hidden;
            v.Decorate(podium, "radial-gradient(ellipse at 50% 35%,var(--pnH),var(--sd) 72%)", 22);
            var winners = (JArray)data["podium"]; int slot = 0;
            foreach (var winner in new[] { 1, 0, 2 })
            {
                int k = slot++;
                var player = (JObject)winners[winner];
                var rank = (int)player["rank"];
                var top = rank == 1 ? "#FFE58A" : rank == 2 ? "#EEF2F8" : "#F4C08C";
                var bottom = rank == 1 ? "#E8A317" : rank == 2 ? "#A9B4C6" : "#B8703A";
                var column = v.Column(); column.style.width = 104; column.style.alignItems = Align.Center;
                if (podium.childCount > 0) column.style.marginLeft = 12;
                if (rank == 1) { var crown = v.Icon("crown", 34, "#FFD964"); crown.style.height = 24; crown.style.marginBottom = 6; column.Add(crown); }
                var avatar = PanelUi.Person(v, player, rank == 1 ? 66 : 54);
                avatar.style.borderTopWidth = avatar.style.borderBottomWidth = avatar.style.borderLeftWidth = avatar.style.borderRightWidth = 3;
                avatar.style.borderTopColor = avatar.style.borderBottomColor = avatar.style.borderLeftColor = avatar.style.borderRightColor = v.C(top);
                GalaxyFilters.Shadow(avatar, 0, 0, 11, v.C(rank == 1 ? "rgba(255,201,74,.6)" : rank == 2 ? "rgba(220,230,245,.45)" : "rgba(240,170,110,.45)"));
                avatar.Q<Label>().name = "menu.p.ini[" + k + "]"; column.Add(avatar);
                var name = v.Text((string)player["name"], 15, true); name.name = "menu.p.name[" + k + "]"; name.style.marginTop = 6; column.Add(name);
                var marker = v.Box(); marker.style.width = marker.style.height = 9; marker.style.rotate = new UnityEngine.UIElements.Rotate(45); marker.style.marginRight = 5;
                v.Decorate(marker, LeagueColor((string)player["league"]), 2);
                var score = v.Row(marker, v.Text(v.N((int)player["points"]), 13, false, "mt")); score.style.marginTop = 6; score.style.marginBottom = 10; column.Add(score);
                var step = v.Box(); step.style.width = 104; step.style.height = rank == 1 ? 150 : rank == 2 ? 112 : 86;
                step.style.alignItems = Align.Center; step.style.paddingTop = 10;
                v.Decorate(step, "linear-gradient(" + top + "," + bottom + ")", 14);
                step.style.borderBottomLeftRadius = step.style.borderBottomRightRadius = 0;
                var number = v.Text(rank.ToString(), 42, true, "rgba(0,0,0,.32)"); number.name = "menu.p.rank[" + k + "]"; number.style.height = 42; number.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter; // 700 42px/1
                step.Add(number); column.Add(step); podium.Add(column);
            }
            var table = root.Q<VisualElement>("table");
            var heading = v.Row(PanelUi.Caption(v, v.L("SIRA", "RANK"), 11, 1.2f), PanelUi.Caption(v, v.L("OYUNCU", "PLAYER"), 11, 1.2f), PanelUi.Caption(v, v.L("LİG", "LEAGUE"), 11, 1.2f), PanelUi.Caption(v, v.L("PUAN", "POINTS"), 11, 1.2f));
            heading.AddToClassList("g-leader-header"); Columns(heading); for (var h = 0; h < 4; h++) heading[h].name = "menu.lb.h" + h; table.Add(heading);
            int index = 0; foreach (JObject player in (JArray)data["rows"]) table.Add(Row(v, player, false, index++));
            table.Add(PanelUi.Spacer(v)); table.Add(Row(v, (JObject)data["me"], true));
            return root;
        }

        /// <summary>v4 league cell: a 24px G101Emblem, gap 6, 700 13px body text in the league colour (lcol).</summary>
        private static VisualElement LeagueCell(MenuView v, string code, string text, string color)
        {
            var emblem = v.Emblem(MenuView.RankIndex(code), 24); emblem.style.marginRight = 6;
            var row = v.Row(emblem, v.Text(text, 13, false, color)); row.style.alignItems = Align.Center; return row;
        }

        private static VisualElement Row(MenuView v, JObject player, bool self = false, int index = 0)
        {
            var face = PanelUi.Person(v, player, self ? 34 : 32); var ini = face.Q<Label>(); ini.style.fontSize = 12; ini.name = self ? "menu.lb.me.ini" : "menu.r.ini[" + index + "]";
            var who = v.Text((string)player["name"], 15, true); who.name = self ? "menu.lb.me.name" : "menu.r.name[" + index + "]";
            var name = v.Row(face, who); PanelUi.Gap(name, 10);
            var league = (string)player["league"];
            var translated = v.Model.Content["leaderboard"]?["leagues"]?[v.Loc.Language]?[league]?.ToString() ?? league;
            var rank = v.Text((int)player["rank"] > 0 ? v.N((int)player["rank"]) : "—", 16, true, self ? "a1" : "mt"); rank.name = self ? "menu.lb.me.rank" : "menu.r.rank[" + index + "]";
            var points = v.Text(v.N((int)player["points"]), 16, true); points.name = self ? "menu.lb.me.pts" : "menu.r.pts[" + index + "]";
            var row = v.Row(rank, name, LeagueCell(v, league, translated, LeagueColor(league)), points);
            row.AddToClassList(self ? "g-leader-self" : "g-leader-row");
            if ((bool?)player["me"] == true && !self) row.AddToClassList("g-panel-selected");
            Columns(row);
            if (self)
            {
                v.Decorate(row, "linear-gradient(90deg,var(--pnH),var(--sd))", 16);
                GalaxyFilters.Shadow(row, 0, 0, 10, v.C("gl"));
                var ring = v.Box(); ring.pickingMode = PickingMode.Ignore; ring.style.position = Position.Absolute;
                ring.style.left = ring.style.right = ring.style.top = ring.style.bottom = 0;
                ring.style.borderTopWidth = ring.style.borderRightWidth = ring.style.borderBottomWidth = ring.style.borderLeftWidth = 2;
                ring.style.borderTopColor = ring.style.borderRightColor = ring.style.borderBottomColor = ring.style.borderLeftColor = v.C("a1");
                ring.style.borderTopLeftRadius = ring.style.borderTopRightRadius = ring.style.borderBottomLeftRadius = ring.style.borderBottomRightRadius = 16;
                row.Add(ring);
            }
            return row;
        }

        private static void Columns(VisualElement row)
        {
            row[0].style.width = 48; row[0].style.flexShrink = 0;
            PanelUi.Grow(row[1]);
            row[2].style.width = 120; row[2].style.flexShrink = 0;
            row[3].style.width = 90; row[3].style.flexShrink = 0;
            row[3].style.unityTextAlign = UnityEngine.TextAnchor.MiddleRight;
        }

        /// <summary>
        /// lcol: Elmas #7FE6FF, Altın #FFD45C (G101-Menu panelVals); the leagues the sample never shows take their
        /// g101-rank.js name colour (ranks.json leagues[].text).
        /// </summary>
        private static string LeagueColor(string code) => string.IsNullOrEmpty(code) ? "#FFD45C" : code[0] == 'd' ? "#7FE6FF" : code[0] == 'g' && code != "gx" ? "#FFD45C" : MenuView.RankInk(MenuView.RankIndex(code));
    }
}
