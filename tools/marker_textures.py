"""Render the lay-off / okey target markers exactly as the design does (table3d.js markerTex / badgeTex and the
gifts3d.js glowTex canvas code, run in Chrome) into Assets/Galaxy101/Resources/Galaxy101/Table/Markers/.

    frame_gold.png, frame_okey.png   markerTex(false / true): 200 x 264 dashed frame with a soft fill
    badge_gold.png, badge_okey.png   badgeTex(false / true): 160 x 160 gold "+" / green arrows button
    glow.png                         glowTex(): 128 x 128 white radial glow (the marker's halo)

Usage: uv run --with playwright python tools/marker_textures.py
"""
import asyncio, base64, os
from playwright.async_api import async_playwright

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
JS = open(os.path.join(ROOT, 'Design/Galaxy101_ClaudeCode/full project/table3d.js'), encoding='utf-8').read()
OUT = os.path.join(ROOT, 'Assets/Galaxy101/Resources/Galaxy101/Table/Markers')


def function_source(name):
    start = JS.index('function ' + name + '(')
    depth, i = 0, JS.index('{', start)
    while True:
        if JS[i] == '{': depth += 1
        elif JS[i] == '}':
            depth -= 1
            if depth == 0: return JS[start:i + 1]
        i += 1


HARNESS = """
const THREE = { SRGBColorSpace: 'srgb', CanvasTexture: function (c) { this.image = c; } };
function cvs(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return [c, c.getContext('2d')]; }
function glowCanvas() {   // gifts3d.js glowTex
  const [c, x] = cvs(128, 128), g = x.createRadialGradient(64, 64, 0, 64, 64, 64);
  g.addColorStop(0, 'rgba(255,255,255,1)'); g.addColorStop(0.25, 'rgba(255,255,255,.55)'); g.addColorStop(1, 'rgba(255,255,255,0)');
  x.fillStyle = g; x.fillRect(0, 0, 128, 128); return c;
}
%s
%s
window.render = () => ({
  frame_gold: markerTex(false).image.toDataURL('image/png'), frame_okey: markerTex(true).image.toDataURL('image/png'),
  badge_gold: badgeTex(false).image.toDataURL('image/png'), badge_okey: badgeTex(true).image.toDataURL('image/png'),
  glow: glowCanvas().toDataURL('image/png'),
});
"""


async def main():
    os.makedirs(OUT, exist_ok=True)
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel='chrome')
        page = await browser.new_page()
        await page.set_content('<html></html>')
        await page.add_script_tag(content=HARNESS % (function_source('markerTex'), function_source('badgeTex')))
        images = await page.evaluate('window.render()')
        for name, data in images.items():
            with open(os.path.join(OUT, name + '.png'), 'wb') as f: f.write(base64.b64decode(data.split(',', 1)[1]))
            print('marker', name)
        await browser.close()


asyncio.run(main())
