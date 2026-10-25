"""Theme USS, logo artwork and the legacy menu icons, from the v4 data.

v4: fonts, the 69 package icons, league emblems, cursors, strings, sound and Resources/Galaxy101/themes.json come from
the v4 handoff package through scripts/import-v4-data.py (run that first). This script derives what is built from them:

  --write-uss      Assets/Galaxy101/UI/Styles/Theme.<Planet>.uss from themes.json cssVars (MenuAssetBuilder copies
                   them to Resources/Galaxy101/UI). Colour values only; numbers (ro) are not CSS colours.
  --logos          Resources/Galaxy101/Artwork/logo-<planet>.png, the "GALAXY" word in Montserrat 700 with the theme's
                   a1/a2/lg1/lS colours.
  --legacy-icons   Resources/Galaxy101/Icons/<name>.txt, the 44 pre-v4 menu icons cut from the old reference page
                   (Design/old/galaxy101_unity_handoff). GalaxyIcon callers still use these names; v4 icons live in
                   Icons/v4 (import-v4-data.py).

Without flags it only checks: fonts present with the Turkish glyphs MenuAssetBuilder needs, and whether each output
is current. Run with Python 3 + Pillow + fontTools:
  uv run --no-project --with fonttools --with pillow python scripts/import-galaxy-ui-assets.py [--write-uss] [--logos]
"""
from pathlib import Path
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import argparse, io, json, re, sys

ROOT = Path(__file__).resolve().parents[1]
OLD_HANDOFF = ROOT / 'Design/old/galaxy101_unity_handoff'
RES = ROOT / 'Assets/Galaxy101/Resources/Galaxy101'
FONTS = RES / 'Fonts'
STYLES = ROOT / 'Assets/Galaxy101/UI/Styles'
IDS = {'Satürn': 'saturn', 'Mars': 'mars', 'Neptün': 'neptune', 'Jüpiter': 'jupiter', 'Venüs': 'venus'}
# The static weights MenuAssetBuilder turns into FontAssets (MenuAssetBuilder.cs), plus the v4 version tag font.
WEIGHTS = ['Montserrat-600', 'Montserrat-700', 'Montserrat-800', 'Montserrat-900', 'Baloo2-600', 'Baloo2-700', 'Baloo2-800',
           'Nunito-700', 'Nunito-900']

parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
parser.add_argument('--write-uss', action='store_true')
parser.add_argument('--logos', action='store_true')
parser.add_argument('--legacy-icons', action='store_true')
args = parser.parse_args()
sys.stdout.reconfigure(encoding='utf-8')
stale = []

for name in WEIGHTS:
    path = FONTS / f'{name}.ttf'
    missing = [c for c in 'çğıİöşüÇĞÖŞÜ₺×·—…' if ord(c) not in TTFont(path).getBestCmap()]
    if missing:
        raise ValueError(f'Missing font glyphs: {path.name}: {missing}')

themes = json.loads((RES / 'themes.json').read_text(encoding='utf-8-sig'))


def same_text(path, text):
    return path.exists() and path.read_bytes().decode('utf-8-sig').replace('\r\n', '\n') == text


for planet, data in themes.items():
    slug = IDS[planet]
    text = '.theme-' + slug + ' {\n' + ''.join(f'    --{key}: {value};\n' for key, value in data['cssVars'].items()
                                             if not isinstance(value, (int, float))) + '}\n'
    path = STYLES / f'Theme.{slug.title()}.uss'
    if not same_text(path, text):
        stale.append(path.relative_to(ROOT).as_posix())
        if args.write_uss:
            path.write_text(text, encoding='utf-8')

if args.legacy_icons:
    html = (OLD_HANDOFF / 'reference/Galaxy101_AnaMenu_1a.dc.html').read_text(encoding='utf-8').split('<!-- 1b -->')[0]
    svgs = re.findall(r'<svg\b[\s\S]*?</svg>', html)
    icons = {}
    actions = {'settings': 'openSettings', 'shop': 'openShop', 'friends': 'openFriends', 'leader': 'openLeader',
               'daily': 'openDaily', 'rules': 'openRules', 'share': 'openShare', 'edit': 'openProfile', 'add': 'openAddFriend'}
    for name, action in actions.items():
        marker = re.search(r'onClick="\{\{ ' + action + r' \}\}"', html)
        if marker:
            hit = re.search(r'<svg\b[\s\S]*?</svg>', html[marker.end():])
            if hit:
                icons[name] = hit.group()
    signatures = {'settings': 'M19.4 15', 'close': 'M6 6l12 12', 'tick': 'M5 12.5', 'share': 'M8.2 10.8', 'edit': 'M4 20h4',
                  'search': 'M20 20l-4-4', 'copy': 'M16 8V5', 'link': 'M10 14a4', 'clock': 'M12 9v4', 'lock': 'M8 11V8',
                  'logout': 'M14 4h4', 'delete': 'M4 7h16', 'crown': 'M3 20 1 5', 'profile': 'M4 21a8'}
    for name, signature in signatures.items():
        icons[name] = next(svg for svg in svgs if signature in svg)
    for name, svg in icons.items():
        (RES / 'Icons' / (name + '.txt')).write_text(svg, encoding='utf-8')


def rgba(hexcolor):
    h = hexcolor.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4)) + (255,)


font = ImageFont.truetype(str(FONTS / 'Montserrat-700.ttf'), 128)
word = 'GALAXY'
spacing = 14
textwidth = round(sum(font.getlength(c) for c in word) + spacing * (len(word) - 1))
for planet, data in themes.items():
    colors = data['cssVars']
    size = (textwidth + 160, 308)
    mask = Image.new('L', size)
    draw = ImageDraw.Draw(mask)
    x = 80
    for letter in word:
        draw.text((x, 104), letter, font=font, fill=255, anchor='lt')
        x += font.getlength(letter) + spacing
    result = Image.new('RGBA', size)

    def layer(color, blur=0, dy=0):
        m = mask.filter(ImageFilter.GaussianBlur(blur)) if blur else mask
        image = Image.new('RGBA', size, color)
        image.putalpha(m.point(lambda value: round(value * color[3] / 255)))
        if dy:
            shifted = Image.new('RGBA', size)
            shifted.alpha_composite(image, (0, dy))
            image = shifted
        result.alpha_composite(image)

    layer(rgba(colors['a1'])[:3] + (115,), 52)
    layer((0, 0, 0, 128), 36, 20)
    layer(rgba(colors['lS']), 0, 10)
    gradient = Image.new('RGBA', size)
    gd = ImageDraw.Draw(gradient)
    stops = [rgba(colors[k]) for k in ['lg1', 'a1', 'a2']]
    for y in range(size[1]):
        t = max(0, min(1, (y - 104) / 96))
        a, b, u = (stops[0], stops[1], t / .45) if t < .45 else (stops[1], stops[2], (t - .45) / .55)
        gd.line((0, y, size[0], y), fill=tuple(round(a[i] + (b[i] - a[i]) * u) for i in range(4)))
    gradient.putalpha(mask)
    result.alpha_composite(gradient)
    path = RES / 'Artwork' / f'logo-{IDS[planet]}.png'
    if args.logos:
        result.save(path)
    else:
        current = Image.open(path).convert('RGBA') if path.exists() else None
        if current is None or current.size != result.size or current.tobytes() != result.tobytes():
            stale.append(path.relative_to(ROOT).as_posix())

print(f'Fonts: {len(WEIGHTS)} static weights with Turkish glyphs.')
print('Out of date (' + ('written' if args.write_uss or args.logos else 'not written; pass --write-uss / --logos') + '):')
for item in stale:
    print('  ' + item)
if not stale:
    print('  none')
