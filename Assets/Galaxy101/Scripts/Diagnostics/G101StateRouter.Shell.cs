#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

namespace Galaxy101.Diagnostics
{
    public sealed partial class G101StateRouter
    {
        /// <summary>S01–S16 (açılış, giriş, lobi, dereceli) — states.json paths fresh / logout + go auth|login|register|guest / go lobby / go ranked.</summary>
        private IEnumerator ShellState(string id, string theme, string lang)
        {
            switch (id)
            {
                case "S01":                                                    // fresh: the splash with its loading bar
                    yield return OpenMenu(theme, lang, true);
                    yield break;
                case "S02": case "S03": case "S04": case "S05":                 // logout → go auth | login | register | guest
                {
                    yield return OpenMenu(theme, lang, true);
                    var auth = Menu.Auth ?? throw new InvalidOperationException("sign-in did not open");
                    float until = Time.realtimeSinceStartup + 20;
                    while (auth.Screen == "splash") { if (Time.realtimeSinceStartup > until) throw new TimeoutException("splash did not end"); yield return null; }
                    if (id == "S05") auth.Prefill("Misafir4821", "#FF8A5B");      // f: {nick: 'Misafir4821', color: '#FF8A5B'}
                    auth.Go(id == "S02" ? "choice" : id == "S03" ? "login" : id == "S04" ? "register" : "guest");
                    yield break;
                }
                default:
                    throw new NotSupportedException("no Unity path for this state yet");
            }
        }
    }
}
#endif
