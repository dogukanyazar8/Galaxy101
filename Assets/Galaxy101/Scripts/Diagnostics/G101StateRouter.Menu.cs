#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Galaxy101.Diagnostics
{
    public sealed partial class G101StateRouter
    {
        /// <summary>Catalogue ids opened from the main menu (states.json group "menu" + S06–S08).</summary>
        private static readonly HashSet<string> MenuStates = new HashSet<string>
        {
            "01","02","03","04","05","06","10","11","12","13","14","15","16","17","18","19","20","21","22","30","31","32",
            "40","41","42","43","44","45","46","47","50","51","52","60","61","70","71","72","73","74","75","80","81","82","83","84",
            "90","91","92","93","94","95","96","97","S06","S07","S08"
        };

        /// <summary>states.json menu paths: acct → go menu → waitFor Menu → set Menu {…} → click/type.</summary>
        private IEnumerator MenuState(string id, string theme, string lang)
        {
            yield return OpenMenu(theme, lang, false);
            var view = Menu.View; var model = Menu.Model;
            switch (id)
            {
                case "06": model.SelectedMode = 1; model.NotifyChanged(); break;                         // set Menu {sel: 1}
                case "10": case "91": case "S08": view.OpenPanel("settings", 0); break;                   // settingsOpen, setTab 0
                case "11": view.OpenPanel("settings", 1); break;
                case "12": case "S07": view.OpenPanel("settings", 2); break;
                case "13": case "90": view.OpenPanel("settings", 3); break;
                case "14": view.OpenPanel("settings", 4); break;
                case "15": case "16": case "17": case "18":                                                 // setTab 4, dlg profile
                    view.OpenPanel("settings", 4); yield return Settle(); view.OpenDialog("profile"); break;
                case "19": case "20": view.OpenPanel("settings", 4); yield return Settle(); view.OpenDialog("logout"); break;
                case "21": case "22": view.OpenPanel("settings", 4); yield return Settle(); view.OpenDialog("delete"); break;
                case "30": case "93": view.OpenPanel("shop", 0); break;
                case "31": view.OpenPanel("shop", 1); break;
                case "32": view.OpenPanel("shop", 2); break;
                case "40": case "94": view.OpenPanel("friends", 0); break;
                case "41": case "42": view.OpenPanel("friends", 1); break;
                case "43": view.OpenPanel("friends", 2); break;
                case "44": case "45": case "46": case "47": view.OpenPanel("friends", 0); yield return Settle(); view.OpenDialog("add"); break;
                case "50": view.OpenPanel("leader", 0); break;
                case "51": view.OpenPanel("leader", 1); break;
                case "52": view.OpenPanel("leader", 2); break;
                case "60": case "61": case "95": view.OpenPanel("daily"); break;
                case "70": case "96": view.OpenPanel("rules", 0); break;
                case "71": view.OpenPanel("rules", 1); break;
                case "72": view.OpenPanel("rules", 2); break;
                case "73": view.OpenPanel("rules", 3); break;
                case "74": view.OpenPanel("rules", 4); break;
                case "75": view.OpenPanel("rules", 5); break;
                case "80": case "97": view.OpenPanel("me", 0); break;
                case "81": view.OpenPanel("me", 1); break;
                case "82": view.OpenPanel("me", 2); break;
                case "83": case "84": view.OpenPanel("me", 0); yield return Settle(); view.OpenDialog("share"); break;
            }
            yield return Settle();
            switch (id)
            {
                case "16": yield return Click("draft-frame-1"); break;                                      // click menu.o.pick[3] (Altın çerçeve)
                case "17": Field("profile-name").value = "A"; break;                                        // type menu.pf.onName "A"
                case "18": yield return Click("draft-frame-1"); yield return Click("profile-save"); break;  // pick[3] + menu.pf.save
                case "20": yield return Click("confirm-account-action"); break;                             // dlg bye, byeFrom logout
                case "22": Field("delete-confirmation").value = "SİL"; break;                               // delTyped "SİL"
                case "42": Field("friend-query").value = "ay"; break;                                       // q "ay"
                case "45": case "46": case "47":
                    Field("add-friend-query").value = id == "47" ? "ZZ-0000" : "MK-3321"; yield return Click("search-add-friend");
                    if (id == "46") { yield return Settle(); yield return Click("send-friend-request"); }
                    break;
                case "61": view.Services.Rewards.ClaimDay(); break;                                         // dayClaimed: true
                case "84":                                                                                   // copied: true
                    var button = Button("copy-profile-link");
                    button.text = "✓ " + view.L("Kopyalandı", "Copied"); view.Decorate(button, "#3CCB6E", 14, 4, "#22914A"); button.style.color = Color.white;
                    break;
            }
            yield return Settle();
        }
    }
}
#endif
