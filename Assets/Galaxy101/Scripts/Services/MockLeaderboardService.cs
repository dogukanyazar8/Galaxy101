using System;
using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    public sealed class MockLeaderboardService : ILeaderboardService
    {
        private readonly MenuModel model;
        public MockLeaderboardService(MenuModel model) { this.model = model; }
        public JObject Get(string scope)
        {
            if (scope != "weekly" && scope != "season" && scope != "friends") throw new ArgumentException("Unknown leaderboard scope.", nameof(scope));
            bool friends = scope == "friends", weekly = scope == "weekly";
            var data = (JArray)model.Content["leaderboard"][friends ? "friends" : "global"];
            var all = new JArray();
            for (int i = 0; i < data.Count; i++)
            {
                var p = (JArray)data[i];
                bool isMe = p.Count > 5 && (bool?)p[5] == true;
                all.Add(new JObject { ["rank"] = i + 1, ["name"] = isMe ? (string)model.Profile["name"] : (string)p[0],
                    ["ini"] = isMe ? ProfileValidation.Initials((string)model.Profile["name"]) : (string)p[1],
                    ["c"] = isMe ? (string)model.Profile["color"] : (string)p[2],
                    ["points"] = (long)Math.Floor((long)p[3] * (weekly ? .32 : 1) + .5), ["league"] = (string)p[4], ["me"] = isMe });
            }
            var podium = new JArray(); var rows = new JArray();
            for (int i = 0; i < all.Count; i++) (i < 3 ? podium : rows).Add(all[i].DeepClone());
            return new JObject { ["podium"] = podium, ["rows"] = rows, ["me"] = new JObject {
                ["rank"] = friends ? 3 : weekly ? 128 : 1204, ["name"] = (string)model.Profile["name"],
                ["ini"] = ProfileValidation.Initials((string)model.Profile["name"]), ["c"] = (string)model.Profile["color"],
                ["points"] = (long)Math.Floor(2140 * (weekly ? .32 : 1) + .5), ["league"] = "g2", ["me"] = true } };
        }
    }
}
