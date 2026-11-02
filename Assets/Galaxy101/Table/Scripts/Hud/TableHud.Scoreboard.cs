using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    public sealed partial class TableHud
    {
        public sealed class ScoreHand
        {
            public int Number, Finisher = -1;
            public readonly int[] Points = new int[4], Totals = new int[4];
            public readonly bool?[] Opened = new bool?[4];
        }

        private int scoreTab = -1, scoreboardBuiltHands, mountedScoreTab = int.MinValue;
        private ScoreHand[] scoreboardBuiltHistory;
        private float scoreRowsStartedAt;
        private float resultScoreStartedAt = float.NaN;

        public void OpenScoreboard()
        {
            scoreTab = -1; mountedScoreTab = int.MinValue; Window = "scoreboard"; CardSeat = 0; RenderWindows(false);
        }

        private void BuildScoreboard()
        {
            var layer = Layer("table-scoreboard-layer"); layer.pickingMode = PickingMode.Position;
            layer.style.alignItems = Align.Center; layer.style.justifyContent = Justify.Center;
            var dim = Layer("table-scoreboard-backdrop"); dim.pickingMode = PickingMode.Position;
            dim.style.backgroundColor = new Color(0, 0, 0, .55f); GalaxyFilters.BackdropBlur(dim, 6); Tap(dim, CloseWindow); layer.Add(dim);
            var card = Box("table-scoreboard-card"); card.pickingMode = PickingMode.Position;
            card.style.width = 822; card.style.maxHeight = 682; card.style.flexShrink = 0; card.style.overflow = Overflow.Hidden;
            view.Decorate(card, "var(--pn)", 30, 10, "var(--pnS)"); Border(card, 1, new Color(1, 1, 1, .1f));
            GalaxyFilters.Shadow(card, 0, 40, 45, new Color(0, 0, 0, .6f));
            card.RegisterCallback<ClickEvent>(evt => evt.StopPropagation()); ModalIn(card, .35f); layer.Add(card); windowLayer.Add(layer);

            var header = Row(16); Pad(header, 24, 30, 16, 30); view.Decorate(header, "linear-gradient(180deg,rgba(255,255,255,.06),transparent)", 0);
            var emblem = Box(); emblem.style.width = emblem.style.height = 48; emblem.style.flexShrink = 0;
            emblem.style.alignItems = Align.Center; emblem.style.justifyContent = Justify.Center;
            view.Decorate(emblem, "linear-gradient(160deg,var(--a1),var(--a2))", 16, 4, "var(--aS)"); emblem.Add(view.Icon("table-scoreboard-title", 26, "var(--aT)")); header.Add(emblem);
            var titles = Box(); titles.style.flexGrow = 1;
            var caption = Text(L("PUAN TABLOSU", "SCOREBOARD"), "Baloo2-800", 11, "var(--mt)"); caption.name = "table.t.sbTitle"; MenuView.Tracking(caption, 2, 11); titles.Add(caption);
            var progress = Text(History.Count + " / " + Current.MatchHands + L(" el", " hands"), "Montserrat-700", 28, "#F4F1E8");
            progress.name = "table.sb.prog"; MenuView.CssLine(progress, 28 * 1.15f); progress.style.marginTop = 3; titles.Add(progress); header.Add(titles);
            var close = IconButton("table-scoreboard-close", 20, L("Kapat", "Close"), "table.closeSb"); Tap(close, CloseWindow);
            close.RegisterCallback<NavigationSubmitEvent>(evt => { CloseWindow(); evt.StopPropagation(); }); header.Add(close); card.Add(header);
            BuildScoreBody(card, null);
            var foot = Box(); foot.style.height = 24; foot.style.flexShrink = 0; card.Add(foot);
        }

        private IReadOnlyList<ScoreHand> History => Current.ScoreHistory ?? Array.Empty<ScoreHand>();
        private bool ScoreHistoryChanged
        {
            get
            {
                var history = History;
                if (scoreboardBuiltHistory == null || scoreboardBuiltHistory.Length != history.Count || scoreboardBuiltHands != Current.MatchHands) return true;
                for (int i = 0; i < history.Count; i++)
                {
                    var before = scoreboardBuiltHistory[i]; var now = history[i];
                    if (before.Number != now.Number || before.Finisher != now.Finisher) return true;
                    for (int seat = 0; seat < 4; seat++)
                        if (before.Points[seat] != now.Points[seat] || before.Totals[seat] != now.Totals[seat] || before.Opened[seat] != now.Opened[seat]) return true;
                }
                return false;
            }
        }

        private void RememberScoreHistory()
        {
            var history = History; scoreboardBuiltHands = Current.MatchHands;
            scoreboardBuiltHistory = new ScoreHand[history.Count];
            for (int i = 0; i < history.Count; i++)
            {
                var source = history[i]; var copy = new ScoreHand { Number = source.Number, Finisher = source.Finisher };
                Array.Copy(source.Points, copy.Points, 4); Array.Copy(source.Totals, copy.Totals, 4); Array.Copy(source.Opened, copy.Opened, 4);
                scoreboardBuiltHistory[i] = copy;
            }
        }
        private static string ScoreValue(int points) => (points > 0 ? "+" : "") + points.ToString(CultureInfo.InvariantCulture);
        private static string ScoreColor(int points) => points < 0 ? "#8EF0B0" : points >= 202 ? "#FF8A94" : "#F4F1E8";

        private void BuildScoreBody(VisualElement card, ResultInfo result)
        {
            var history = History; RememberScoreHistory();
            if (result != null && resultScoreStartedAt != result.StartedAt) { resultScoreStartedAt = result.StartedAt; scoreTab = Math.Max(0, history.Count - 1); mountedScoreTab = int.MinValue; }
            bool all = scoreTab < 0 || scoreTab >= history.Count;
            // A standalone result used by the preview/tests still has a real current-hand tab.
            bool resultOnly = result != null && history.Count == 0;
            if (resultOnly && scoreTab == 0) all = false;
            int branch = all ? -1 : scoreTab;
            if (mountedScoreTab != branch) { mountedScoreTab = branch; scoreRowsStartedAt = Time.realtimeSinceStartup; }
            int tabsCount = resultOnly ? 1 : history.Count;
            var tabsPad = Box(); Pad(tabsPad, 0, 30, 12, 30);
            var tabs = Row(4); tabs.style.alignSelf = Align.FlexStart; tabs.style.flexWrap = Wrap.Wrap; Pad(tabs, 4, 4, 4, 4); view.Decorate(tabs, "var(--dp)", 14);
            view.InsetRing(tabs, 14)("rest");
            for (int i = 0; i <= tabsCount; i++)
            {
                int index = i == tabsCount ? -1 : i; bool on = index < 0 ? all : !all && scoreTab == index;
                string text = index < 0 ? L("Tüm eller", "All hands") : L((resultOnly ? Current.Hand : history[index].Number) + ". El", "Hand " + (resultOnly ? Current.Hand : history[index].Number));
                var tab = view.Button(text, () => { scoreTab = index; if (result != null) Render(Current); else RenderWindows(true); }, "plain");
                tab.name = "table.sb.tab[" + i + "]"; tab.style.height = 32; Pad(tab, 0, 14, 0, 14); tab.style.unityFont = view.Font("Montserrat-800"); tab.style.fontSize = 13;
                tab.style.color = view.C(on ? "var(--aT)" : "var(--ic)"); tab.EnableInClassList("selected", on);
                view.Decorate(tab, on ? "linear-gradient(var(--a1),var(--a2))" : "transparent", 10); tabs.Add(tab);
            }
            tabsPad.Add(tabs); card.Add(tabsPad);
            var body = Box("table.sb.body"); body.style.height = 300; body.style.flexShrink = 0; body.style.overflow = Overflow.Hidden; card.Add(body);
            if (all) BuildScoreMatrix(body, result); else BuildScoreHand(body, resultOnly ? null : history[scoreTab], result);
        }

        private VisualElement ScoreRow(int seat, int rank, bool ring, float gap)
        {
            var row = Row(gap); Pad(row, 9, 14, 9, 14); row.style.marginTop = 6;
            view.Decorate(row, seat == 0 ? "var(--pnH)" : "var(--dp)", 16);
            if (ring) { var edge = Layer("score-row-ring"); Radius(edge, 16); Border(edge, 2, view.C("var(--a1)")); row.Add(edge); }
            return row;
        }

        private VisualElement ScorePlayer(int seat, int size, int fontSize)
        {
            var who = Row(10); who.style.flexGrow = 1; who.style.flexBasis = 0; who.style.minWidth = 0;
            var avatar = Box(); avatar.style.width = avatar.style.height = size; avatar.style.flexShrink = 0;
            avatar.style.alignItems = Align.Center; avatar.style.justifyContent = Justify.Center; view.Decorate(avatar, (string)Seat(seat)["color"], size / 2);
            avatar.Add(Text((string)Seat(seat)["ini"], "Montserrat-700", size == 32 ? 12 : 13, "#fff")); who.Add(avatar);
            var name = Text((string)Seat(seat)["name"], "Montserrat-700", fontSize, "#F4F1E8"); name.style.flexShrink = 1; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; who.Add(name);
            return who;
        }

        private Label ScoreCell(string text, float width, int size, string color, TextAnchor align = TextAnchor.MiddleRight)
        {
            var cell = Text(text, "Montserrat-700", size, color); cell.style.width = width; cell.style.flexShrink = 0; cell.style.unityTextAlign = align; return cell;
        }

        private void BuildScoreMatrix(VisualElement body, ResultInfo result)
        {
            int hands = Mathf.Clamp(Current.MatchHands, 1, 50); // All supported match lengths, with a finite UI budget for invalid snapshots.
            var history = History;
            // Source grid: 30px minmax(0,1fr) repeat(HN,minmax(42px,56px)) 74px, with 8px gaps.
            // The 820px card content loses 60px outer padding and 28px row padding. Overflow stays clipped.
            float available = 732 - 30 - 74 - (hands + 2) * 8;
            float cellWidth = Mathf.Clamp(available / hands, 42, 56), playerWidth = Mathf.Max(0, available - hands * cellWidth);
            var rows = Box(); Pad(rows, 0, 30, 0, 30); body.Add(rows);
            var headers = Row(8); Pad(headers, 0, 14, 0, 14); headers.Add(ScoreCell("", 30, 10, "var(--mt)"));
            var player = Caption(L("OYUNCU", "PLAYER")); player.name = "table.t.colPlayer#2"; player.style.width = playerWidth; player.style.minWidth = 0; player.style.flexShrink = 0; headers.Add(player);
            for (int i = 0; i < hands; i++) { var number = Caption((i + 1).ToString()); number.style.width = cellWidth; number.style.flexShrink = 0; number.style.unityTextAlign = TextAnchor.MiddleCenter; headers.Add(number); }
            var totalHead = Caption(L("TOPLAM", "TOTAL")); totalHead.style.width = 74; totalHead.style.flexShrink = 0; totalHead.style.unityTextAlign = TextAnchor.MiddleRight; totalHead.name = "table.t.sbTot"; headers.Add(totalHead); rows.Add(headers);
            int Total(int seat) => history.Count > 0 ? history[history.Count - 1].Totals[seat] : result?.Rows.FirstOrDefault(r => r.Seat == seat)?.Total ?? 0;
            var order = Enumerable.Range(0, 4).OrderBy(Total).ThenBy(k => k);
            int rank = 0;
            foreach (int seat in order)
            {
                var row = ScoreRow(seat, rank, rank == 0 && (history.Count > 0 || result != null), 8); row.name = "score-matrix-row-" + seat;
                row.Add(ScoreCell((rank + 1) + ".", 30, 18, rank == 0 ? "var(--a1)" : "var(--mt)", TextAnchor.MiddleLeft));
                var who = ScorePlayer(seat, 32, 15); who.style.width = playerWidth; who.style.flexGrow = 0; who.style.flexBasis = StyleKeyword.Auto; who.style.flexShrink = 0; row.Add(who);
                for (int hand = 0; hand < hands; hand++)
                {
                    var data = hand < history.Count ? history[hand] : null;
                    var cell = ScoreCell(data != null ? ScoreValue(data.Points[seat]) : "·", cellWidth, 14, data != null ? ScoreColor(data.Points[seat]) : "var(--mt)", TextAnchor.MiddleCenter);
                    cell.name = "score-cell-" + seat + "-" + hand; cell.style.height = 30; view.Decorate(cell, "rgba(0,0,0,.2)", 9);
                    if (data?.Finisher == seat) { var edge = Layer("score-finisher-ring"); Radius(edge, 9); Border(edge, 1.5f, view.C("#3CCB6E")); cell.Add(edge); }
                    row.Add(cell);
                }
                row.Add(ScoreCell(history.Count > 0 || result != null ? Total(seat).ToString(CultureInfo.InvariantCulture) : "–", 74, 19, "#F4F1E8")); rows.Add(row); rank++;
            }
            if (history.Count == 0 && result == null) { var empty = Text(L("Henüz el oynanmadı", "No hands played yet"), "Baloo2-700", 13, "var(--mt)"); empty.name = "table.t.sbEmpty"; Pad(empty, 6, 14, 6, 14); rows.Add(empty); }
        }

        private void BuildScoreHand(VisualElement body, ScoreHand hand, ResultInfo result)
        {
            var rows = Box(); Pad(rows, 0, 30, 0, 30); body.Add(rows);
            var headers = Row(10); Pad(headers, 0, 14, 0, 14); headers.Add(ScoreCell("", 40, 10, "var(--mt)"));
            var player = Caption(L("OYUNCU", "PLAYER")); player.name = "table.t.colPlayer"; player.style.flexGrow = 1; player.style.flexBasis = 0; headers.Add(player);
            var handTitle = Caption(L("EL PUANI", "HAND")); handTitle.name = "table.t.colHand"; handTitle.style.width = 110; handTitle.style.flexShrink = 0; handTitle.style.unityTextAlign = TextAnchor.MiddleRight; headers.Add(handTitle);
            var totalTitle = Caption(L("TOPLAM", "TOTAL")); totalTitle.name = "table.t.colTotal"; totalTitle.style.width = 110; totalTitle.style.flexShrink = 0; totalTitle.style.unityTextAlign = TextAnchor.MiddleRight; headers.Add(totalTitle); rows.Add(headers);
            IEnumerable<ResultRow> scores = result?.Rows ?? Enumerable.Empty<ResultRow>();
            if (hand != null) scores = Enumerable.Range(0, 4).OrderBy(k => hand.Points[k]).ThenBy(k => k).Select(k => new ResultRow
            { Seat = k, Hand = hand.Points[k], Total = hand.Totals[k], Note = hand.Finisher == k ? S("noteFin") : hand.Opened[k].HasValue ? S(hand.Opened[k].Value ? "noteOpen" : "noteClosed") : "" });
            int rank = 0;
            foreach (var data in scores)
            {
                var row = ScoreRow(data.Seat, rank, rank == 0, 10); row.name = "result-row-" + data.Seat;
                var place = ScoreCell((rank + 1) + ".", 40, 20, rank == 0 ? "var(--a1)" : "var(--mt)", TextAnchor.MiddleLeft); place.name = "table.r.rank[" + rank + "]"; row.Add(place);
                var who = ScorePlayer(data.Seat, 36, 16); who[0].name = "table.r.ini[" + rank + "]"; who[1].name = "table.r.name[" + rank + "]";
                var note = Text(data.Note ?? "", "Baloo2-800", 11, "var(--mt)"); note.name = "table.r.note[" + rank + "]"; who.Add(note); row.Add(who);
                int points = (data.Hand ?? 0) + data.Penalty;
                var score = ScoreCell(data.Hand.HasValue ? ScoreValue(points) : "—", 110, 18, ScoreColor(points)); score.name = "table.r.hand[" + rank + "]"; score.tooltip = data.Explain; row.Add(score);
                var total = ScoreCell(data.Total.ToString(CultureInfo.InvariantCulture), 110, 18, "#F4F1E8"); total.name = "table.r.total[" + rank + "]"; row.Add(total);
                float began = scoreRowsStartedAt + rank * .06f;
                Action<float> animate = t => { float u = MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01((t - began) / .35f)); row.style.translate = new Translate(44 * (1 - u), 0); row.style.opacity = u; };
                (result != null ? motions : windowMotions).Add(animate);
                rows.Add(row); rank++;
            }
        }
    }
}
