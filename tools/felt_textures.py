"""Render the table felt exactly as the design does (table3d.js feltTex / andromedaTex canvas code, run in Chrome) for
every table skin and planet theme, into Assets/Galaxy101/Resources/Galaxy101/Table/Felt/<skin>_<planet>.png.

The PNGs are flipped vertically like the GLB exports (the glTF exporter writes canvas rows bottom-up), so the felt
materials' existing texture transform maps them unchanged.

Usage: uv run --with playwright python felt_textures.py
"""
import asyncio, base64, json, os, re
from playwright.async_api import async_playwright

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
JS = open(os.path.join(ROOT, 'Design/Galaxy101_ClaudeCode/full project/table3d.js'), encoding='utf-8').read()
THEMES = json.load(open(os.path.join(ROOT, 'Assets/Galaxy101/Resources/Galaxy101/Table/table_content.json'), encoding='utf-8'))['planets_table']
FONT = os.path.join(ROOT, 'Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-700.ttf')
OUT = os.path.join(ROOT, 'Assets/Galaxy101/Resources/Galaxy101/Table/Felt')
PLANET_IDS = {'Satürn': 'saturn', 'Mars': 'mars', 'Neptün': 'neptune', 'Jüpiter': 'jupiter', 'Venüs': 'venus'}


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
const font = 'Montserrat', FELT = 0.76, _andro = {};
const THREE = { SRGBColorSpace: 'srgb', RepeatWrapping: 1000, CanvasTexture: function (c) { this.image = c; this.repeat = { set() {} }; this.offset = { set() {} }; } };
function cvs(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return [c, c.getContext('2d')]; }
%s
%s
%s
window.render = (kind, c1, c2, acc) => {
  const t = kind === 'andromeda' ? andromedaTex(1024, 1024, false) : kind === 'holo' ? feltTex(c2, '#03050d', acc, 'holo') : kind === 'wood' ? feltTex(c1, c2, acc, 'wood') : feltTex(c1, c2, acc);
  const src = t.image, out = document.createElement('canvas'); out.width = src.width; out.height = src.height;
  const x = out.getContext('2d'); x.translate(0, out.height); x.scale(1, -1); x.drawImage(src, 0, 0);
  return out.toDataURL('image/png');
};
"""


async def main():
    os.makedirs(OUT, exist_ok=True)
    script = HARNESS % (function_source('arcText'), function_source('feltTex'), function_source('andromedaTex'))
    font = base64.b64encode(open(FONT, 'rb').read()).decode()
    async with async_playwright() as p:
        browser = await p.chromium.launch(channel='chrome')
        page = await browser.new_page()
        await page.set_content('<style>@font-face{font-family:Montserrat;font-weight:700;src:url(data:font/ttf;base64,%s)}</style><div style="font:700 30px Montserrat">GALAXY 101</div>' % font)
        await page.evaluate('document.fonts.ready')
        await page.add_script_tag(content=script)
        jobs = [('andromeda', 'saturn', None)] + [(skin, PLANET_IDS[name], theme) for skin in ('station', 'wood', 'holo') for name, theme in THEMES.items() if name in PLANET_IDS]
        for skin, planet, theme in jobs:
            c1, c2, acc = (theme['f1'], theme['f2'], theme['a1']) if theme else ('', '', '')
            data = await page.evaluate('([k, a, b, c]) => window.render(k, a, b, c)', [skin, c1, c2, acc])
            name = 'andromeda' if skin == 'andromeda' else skin + '_' + planet
            open(os.path.join(OUT, name + '.png'), 'wb').write(base64.b64decode(data.split(',', 1)[1]))
            print('felt', name)
        await browser.close()

asyncio.run(main())
