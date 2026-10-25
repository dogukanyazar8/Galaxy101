"""Lane D difference matrix: v4 package data vs the project before v4 (base commit) vs the imported result.

Writes docs/v4/matris/hat-d.md (Turkish matrix rows for themes, fonts, icons, league emblems, cursors, sound and a
strings summary with the changed/missing keys) and docs/v4/hat-d/strings_compare.csv (every string key with its status).
"Projede ne vardı" is read from git (--base, default 753287e) so the matrix stays reproducible after the import.

  uv run --no-project --with fonttools python tools/v4/hat_d_matrix.py [--base 753287e]
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from collections import Counter, OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PKG = ROOT / 'Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4'
RESP = 'Assets/Galaxy101/Resources/Galaxy101'
RES = ROOT / RESP
OUT = ROOT / 'docs/v4/matris/hat-d.md'
CMP = ROOT / 'docs/v4/hat-d/strings_compare.csv'
REPORT = ROOT / 'docs/v4/hat-d/import_report.json'

parser = argparse.ArgumentParser()
parser.add_argument('--base', default='753287e')
args = parser.parse_args()
sys.stdout.reconfigure(encoding='utf-8')
BASE = args.base


# ---------------------------------------------------------------- helpers

def git_bytes(path: str) -> bytes | None:
    r = subprocess.run(['git', 'show', f'{BASE}:{path}'], cwd=ROOT, capture_output=True)
    if r.returncode != 0:
        return None
    data = r.stdout
    if data.startswith(b'version https://git-lfs'):
        data = subprocess.run(['git', 'lfs', 'smudge'], cwd=ROOT, input=data, capture_output=True, check=True).stdout
    return data


def git_text(path: str) -> str | None:
    data = git_bytes(path)
    return None if data is None else data.decode('utf-8-sig').replace('\r\n', '\n')


def git_ls(prefix: str) -> list[str]:
    r = subprocess.run(['git', 'ls-tree', '-r', '--name-only', BASE, prefix], cwd=ROOT, capture_output=True, text=True,
                       check=True)
    return [x for x in r.stdout.splitlines() if x]


def read_csv_text(text: str) -> list[list[str]]:
    return list(csv.reader(io.StringIO(text)))


def cell(value) -> str:
    if value is None:
        return '—'
    if isinstance(value, (list, tuple)):
        value = ', '.join(str(v) for v in value)
    text = str(value)
    return text.replace('|', '\\|').replace('\r\n', '\n').replace('\n', '<br>') or '""'


def code(value) -> str:
    return '—' if value is None else '`' + cell(value).replace('`', "'") + '`'


def table(rows: list[list], header=('Kimlik', 'Pakette ne diyor (kaynak)', 'Projede ne vardı (dosya)',
                                     'Durum (var / farklı / eksik / pakette yok)', 'v4 sonrası')) -> str:
    out = ['| ' + ' | '.join(header) + ' |', '|' + '---|' * len(header)]
    out += ['| ' + ' | '.join(r) + ' |' for r in rows]
    return '\n'.join(out)


def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


report = json.loads(REPORT.read_text(encoding='utf-8'))
md: list[str] = []
counts: OrderedDict = OrderedDict()

# ---------------------------------------------------------------- themes

pk_themes = json.loads((PKG / 'assets/themes.json').read_text(encoding='utf-8'))
old_themes = json.loads(git_text(f'{RESP}/themes.json'))
new_themes = json.loads((RES / 'themes.json').read_text(encoding='utf-8-sig'))
old_table = json.loads(git_text(f'{RESP}/Table/table_content.json'))['planets_table']
pk_strings = json.loads((PKG / 'strings/strings.json').read_text(encoding='utf-8'))
pk_by_key = {e['key']: e for e in pk_strings['tables']}

theme_rows = []
st = Counter()


def theme_row(ident, pkg_value, pkg_src, old_value, old_src, new_value, new_src):
    status = 'var' if pkg_value == old_value else ('eksik' if old_value is None else 'farklı')
    if pkg_value is None:
        status = 'pakette yok'
    st[status] += 1
    after = f'{code(new_value)} ({new_src})' if new_src else '—'
    theme_rows.append([code(ident), f'{code(pkg_value)} ({pkg_src})', f'{code(old_value)} ({old_src})' if old_src else '—',
                       status, after])


for planet in pk_themes['order']:
    p = pk_themes['planets'][planet]
    o = old_themes.get(planet, {})
    n = new_themes[planet]
    css_o, css_n = o.get('cssVars', {}), n['cssVars']
    src = f'themes.json planets.{planet}'
    pn, pd = pk_by_key[f'menu.STR.pn.{planet}'], pk_by_key[f'menu.STR.pd.{planet}']
    theme_row(f'tema.{planet}.ad.tr', pn['tr'], 'strings.json menu.STR.pn', o.get('name_tr'), 'themes.json name_tr', n['name_tr'], 'name_tr')
    theme_row(f'tema.{planet}.ad.en', pn['en'], 'strings.json menu.STR.pn', o.get('name_en'), 'themes.json name_en', n['name_en'], 'name_en')
    theme_row(f'tema.{planet}.menu.desc', p['menu']['desc'], f'{src}.menu.desc', o.get('desc_tr'), 'themes.json desc_tr', n['desc_tr'], 'desc_tr')
    theme_row(f'tema.{planet}.menu.desc.en', pd['en'], 'strings.json menu.STR.pd', o.get('desc_en'), 'themes.json desc_en', n['desc_en'], 'desc_en')
    theme_row(f'tema.{planet}.menu.orb', p['menu']['orb'], f'{src}.menu.orb', o.get('pickerOrb'), 'themes.json pickerOrb', n['pickerOrb'], 'pickerOrb')
    for k, v in p['ui'].items():
        if k == 'pf':
            theme_row(f'tema.{planet}.ui.pf', v, f'{src}.ui.pf', o.get('planetFlattening'), 'themes.json planetFlattening', n['planetFlattening'], 'planetFlattening')
        elif k == 'ro':
            theme_row(f'tema.{planet}.ui.ro', v, f'{src}.ui.ro (yükleme ekranı halka opaklığı)', None, 'themes.json (cssVars.ro = menü halkası, ayrı değer)', n['loadingRingOpacity'], 'loadingRingOpacity')
        else:
            theme_row(f'tema.{planet}.ui.{k}', v, f'{src}.ui.{k}', css_o.get(k), f'themes.json cssVars.{k}', css_n.get(k), f'cssVars.{k}')
    for k, v in p['rings'].items():
        theme_row(f'tema.{planet}.rings.{k}', v, f'{src}.rings.{k}', css_o.get(k), f'themes.json cssVars.{k}', css_n.get(k), f'cssVars.{k}')
    theme_row(f'tema.{planet}.atmosphere', p['atmosphere'], f'{src}.atmosphere', css_o.get('atm'), 'themes.json cssVars.atm', css_n.get('atm'), 'cssVars.atm')
    for k, v in p['logo'].items():
        theme_row(f'tema.{planet}.logo.{k}', v, f'{src}.logo.{k}', css_o.get(k), f'themes.json cssVars.{k}', css_n.get(k), f'cssVars.{k}')
    for k, v in p['table3d'].items():
        theme_row(f'tema.{planet}.table3d.{k}', v, f'{src}.table3d.{k}', old_table.get(planet, {}).get(k),
                  f'Table/table_content.json planets_table.{planet}.{k}', v, f'table_content planets_table.{k}')
for k, text in pk_themes['vars'].items():
    if k in ('fL', 'fH', 'fB'):
        theme_rows.append([code(f'tema.vars.{k}'), f'{cell(text)} (themes.json vars.{k}; değer gezegenlerde yok, fonts.json used.*.var)',
                           '— (USS --fL/--fH/--fB yok; MenuView Font("Montserrat-700"/"Baloo2-700"))', 'farklı',
                           'değişmedi; fontlar fonts.json rollerine göre (bölüm Fontlar)'])
        st['farklı'] += 1
theme_rows.append([code('tema.transition'), f"{cell(pk_themes['transition'])} (themes.json transition)", '— (themes.json)',
                   'eksik', 'themes.json gezegen sözlüğünde yer yok; süre ve eğri Faz 2 animasyonunda (anim/easings.json cssPlanetMove)'])
st['eksik'] += 1
counts['Temalar'] = st

# ---------------------------------------------------------------- fonts

from fontTools.ttLib import TTFont  # noqa: E402

pk_fonts = json.loads((PKG / 'assets/fonts.json').read_text(encoding='utf-8'))
font_report = {r['package']: r for r in report['fonts']['files']}


def font_info(data: bytes) -> dict:
    f = TTFont(io.BytesIO(data))
    name = f['name']
    return dict(family=name.getDebugName(16) or name.getDebugName(1), sub=name.getDebugName(17) or name.getDebugName(2),
                version=(name.getDebugName(5) or '').split(';')[0], weight=f['OS/2'].usWeightClass,
                glyphs=len(f.getGlyphOrder()), variable='fvar' in f, sha=sha(data)[:12], font=f)


def metric_compare(a, b) -> str:
    sample = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789çğıİöşüÇĞÖŞÜ₺×·—…%.,:!?'
    ca, cb = a.getBestCmap(), b.getBestCmap()
    adv = outl = 0
    for ch in sample:
        u = ord(ch)
        if u not in ca or u not in cb:
            continue
        if a['hmtx'][ca[u]][0] != b['hmtx'][cb[u]][0]:
            adv += 1
        ga, gb = a['glyf'][ca[u]], b['glyf'][cb[u]]
        pa = list(ga.getCoordinates(a['glyf'])[0]) if ga.numberOfContours else []
        pb = list(gb.getCoordinates(b['glyf'])[0]) if gb.numberOfContours else []
        if pa != pb:
            outl += 1
    return f'{len(sample)} örnek karakterde ilerleme farkı {adv}, çizim farkı {outl}'


font_rows = []
st = Counter()
for family, files in pk_fonts['files'].items():
    for name in files:
        data = (PKG / 'assets' / name).read_bytes()
        r = font_report[name]
        if family == 'license':
            old = git_bytes(r['project'])
            def lf(b: bytes) -> bytes:  # text=auto: the repository blob is LF, checkouts may be CRLF
                return b.replace(b'\r\n', b'\n')
            status = 'var' if old is not None and sha(lf(old)) == sha(lf(data)) else ('eksik' if old is None else 'farklı')
            st[status] += 1
            font_rows.append([code(name), f'OFL 1.1 metni, sha256 (LF) {sha(lf(data))[:12]} (fonts.json files.license)',
                              f"{code(r['project'])} sha256 (LF) {sha(lf(old))[:12] if old else '—'}", status,
                              f"{code(r['project'])} (aynı)" if status == 'var' else code(r['project'])])
            continue
        info = font_info(data)
        pkg_desc = f"{info['family']} {info['weight']}, {info['version']}, {info['glyphs']} glif, sha {info['sha']} (fonts.json files.{family})"
        if r.get('variable'):
            st['eksik'] += 1
            pinned = {'Montserrat': 'Montserrat-Variable.ttf sha 0f7b311b… (PINNED)', 'Baloo 2': 'Baloo2-Variable.ttf sha d47a6852… (PINNED)'}
            font_rows.append([code(name), pkg_desc + ', değişken font',
                              ('— (dosya projede yok; eski sabit ağırlıklar bu dosyadan kesilmişti: scripts/import-galaxy-ui-assets.py '
                               + pinned[family] + ')') if family in pinned else '— (dosya projede yok; Nunito-900.ttf aynı glif sayılı bir değişken fonttan kesilmiş)',
                              'eksik', 'kopyalanmadı: istemci sabit ağırlıklı TTF + FontAsset kullanır (fonts.json license notu)'])
            continue
        old = git_bytes(r['project'])
        if old is None:
            status = 'eksik'
            old_desc = '— (yok)'
        else:
            oi = font_info(old)
            status = 'var' if oi['sha'] == info['sha'] else 'farklı'
            old_desc = (f"{code(r['project'])}: ad tablosu \"{oi['family']} {oi['sub']}\", ağırlık {oi['weight']}, {oi['version']}, "
                        f"{oi['glyphs']} glif, sha {oi['sha']}; {metric_compare(oi['font'], info['font'])}")
        st[status] += 1
        font_rows.append([code(name), pkg_desc, old_desc, status,
                          f"{code(r['project'])} ← paket dosyası (.meta/GUID korunur); FontAsset yeniden üretilecek (RAPOR §Unity)"
                          if status != 'var' else code(r['project'])])
for role, spec in pk_fonts['used'].items():
    weights = '/'.join(str(w) for w in spec['weights'])
    have = [w for w in spec['weights'] if git_bytes(f"{RESP}/Fonts/{spec['family'].replace(' ', '')}-{w}.ttf") is not None]
    status = 'var' if len(have) == len(spec['weights']) else ('eksik' if not have else 'farklı')
    st[status] += 1
    font_rows.append([code(f'font.rol.{role}'), f"{spec['family']} {weights}{' · ' + spec['var'] if spec.get('var') else ''} (fonts.json used.{role})",
                      f"{spec['family']} ağırlıkları projede: {have or 'yok'} (Fonts/)", status,
                      'tüm ağırlıklar Fonts/ altında' + (' (Nunito-700 yeni)' if role == 'versionTag' else '')])
old_nunito = font_info(git_bytes(f'{RESP}/Fonts/Nunito-900.ttf'))
st['pakette yok'] += 1
font_rows.append([code('Fonts/Nunito-900.ttf'), '— (fonts.json yalnız Nunito 700 · versionTag; 900 yalnız "optional" URL listesinde)',
                  f"Nunito 900 sha {old_nunito['sha']} — MenuView.Main.cs indirim rozeti", 'pakette yok',
                  'dokunulmadı (kullanan kod Faz 2\'de v4 menüye göre değişecek)'])
counts['Fontlar'] = st

# ---------------------------------------------------------------- icons

pk_icons = json.loads((PKG / 'assets/icons.json').read_text(encoding='utf-8'))
new_icons = json.loads((RES / 'Icons/v4/icons.json').read_text(encoding='utf-8'))['icons']


def strip(svg: str) -> str:
    return re.sub(r'<metadata>.*?</metadata>', '', svg, flags=re.S).replace(' xmlns:c2pa="http://c2pa.org/manifest"', '')


GEOM = ('d', 'cx', 'cy', 'r', 'rx', 'ry', 'x', 'y', 'width', 'height', 'points', 'x1', 'y1', 'x2', 'y2')


def signature(svg: str):
    root = ET.fromstring(strip(svg))
    shapes = []
    for el in root.iter():
        tag = el.tag.split('}')[-1]
        if tag in ('svg', 'metadata', 'defs', 'stop', 'linearGradient', 'radialGradient'):
            continue
        shapes.append((tag,) + tuple((k, re.sub(r'\s+', ' ', el.get(k, '')).strip()) for k in GEOM if el.get(k)))
    return root.get('viewBox'), tuple(sorted(shapes))


def paint(svg: str):
    root = ET.fromstring(strip(svg))
    return OrderedDict((k, root.get(k)) for k in ('stroke-width', 'stroke-linecap', 'stroke-linejoin') if root.get(k))


legacy = {}
for path in git_ls(f'{RESP}/Icons/'):
    if path.endswith('.txt'):
        text = git_text(path)
        legacy[Path(path).stem] = (signature(text), paint(text))
matched_legacy = set()
icon_rows = []
st = Counter()
for item in pk_icons['icons']:
    name = Path(item['file']).stem
    svg = (PKG / 'assets' / item['file']).read_text(encoding='utf-8')
    sig, pnt = signature(svg), paint(svg)
    hits = [k for k, (s, _) in legacy.items() if s == sig]
    if hits:
        same = [k for k in hits if legacy[k][1] == pnt]
        status = 'var' if same else 'farklı'
        old = ', '.join(sorted(hits))
        matched_legacy.update(hits)
        old_desc = f"{code(old)} (Icons/*.txt, aynı geometri" + ('' if same else f"; stroke {', '.join(str(legacy[k][1].get('stroke-width')) for k in hits)}") + ')'
    else:
        status, old_desc = 'eksik', '—'
    st[status] += 1
    n = new_icons[name]
    used = ', '.join(item['usedBy'][:3]) + (f' +{len(item["usedBy"]) - 3}' if len(item['usedBy']) > 3 else '') if item['usedBy'] else 'usedBy boş'
    compat = 'GalaxyIcon çizer' if n['galaxyIcon']['ok'] else 'GalaxyIcon çizemez: ' + ', '.join(n['galaxyIcon']['issues']) + ' → SVG içe aktarma'
    icon_rows.append([code(name), f"viewBox {item['viewBox']}, stroke {item.get('stroke')}; {cell(used)} ({item['file']})", old_desc, status,
                      f"{code('Icons/v4/' + name + '.txt')} + {code('Icons/v4/svg/' + name + '.svg')}; {compat}"])
for k in sorted(set(legacy) - matched_legacy):
    st['pakette yok'] += 1
    icon_rows.append([code(f'Icons/{k}.txt'), '— (69 paket ikonundan hiçbirinin geometrisi aynı değil)', f'{code(k)} (Icons/{k}.txt)',
                      'pakette yok', 'dokunulmadı; GalaxyIcon çağıranlar bu adı kullanıyor (Faz 2 v4 ikonlarına geçer)'])
counts['İkonlar'] = st

# ---------------------------------------------------------------- league emblems

ranks = json.loads((RES / 'Rank/ranks.json').read_text(encoding='utf-8'))
old_rank_files = [p for p in git_ls(RESP) if '/Rank' in p or 'rank' in Path(p).name.lower()]
rank_rows = []
st = Counter()
for name, e in ranks['emblems'].items():
    st['eksik'] += 1
    what = ('madalya' if e['kind'] == 'medal' else 'rozet') + f" {e['league']}" + (f" {ranks['roman'][e['tier']]}" if e['tier'] else '') + f", {e['size']} px"
    if e.get('divisionMarks'):
        what += f", kademe taşı {e['divisionMarks']}"
    rank_rows.append([code(name), f'{what} (assets/rank/{name}.svg; ranks.json)',
                      '— (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond)', 'eksik',
                      f"{code(e['png'] + '.png')} + {code(e['png2x'] + '.png')} + {code(e['svg'] + '.svg')}"])
counts['Lig rozetleri'] = st

# ---------------------------------------------------------------- cursors

cursors = json.loads((RES / 'Cursors/cursors.json').read_text(encoding='utf-8'))
cursor_rows = []
st = Counter()
for name, c in cursors['cursors'].items():
    st['eksik'] += 1
    cursor_rows.append([code(name), f"{c['size']} px, sıcak nokta {c['hotspot']} (assets/cursors/{name}.svg; cursors.json hotspots.{name})",
                        '— (projede özel imleç yok; işletim sistemi imleci)', 'eksik',
                        f"{code(c['png'] + '.png')} (Texture Type Cursor) + {code(c['png2x'] + '.png')} sıcak nokta {c['hotspot2x']}; Cursors/cursors.json"])
counts['İmleçler'] = st

# ---------------------------------------------------------------- sound

events = json.loads((PKG / 'sound/events.json').read_text(encoding='utf-8'))
sound = json.loads((RES / 'Audio/sound_events.json').read_text(encoding='utf-8'))
# Pre-v4 table sound: Okey101 TableAudio plays three recorded domino clips (scripts/prepare-table-audio.py).
LEGACY_SOUND = {'tile.draw': 'draw.wav (TableAudio Draw)', 'tile.discard': 'discard.wav (TableAudio Discard)',
                'meld.lay': 'place.wav (TableAudio GroupsOpened)', 'meld.add': 'place.wav (TableAudio GroupAdded/GroupExtended)',
                'okey.swap': 'place.wav (TableAudio JokerSwapped)'}
sound_rows = []
st = Counter()
for e in events['events']:
    old = LEGACY_SOUND.get(e['id'])
    status = 'farklı' if old else 'eksik'
    st[status] += 1
    s = sound['events'][e['id']]
    sound_rows.append([code(e['id']), f"{e['cat']} · öncelik {e['prio']} · poly {e['poly']} · {e['ms']} ms · {e['wav']} — {cell(e['trigger'])} (events.json)",
                       f"{old} — Assets/Okey101/Art/Audio, kayıttan kesilmiş domino sesi, tek kanal, ses 0.38" if old else '—',
                       status, f"{code(s['clip'] + '.wav')} ({s['wavSeconds']} sn) + Audio/sound_events.json events.{e['id']}"])
for m in events['music']:
    st['eksik'] += 1
    s = sound['music'][m['id']]
    sound_rows.append([code(m['id']), f"{m['file']}, döngü {m['seconds']} sn, {cell(m['where'])} (events.json music)", '— (müzik yok)', 'eksik',
                       f"{code(s['clip'] + '.wav')} ({s['wavSeconds']} sn)"])
old_content = json.loads(git_text(f'{RESP}/content.json'))['settingsDefaults']
old_tc = json.loads(git_text(f'{RESP}/Table/table_content.json'))['settingsDefaults']
for k, v in sound['volume']['defaults'].items():
    o1, o2 = old_content.get(k), old_tc.get(k)
    status = 'var' if o1 == v and o2 == v else ('eksik' if o1 is None and o2 is None else 'farklı')
    st[status] += 1
    sound_rows.append([code(f'ses.ayar.{k}'), f'varsayılan {v} (g101-sound.js S(); events.json volume)',
                       f'content.json settingsDefaults.{k} = {cell(o1)} · Table/table_content.json settingsDefaults.{k} = {cell(o2)}',
                       status, f'sound_events.json volume.defaults.{k} = {v}; ayar dosyalarına dokunulmadı (EK-402)'])
rules = [('ses.kural.kanal', 'efekt kategorileri sfx, müzik music kaydırıcısı; tile → tileSnd, chat → chatSnd ve notifSnd (g101-sound.js CAT, gainFor)', 'channels, toggles'),
         ('ses.kural.kazanç', events['volume'], 'volume.rule'),
         ('ses.kural.kısma', events['mixing']['duck'], 'mixing.duck'),
         ('ses.kural.poly', events['mixing']['poly'], 'events[].poly'),
         ('ses.kural.geçiş', events['mixing']['crossfade'] + ' (g101-sound.js: eski 1,2 sn söner, yeni 1,5 sn açılır)', 'mixing.crossfadeOutSeconds, fadeInSeconds'),
         ('ses.kural.eşzaman', events['mixing']['sync'], 'mixing.sync'),
         ('ses.kural.rakip', 'rakibin taş çekişi %70 ses (G101-Masa sfx())', 'volume.opponentDrawVolume'),
         ('ses.kural.motor', 'motor olayı → ses tablosu (sound/README.md)', 'engineEvents'),
         ('ses.kural.ekran', 'masada masa müziği, diğer ekranlarda menü müziği; açılış dışındaki her ekran geçişinde ui.screen', 'screenMusic, screenChange')]
for ident, text, key in rules:
    st['eksik'] += 1
    sound_rows.append([code(ident), f'{cell(text)} (events.json / README.md / g101-sound.js)',
                       '— (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok)', 'eksik', f'Audio/sound_events.json {key}'])
counts['Ses'] = st

# ---------------------------------------------------------------- strings

PAIR_RE = re.compile(r'\b(?:L|Text)\(\s*"(?P<tr>(?:[^"\\]|\\.)*)"\s*,\s*"(?P<en>(?:[^"\\]|\\.)*)"')


def unescape(s: str) -> str:
    return s.encode('utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8') if '\\' in s else s


pairs: dict[tuple[str, str], str] = {}
by_tr: dict[str, list[tuple[str, str]]] = {}
by_en: dict[str, list[tuple[str, str]]] = {}


def add_pair(tr, en, src):
    if not isinstance(tr, str) or not isinstance(en, str) or not tr or not en:
        return
    if (tr, en) not in pairs:
        pairs[(tr, en)] = src
        by_tr.setdefault(tr, []).append((en, src))
        by_en.setdefault(en, []).append((tr, src))


def visit(token, src):
    """LocalizationCatalog.Visit: tr/en pairs, *_tr/*_en siblings and "st": [tr, en]."""
    if isinstance(token, dict):
        def pair(a, b):
            if isinstance(a, str) and isinstance(b, str):
                add_pair(a, b, src)
            elif isinstance(a, list) and isinstance(b, list):
                for x, y in zip(a, b):
                    pair(x, y)
            elif isinstance(a, dict) and isinstance(b, dict):
                for k in a:
                    pair(a[k], b.get(k))
        pair(token.get('tr'), token.get('en'))
        for k, v in token.items():
            if k.endswith('_tr'):
                pair(v, token.get(k[:-3] + '_en'))
        if isinstance(token.get('st'), list) and len(token['st']) == 2:
            pair(token['st'][0], token['st'][1])
        for v in token.values():
            visit(v, src)
    elif isinstance(token, list):
        for v in token:
            visit(v, src)


for name in ('strings_tr_en', 'additional_strings'):
    rows = read_csv_text(git_text(f'{RESP}/{name}.csv'))[1:]
    for r in rows:
        if r[0] == 'inline-list':
            for tr, en in zip(r[1].split("', '"), r[2].split("', '")):
                add_pair(tr.strip("'"), en.strip("'"), f'{name}.csv inline-list')
        else:
            add_pair(r[1], r[2], f'{name}.csv {r[0]}')
for name in ('content.json', 'themes.json', 'supplemental.json', 'Table/table_content.json'):
    visit(json.loads(git_text(f'{RESP}/{name}')), name)
for cs in sorted((ROOT / 'Assets/Galaxy101').rglob('*.cs')):
    if '/Tests/' in cs.as_posix():
        continue
    for m in PAIR_RE.finditer(cs.read_text(encoding='utf-8')):
        add_pair(unescape(m.group('tr')), unescape(m.group('en')), cs.relative_to(ROOT / 'Assets/Galaxy101').as_posix())
table_keys = {}
for name in ('table_strings', 'table_strings_ek', 'table_strings_extra'):
    for r in read_csv_text(git_text(f'{RESP}/Table/Strings/{name}.csv'))[1:]:
        table_keys[r[0]] = (r[1], r[2], f'Table/Strings/{name}.csv')
        add_pair(r[1], r[2], f'Table/Strings/{name}.csv {r[0]}')

v4 = json.loads((RES / 'Strings/strings_v4.json').read_text(encoding='utf-8'))
ts = report['strings']['tableStrings']
table_updated = {u['key'] for u in ts['updated']}
table_shift = {u['key']: u['movedTo'] for u in ts['keptListShift']}
table_conditional = {u['key'] for u in ts['keptConditional']}
table_added = set(ts['added'])
compare = []
for key, s in v4['strings'].items():
    pkg_key = s.get('pkgKey', key)
    group = 'error' if key.startswith('err.') or (pkg_key.startswith('table.STR.engErr.') and key not in {e['key'] for e in pk_strings['tables']}) \
        else ('inline' if '.inline.' in pkg_key else 'table')
    tr, en = s['tr'], s['en']
    old_tr = old_en = src = None
    if not tr or not en:
        status, how = 'boş (pakette bilinçli)', 'paket'
    elif pkg_key.startswith('table.STR.') and pkg_key[10:] in table_keys:
        old_tr, old_en, src = table_keys[pkg_key[10:]]
        src += ' ' + pkg_key[10:]
        status = 'var' if (old_tr, old_en) == (tr, en) else 'farklı'
        how = 'anahtar'
    elif (tr, en) in pairs:
        status, how, src, old_tr, old_en = 'var', 'metin', pairs[(tr, en)], tr, en
    elif tr in by_tr:
        old_en, src = by_tr[tr][0]
        old_tr, status, how = tr, 'farklı', 'TR metni'
    elif en in by_en:
        old_tr, src = by_en[en][0]
        old_en, status, how = en, 'farklı', 'EN metni'
    else:
        status, how = 'eksik', '—'
    if pkg_key.startswith('table.STR.'):
        k = pkg_key[10:]
        after = ('Table/Strings güncellendi' if k in table_updated else
                 f'Table/Strings eski metin kaldı (dizi kaydı → {table_shift[k]})' if k in table_shift else
                 'Table/Strings eski metin kaldı (koşullu fonksiyon, EK-412)' if k in table_conditional else
                 'Table/Strings eklendi' if k in table_added else 'Table/Strings aynı')
        after = 'strings_v4.csv; ' + after
    else:
        after = 'strings_v4.csv'
    compare.append(OrderedDict(key=key, pkgKey=pkg_key, group=group, status=status, match=how, tr=tr, en=en,
                               projectTr=old_tr, projectEn=old_en, projectSource=src, after=after))

CMP.parent.mkdir(parents=True, exist_ok=True)
buf = io.StringIO()
w = csv.writer(buf, quoting=csv.QUOTE_ALL, lineterminator='\n')
w.writerow(list(compare[0].keys()))
for c in compare:
    w.writerow(['' if v is None else v for v in c.values()])
CMP.write_bytes(b'\xef\xbb\xbf' + buf.getvalue().encode('utf-8'))

sc = Counter((c['group'], c['status']) for c in compare)
groups = ['table', 'inline', 'error']
statuses = ['var', 'farklı', 'eksik', 'boş (pakette bilinçli)']
summary_rows = []
for g in groups:
    total = sum(v for (gg, _), v in sc.items() if gg == g)
    summary_rows.append([g, str(total)] + [str(sc[(g, s)]) for s in statuses])
summary_rows.append(['toplam', str(len(compare))] + [str(sum(sc[(g, s)] for g in groups)) for s in statuses])
counts['Metinler'] = Counter({s: sum(sc[(g, s)] for g in groups) for s in statuses})
project_only_table = ts['projectOnly']
counts['Metinler']['pakette yok (masa anahtarı)'] = len(project_only_table)

# ---------------------------------------------------------------- write

def section(title, intro, rows):
    md.append(f'## {title}\n')
    if intro:
        md.append(intro + '\n')
    md.append(table(rows) + '\n')


md.append('# Fark matrisi · Hat D (veri varlıkları)\n')
md.append(f'Paket: `Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/` (aşağıda dosya adları buna göre). '
          f'"Projede ne vardı" v4 öncesi taban commit `{BASE}` (`Assets/Galaxy101/Resources/Galaxy101/` altı, aşağıda kısaltılmış). '
          f'"v4 sonrası" bu dalın ürettiği dosya. Üretici: `tools/v4/hat_d_matrix.py` (elle düzenleme yok; yeniden üret). '
          f'Kayıtlar: `scripts/import-v4-data.py` → `docs/v4/hat-d/import_report.json`.\n')
md.append('## Özet\n')
md.append(table([[k, ', '.join(f'{s} {n}' for s, n in v.items() if n)] for k, v in counts.items()], header=('Bölüm', 'Durum sayıları')) + '\n')
section('Temalar', 'Her gezegen × her anahtar. `ui.ro` (yükleme ekranındaki halka opaklığı) ile `rings.ro` (menü gezegen halkası) pakette aynı adlı iki ayrı değer; projede yalnız ikincisi vardı (`cssVars.ro`), ilki `loadingRingOpacity` olarak eklendi.', theme_rows)
section('Fontlar', None, font_rows)
section('İkonlar', 'Eşleme geometriyle (yol/şekil öznitelikleri aynı): aynı geometri + aynı çizgi → var; aynı geometri, farklı çizgi kalınlığı → farklı. '
        'Paket ikonları kullanım yerine göre ayrı dosya (aynı çizim farklı `stroke-width` ile birden çok kez var).', icon_rows)
section('Lig rozetleri', 'Dosya adı `rank/<lig>[_<I|II|III>]_<24|52|160>.svg` · `medal_<lig>_<16|24>.svg` (ranks.json files). '
        'Kademe taşı sayısı g101-rank.js `emblem()`: n = 4 − kademe.', rank_rows)
section('İmleçler', None, cursor_rows)
section('Ses', None, sound_rows)
md.append('## Metinler\n')
md.append(f"Paket: {pk_strings['counts']['tables']} tablo + {pk_strings['counts']['inline']} satır içi + {report['strings']['errorTexts']} hata metni "
          f"({report['strings']['errorTotal']} hata kodu; {report['strings']['errorTotal'] - report['strings']['errorTexts']} sessiz/resync, metinsiz). "
          f"Tekil anahtar: {len(compare)} (23 tekrarlı satır içi anahtar #2/#3 aldı; aynı metni gösteren 24 hata kodu oyun metninin anahtarını kullanır). "
          'Karşılaştırma: `table.STR.<k>` için masa anahtarı `<k>` (Table/Strings/*.csv); diğerleri için TR+EN metin çifti '
          '(strings_tr_en.csv, additional_strings.csv, content/themes/supplemental/table_content.json ve C# `L("tr","en")` / `Text("tr","en")` çağrıları). '
          'Menü yerelleştirmesi anahtar değil Türkçe metinle çalıştığı için (LocalizationBridge) metin eşleşmesi kullanıldı. '
          'Tüm anahtarlar ve durumları: `docs/v4/hat-d/strings_compare.csv`.\n')
md.append(table(summary_rows, header=('Grup', 'Anahtar', 'var', 'farklı', 'eksik (yeni)', 'boş (pakette bilinçli)')) + '\n')
md.append(f"Masa HUD tabloları (Table/Strings): pakette {ts['packageKeys']} `table.STR` anahtarı; projede {ts['present']} vardı; "
          f"{len(ts['updated'])} metin güncellendi, {len(ts['keptListShift'])} dizi kaydı ve {len(ts['keptConditional'])} koşullu metin eski haliyle kaldı, "
          f"{len(ts['keptPlaceholderMismatch'])} yer tutucu uyuşmazlığı, {len(ts['added'])} anahtar eklendi; "
          f"{len(project_only_table)} masa anahtarı pakette yok.\n")
md.append('### Ek A · farklı metinler\n')
diff_rows = [[code(c['key']), f"TR {cell(c['tr'])}<br>EN {cell(c['en'])}", f"TR {cell(c['projectTr'])}<br>EN {cell(c['projectEn'])} ({cell(c['projectSource'])}; eşleşme: {c['match']})",
              'farklı', cell(c['after'])] for c in compare if c['status'] == 'farklı']
md.append(table(diff_rows) + '\n')
md.append('### Ek B · boş metinler (pakette bilinçli boş)\n')
md.append(table([[code(c['key']), 'TR "" · EN "" (strings.json tables; G101-Menu.dc.html:1033 RULES örneği "")', '—', c['status'], 'strings_v4.csv (boş; örneği olmayan kural bölümü)'] for c in compare
                 if c['status'] == 'boş (pakette bilinçli)']) + '\n')
md.append('### Ek C · masa anahtarları: pakette yok\n')
md.append('Projenin `Table/Strings/*.csv` dosyalarında olup pakette `table.STR.<k>` karşılığı olmayan anahtarlar (silinmedi; HUD kodu kullanıyor).\n')
md.append(', '.join(code(k) for k in project_only_table) + '\n')
md.append('### Ek D · masa metinleri: eski metin kaldı\n')
md.append('Dizi kayıtları HUD\'da konumla okunur (`TableHud.Windows.cs` `views.{i}`); koşullu fonksiyonlardan düzleşmiş metinler HUD\'da kodla kurulur (`TableHud.Gifts.cs` `GiftSentence`).\n')
md.append(table([[code(u['key']), f"TR {cell(u['new'][0])} · EN {cell(u['new'][1])} (table.STR.{u['key']})",
                  f"TR {cell(u['old'][0])} · EN {cell(u['old'][1])} ({u['file']}.csv)", 'farklı',
                  f"eski metin kaldı; pakette bu metin {code(u['movedTo'])} sırasında (EK-403)"] for u in ts['keptListShift']]
                + [[code(u['key']), f"TR {cell(u['new'][0])} · EN {cell(u['new'][1])} (table.STR.{u['key']}; kullanılmayan parametre {', '.join(u['unusedParams'])})",
                    f"TR {cell(u['old'][0])} · EN {cell(u['old'][1])} ({u['file']}.csv)", 'farklı',
                    'eski metin kaldı; paket metni G101-Masa.dc.html fonksiyonunun tek dalı (EK-412)'] for u in ts['keptConditional']]) + '\n')
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text('\n'.join(md), encoding='utf-8')
print(json.dumps({k: dict(v) for k, v in counts.items()}, ensure_ascii=False, indent=1))
print(f'{OUT.relative_to(ROOT)}: {sum(len(r) for r in (theme_rows, font_rows, icon_rows, rank_rows, cursor_rows, sound_rows))} rows '
      f'+ {len(diff_rows)} changed strings; {CMP.relative_to(ROOT)}: {len(compare)} keys')
