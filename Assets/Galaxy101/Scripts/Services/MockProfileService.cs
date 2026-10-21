using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    public sealed class MockProfileService : IProfileService
    {
        private readonly MenuModel model;
        private readonly JObject content;
        public bool LoggedOut { get; private set; }
        public bool DeletionRequested { get; private set; }
        public MockProfileService(MenuModel model, JObject content) { this.model = model; this.content = content; }
        public JObject Get() => (JObject)model.Profile.DeepClone();
        public bool Save(string name, string color, string status, int frame)
        {
            var value = new JObject { ["name"] = name, ["color"] = color, ["status"] = status ?? "", ["frame"] = frame };
            if (!ProfileValidation.IsValid(value)) return false;
            var previous = model.Profile;
            model.Profile = ProfileValidation.Sanitize(value);
            if (!model.Persist()) { model.Profile = previous; return false; }
            model.NotifyChanged();
            return true;
        }
        public JObject GetStats() => (JObject)content["stats"].DeepClone();
        public JArray GetBadges() => (JArray)content["badges"].DeepClone();
        public JArray GetHistory() => (JArray)content["history"].DeepClone();
        public bool Logout() { if (LoggedOut) return false; LoggedOut = true; model.NotifyChanged(); return true; }
        public bool RequestDelete() { if (DeletionRequested) return false; DeletionRequested = true; model.NotifyChanged(); return true; }
    }
}
