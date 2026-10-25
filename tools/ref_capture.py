"""Capture the v4 prototype reference: every active state of states.json opened through the prototype's own state
router (g101-state.js), saved as a 1280x720 PNG plus the router's layout dump (window.G101Dump, dump=1&nodl=1).

    uv run --with playwright python tools/ref_capture.py [--out artifacts/v4/ref] [--only S10,G13] [--jobs 4]
        [--variants main,repeat,en,mars,neptun,jupiter,venus]

Variants (each one directory under --out):
    main      ?state=ID&seed=42&theme=saturn&lang=tr&ft=<hold>  -> <out>/<ID>.png, <ID>.layout.json
              (a state that fixes its own theme or lang in states.json keeps it: the URL then leaves that parameter out,
               because the router lets a URL parameter override the state's own value)
    repeat    the same URL again                                   -> <out>/_repeat/  (determinism check)
    en        lang=en                                              -> <out>/en/
    <theme>   theme=<theme>                                        -> <out>/themes/<theme>/
Writes <out>/capture_report.json: per variant and state the URL, router error, console errors, timing, and for
'repeat' the number of differing pixels against 'main'.
"""
import argparse, asyncio, functools, http.server, json, os, sys, threading, time, urllib.parse

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
PROTO = os.path.join(REPO, "Design", "Galaxy101_ClaudeCode", "prototip")
PAGE = "Galaxy 101 - Gerçek Ana Oyun.dc.html"
THEMES = ["saturn", "mars", "neptun", "jupiter", "venus"]
# Every visible text node (the format tools/compare_ui.py reads): [text, x, y, w, h, fontSize, weight, family, color].
TEXT_DUMP = """() => { const out=[]; const walker=document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT); let n;
 while((n=walker.nextNode())){ const t=n.textContent.trim(); if(!t) continue; const r=document.createRange(); r.selectNodeContents(n);
  const b=r.getBoundingClientRect(); if(b.width<1||b.height<1||b.right<0||b.bottom<0||b.left>1280||b.top>720) continue; let vis=true;
  for(let e=n.parentElement;e;e=e.parentElement){const cs=getComputedStyle(e); if(cs.visibility==='hidden'||cs.display==='none'||+cs.opacity<0.02){vis=false;break;}}
  if(!vis) continue; const cs=getComputedStyle(n.parentElement); const owner=n.parentElement.closest('[data-g101-id]');
  out.push([t.slice(0,80), Math.round(b.x), Math.round(b.y), Math.round(b.width), Math.round(b.height), cs.fontSize, cs.fontWeight, cs.fontFamily.split(',')[0], cs.color, owner?owner.getAttribute('data-g101-id'):null]); }
 return out; }"""


def load_states():
    with open(os.path.join(PROTO, "galaxy101_handoff_v4", "states", "states.json"), encoding="utf-8") as f:
        return [s for s in json.load(f)["states"] if not s.get("removed")]


def url_for(base, st, variant):
    q = {"state": st["id"], "seed": "42"}
    theme, lang = st.get("theme"), st.get("lang")
    if variant in ("main", "repeat"):
        if not theme: q["theme"] = "saturn"
        if not lang: q["lang"] = "tr"
    elif variant == "en":
        if not theme: q["theme"] = "saturn"
        q["lang"] = "en"
    else:
        q["theme"] = variant
        if not lang: q["lang"] = "tr"
    q["ft"] = str(st.get("hold", 600))
    q["dump"] = "1"; q["nodl"] = "1"
    return base + urllib.parse.quote(PAGE) + "?" + urllib.parse.urlencode(q)


def out_dir(root, variant):
    if variant == "main": return root
    if variant == "repeat": return os.path.join(root, "_repeat")
    if variant == "en": return os.path.join(root, "en")
    return os.path.join(root, "themes", variant)


async def capture(browser, base, st, variant, root, timeout_s):
    sid = st["id"]
    d = out_dir(root, variant); os.makedirs(d, exist_ok=True)
    ctx = await browser.new_context(viewport={"width": 1280, "height": 720}, device_scale_factor=1)
    page = await ctx.new_page()
    errors = []
    page.on("console", lambda m: errors.append(m.text[:300]) if m.type == "error" else None)
    page.on("pageerror", lambda e: errors.append("pageerror: " + str(e)[:300]))
    url = url_for(base, st, variant)
    t0 = time.time(); rec = {"id": sid, "variant": variant, "url": url}
    try:
        await page.goto(url, wait_until="load", timeout=60000)
        # Polled from here: once the clock freezes, the page's own setTimeout is held, so in-page polling would stall.
        ready = "id => document.documentElement.getAttribute('data-g101-state') === id || !!(window.G101State && window.G101State.error)"
        deadline = time.time() + timeout_s
        while not await page.evaluate(ready, sid):
            if time.time() > deadline: raise TimeoutError(f"state {sid} not ready after {timeout_s}s")
            await asyncio.sleep(0.25)
        await asyncio.sleep(0.3)
        await page.screenshot(path=os.path.join(d, sid + ".png"))
        dump = await page.evaluate("() => window.G101Dump || null")
        with open(os.path.join(d, sid + ".layout.json"), "w", encoding="utf-8") as f:
            json.dump(dump, f, ensure_ascii=False, indent=1)
        with open(os.path.join(d, sid + ".text.json"), "w", encoding="utf-8") as f:
            json.dump(await page.evaluate(TEXT_DUMP), f, ensure_ascii=False)
        rec["routerError"] = await page.evaluate("() => window.G101State && window.G101State.error")
        rec["items"] = dump and dump.get("count")
    except Exception as e:  # noqa: BLE001 - every failure is recorded, the run continues
        rec["captureError"] = str(e)[:400]
    rec["consoleErrors"] = errors[:20]
    rec["seconds"] = round(time.time() - t0, 1)
    await ctx.close()
    return rec


def pixel_diff(a, b):
    try:
        from PIL import Image, ImageChops
    except ImportError:
        return None
    if not (os.path.exists(a) and os.path.exists(b)): return None
    ia, ib = Image.open(a).convert("RGB"), Image.open(b).convert("RGB")
    if ia.size != ib.size: return -1
    diff = ImageChops.difference(ia, ib).convert("L").point(lambda v: 255 if v > 0 else 0)
    return sum(1 for v in diff.getdata() if v)


async def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=os.path.join(REPO, "artifacts", "v4", "ref"))
    ap.add_argument("--only")
    ap.add_argument("--jobs", type=int, default=4)
    ap.add_argument("--timeout", type=int, default=150)
    ap.add_argument("--chrome-args", default="--enable-gpu --ignore-gpu-blocklist")
    ap.add_argument("--variants", default="main,repeat,en," + ",".join(THEMES[1:]))
    a = ap.parse_args()
    from playwright.async_api import async_playwright

    states = load_states()
    if a.only: states = [s for s in states if s["id"] in a.only.split(",")]
    variants = a.variants.split(",")
    root = os.path.abspath(a.out); os.makedirs(root, exist_ok=True)

    class Quiet(http.server.SimpleHTTPRequestHandler):
        def log_message(self, *x): pass
    handler = functools.partial(Quiet, directory=PROTO)
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    base = f"http://127.0.0.1:{httpd.server_address[1]}/"

    report_path = os.path.join(root, "capture_report.json")
    report = {}
    if os.path.exists(report_path):
        with open(report_path, encoding="utf-8") as f: report = json.load(f)
    jobs = [(st, v) for v in variants for st in states
            if not (v in THEMES and st.get("theme"))]          # a state pinned to one theme has no other-theme variant
    sem = asyncio.Semaphore(a.jobs)
    done = 0
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel="chrome", args=a.chrome_args.split() if a.chrome_args else [])

        async def one(st, v):
            nonlocal done
            async with sem:
                rec = await capture(browser, base, st, v, root, a.timeout)
            report.setdefault(v, {})[st["id"]] = rec
            done += 1
            print(f"[{done}/{len(jobs)}] {v} {st['id']} {rec.get('seconds')}s"
                  + (f" ERR {rec.get('captureError') or rec.get('routerError')}" if rec.get('captureError') or rec.get('routerError') else ""), flush=True)
            if done % 20 == 0:
                with open(report_path, "w", encoding="utf-8") as f: json.dump(report, f, ensure_ascii=False, indent=1)

        await asyncio.gather(*(one(st, v) for st, v in jobs))
        await browser.close()
    httpd.shutdown()

    if "repeat" in report:
        for sid, rec in report["repeat"].items():
            rec["diffPixels"] = pixel_diff(os.path.join(root, sid + ".png"), os.path.join(root, "_repeat", sid + ".png"))
    with open(report_path, "w", encoding="utf-8") as f: json.dump(report, f, ensure_ascii=False, indent=1)
    print("report:", report_path)


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
