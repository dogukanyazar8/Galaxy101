"""Drive the design prototype (Galaxy 101 - Oyun.dc.html) as a guest into the main menu and open every menu window,
saving per window a 1280×720 PNG and the text-box dump (<step>.ref.json) for tools/compare_ui.py.

Usage: uv run --with playwright python ref_menu_capture.py <reference dir> <out dir> [step filter]
"""
import asyncio, functools, http.server, json, os, sys, threading
from playwright.async_api import async_playwright
from ref_capture import DUMP, click_text

ROOT, OUT = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
ONLY = sys.argv[3] if len(sys.argv) > 3 else None

# (step, how to open it); closing is Escape.
WINDOWS = [
    ("M01_settings", ("title", "Ayarlar")),
    ("M02_shop", ("text", "Mağaza")),
    ("M03_friends", ("text", "Arkadaşlar")),
    ("M04_leader", ("text", "Liderlik")),
    ("M05_daily", ("bobbing", "Günlük ödül")),
    ("M06_rules", ("text", "Kurallar")),
    ("M07_profile", ("title", "Profilim")),
]


async def main():
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=ROOT); handler.log_message = lambda *a: None
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler); threading.Thread(target=httpd.serve_forever, daemon=True).start()
    os.makedirs(OUT, exist_ok=True)
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel="chrome")
        page = await browser.new_page(viewport={"width": 1280, "height": 720}, device_scale_factor=1)
        await page.goto(f"http://127.0.0.1:{httpd.server_address[1]}/game/Galaxy%20101%20-%20Oyun.dc.html")
        await page.wait_for_timeout(4600)
        await click_text(page, "MİSAFİR OLARAK OYNA"); await page.wait_for_timeout(700)
        await click_text(page, "Oynamaya başla", exact=True); await page.wait_for_timeout(4000)

        async def snap(step, settle=1.0):
            await page.wait_for_timeout(int(settle * 1000))
            await page.screenshot(path=os.path.join(OUT, step + ".ref.png"))
            json.dump(await page.evaluate(DUMP), open(os.path.join(OUT, step + ".ref.json"), "w", encoding="utf-8"), ensure_ascii=False)
            print("SNAP", step)

        for step, (kind, value) in WINDOWS:
            if ONLY and ONLY not in step: continue
            try:
                if kind == "title": await page.locator(f"[title='{value}']").first.click()
                elif kind == "bobbing": await page.get_by_text(value, exact=True).first.click(force=True)  # never "stable"
                else: await click_text(page, value, exact=True)
                await snap(step)
            except Exception as e:
                print("FAIL", step, e)
            await page.keyboard.press("Escape"); await page.wait_for_timeout(600)
        await browser.close()
    httpd.shutdown()

asyncio.run(main())
