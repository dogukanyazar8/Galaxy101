using System;
using Galaxy101.Contracts;
using Galaxy101.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Dereceli: "Rakip aranıyor" over the menu while the server matches players from nearby leagues (one league apart,
    /// two after 30 s). The table opens when the match starts; Vazgeç leaves the queue.
    /// </summary>
    public sealed class RankedQueue
    {
        private readonly MenuView view;
        private readonly GalaxyOnline online;
        private VisualElement layer;
        private Action off;
        private float since;

        /// <summary>The match was formed: the menu opens its table.</summary>
        public event Action<MatchStartMessage> Matched;
        public bool Visible => layer != null;

        public RankedQueue(MenuView view, GalaxyOnline online) { this.view = view; this.online = online; }

        public async void Start()
        {
            if (layer != null) return;
            Show();
            off = online.Realtime.On<MatchStartMessage>(MessageTypes.MatchStart, m =>
            {
                if (layer == null || m == null || m.Spectator || m.Mode != GameModes.Ranked) return;
                Close(); Matched?.Invoke(m);
            });
            var ack = await online.Realtime.RequestAsync(MessageTypes.QueueJoin, new QueueJoinMessage { Mode = GameModes.Ranked, Stake = 0 });
            if (ack.Ok || layer == null) return;
            Close();
            view.Flash(ack.Code == ErrorCodes.Conflict ? view.L("Zaten bir masadasın ya da sıradasın", "You are already at a table or in a queue")
                : ack.Code == "offline" || ack.Code == "timeout" ? view.L("Sunucuya ulaşılamadı", "Server unreachable") : view.L("Eşleşme başlatılamadı", "Could not start matchmaking"), "bad");
        }

        public void Cancel()
        {
            if (layer == null) return;
            _ = online.Realtime.RequestAsync(MessageTypes.QueueLeave, new { });
            Close();
        }

        private void Close()
        {
            off?.Invoke(); off = null;
            layer?.RemoveFromHierarchy(); layer = null;
        }

        private void Show()
        {
            var canvas = view.Root.Q<VisualElement>("galaxy-canvas") ?? view.Root;
            layer = view.Box(); layer.name = "ranked-queue";
            layer.style.position = Position.Absolute; layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            layer.style.alignItems = Align.Center; layer.style.justifyContent = Justify.Center; layer.style.backgroundColor = view.C("rgba(4,6,18,.62)");
            layer.pickingMode = PickingMode.Position;
            var card = view.Box(); card.style.width = 420; card.style.alignItems = Align.Center;
            card.style.paddingTop = card.style.paddingBottom = 28; card.style.paddingLeft = card.style.paddingRight = 32;
            view.Decorate(card, "var(--pn)", 26, 6, "var(--pnS)");
            var caption = view.Text(view.L("DERECELİ", "RANKED"), 12, false, "mt"); caption.style.unityFont = view.Font("Baloo2-800"); MenuView.Tracking(caption, 1.4f, 12); card.Add(caption);
            var title = view.Text(view.L("Rakip aranıyor", "Finding opponents"), 26, true); title.style.marginTop = 8; card.Add(title);
            var progress = online.Profile?.Progress;
            string code = progress?.League ?? "b3";
            string league = view.Model.Content["leaderboard"]?["leagues"]?[view.Loc.Language]?[code]?.ToString() ?? code;
            var line = view.Text(league + (code == "gx" && progress?.SeasonRank > 0 ? " · #" + progress.SeasonRank : "") + view.L(" · ligine yakın oyuncular", " · players near your league"), 14, false, "mt");
            line.style.marginTop = 6; card.Add(line);
            var clock = view.Text("0:00", 34, true, "a1"); clock.name = "ranked-queue-time"; clock.style.marginTop = 18; MenuView.TabularDigits(clock); card.Add(clock);
            since = Time.realtimeSinceStartup;
            clock.schedule.Execute(() =>
            {
                int seconds = Mathf.FloorToInt(Time.realtimeSinceStartup - since);
                clock.text = seconds / 60 + ":" + (seconds % 60).ToString("00");
            }).Every(500);
            var cancel = view.Button(view.L("Vazgeç", "Cancel"), Cancel, "secondary"); cancel.name = "ranked-queue-cancel"; cancel.style.marginTop = 22; card.Add(cancel);
            layer.Add(card); canvas.Add(layer);
        }
    }
}
