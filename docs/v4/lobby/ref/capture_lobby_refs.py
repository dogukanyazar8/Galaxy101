"""Lane "Lobi" reference frames the package's states.json does not give: G101-Lobi views opened through the
prototype's own state router (g101-state.js) with extra states injected into states.json (route interception).
Output: <out>/<ID>.png, .layout.json, .text.json — the format of tools/ref_capture.py (tools/v4/compare_states.py reads it).
X14 / X15 are S14 / S15 with the path that really opens the invite panel (wait.inv, EK-801); the rest are lobby
views without a catalogue state.
    uv run --with playwright --with pillow python docs/v4/lobby/ref/capture_lobby_refs.py <out> [tr|en] [ID,ID,...]
(G101_PROTO overrides the prototype folder; the repo's Design/ is the default.)
"""
import asyncio, functools, http.server, json, os, sys, threading, time, urllib.parse

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
PROTO = os.environ.get("G101_PROTO") or os.path.join(REPO, "Design", "Galaxy101_ClaudeCode", "prototip")
PAGE = "Galaxy 101 - Gerçek Ana Oyun.dc.html"
sys.path.insert(0, os.path.join(REPO, "tools"))
from ref_capture import TEXT_DUMP  # noqa: E402

BASE = [{"acct": True}, {"go": "lobby"}, {"waitFor": "Lobi"}]
WAIT = BASE + [{"call": "Lobi", "fn": "enterWait", "args": ["$tbl:mine", True]}]
P = lambda n, c, lv: {"name": n, "color": c, "lv": lv}
R0 = {"okey": True, "isle": True, "spec": True, "chat": "on", "gifts": True}
PAIRS = {"id": "x1", "name": "Takım Ruhu", "type": "esli", "bet": 1000, "hands": 3, "turn": 20, "kat": True, "katP": False, "pass": None,
         "vis": "open", "code": None, "rules": dict(R0, gifts=False), "players": [P("Mert K.", "#E8A317", 22), None, P("Ayşe Y.", "#3D7BFF", 42), None],
         "owner": 0, "status": "waiting", "hand": 0}
PRIV = {"id": "x2", "name": "Ayşe’nin masası", "type": "tekli", "bet": 5000, "hands": 5, "turn": 30, "kat": False, "pass": "4821", "vis": "private",
        "code": "GX7Q2M", "rules": dict(R0), "players": [P("Doğukan Y.", "#FF8A5B", 24), P("Mert K.", "#E8A317", 22), None, None], "owner": 0,
        "status": "waiting", "hand": 0}
EXTRA = {
    "X14": WAIT + [{"set": "Lobi", "deep": {"wait": {"inv": {"tab": "fr", "q": "", "seat": None}}}}],
    "X15": WAIT + [{"set": "Lobi", "deep": {"wait": {"inv": {"tab": "recent", "q": "", "seat": None}}}}],
    "X14pend": WAIT + [{"set": "Lobi", "deep": {"wait": {"inv": {"tab": "fr", "q": "", "seat": 3}}}}, {"click": "lobby.r.go[0]"}],
    "X14q": WAIT + [{"set": "Lobi", "deep": {"wait": {"inv": {"tab": "fr", "q": "zz", "seat": None}}}}],
    "XCpairs": BASE + [{"call": "Lobi", "fn": "openCreate"}, {"set": "Lobi", "deep": {"create": {"type": "esli", "kat": True}}}],
    "XCpairsScroll": BASE + [{"call": "Lobi", "fn": "openCreate"}, {"set": "Lobi", "deep": {"create": {"type": "esli", "kat": True, "rulesOpen": True, "passOn": True, "pass": "12", "vis": "private", "tried": True, "name": ""}}}],
    "XCwarn": BASE + [{"call": "Lobi", "fn": "openCreate"}, {"set": "Lobi", "deep": {"create": {"warn": 50000, "warnKey": 1}}}],
    "XCrules": BASE + [{"call": "Lobi", "fn": "openCreate"}, {"set": "Lobi", "deep": {"create": {"rulesOpen": True, "rules": dict(R0, spec=False, chat="quick")}}}],
    "Xshare": BASE + [{"call": "Lobi", "fn": "enterWait", "args": [PRIV, True]}, {"set": "Lobi", "s": {"dlg": {"kind": "share", "table": PRIV, "copied": False, "shared": False, "first": True}}}],
    "XwaitPriv": BASE + [{"call": "Lobi", "fn": "enterWait", "args": [PRIV, True]}],
    "XinvPriv": BASE + [{"call": "Lobi", "fn": "enterWait", "args": [PRIV, True]}, {"set": "Lobi", "deep": {"wait": {"inv": {"tab": "fr", "q": "", "seat": None}}}}],
    "XwaitPairs": BASE + [{"call": "Lobi", "fn": "enterWait", "args": [PAIRS, True]}],
    "XwaitGuest": BASE + [{"call": "Lobi", "fn": "enterWait", "args": [dict(PAIRS, kat=False, type="tekli", players=[P("Mert K.", "#E8A317", 22), P("Doğukan Y.", "#FF8A5B", 24), None, None]), False]}],
    "Xconnect": BASE + [{"call": "Lobi", "fn": "connect", "args": ["$tbl:open", "sit"]}],
    "Xfull": BASE + [{"call": "Lobi", "fn": "connect", "args": ["$tbl:open", "sit"]}, {"wait": 1100}],
    "Xclosed": BASE + [{"set": "Lobi", "s": {"dlg": {"kind": "closed", "table": {"name": "Akşam Keyfi", "bet": 10000, "type": "esli", "status": "waiting", "rules": R0}}}}],
    "XfullWatch": BASE + [{"set": "Lobi", "s": {"dlg": {"kind": "full", "table": {"name": "Usta Masası", "bet": 50000, "type": "tekli", "status": "playing", "rules": R0}}}}],
    "XpinErr": BASE + [{"call": "Lobi", "fn": "attempt", "args": ["$tbl:pass", "join"]}, {"set": "Lobi", "deep": {"dlg": {"pin": "", "err": True, "key": 1}}}],
    "XpinTwo": BASE + [{"call": "Lobi", "fn": "attempt", "args": ["$tbl:pass", "join"]}, {"set": "Lobi", "deep": {"dlg": {"pin": "12"}}}],
    "XpinFull": BASE + [{"call": "Lobi", "fn": "attempt", "args": ["$tbl:pass", "join"]}, {"set": "Lobi", "deep": {"dlg": {"pin": "1234"}}}],
    "XcodeErr": BASE + [{"set": "Lobi", "s": {"dlg": {"kind": "code", "code": "GX-12", "err": "Kod 6 karakter olmalı", "key": 1}}}],
    "Xempty": BASE + [{"set": "Lobi", "s": {"q": "zzz"}}],
    "XfilterOn": BASE + [{"set": "Lobi", "deep": {"filt": {"type": "esli", "free": True, "kat": True, "bet": 5000}}}],
    "Xtoast": BASE + [{"call": "Lobi", "fn": "flash", "args": ["Bekleme odasından ayrıldın", "info"]}],
}


async def main():
    out = os.path.abspath(sys.argv[1]); os.makedirs(out, exist_ok=True)
    lang = sys.argv[2] if len(sys.argv) > 2 else "tr"
    only = sys.argv[3].split(",") if len(sys.argv) > 3 else None
    from playwright.async_api import async_playwright
    with open(os.path.join(PROTO, "galaxy101_handoff_v4", "states", "states.json"), encoding="utf-8") as f:
        doc = json.load(f)
    for k, path in EXTRA.items():
        doc["states"].append({"id": k, "group": "lobi", "title": k, "path": path, "hold": 900})
    body = json.dumps(doc, ensure_ascii=False)

    class Quiet(http.server.SimpleHTTPRequestHandler):
        def log_message(self, *x): pass
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 0), functools.partial(Quiet, directory=PROTO))
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    base = f"http://127.0.0.1:{httpd.server_address[1]}/"
    ids = [k for k in EXTRA if not only or k in only]
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel="chrome", args=["--enable-gpu", "--ignore-gpu-blocklist"])
        for sid in ids:
            ctx = await browser.new_context(viewport={"width": 1280, "height": 720}, device_scale_factor=1)
            page = await ctx.new_page()
            await page.route("**/states/states.json", lambda r: r.fulfill(status=200, content_type="application/json", body=body))
            hold = 300 if sid == "Xconnect" else 900
            url = base + urllib.parse.quote(PAGE) + "?" + urllib.parse.urlencode({"state": sid, "seed": "42", "theme": "saturn", "lang": lang, "ft": str(hold), "dump": "1", "nodl": "1"})
            await page.goto(url, wait_until="load", timeout=60000)
            t0 = time.time()
            while not await page.evaluate("id => document.documentElement.getAttribute('data-g101-state') === id || !!(window.G101State && window.G101State.error)", sid):
                if time.time() - t0 > 90: break
                await asyncio.sleep(.25)
            await asyncio.sleep(.3)
            await page.screenshot(path=os.path.join(out, sid + ".png"))
            dump = await page.evaluate("() => window.G101Dump || null")
            with open(os.path.join(out, sid + ".layout.json"), "w", encoding="utf-8") as f: json.dump(dump, f, ensure_ascii=False, indent=1)
            with open(os.path.join(out, sid + ".text.json"), "w", encoding="utf-8") as f: json.dump(await page.evaluate(TEXT_DUMP), f, ensure_ascii=False)
            err = await page.evaluate("() => window.G101State && window.G101State.error")
            print(sid, "error=" + str(err), "items=" + str(dump and dump.get("count")), flush=True)
            await ctx.close()
        await browser.close()
    httpd.shutdown()

asyncio.run(main())
