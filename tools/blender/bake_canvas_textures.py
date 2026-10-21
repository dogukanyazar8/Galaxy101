"""Re-run the prototype's own canvas texture generators at high resolution and save every texture.

The handoff GLBs embed the prototype's procedural canvas textures at 256–1024 px. Instead of
re-implementing those generators, this script serves reference/table, patches each module's
`cvs(w, h)` helper so it returns a supersampled canvas (same logical size, ctx pre-scaled), builds
every exportable asset with `createTable().exportAsset()` and writes each CanvasTexture as PNG.
Tile textures (tiles3d.js) are left untouched on purpose: tile faces are rebuilt as an atlas in Unity.

Usage: uv run --with playwright python bake_canvas_textures.py <reference/table dir> <out dir> [theme]
Output: <out>/<asset>/<material>__<slot>.png  +  <out>/manifest.json
"""
import base64, functools, http.server, json, os, re, sys, threading, asyncio
from playwright.async_api import async_playwright

ROOT, OUT = os.path.abspath(sys.argv[1]), os.path.abspath(sys.argv[2])
THEME = sys.argv[3] if len(sys.argv) > 3 else "Satürn"
TARGET = {"table3d.js": 2048, "racks3d.js": 2048, "gifts3d.js": 1024}

# Supersampled drop-in for `function cvs(w, h) {...}`: logical size stays w×h for the drawing code.
CVS = ("function cvs(w, h) { const k = Math.max(1, Math.min(8, Math.round(%d / Math.max(w, h)))); "
       "const c = document.createElement('canvas'); c.width = Math.round(w * k); c.height = Math.round(h * k); "
       "const x = c.getContext('2d'); x.scale(k, k); c.__k = k; return [c, x]; }")
ASSETS = [("table", s) for s in ("station", "wood", "holo", "andromeda")] + \
         [("rack", s) for s in ("walnut", "sedef", "chrome", "carbon", "neon", "andromeda")] + \
         [("chair", None), ("room", None)] + \
         [("gift", g) for g in ("tea", "coffee", "cola", "simit", "baklava", "seeds", "flower", "cake", "trophy", "rocket")]
NAMES = {("table", "station"): "masa_istasyon", ("table", "wood"): "masa_ceviz", ("table", "holo"): "masa_hologram", ("table", "andromeda"): "masa_andromeda",
         ("rack", "walnut"): "istaka_ceviz", ("rack", "sedef"): "istaka_sedef", ("rack", "chrome"): "istaka_aluminyum", ("rack", "carbon"): "istaka_karbon",
         ("rack", "neon"): "istaka_neon", ("rack", "andromeda"): "istaka_andromeda", ("chair", None): "koltuk", ("room", None): "oda_istasyon"}

COLLECT = r"""async ([assets, theme]) => {
  const THREE = await import('three');
  const { createTable } = await import('./table3d.js');
  const THEMES = { 'Satürn': { a1: '#FFD964', a2: '#F5A623', f1: '#2C3790', f2: '#11153F', tile: '#FFFDF6', rim: '#4a5080', leather: '#232A66', glow: '#F5A623', sky: '#FFE7A3', lg1: '#FFF4C8' } };
  await document.fonts.load('700 30px "Montserrat"');
  const hidden = document.createElement('canvas'); hidden.width = hidden.height = 64; document.body.appendChild(hidden);
  const T = createTable(hidden, { theme: THEMES[theme], font: 'Montserrat', tableSkin: 'station', rackSkin: 'walnut', tileDepth: 0.010 });
  const out = {};
  for (const [kind, skin, name] of assets) {
    const g = T.exportAsset(kind, skin); const seen = {};
    g.traverse((o) => { if (!o.isMesh) return; (Array.isArray(o.material) ? o.material : [o.material]).forEach((m) => {
      ['map', 'emissiveMap', 'alphaMap', 'roughnessMap', 'metalnessMap', 'bumpMap'].forEach((slot) => {
        const t = m[slot]; if (!t || !t.image || !t.image.toDataURL) return;
        const key = m.name + '__' + slot; if (seen[key]) return;
        seen[key] = { png: t.image.toDataURL('image/png'), w: t.image.width, h: t.image.height, k: t.image.__k || 1,
          repeat: [t.repeat.x, t.repeat.y], offset: [t.offset.x, t.offset.y], wrap: [t.wrapS, t.wrapT], srgb: t.colorSpace === THREE.SRGBColorSpace };
      });
    }); });
    out[name] = seen;
  }
  return out;
}"""


def serve():
    handler = functools.partial(http.server.SimpleHTTPRequestHandler, directory=ROOT)
    handler.log_message = lambda *a: None
    httpd = http.server.ThreadingHTTPServer(("127.0.0.1", 0), handler)
    threading.Thread(target=httpd.serve_forever, daemon=True).start()
    return httpd


async def main():
    httpd = serve(); base = f"http://127.0.0.1:{httpd.server_address[1]}/"
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel="chrome")
        page = await browser.new_page()

        async def patch(route):
            name = route.request.url.split("/")[-1].split("?")[0]
            body = open(os.path.join(ROOT, name), encoding="utf-8").read()
            body, n = re.subn(r"function cvs\(w, h\) \{[^\n]*\}", CVS % TARGET[name], body, count=1)
            assert n == 1, name
            await route.fulfill(body=body, content_type="text/javascript")
        for name in TARGET:
            await page.route(f"**/{name}", patch)
        # The export page supplies the import map and the fonts the generators draw with.
        await page.goto(base + "Galaxy101_3D_Varlik_Disa_Aktarma.html")
        await page.wait_for_function("document.querySelector('#list .it') !== null", timeout=60000)
        assets = [[k, s, "galaxy101_" + (NAMES.get((k, s)) or ("ikram_" if s in ("tea", "coffee", "cola", "simit", "baklava", "seeds") else "hediye_") + s)] for k, s in ASSETS]
        result = await page.evaluate(COLLECT, [assets, THEME])
        await browser.close()
    httpd.shutdown()
    manifest = {}
    for asset, textures in result.items():
        os.makedirs(os.path.join(OUT, asset), exist_ok=True)
        for key, t in textures.items():
            path = os.path.join(OUT, asset, key + ".png")
            open(path, "wb").write(base64.b64decode(t.pop("png").split(",", 1)[1]))
            manifest.setdefault(asset, {})[key] = t
    json.dump(manifest, open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
    print("BAKED", sum(len(v) for v in manifest.values()), "textures for", len(manifest), "assets")

asyncio.run(main())
