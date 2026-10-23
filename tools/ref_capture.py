"""Drive the design prototype (Galaxy 101 - Oyun.dc.html) through its screens and save, per step, a 1280×720 PNG and
the text-box dump (<step>.ref.json) that tools/compare_ui.py matches against the Unity UiDump.

Usage: uv run --with playwright python ref_capture.py <reference dir> <out dir> [step filter]
"""
import asyncio, functools, http.server, json, os, sys, threading
from playwright.async_api import async_playwright

ROOT, OUT = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
ONLY = sys.argv[3] if len(sys.argv) > 3 else None
DUMP = """() => { const out=[]; const walker=document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT); let n;
 while((n=walker.nextNode())){ const t=n.textContent.trim(); if(!t) continue; const r=document.createRange(); r.selectNodeContents(n);
  const b=r.getBoundingClientRect(); if(b.width<1||b.height<1||b.right<0||b.bottom<0||b.left>1280||b.top>720) continue; let vis=true;
  for(let e=n.parentElement;e;e=e.parentElement){const cs=getComputedStyle(e); if(cs.visibility==='hidden'||cs.display==='none'||+cs.opacity<0.02){vis=false;break;}}
  if(!vis) continue; const cs=getComputedStyle(n.parentElement);
  out.push([t.slice(0,40), Math.round(b.x), Math.round(b.y), Math.round(b.width), Math.round(b.height), cs.fontSize, cs.fontWeight, cs.fontFamily.split(',')[0], cs.color]); }
 return out; }"""


async def click_text(page, text, exact=False):
    # The visible match on top (lobby cards behind a dialog carry the same words).
    locs = page.get_by_text(text, exact=exact)
    for i in reversed(range(await locs.count())):
        loc = locs.nth(i)
        if await loc.is_visible():
            try: await loc.click(timeout=3000); return
            except Exception: continue
    raise RuntimeError("no clickable text: " + text)


async def main():
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=ROOT); handler.log_message = lambda *a: None
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler); threading.Thread(target=httpd.serve_forever, daemon=True).start()
    os.makedirs(OUT, exist_ok=True)
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel="chrome")
        page = await browser.new_page(viewport={"width": 1280, "height": 720}, device_scale_factor=1)
        await page.goto(f"http://127.0.0.1:{httpd.server_address[1]}/game/Galaxy%20101%20-%20Oyun.dc.html")

        async def snap(step, settle=1.6):
            if ONLY and ONLY not in step: return
            await page.wait_for_timeout(int(settle * 1000))
            await page.screenshot(path=os.path.join(OUT, step + ".ref.png"))
            json.dump(await page.evaluate(DUMP), open(os.path.join(OUT, step + ".ref.json"), "w", encoding="utf-8"), ensure_ascii=False)
            print("SNAP", step)

        await page.wait_for_timeout(600); await snap("S01_splash", 0.4)
        await page.wait_for_timeout(4000); await snap("S02_choice")
        await click_text(page, "Giriş yap", exact=True); await snap("S03_login")
        await click_text(page, "Kayıt ol", exact=True); await snap("S04_register")
        await page.locator("[title='Geri']").first.click(); await page.wait_for_timeout(700)
        await click_text(page, "MİSAFİR OLARAK OYNA"); await snap("S05_guest")
        await click_text(page, "Oynamaya başla", exact=True); await page.wait_for_timeout(2500); await snap("S06_menu", 2.0)
        await click_text(page, "HEMEN OYNA"); await page.wait_for_timeout(1200); await snap("S09_lobby", 1.5)
        await click_text(page, "Masa aç", exact=True); await snap("S10_create", 1.0)
        await click_text(page, "Vazgeç", exact=True); await page.wait_for_timeout(500)
        await click_text(page, "Kodla katıl", exact=True); await snap("S12_code", 1.0)
        await page.keyboard.type("GX-ZZ99"); await page.keyboard.press("Enter"); await snap("S12_code_error", 1.2)
        await click_text(page, "Vazgeç", exact=True); await page.wait_for_timeout(500)
        await click_text(page, "Masa aç", exact=True); await page.wait_for_timeout(800)
        await click_text(page, "Masayı oluştur"); await snap("S13_room", 2.0)
        await page.screenshot(path=os.path.join(OUT, "_after_room.png"))
        await browser.close()
    httpd.shutdown()

asyncio.run(main())
