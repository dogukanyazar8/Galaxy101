"""Galaxy 101 v4 data import (lane D): strings, themes, fonts, icons, league emblems, cursors and sound.

Reads the v4 handoff package (Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4, below "pkg/") and writes the
files the Unity client loads under Assets/Galaxy101/Resources/Galaxy101. Every value is copied or mechanically converted
from the package; nothing is authored here. Where the package and the client disagree the run report says so
(docs/v4/hat-d/import_report.json); docs/v4/hat-d/RAPOR.md explains what the Unity phase does with each output.

Run (rasterising needs resvg, fonts need fontTools):
  uv run --no-project --with resvg-py --with fonttools --with pillow python scripts/import-v4-data.py
  ... --only strings,themes   (sections: strings themes fonts icons ranks cursors sound)
  ... --check                 (write nothing; exit 1 if any output would change)

New files get a .meta with a fresh GUID; an existing .meta is never touched (GUIDs stay stable across runs).
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import io
import json
import re
import sys
import uuid
import wave
import xml.etree.ElementTree as ET
from collections import Counter, OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PKG = ROOT / 'Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4'
RES = ROOT / 'Assets/Galaxy101/Resources/Galaxy101'
REPORT = ROOT / 'docs/v4/hat-d/import_report.json'
SECTIONS = ['strings', 'themes', 'fonts', 'icons', 'ranks', 'cursors', 'sound']
SYMBOL_FALLBACK = Path('C:/Windows/Fonts/seguisym.ttf')

# Importer blocks copied from similar assets already in the project (see RAPOR.md "Meta dosyaları").
META_TEMPLATES = {
    'folder': RES / 'Icons.meta',
    'text': RES / 'Icons/close.txt.meta',
    'ttf': RES / 'Fonts/Nunito-900.ttf.meta',
    'png': RES / 'Artwork/logo-saturn.png.meta',
    'wav': ROOT / 'Assets/Okey101/Art/Audio/draw.wav.meta',
}

check_only = False
changed_files: list[str] = []
report: dict = {}


# ---------------------------------------------------------------- file helpers

def rel(path: Path) -> str:
    return path.relative_to(ROOT).as_posix()


def write_bytes(path: Path, data: bytes) -> bool:
    """Write only when the content differs; returns True when the file changed (or would change)."""
    if path.exists() and path.read_bytes() == data:
        return False
    changed_files.append(rel(path))
    if not check_only:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    return True


def write_text(path: Path, text: str, bom: bool = False) -> bool:
    if path.exists():
        # Git normalises text to LF and checks out CRLF on Windows: compare line-ending independently.
        old = path.read_bytes().decode('utf-8-sig').replace('\r\n', '\n')
        if old == text and path.read_bytes().startswith(b'\xef\xbb\xbf') == bom:
            return False
    return write_bytes(path, (b'\xef\xbb\xbf' if bom else b'') + text.encode('utf-8'))


def write_json(path: Path, value, indent=1, trailing_newline=True) -> bool:
    text = json.dumps(value, ensure_ascii=False, indent=indent)
    return write_text(path, text + ('\n' if trailing_newline else ''))


def csv_text(rows: list[list[str]]) -> str:
    out = io.StringIO()
    writer = csv.writer(out, quoting=csv.QUOTE_ALL, lineterminator='\n')
    writer.writerows(rows)
    return out.getvalue()


def read_csv(path: Path) -> list[list[str]]:
    return list(csv.reader(io.StringIO(path.read_bytes().decode('utf-8-sig').replace('\r\n', '\n'))))


def meta_body(kind: str) -> str:
    lines = META_TEMPLATES[kind].read_text(encoding='utf-8').replace('\r\n', '\n').split('\n')
    assert lines[0] == 'fileFormatVersion: 2' and lines[1].startswith('guid: '), META_TEMPLATES[kind]
    return '\n'.join(lines[2:])


def cursor_meta_body() -> str:
    """The project PNG importer block with the settings Cursor.SetCursor requires (Texture Type Cursor, readable, no mips)."""
    body = meta_body('png')
    for old, new in [('  textureType: 0\n', '  textureType: 7\n'), ('  isReadable: 0\n', '  isReadable: 1\n'),
                     ('    enableMipMap: 1\n', '    enableMipMap: 0\n')]:
        assert old in body, old
        body = body.replace(old, new)
    return body


def ensure_meta(path: Path, kind: str | None) -> None:
    meta = path.with_name(path.name + '.meta')
    if meta.exists():
        return
    head = f'fileFormatVersion: 2\nguid: {uuid.uuid4().hex}\n'
    body = '' if kind is None else (cursor_meta_body() if kind == 'cursor' else meta_body(kind))
    write_text(meta, head + body)


def ensure_folder(path: Path) -> None:
    """Create the folder (and the .meta of every new folder up to Resources/Galaxy101)."""
    chain = []
    p = path
    while p != RES and RES in p.parents:
        chain.append(p)
        p = p.parent
    for folder in reversed(chain):
        if not check_only:
            folder.mkdir(parents=True, exist_ok=True)
        ensure_meta(folder, 'folder')


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def strip_metadata(svg: str) -> str:
    """Drop the C2PA <metadata> block (content credentials only; it carries no geometry)."""
    return re.sub(r'<metadata>.*?</metadata>', '', svg, flags=re.S).replace(' xmlns:c2pa="http://c2pa.org/manifest"', '')


def rasterize(svg: str, width: int, height: int, fonts: list[Path]) -> bytes:
    import resvg_py  # uv run --with resvg-py
    data = resvg_py.svg_to_bytes(svg_string=svg, width=width, height=height, skip_system_fonts=True,
                                 font_files=[str(f) for f in fonts], font_family='Montserrat')
    return bytes(data)


def png_size(data: bytes) -> tuple[int, int]:
    assert data[:8] == b'\x89PNG\r\n\x1a\n'
    return int.from_bytes(data[16:20], 'big'), int.from_bytes(data[20:24], 'big')


# ---------------------------------------------------------------- strings

PLACEHOLDER = re.compile(r'\{(\d+)\}')


def placeholders(text: str) -> set[str]:
    return set(PLACEHOLDER.findall(text or ''))


def unused_params(entry: dict) -> list[str]:
    """Named params with no {n} in either text: the package flattened a conditional string function to one branch
    (G101-Masa.dc.html gave/gaveAll/toYou pick the verb from tab/me)."""
    used = placeholders(entry['tr']) | placeholders(entry['en'])
    return [p for i, p in enumerate(entry.get('params') or []) if str(i) not in used]


def package_strings() -> tuple[dict, list[dict], dict]:
    """All package texts with unique keys. Duplicate inline keys get "#2", "#3" in source order (the D-62 rule for
    repeated element names); error texts reuse the key of the identical game text when there is one."""
    d = json.loads((PKG / 'strings/strings.json').read_text(encoding='utf-8'))
    entries: list[dict] = []
    for e in d['tables']:
        entries.append(OrderedDict(key=e['key'], pkgKey=e['key'], group='table', tr=e['tr'], en=e['en'],
                                   params=e.get('params')))
    seen = Counter()
    for e in d['inline']:
        seen[e['key']] += 1
        key = e['key'] if seen[e['key']] == 1 else f"{e['key']}#{seen[e['key']]}"
        entries.append(OrderedDict(key=key, pkgKey=e['key'], group='inline', tr=e['tr'], en=e['en'], uses=e.get('uses')))
    by_key = {x['key']: x for x in entries}
    errors = OrderedDict()
    error_conflicts = []
    for e in d['errors']:
        rec = OrderedDict(ui=e['ui'])
        if e.get('state') is not None:
            rec['state'] = e['state']
        if e.get('tr') is not None or e.get('en') is not None:
            candidates = [x for x in entries if x['pkgKey'] == e['key']]
            match = next((x for x in candidates if (x['tr'], x['en']) == (e['tr'], e['en'])), None)
            if match is None and candidates:
                error_conflicts.append(e['code'])
                key = 'err.' + e['code']
            else:
                key = match['key'] if match else e['key']
            if match is None:
                entries.append(OrderedDict(key=key, pkgKey=e['key'], group='error', tr=e['tr'], en=e['en'],
                                           params=e.get('params'), code=e['code']))
                by_key[key] = entries[-1]
            rec['key'] = key
            rec['source'] = e.get('source')
            if e.get('params'):
                rec['params'] = e['params']
        errors[e['code']] = rec
    keys = [x['key'] for x in entries]
    assert len(keys) == len(set(keys)), 'duplicate string keys after disambiguation'
    meta = dict(version=d['version'], locales=d['locales'], defaultLocale=d['defaultLocale'], format=d['format'],
                counts=d['counts'], notes=d['notes'], duplicateInlineKeys=sorted(k for k, n in seen.items() if n > 1),
                errorConflicts=error_conflicts, errorTotal=len(d['errors']),
                errorTexts=sum(1 for e in d['errors'] if e.get('tr') is not None))
    return meta, entries, errors


def section_strings() -> None:
    meta, entries, errors = package_strings()
    folder = RES / 'Strings'
    ensure_folder(folder)
    missing = [x['key'] for x in entries if not x['tr'] or not x['en']]

    rows = [['key', 'tr', 'en']] + [[x['key'], x['tr'], x['en']] for x in entries]
    path = folder / 'strings_v4_flat.csv'
    write_text(path, csv_text(rows), bom=True)
    ensure_meta(path, 'text')

    strings = OrderedDict()
    for x in entries:
        rec = OrderedDict(tr=x['tr'], en=x['en'])
        if x.get('params'):
            rec['params'] = x['params']
            if unused_params(x):
                rec['unusedParams'] = unused_params(x)
        if x['pkgKey'] != x['key']:
            rec['pkgKey'] = x['pkgKey']
        if x.get('uses'):
            rec['uses'] = x['uses']
        strings[x['key']] = rec
    doc = OrderedDict(
        version=meta['version'], source='pkg/strings/strings.json', locales=meta['locales'],
        defaultLocale=meta['defaultLocale'], format=meta['format'],
        keyRules=OrderedDict(
            scheme='<ekran>.<tablo>.<yol> · satır içi <ekran>.inline.<alan>@<satır> (pkg/strings/README.md)',
            duplicates='Pakette aynı anahtarla birden çok satır içi metin var; ikinci ve sonrakiler kaynak sırasıyla #2, #3 '
                       'alır (D-62 kuralı). Asıl paket anahtarı "pkgKey" alanında.',
            errors='errors[kod].key metnin anahtarıdır; ui=silent/resync kodlarının metni yoktur (pkg/protocol/protocol.json).',
            unusedParams='Paket params listesinde olup metinde {n} karşılığı olmayan parametreler: kaynak fonksiyon koşulluydu '
                         '(ör. G101-Masa.dc.html gave: fiil tab/me ile seçilir); metin yalnız bir dalı gösterir.'),
        counts=OrderedDict(package=meta['counts'], keys=len(entries),
                           byGroup=dict(Counter(x['group'] for x in entries)), errorCodes=len(errors)),
        strings=strings, errors=errors, notes=meta['notes'])
    path = folder / 'strings_v4.json'
    write_json(path, doc)
    ensure_meta(path, 'text')

    table_report = update_table_strings(entries)
    report['strings'] = OrderedDict(
        packageEntries=meta['counts'], errorTotal=meta['errorTotal'], errorTexts=meta['errorTexts'],
        uniqueKeys=len(entries), byGroup=dict(Counter(x['group'] for x in entries)),
        duplicateInlineKeys=meta['duplicateInlineKeys'], errorConflicts=meta['errorConflicts'],
        missingText=missing, tableStrings=table_report)


def update_table_strings(entries: list[dict]) -> dict:
    """Bring the table HUD's key tables (Resources/Galaxy101/Table/Strings, read by TableStrings.cs) to the v4 text.
    pkg key table.STR.<k> is the HUD key <k>. A text whose {n} placeholders differ from the current one is not swapped:
    the HUD passes a fixed argument list, so that needs the C# call changed first (listed in the report)."""
    pkg = OrderedDict((x['pkgKey'][len('table.STR.'):], x) for x in entries
                      if x['pkgKey'].startswith('table.STR.') and x['group'] in ('table', 'error'))

    def shifted(key: str, old: tuple[str, str]) -> str | None:
        """List entries (views.1.0, flows.4) whose old text moved to another index in the v4 list: the HUD indexes
        these lists by position (TableHud.Windows.cs views.{i}), so the text stays until the list itself is ported."""
        m = re.fullmatch(r'(.+?)\.(\d+)((?:\.\d+)*)', key)
        if not m:
            return None
        pattern = re.compile(re.escape(m.group(1)) + r'\.(\d+)' + re.escape(m.group(3)))
        return next((k for k, x in pkg.items() if (mm := pattern.fullmatch(k)) and mm.group(1) != m.group(2)
                     and (x['tr'], x['en']) == old), None)

    folder = RES / 'Table/Strings'
    names = ['table_strings', 'table_strings_ek', 'table_strings_extra']
    present, updated, kept, moved, conditional = set(), [], [], [], []
    tables = {}
    for name in names:
        rows = read_csv(folder / f'{name}.csv')
        assert rows[0] == ['key', 'tr', 'en'], name
        for row in rows[1:]:
            key = row[0]
            if key not in pkg:
                continue
            present.add(key)
            x = pkg[key]
            if (row[1], row[2]) == (x['tr'], x['en']):
                continue
            entry = OrderedDict(key=key, file=name, old=[row[1], row[2]], new=[x['tr'], x['en']])
            if (target := shifted(key, (row[1], row[2]))) is not None:
                entry['movedTo'] = target
                moved.append(entry)
            elif unused_params(x):
                # A prototype string function with a condition (gave: verb by tab/me) flattened to one branch.
                entry['unusedParams'] = unused_params(x)
                conditional.append(entry)
            elif placeholders(row[1]) == placeholders(x['tr']) and placeholders(row[2]) == placeholders(x['en']):
                row[1], row[2] = x['tr'], x['en']
                updated.append(entry)
            else:
                kept.append(entry)
        tables[name] = rows
    added = []
    for key, x in pkg.items():
        if key not in present:
            tables['table_strings'].append([key, x['tr'], x['en']])
            added.append(key)
    for name in names:
        write_text(folder / f'{name}.csv', csv_text(tables[name]))
    project_only = sorted({r[0] for n in names for r in tables[n][1:]} - set(pkg))
    return OrderedDict(packageKeys=len(pkg), present=len(present), updated=updated, keptPlaceholderMismatch=kept,
                       keptListShift=moved, keptConditional=conditional, added=added, projectOnly=project_only)


# ---------------------------------------------------------------- themes

def section_themes() -> None:
    pkg = json.loads((PKG / 'assets/themes.json').read_text(encoding='utf-8'))
    strings = {x['key']: x for x in package_strings()[1]}
    path = RES / 'themes.json'
    old = json.loads(path.read_text(encoding='utf-8-sig'))
    out = OrderedDict()
    problems = []
    for planet in pkg['order']:
        p = pkg['planets'][planet]
        ui = p['ui']
        name, desc = strings[f'menu.STR.pn.{planet}'], strings[f'menu.STR.pd.{planet}']
        if desc['tr'] != p['menu']['desc']:
            problems.append(f'{planet}: menu.desc "{p["menu"]["desc"]}" != menu.STR.pd tr "{desc["tr"]}"')
        # cssVars: ui block (colours), rings (menu planet rings; rings.ro is the menu ring opacity), atmosphere, logo.
        source = OrderedDict((k, v) for k, v in ui.items() if k not in ('pf', 'ro'))
        for block, values in (('rings', p['rings']), ('logo', p['logo'])):
            for k, v in values.items():
                if k in source and source[k] != v:
                    problems.append(f'{planet}: {block}.{k}={v} != ui.{k}={source[k]}')
                source[k] = v
        if p['atmosphere'] != ui.get('atm'):
            problems.append(f'{planet}: atmosphere={p["atmosphere"]} != ui.atm={ui.get("atm")}')
        source['atm'] = p['atmosphere']
        before = old.get(planet, {}).get('cssVars', {})
        css = OrderedDict((k, source[k]) for k in before if k in source)
        css.update((k, v) for k, v in source.items() if k not in css)
        out[planet] = OrderedDict(name_tr=name['tr'], name_en=name['en'], desc_tr=p['menu']['desc'], desc_en=desc['en'],
                                  pickerOrb=p['menu']['orb'], cssVars=css, planetFlattening=ui['pf'],
                                  loadingRingOpacity=ui['ro'])
    write_text(path, json.dumps(out, ensure_ascii=False, indent=2))
    changes = []
    for planet, value in out.items():
        before = old.get(planet, {})
        for k in sorted(set(value) | set(before)):
            if k == 'cssVars':
                for c in sorted(set(value[k]) | set(before.get(k, {}))):
                    if value[k].get(c) != before.get(k, {}).get(c):
                        changes.append([planet, 'cssVars.' + c, before.get(k, {}).get(c), value[k].get(c)])
            elif value.get(k) != before.get(k):
                changes.append([planet, k, before.get(k), value.get(k)])

    # The 3D table recolours from Table/table_content.json planets_table (TableTheme.cs) = pkg table3d.
    table_path = RES / 'Table/table_content.json'
    table = json.loads(table_path.read_text(encoding='utf-8-sig'), object_pairs_hook=OrderedDict)
    table_changes = []
    for planet in pkg['order']:
        want, have = pkg['planets'][planet]['table3d'], table['planets_table'].get(planet, {})
        for k in sorted(set(want) | set(have)):
            if want.get(k) != have.get(k):
                table_changes.append([planet, k, have.get(k), want.get(k)])
    if table_changes:
        for planet in pkg['order']:
            table['planets_table'][planet] = OrderedDict(pkg['planets'][planet]['table3d'])
        write_text(table_path, json.dumps(table, ensure_ascii=False, indent=2))
    report['themes'] = OrderedDict(changes=changes, packageProblems=problems, tableChanges=table_changes,
                                   transition=pkg.get('transition'))


# ---------------------------------------------------------------- fonts

def section_fonts() -> None:
    from fontTools.ttLib import TTFont
    spec = json.loads((PKG / 'assets/fonts.json').read_text(encoding='utf-8'))
    folder = RES / 'Fonts'
    rows = []
    turkish = 'çğıİöşüÇĞÖŞÜ₺×·—…'
    for family, files in spec['files'].items():
        for name in files:
            src = PKG / 'assets' / name
            if family == 'license':
                stem = src.stem.split('-', 1)[1]          # OFL-Montserrat.txt -> Montserrat-OFL.txt
                dst = folder / f'{stem}-OFL.txt'
                existed = dst.exists()
                same = existed and sha(dst) == sha(src)
                if not same:
                    write_bytes(dst, src.read_bytes())
                ensure_meta(dst, 'text')
                rows.append(OrderedDict(package=name, project=rel(dst), status='var' if same else ('farklı' if existed else 'eksik')))
                continue
            font = TTFont(src)
            weight = font['OS/2'].usWeightClass
            cmap = font.getBestCmap()
            missing = [c for c in turkish if ord(c) not in cmap]
            if 'fvar' in font:
                rows.append(OrderedDict(package=name, project=None, weight=weight, variable=True, status='kopyalanmadı',
                                        note='değişken font; istemci sabit ağırlıklı TTF kullanır (fonts.json license notu)'))
                continue
            dst = folder / f"{family.replace(' ', '')}-{weight}.ttf"
            existed = dst.exists()
            same = existed and sha(dst) == sha(src)
            if not same:
                write_bytes(dst, src.read_bytes())
            ensure_meta(dst, 'ttf')
            rows.append(OrderedDict(package=name, project=rel(dst), weight=weight, sha256=sha(src),
                                    status='var' if same else ('farklı' if existed else 'eksik'), missingGlyphs=missing))
    report['fonts'] = OrderedDict(files=rows, used=spec['used'])


# ---------------------------------------------------------------- icons

ALLOWED_SHAPES = {'path', 'circle', 'rect'}
ALLOWED_ATTRS = {'d', 'cx', 'cy', 'r', 'x', 'y', 'width', 'height', 'rx', 'fill', 'stroke', 'stroke-width',
                 'stroke-linecap', 'stroke-linejoin', 'stroke-miterlimit', 'opacity', 'fill-opacity', 'stroke-opacity',
                 'fill-rule'}
STYLE_PROPS = {'fill', 'stroke', 'stroke-linecap', 'stroke-linejoin', 'fill-rule'}
PATH_ARGS = {'M': 2, 'L': 2, 'H': 1, 'V': 1, 'C': 6, 'S': 4, 'Q': 4, 'A': 7, 'Z': 0}


def galaxy_icon_issues(svg: str) -> list[str]:
    """What GalaxyIcon.cs (Painter2D renderer of Resources/Galaxy101/Icons/*.txt) cannot draw in this SVG."""
    root = ET.fromstring(svg)
    local = lambda tag: tag.split('}')[-1]
    issues = []
    for attr in root.attrib:
        if attr in ('fill', 'stroke') and root.get(attr, '').startswith('url('):
            issues.append(f'kök {attr} gradyan')
    for child in root:
        tag = local(child.tag)
        if tag == 'metadata':
            continue
        if tag not in ALLOWED_SHAPES:
            issues.append(f'<{tag}>')
            continue
        for attr, value in child.attrib.items():
            if attr == 'style':
                # GalaxyIcon.Property() reads only these from style="…"; numbers (opacity, widths) come from attributes.
                for prop, paint in re.findall(r'([\w-]+)\s*:\s*([^;]+)', value):
                    if prop not in STYLE_PROPS:
                        issues.append(f'style:{prop}')
                    elif paint.strip().startswith('url('):
                        issues.append(f'{tag} {prop} gradyan')
            elif attr not in ALLOWED_ATTRS:
                issues.append(f'{tag}@{attr}')
            elif attr in ('fill', 'stroke') and value.startswith('url('):
                issues.append(f'{tag} {attr} gradyan')
        if tag == 'path':
            tokens = re.findall(r'[a-zA-Z]|[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?', child.get('d', ''))
            i, command = 0, 'M'
            while i < len(tokens):
                if tokens[i].isalpha():
                    command = tokens[i]
                    i += 1
                kind = command.upper()
                if kind not in PATH_ARGS:
                    issues.append(f'path komutu {command}')
                    break
                if kind == 'Z':
                    continue
                args = tokens[i:i + PATH_ARGS[kind]]
                if kind == 'A' and abs(float(args[0])) != abs(float(args[1])) and float(args[0]) != 0 and float(args[1]) != 0:
                    issues.append('eliptik yay')
                i += PATH_ARGS[kind]
                if kind == 'M':
                    command = 'l' if command == 'm' else 'L'
    return sorted(set(issues))


def section_icons() -> None:
    spec = json.loads((PKG / 'assets/icons.json').read_text(encoding='utf-8'))
    folder = RES / 'Icons/v4'
    svg_folder = folder / 'svg'
    ensure_folder(svg_folder)
    icons = OrderedDict()
    for item in spec['icons']:
        src = PKG / 'assets' / item['file']
        name = src.stem
        raw = src.read_text(encoding='utf-8')
        clean = strip_metadata(raw)
        txt = folder / f'{name}.txt'
        write_text(txt, clean)
        ensure_meta(txt, 'text')
        svg = svg_folder / f'{name}.svg'
        write_bytes(svg, src.read_bytes())
        ensure_meta(svg, None)
        issues = galaxy_icon_issues(clean)
        icons[name] = OrderedDict(txt=f'Galaxy101/Icons/v4/{name}', svg=f'Galaxy101/Icons/v4/svg/{name}',
                                  viewBox=item['viewBox'], stroke=item.get('stroke'), usedBy=item['usedBy'],
                                  uses=item['uses'], galaxyIcon=OrderedDict(ok=not issues, issues=issues))
    manifest = OrderedDict(version=spec['version'], source='pkg/assets/icons.json', note=spec['note'],
                           count=len(icons), loader='GalaxyIcon: view.Icon("v4/<id>") → Resources Galaxy101/Icons/v4/<id>.txt',
                           icons=icons)
    path = folder / 'icons.json'
    write_json(path, manifest)
    ensure_meta(path, 'text')
    report['icons'] = OrderedDict(count=len(icons), galaxyIconIncompatible={k: v['galaxyIcon']['issues'] for k, v in icons.items()
                                                                           if not v['galaxyIcon']['ok']})


# ---------------------------------------------------------------- league emblems

def section_ranks() -> None:
    spec = json.loads((PKG / 'assets/ranks.json').read_text(encoding='utf-8'), object_pairs_hook=OrderedDict)
    folder = RES / 'Rank'
    svg_folder = folder / 'svg'
    ensure_folder(svg_folder)
    fonts = sorted((PKG / 'assets/fonts').glob('Montserrat-*.ttf'))
    fonts = [f for f in fonts if 'Variable' not in f.name]
    # The galaxy emblem's centre tile is "★" (g101-rank.js TILE[5]); Montserrat has no such glyph, so the browser falls
    # back to a system font. On Windows that is Segoe UI Symbol; use it explicitly so the raster does not depend on
    # whatever else is installed.
    if SYMBOL_FALLBACK.exists():
        fonts.append(SYMBOL_FALLBACK)
    else:
        print(f'warning: {SYMBOL_FALLBACK} not found; the galaxy emblem star renders as a missing glyph', file=sys.stderr)
    leagues = [l['key'] for l in spec['leagues']]
    roman = spec['roman']
    emblems = OrderedDict()
    for src in sorted((PKG / 'assets/rank').glob('*.svg')):
        name = src.stem
        raw = src.read_text(encoding='utf-8')
        root = re.match(r'<svg[^>]*>', strip_metadata(raw)).group()
        size = int(re.search(r'\bwidth="(\d+)"', root).group(1))
        assert size == int(re.search(r'\bheight="(\d+)"', root).group(1))
        m = re.fullmatch(r'medal_([a-z]+)_(\d+)', name) or re.fullmatch(r'([a-z]+)(?:_(I|II|III))?_(\d+)', name)
        if name.startswith('medal_'):
            league, tier, kind = m.group(1), 0, 'medal'
        else:
            league, tier, kind = m.group(1), roman.index(m.group(2)) if m.group(2) else 0, 'emblem'
        assert league in leagues and int(m.groups()[-1]) == size, name
        svg = svg_folder / f'{name}.svg'
        write_bytes(svg, src.read_bytes())
        ensure_meta(svg, None)
        entry = OrderedDict(kind=kind, league=league, tier=tier, size=size, svg=f'Galaxy101/Rank/svg/{name}')
        if kind == 'emblem' and tier:
            # g101-rank.js emblem(): "kademe taşları" under the rim, n = 4 - tier (I = 3, II = 2, III = 1).
            entry['divisionMarks'] = 4 - tier
        for scale in (1, 2):
            data = rasterize(raw, size * scale, size * scale, fonts)
            assert png_size(data) == (size * scale, size * scale)
            png = folder / (f'{name}.png' if scale == 1 else f'{name}@2x.png')
            write_bytes(png, data)
            ensure_meta(png, 'png')
            entry['png' if scale == 1 else 'png2x'] = 'Galaxy101/Rank/' + png.stem
        emblems[name] = entry
    spec['source'] = 'pkg/assets/ranks.json (' + spec['source'] + ')'
    spec['emblems'] = emblems
    spec['rasterized'] = ('resvg ' + resvg_version() + '; metin Montserrat (pkg/assets/fonts, sistem fontu kapalı), '
                          'galaksi taşındaki ★ Montserrat\'ta yok → Segoe UI Symbol (tarayıcının Windows yedeği); '
                          '@1x = dosya adındaki boyut, @2x = iki katı (1280×720 referans panelin 2560×1440 ölçeği)')
    path = folder / 'ranks.json'
    write_json(path, spec)
    ensure_meta(path, 'text')
    report['ranks'] = OrderedDict(count=len(emblems), byKind=dict(Counter(e['kind'] for e in emblems.values())))


def resvg_version() -> str:
    import resvg_py
    return f'{resvg_py.__resvg_version__} (resvg-py {resvg_py.__version__})'


# ---------------------------------------------------------------- cursors

def section_cursors() -> None:
    spec = json.loads((PKG / 'assets/cursors.json').read_text(encoding='utf-8'), object_pairs_hook=OrderedDict)
    folder = RES / 'Cursors'
    svg_folder = folder / 'svg'
    ensure_folder(svg_folder)
    size = spec['size']
    cursors = OrderedDict()
    for name in spec['names']:
        src = PKG / 'assets/cursors' / f'{name}.svg'
        raw = src.read_text(encoding='utf-8')
        svg = svg_folder / f'{name}.svg'
        write_bytes(svg, src.read_bytes())
        ensure_meta(svg, None)
        hx, hy = spec['hotspots'][name]
        entry = OrderedDict(svg=f'Galaxy101/Cursors/svg/{name}', size=size, hotspot=[hx, hy])
        for scale in (1, 2):
            data = rasterize(raw, size * scale, size * scale, [])
            assert png_size(data) == (size * scale, size * scale)
            png = folder / (f'{name}.png' if scale == 1 else f'{name}@2x.png')
            write_bytes(png, data)
            ensure_meta(png, 'cursor')
            if scale == 1:
                entry['png'] = 'Galaxy101/Cursors/' + png.stem
            else:
                entry['png2x'] = 'Galaxy101/Cursors/' + png.stem
                entry['hotspot2x'] = [hx * 2, hy * 2]
        cursors[name] = entry
    manifest = OrderedDict(version=spec['version'], source='pkg/assets/cursors.json (' + spec['source'] + ')',
                           size=size, names=spec['names'], rules=spec['rules'], cursors=cursors,
                           unity='Cursor.SetCursor(Resources.Load<Texture2D>(png), new Vector2(hotspot[0], hotspot[1]), '
                                 'CursorMode.Auto); hotspot sol üstten piksel (CSS cursor: url() x y ile aynı).')
    path = folder / 'cursors.json'
    write_json(path, manifest)
    ensure_meta(path, 'text')
    report['cursors'] = OrderedDict(count=len(cursors))


# ---------------------------------------------------------------- sound

def wav_info(path: Path) -> OrderedDict:
    with wave.open(str(path), 'rb') as w:
        return OrderedDict(rate=w.getframerate(), bits=w.getsampwidth() * 8, channels=w.getnchannels(),
                           seconds=round(w.getnframes() / w.getframerate(), 4))


def section_sound() -> None:
    spec = json.loads((PKG / 'sound/events.json').read_text(encoding='utf-8'), object_pairs_hook=OrderedDict)
    folder = RES / 'Audio'
    ensure_folder(folder / 'Sfx')
    ensure_folder(folder / 'Music')
    events = OrderedDict()
    wavs = OrderedDict()
    for e in spec['events']:
        src = PKG / 'sound' / e['wav']
        assert src.stem == e['file'], e['id']
        dst = folder / 'Sfx' / src.name
        write_bytes(dst, src.read_bytes())
        ensure_meta(dst, 'wav')
        info = wav_info(src)
        wavs[e['id']] = info
        events[e['id']] = OrderedDict(cat=e['cat'], prio=e['prio'], poly=e['poly'], ms=e['ms'],
                                      clip='Galaxy101/Audio/Sfx/' + src.stem, wavSeconds=info['seconds'],
                                      trigger=e['trigger'])
    music = OrderedDict()
    for m in spec['music']:
        src = PKG / 'sound' / m['file']
        dst = folder / 'Music' / src.name
        write_bytes(dst, src.read_bytes())
        ensure_meta(dst, 'wav')
        info = wav_info(src)
        wavs[m['id']] = info
        music[m['id']] = OrderedDict(clip='Galaxy101/Audio/Music/' + src.stem, loop=m['loop'], seconds=m['seconds'],
                                     wavSeconds=info['seconds'], where=m['where'], chords=m['chords'])
    doc = OrderedDict(
        version=spec['version'],
        source='pkg/sound/events.json (' + spec['source'] + ') + pkg/sound/README.md + prototip/g101-sound.js',
        format=spec['format'],
        categories=spec['categories'],
        # g101-sound.js CAT: every effect category plays on the sfx slider, music on the music slider.
        channels=OrderedDict(ui='sfx', game='sfx', tile='sfx', chat='sfx', music='music'),
        # g101-sound.js gainFor(): tile needs tileSnd; chat needs chatSnd and notifSnd.
        toggles=OrderedDict(tile=['tileSnd'], chat=['chatSnd', 'notifSnd']),
        volume=OrderedDict(
            rule=spec['volume'],
            # g101-sound.js S() defaults.
            defaults=OrderedDict(master=80, sfx=90, music=55, tileSnd=True, chatSnd=True, notifSnd=True),
            settings=OrderedDict(master='Ana ses', sfx='Efektler', music='Müzik', tileSnd='Taş sesleri',
                                 chatSnd='Sohbet sesleri', notifSnd='Bildirim sesleri'),
            # G101-Masa.dc.html sfx(): tile.draw at vol 0.7 when the active seat is not me.
            opponentDrawVolume=0.7),
        mixing=OrderedDict(
            text=spec['mixing'],
            # g101-sound.js duck(): music to 40 % over 0.3 s, back to full by 2.4 s, for prio >= 5.
            duck=OrderedDict(minPrio=5, musicLevel=0.4, downSeconds=0.3, restoreAtSeconds=2.4),
            # g101-sound.js setMusic(): old loop fades out over 1.2 s, the new one fades in over 1.5 s.
            crossfadeOutSeconds=1.2, fadeInSeconds=1.5,
            sync=OrderedDict([('dice.roll', 1450), ('finish.nuke', 1750), ('gift.rocket', 5600)])),
        # pkg/sound/README.md "Kural motoru olaylarıyla eşleme" + the event triggers in events.json.
        engineEvents=OrderedDict([
            ('drew', OrderedDict(sound='tile.draw', rule='ben ya da rakip; rakipte opponentDrawVolume')),
            ('discarded', OrderedDict(sound='tile.discard')),
            ('opened', OrderedDict(sound='meld.lay')),
            ('laid', OrderedDict(sound='meld.lay')),
            ('added', OrderedDict(sound='meld.add')),
            ('okeySwapped', OrderedDict(sound='okey.swap')),
            ('penalty', OrderedDict(sound='penalty')),
            ('finished', OrderedDict(sound=['finish', 'finish.nuke'], rule='okeyle / elden bitiş fırlatması (nuke) → finish.nuke, değilse finish')),
            ('handEnd', OrderedDict(sound='hand.end')),
            ('turn', OrderedDict(sound='turn.mine', rule='seat = ben, dağıtım bitince')),
            ('dealStart', OrderedDict(sound='deal.shuffle')),
            ('dealDie', OrderedDict(sound='dice.roll')),
            ('dealStep', OrderedDict(sound=['deal.indicator', 'deal.distribute'], rule='indicator → deal.indicator, distribute → deal.distribute')),
            ('gift', OrderedDict(sound=['gift.send', 'gift.receive', 'gift.rocket'], rule='uçuş başladı → gift.send (roket: gift.rocket); sana geldi → gift.receive')),
            ('chat', OrderedDict(sound='chat.message', rule='rakipten mesaj/balon')),
        ]),
        # Galaxy 101 - Gerçek Ana Oyun.dc.html go(): table music on the game screen, menu music everywhere else;
        # ui.screen on every screen change except leaving the splash.
        screenMusic=OrderedDict([('game', 'music.table'), ('*', 'music.menu')]),
        screenChange=OrderedDict(sound='ui.screen', skipFrom=['splash']),
        events=events, music=music)
    path = folder / 'sound_events.json'
    write_json(path, doc)
    ensure_meta(path, 'text')
    off_format = {k: v for k, v in wavs.items() if (v['rate'], v['bits'], v['channels']) != (44100, 16, 2)}
    report['sound'] = OrderedDict(events=len(events), music=len(music), wav=wavs, offFormat=off_format)


# ---------------------------------------------------------------- main

def main() -> int:
    global check_only
    sys.stdout.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    parser.add_argument('--only', default=','.join(SECTIONS))
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    check_only = args.check
    sections = [s.strip() for s in args.only.split(',') if s.strip()]
    unknown = set(sections) - set(SECTIONS)
    if unknown:
        parser.error(f'unknown sections: {sorted(unknown)}')
    if not PKG.is_dir():
        parser.error(f'package not found: {PKG}')
    for name in sections:
        globals()['section_' + name]()
    report['changedFiles'] = changed_files
    if not check_only:
        REPORT.parent.mkdir(parents=True, exist_ok=True)
        previous = json.loads(REPORT.read_text(encoding='utf-8')) if REPORT.exists() else {}
        previous.update({k: v for k, v in report.items() if k != 'changedFiles'})
        previous['lastRun'] = OrderedDict(sections=sections, changedFiles=changed_files)
        REPORT.write_text(json.dumps(previous, ensure_ascii=False, indent=1) + '\n', encoding='utf-8')
    summary = {k: (v if not isinstance(v, dict) else {kk: (len(vv) if isinstance(vv, (list, dict)) else vv)
                                                       for kk, vv in v.items()}) for k, v in report.items() if k != 'changedFiles'}
    print(json.dumps(summary, ensure_ascii=False, indent=1))
    print(f'{len(changed_files)} file(s) {"would change" if check_only else "written"}')
    return 1 if check_only and changed_files else 0


if __name__ == '__main__':
    sys.exit(main())
