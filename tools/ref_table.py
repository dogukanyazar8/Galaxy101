"""Render the design's 3D table (G101-Masa.dc.html, three.js) to <out>.png at 1280×720 for colour comparison with the
Unity table capture (TableAssetBuilder.Capture).

Usage: uv run --with playwright python ref_table.py <reference dir> <out.png> [settle seconds]
"""
import asyncio, functools, http.server, os, sys, threading
from playwright.async_api import async_playwright

ROOT, OUT = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
SETTLE = float(sys.argv[3]) if len(sys.argv) > 3 else 8


async def main():
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=ROOT); handler.log_message = lambda *a: None
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler); threading.Thread(target=httpd.serve_forever, daemon=True).start()
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel="chrome", args=["--use-angle=d3d11", "--enable-gpu", "--ignore-gpu-blocklist"])
        page = await browser.new_page(viewport={"width": 1280, "height": 720}, device_scale_factor=1)
        page.on("console", lambda m: print("console:", m.text) if m.type == "error" else None)
        await page.goto(f"http://127.0.0.1:{httpd.server_address[1]}/game/G101-Masa.dc.html")
        await page.wait_for_timeout(int(SETTLE * 1000))
        await page.screenshot(path=OUT)
        await browser.close()
    httpd.shutdown()

asyncio.run(main())
