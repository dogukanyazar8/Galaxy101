#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;

namespace Galaxy101.Diagnostics
{
    public sealed partial class G101StateRouter
    {
        /// <summary>The demo season of the last ranked state opened (tests and further steps drive it).</summary>
        public G101DemoRanked RankedDemo { get; private set; }

        /// <summary>S16 Dereceli ana ekran — states.json path acct → go ranked → waitFor Dereceli (prototype data: G101DemoRanked).</summary>
        private IEnumerator RankedState(string id, string theme, string lang)
        {
            var demo = RankedDemo = new G101DemoRanked();
            MainMenuController.RankedBackend = () => demo;
            try { yield return OpenMenu(theme, lang, false); Menu.OpenRanked(); }
            finally { MainMenuController.RankedBackend = null; }
            if (Menu.Ranked == null || !Menu.Ranked.Visible) throw new InvalidOperationException("ranked screen did not open");
            yield return Settle(.4f);
        }
    }
}
#endif
