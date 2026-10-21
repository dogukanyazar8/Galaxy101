"""Bake the tile face atlas (spec/16 D4) from the prototype's own tiles3d.js faceMat() drawing.

5 skins × 53 cells (R/B/Y/K 1–13 + fake okey), each cell 128×180 drawn at 2× (256×360), laid out
8 columns × 7 rows → 2048×2520 per skin. Cell order: R1…R13, B1…B13, Y1…Y13, K1…K13, J.
Writes <out>/TileFaces_<skin>.png and <out>/tile_faces.json (cell size, grid, per-skin roughness/emission).

Usage: uv run --with playwright --with pillow python bake_tile_faces.py <reference/table dir> <out dir>
"""
import base64, functools, http.server, io, json, os, re, sys, threading, asyncio
from PIL import Image
from playwright.async_api import async_playwright

ROOT, OUT = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
SKINS = ["classic", "galaxy", "gold", "neon", "marble"]
COLS, ROWS, CW, CH = 8, 7, 256, 360
CVS = ("function cvs(w, h) { const k = 2; const c = document.createElement('canvas'); c.width = w * k; c.height = h * k; "
       "const x = c.getContext('2d'); x.scale(k, k); return [c, x]; }")
SCRIPT = r"""async (skins) => {
  const { createTileKit } = await import('./tiles3d.js');
  await document.fonts.load('800 40px "Montserrat"');
  const kit = createTileKit(), out = {};
  for (const skin of skins) {
    const cells = [];
    for (const c of ['R', 'B', 'Y', 'K']) for (let n = 1; n <= 13; n++) cells.push([c, n]);
    cells.push(['J', 0]);
    out[skin] = cells.map(([c, n]) => { const m = kit.faceMat(skin, c, n, 'Montserrat');
      return { png: m.map.image.toDataURL('image/png'), rough: m.roughness, emis: m.emissiveMap ? m.emissiveIntensity : 0 }; });
  }
  return out;
}"""


async def main():
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=ROOT); handler.log_message = lambda *a: None
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler); threading.Thread(target=httpd.serve_forever, daemon=True).start()
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel="chrome"); page = await browser.new_page()

        async def patch(route):
            body = open(os.path.join(ROOT, "tiles3d.js"), encoding="utf-8").read()
            body, n = re.subn(r"function cvs\(w, h\) \{[^\n]*\}", CVS, body, count=1); assert n == 1
            await route.fulfill(body=body, content_type="text/javascript")
        await page.route("**/tiles3d.js", patch)
        await page.goto(f"http://127.0.0.1:{httpd.server_address[1]}/Galaxy101_3D_Varlik_Disa_Aktarma.html")
        await page.wait_for_function("document.querySelector('#list .it') !== null", timeout=60000)
        result = await page.evaluate(SCRIPT, SKINS)
        await browser.close()
    httpd.shutdown()
    os.makedirs(OUT, exist_ok=True); meta = {"cell": [CW, CH], "grid": [COLS, ROWS], "order": "R1-13,B1-13,Y1-13,K1-13,J", "skins": {}}
    for skin, cells in result.items():
        atlas = Image.new("RGBA", (COLS * CW, ROWS * CH), (0, 0, 0, 0))
        for i, cell in enumerate(cells):
            img = Image.open(io.BytesIO(base64.b64decode(cell["png"].split(",", 1)[1]))).convert("RGBA")
            assert img.size == (CW, CH), img.size
            atlas.paste(img, ((i % COLS) * CW, (i // COLS) * CH))
        atlas.save(os.path.join(OUT, f"TileFaces_{skin}.png"), optimize=True)
        meta["skins"][skin] = {"roughness": cells[0]["rough"], "emission": cells[0]["emis"]}
    json.dump(meta, open(os.path.join(OUT, "tile_faces.json"), "w"), indent=1)
    print("ATLAS", list(meta["skins"].items()))

asyncio.run(main())
