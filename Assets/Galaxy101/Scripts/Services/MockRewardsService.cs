using System;
using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    public sealed class MockRewardsService : IRewardsService
    {
        private readonly MenuModel model;
        public MockRewardsService(MenuModel model) { this.model = model; }
        public DateTime ResetTime => DateTime.Today.AddDays(1);
        public JArray GetStreak()
        {
            var result = new JArray();
            var amounts = (JArray)model.Content["dailyStreakChips"];
            for (int i = 0; i < amounts.Count; i++) result.Add(new JObject { ["day"] = i + 1,
                ["chips"] = (long)amounts[i], ["today"] = i == 4 && !model.DayClaimed,
                ["claimed"] = i < 4 || (i == 4 && model.DayClaimed) });
            return result;
        }
        public bool ClaimDay()
        {
            long reward = (long)model.Content["dailyStreakChips"][4];
            if (model.DayClaimed || model.Chips > long.MaxValue - reward) return false;
            model.DayClaimed = true; model.Chips += reward; model.NotifyChanged(); return true;
        }
        public JArray GetQuests()
        {
            var result = (JArray)model.Content["dailyQuests"].DeepClone();
            for (int i = 0; i < result.Count; i++)
            {
                result[i]["claimed"] = model.QuestsClaimed.Contains(i);
                result[i]["complete"] = (int)result[i]["cur"] >= (int)result[i]["max"];
            }
            return result;
        }
        public bool ClaimQuest(int index)
        {
            var quests = (JArray)model.Content["dailyQuests"];
            if (index < 0 || index >= quests.Count || model.QuestsClaimed.Contains(index)) return false;
            var quest = quests[index];
            if ((int)quest["cur"] < (int)quest["max"]) return false;
            long chips = (long)quest["rw"], gems = (long?)quest["gem"] ?? 0;
            if (model.Chips > long.MaxValue - chips || model.Gems > long.MaxValue - gems) return false;
            model.QuestsClaimed.Add(index); model.Chips += chips; model.Gems += gems;
            model.NotifyChanged(); return true;
        }
    }
}
