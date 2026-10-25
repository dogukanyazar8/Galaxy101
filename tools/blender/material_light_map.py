"""Galaxy 101 · v4 Hat C: map GLB materials and the full-scene lights to the v4 package.

Inputs (all read-only):
  * inventories from tools/blender/gltf_inventory.py: the 103 v4 GLBs and the project's Models (Satürn, polished);
  * pkg/assets/materials.json, pkg/assets/lighting.json, pkg/assets/themes.json, pkg/anim/tween3d.json.

Outputs (JSON, docs/v4/hat-c/):
  * material_map.json  every materials.json entry vs the v4 GLBs and the project GLBs (value by value, three.js → glTF
                       conversions below), plus every GLB material name the package does not list;
  * light_map.json     every lighting.json light / renderer / environment / ambient setting vs sahne_tam (KHR_lights_punctual)
                       and the project's Unity code;
  * sahne_tam_layout.json  table / seats / racks / chairs / markers / room / lights of sahne_tam in glTF and Unity space,
                       checked against the project's TableConstants, with the camera views of tween3d.json.

three.js → glTF (GLTFExporter r18x): colours are linear (hex is sRGB), opacity = baseColorFactor[3] (alphaMode BLEND when
transparent), emissiveIntensity = KHR_materials_emissive_strength, clearcoat* = KHR_materials_clearcoat,
iridescence* = KHR_materials_iridescence, MeshBasicMaterial = KHR_materials_unlit, side DoubleSide = doubleSided.
blending, depthWrite, toneMapped and envMapIntensity have no glTF field.

Usage:
  python tools/blender/material_light_map.py <new_inventory.json> <project_inventory.json> <pkg dir> <out dir>
"""
import json
import math
import os
import re
import sys

TOL = 2e-3
THEME_IDS = {"saturn": "Satürn", "mars": "Mars", "neptun": "Neptün", "jupiter": "Jüpiter", "venus": "Venüs"}
# Shared GLBs (chair, room, gifts, die) are exported once, in the first theme of the export tool: Satürn
# (Araç - 3D Varlık Dışa Aktarma.html: jobs push non-themed assets only for ti === 0, THEMES order Satürn first).
SHARED_THEME = "saturn"
# sahne_tam clones the live scene after the export loop: last table skin andromeda, last rack andromeda, last tiles marble.
SCENE_TABLE_SKIN = "andromeda"
TABLE_VARIANT = {"istasyon": "station", "ceviz": "wood", "hologram": "holo", "andromeda": "andromeda"}


# ---------- colours ----------
def srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def linear_to_srgb(c):
    c = max(0.0, min(1.0, c))
    return c * 12.92 if c <= 0.0031308 else 1.055 * c ** (1 / 2.4) - 0.055


def hex_to_linear(h):
    h = h.strip().lstrip("#")
    if len(h) == 3:
        h = "".join(ch * 2 for ch in h)
    return [round(srgb_to_linear(int(h[i:i + 2], 16) / 255), 5) for i in (0, 2, 4)]


def linear_to_hex(v):
    return "#" + "".join(f"{round(linear_to_srgb(x) * 255):02X}" for x in v[:3])


# ---------- materials.json params parsing ----------
def split_top(s):
    out, depth, quote, cur = [], 0, None, ""
    for ch in s:
        if quote:
            cur += ch
            if ch == quote:
                quote = None
            continue
        if ch in "'\"":
            quote = ch; cur += ch; continue
        if ch in "([{":
            depth += 1
        elif ch in ")]}":
            depth -= 1
        if ch == "," and depth == 0:
            out.append(cur.strip()); cur = ""; continue
        cur += ch
    if cur.strip():
        out.append(cur.strip())
    return out


def parse_value(v):
    v = v.strip()
    if re.fullmatch(r"'[^']*'|\"[^\"]*\"", v):
        return v[1:-1]
    if re.fullmatch(r"-?\d+(\.\d+)?", v):
        return float(v)
    if v in ("true", "false"):
        return v == "true"
    if v.startswith("[") and v.endswith("]"):
        return [parse_value(x) for x in split_top(v[1:-1])]
    return {"expr": v}


def parse_params(s):
    props = {}
    for part in split_top(s):
        k, _, v = part.partition(":")
        props[k.strip()] = parse_value(v)
    return props


def package_materials(mj):
    entries = []
    for i, e in enumerate(mj["table"]):
        props = parse_params(e["params"])
        entries.append({"id": f"table[{i}] {e['key']}", "source": f"materials.json table[{i}] (key {e['key']})", "type": e["type"], "props": props})
    for skin, slots in mj["tableSkins"].items():
        for slot, p in slots.items():
            props = {k: v for k, v in p.items() if k != "type"}
            entries.append({"id": f"tableSkins.{skin}.{slot}", "source": f"materials.json tableSkins.{skin}.{slot}", "type": p["type"], "props": props})
    for e in entries:
        n = e["props"].get("name")
        if isinstance(n, dict):  # expression: 'simge_' + kind, base.name + '_duz'
            expr = n["expr"]
            m = re.fullmatch(r"'([^']*)'\s*\+\s*\w+", expr)
            if m:
                e["name"], e["pattern"] = m.group(1) + "*", "^" + re.escape(m.group(1)) + r".+$"
            elif re.fullmatch(r"base\.name\s*\+\s*'([^']*)'", expr):
                suf = re.fullmatch(r"base\.name\s*\+\s*'([^']*)'", expr).group(1)
                e["name"], e["pattern"] = "*" + suf, "^.+" + re.escape(suf) + "$"
            else:
                e["name"], e["pattern"] = expr, None
        else:
            e["name"], e["pattern"] = n, "^" + re.escape(n) + "$"
    return entries


# ---------- themes ----------
def theme_tokens(themes):
    """Per theme: the tokens table3d.js applyLook() reads. G101-Masa.dc.html:885 tableTheme():
    { a1, a2, f1, f2, tile: tf1, rim, leather: pn, glow: a2, sky: st } (themes.json table3d lacks tile/leather/glow/sky)."""
    out = {}
    for tid, tr in THEME_IDS.items():
        p = themes["planets"][tr]; t3, ui = p["table3d"], p["ui"]
        out[tid] = {"a1": t3["a1"], "a2": t3["a2"], "f1": t3["f1"], "f2": t3["f2"], "rim": t3["rim"], "tile": ui["tf1"],
                    "leather": ui["pn"], "glow": ui["a2"], "sky": ui["st"]}
    return out


def theme_override(name, prop, theme, table_skin, tok):
    """Value table3d.js applyLook() (pkg materials.json tableSkinApply) / tiles3d.js applyBody puts on a material, or None."""
    t = tok[theme]
    rules = {
        ("led", "emissive"): t["a1"], ("led_cizgi", "color"): t["a1"], ("atma_alani", "color"): t["a1"],
        ("atma_dolgu", "color"): t["a1"], ("duvar", "emissive"): t["a1"], ("masa_kenar", "color"): t["rim"],
        ("deri", "color"): t["leather"], ("masa_cam_kenar", "emissive"): t["a1"], ("masa_holo_led", "emissive"): t["a1"],
        ("masa_holo_cizgi", "color"): t["a1"],
        ("tas_classic", "color"): t["tile"],  # tiles3d.js:114 applyBody: classic body = th.tile (tf1)
    }
    if (name, prop) in rules:
        return rules[(name, prop)]
    if name == "hale" and prop == "color":
        # Several materials are called 'hale': M.halo (a2; #7F9CFF on andromeda) and deckGlow / marker glows (a1).
        return ["#7F9CFF" if table_skin == "andromeda" else t["a2"], t["a1"]]
    if name == "holo" and prop == "color":
        return "#B79CFF" if table_skin == "andromeda" else t["a1"]
    if name == "holo" and prop == "opacity":
        return 0.5 if table_skin == "holo" else 0.12 if table_skin == "andromeda" else 0.3
    if name == "hale" and prop == "opacity":
        return 0.12 if table_skin == "wood" else 0.4 if table_skin == "holo" else 0.25
    if name == "cuha" and prop == "emissive":
        return t["a1"] if table_skin == "holo" else "#ffffff" if table_skin == "andromeda" else "#000000"
    if name == "cuha" and prop == "emissiveIntensity":
        return 0.35 if table_skin == "holo" else 0.5 if table_skin == "andromeda" else 0
    return None


# ---------- glTF material accessors ----------
def g_ext(m, ext, key, default=None):
    return ((m.get("extensions") or {}).get(ext) or {}).get(key, default)


def glb_value(m, prop):
    """The glTF field a three.js property lands in; returns (value, comparable)."""
    pbr = m.get("pbrMetallicRoughness") or {}
    bc = pbr.get("baseColorFactor", [1, 1, 1, 1])
    unlit = "KHR_materials_unlit" in (m.get("extensions") or {})
    if prop == "color":
        return bc[:3], True
    if prop == "opacity":
        return bc[3], True
    if prop == "transparent":
        return m.get("alphaMode") == "BLEND", True
    if prop == "metalness":
        return (None, False) if unlit else (pbr.get("metallicFactor", 1), True)
    if prop == "roughness":
        return (None, False) if unlit else (pbr.get("roughnessFactor", 1), True)
    if prop == "emissive":
        return m.get("emissiveFactor", [0, 0, 0]), True
    if prop == "emissiveIntensity":
        return g_ext(m, "KHR_materials_emissive_strength", "emissiveStrength", 1), True
    if prop == "clearcoat":
        return g_ext(m, "KHR_materials_clearcoat", "clearcoatFactor", 0), True
    if prop == "clearcoatRoughness":
        return g_ext(m, "KHR_materials_clearcoat", "clearcoatRoughnessFactor", 0), True
    if prop == "iridescence":
        return g_ext(m, "KHR_materials_iridescence", "iridescenceFactor", 0), True
    if prop == "iridescenceIOR":
        return g_ext(m, "KHR_materials_iridescence", "iridescenceIor", 1.3), True
    if prop == "iridescenceThicknessRange":
        return [g_ext(m, "KHR_materials_iridescence", "iridescenceThicknessMinimum", 100), g_ext(m, "KHR_materials_iridescence", "iridescenceThicknessMaximum", 400)], True
    if prop == "side":
        return m.get("doubleSided", False), True
    if prop == "map":
        return "baseColorTexture" in pbr, True
    return None, False


NOT_IN_GLTF = {"blending", "depthWrite", "toneMapped", "envMapIntensity"}


def expected(prop, v):
    """Package value converted to its glTF form."""
    if prop in ("color", "emissive") and isinstance(v, str):
        return hex_to_linear(v)
    if prop == "side":
        return isinstance(v, dict) and "DoubleSide" in v.get("expr", "")
    if prop == "map":
        return True
    return v


def close(a, b):
    if isinstance(a, (int, float)) and isinstance(b, (int, float)):
        return abs(a - b) <= TOL
    if isinstance(a, list) and isinstance(b, list) and len(a) == len(b):
        return all(close(x, y) for x, y in zip(a, b))
    return a == b


def show(prop, v):
    if prop in ("color", "emissive") and isinstance(v, list):
        return linear_to_hex(v)
    if isinstance(v, float):
        return round(v, 4)
    return v


def table_skin_of(f):
    if f["kind"] == "table":
        return TABLE_VARIANT.get(f["variant"])
    # markers, sahne_tam, chair and room are exported after the table loop: the andromeda skin is still applied.
    if f["kind"] in ("scene", "markers", "chair", "room"):
        return SCENE_TABLE_SKIN
    return None


def check_material(entry, occurrences, tok):
    """occurrences: list of (file, material). Returns per-prop checks and an overall status."""
    props = {k: v for k, v in entry["props"].items() if k != "name"}
    if entry["type"] == "MeshBasicMaterial":
        props.setdefault("_unlit", True)
    checks = []
    for prop, pv in props.items():
        if prop == "_unlit":
            got = sorted({("KHR_materials_unlit" in (m.get("extensions") or {})) for _, m in occurrences})
            checks.append({"prop": "MeshBasicMaterial→KHR_materials_unlit", "package": True, "glb": got, "status": "var" if got == [True] else "farklı"})
            continue
        if prop in NOT_IN_GLTF:
            checks.append({"prop": prop, "package": pv if not isinstance(pv, dict) else pv["expr"], "glb": None, "status": "glTF'e geçmez"})
            continue
        exp = expected(prop, pv)
        if isinstance(pv, dict) and prop != "side":  # an expression (glowTex(), tx, base.map)
            got = sorted({str(glb_value(m, "map")[0]) for _, m in occurrences}) if prop == "map" else None
            checks.append({"prop": prop, "package": pv["expr"], "glb": got, "status": "ifade (doku/çalışma anı)"})
            continue
        values, statuses = [], set()
        for f, m in occurrences:
            got, ok = glb_value(m, prop)
            if not ok:
                statuses.add("unlit: yok"); continue
            theme = f["theme"] or SHARED_THEME
            over = theme_override(m.get("name"), prop, theme, table_skin_of(f), tok)
            overs = over if isinstance(over, list) else [over] if over is not None else []
            pbr = m.get("pbrMetallicRoughness") or {}
            st = None
            if close(got, exp):
                st = "var"
            elif any(close(got, expected(prop, o)) for o in overs):
                st = "tema"
            elif prop in ("metalness", "roughness") and "metallicRoughnessTexture" in pbr and close(got, 1):
                st = "doku (cila: MR haritası × 1)"
            elif prop in ("emissive", "emissiveIntensity") and emissive_product_matches(
                    m, entry["props"], theme_override(m.get("name"), "emissive", theme, table_skin_of(f), tok)):
                st = "güç renge katlanmış"
            elif prop == "opacity" and f["kind"] == "scene":
                st = "çalışma anı (sahne_tam canlı sahne kopyası)"  # e.g. table3d.js:986 zone idle opacity 0.16
            elif prop == "emissiveIntensity" and m.get("name") in ("led",) and 1.95 <= got <= 2.45:
                st = "animasyon anı"  # lighting.json ambient.led: 2.2 + sin(t·1.2)·0.25, the export caught one frame
            elif prop == "opacity" and f["kind"] == "markers" and got in (0.6, 0.8):
                st = "dışa aktarma (opaklık <0.3 → 0.6/0.8)"  # table3d.js exportAsset('markers')
            elif prop == "opacity" and not entry["props"].get("transparent", False) and m.get("alphaMode") != "BLEND":
                st = "var"
            else:
                st = "farklı"
            statuses.add(st)
            values.append([f["file"], show(prop, got), st])
        uniq = sorted({json.dumps(v[1]) for v in values})
        checks.append({"prop": prop, "package": show(prop, exp) if prop not in ("color", "emissive") or not isinstance(pv, str) else pv,
                       "glb": [json.loads(u) for u in uniq][:8], "glb_distinct": len(uniq),
                       "status": "farklı" if "farklı" in statuses else ", ".join(sorted(statuses)) or "—",
                       "differs_in": [v[0] for v in values if v[2] == "farklı"][:6]})
    hard = [c for c in checks if c["status"] == "farklı"]
    return checks, ("farklı" if hard else "var")


def emissive_product_matches(m, props, overs):
    """Blender's exporter folds emissiveStrength into emissiveFactor: compare emissive × intensity."""
    overs = overs if isinstance(overs, list) else [overs] if overs else []
    cands = [c for c in [props.get("emissive")] + overs if isinstance(c, str)]
    if not cands:
        return False
    inten = props.get("emissiveIntensity", 1)
    inten = inten if isinstance(inten, (int, float)) else 1
    got = [x * g_ext(m, "KHR_materials_emissive_strength", "emissiveStrength", 1) for x in m.get("emissiveFactor", [0, 0, 0])]
    for c in cands:
        want = [x * inten for x in hex_to_linear(c)]
        if close(got, want):
            return True
    return False


# The package names a generic tile body ('tas', 'tas_kenar'); tiles3d.js applyBody renames it per skin and
# sets per-skin values (tiles3d.js:104-113). The package values are the classic skin's.
ALIASES = {"tas": "^tas_classic$", "tas_kenar": "^tas_kenar_classic$"}
ALIAS_NOTE = "tiles3d.js:104-113 applyBody: ad tas_<skin> / tas_kenar_<skin>; paket değerleri klasik setin"

# Where the project's Unity code handles a material (read-only references for the matrix).
PROJECT_CODE = {
    "led": "SkinService.cs:59 emisyon ← A1; TableFx.cs:84 LED nabzı",
    "led_cizgi": "SkinService.cs:68 renk ← A1 (alfa korunur)",
    "masa_holo_cizgi": "SkinService.cs:68 renk ← A1 (alfa korunur)",
    "holo": "SkinService.cs:68 renk ← A1 (andromeda istisnası yok); TableFx.cs:76-80 additive, dönüş",
    "masa_kenar": "SkinService.cs:70 renk ← Rim",
    "deri": "SkinService.cs:72 renk ← Leather (pn)",
    "duvar": "SkinService.cs:63 emisyon ← A1",
    "masa_cam_kenar": "SkinService.cs:65 emisyon ← A1",
    "masa_holo_led": "SkinService.cs:59 emisyon ← A1",
    "cuha": "TableScene.cs:296-308 Resources/Galaxy101/Table/Felt dokusu; SkinService.cs:78 speküler eşleme",
    "tas": "SkinService.cs:76 tas_classic renk ← Tile (tf1)",
    "atma_alani": "TableScene.cs:334 kodda üretilir (Unlit, A1, opaklık 0.4)",
    "atma_dolgu": "TableFx.cs:60 kodda üretilir (additive)",
    "hale": "TableFx.cs:62 deste_parilti kodda üretilir; masa_hale yok",
    "gecerli": "TileFactory.cs:159 kodda üretilir (#3CCB6E)",
    "yeni_tas": "TileFactory.cs:160 kodda üretilir",
    "*_duz": "TileFactory.cs:48 tas_yuz_isiksiz_<skin> (Galaxy101/FlatFace)",
    "simge_*": "Unity'de 3D simge yok (HUD)",
}


def find(inv, entry):
    occ = []
    rx = re.compile(ALIASES.get(entry["name"], entry["pattern"])) if entry.get("pattern") else None
    for f in inv:
        for m in f["materials"]:
            n = m.get("name")
            if n and rx and rx.match(n):
                occ.append((f, m))
    return occ


def material_map(new, proj, mj, tok):
    entries = package_materials(mj)
    rows, covered = [], set()
    for e in entries:
        occ, pocc = find(new, e), find(proj, e)
        for _, m in occ:
            covered.add(m.get("name"))
        row = {"id": e["id"], "source": e["source"], "type": e["type"], "name": e["name"],
               "package": {k: (v if not isinstance(v, dict) else v["expr"]) for k, v in e["props"].items()}}
        if e["name"] in ALIASES:
            row["alias"] = {"glb_name": ALIASES[e["name"]].strip("^$"), "note": ALIAS_NOTE}
        if e["name"] in PROJECT_CODE:
            row["project_code"] = PROJECT_CODE[e["name"]]
        if occ:
            checks, status = check_material(e, occ, tok)
            row["v4"] = {"files": len(occ), "kinds": sorted({f["kind"] + ("/" + f["variant"] if f["variant"] else "") for f, _ in occ}),
                         "themes": sorted({f["theme"] or "ortak" for f, _ in occ}), "checks": checks, "status": status}
        else:
            row["v4"] = {"files": 0, "status": "eksik"}
        if pocc:
            checks, status = check_material(e, pocc, tok)
            row["project"] = {"files": len(pocc), "kinds": sorted({f["kind"] + ("/" + f["variant"] if f["variant"] else "") for f, _ in pocc}),
                              "checks": checks, "status": status}
        else:
            row["project"] = {"files": 0, "status": "eksik"}
        rows.append(row)
    # GLB material names the package does not list.
    extra = {}
    for f in new:
        for m in f["materials"]:
            n = m.get("name")
            key = re.sub(r"^(tas_yuz_[a-z]+)_\w+$", r"\1_*", n) if n else "(adsız)"
            if n in covered:
                continue
            x = extra.setdefault(key, {"name": key, "files": set(), "kinds": set(), "instances": 0})
            x["files"].add(f["file"]); x["kinds"].add(f["kind"] + ("/" + f["variant"] if f["variant"] else "")); x["instances"] += 1
    proj_names = {m.get("name") for f in proj for m in f["materials"]}
    extras = []
    for k, x in sorted(extra.items()):
        base = k[:-2] if k.endswith("_*") else k
        extras.append({"name": k, "files": len(x["files"]), "kinds": sorted(x["kinds"]),
                       "in_project": any((n or "").startswith(base) for n in proj_names) if k.endswith("_*") else k in proj_names})
    return rows, extras


# ---------- lights ----------
def light_map(new_by_file, lighting, tok):
    scenes = {f["theme"]: f for f in new_by_file.values() if f["kind"] == "scene"}
    sat = scenes["saturn"]
    by_type = {}
    for l in sat["lights"]:
        by_type.setdefault(l["type"], []).append(l)

    def pick(kind, pos=None, intensity=None):
        cands = by_type.get(kind, [])
        for l in cands:
            if pos is not None and isinstance(pos, list) and max(abs(a - b) for a, b in zip(l["world_pos"], pos)) < 1e-3:
                return l
            if intensity is not None and abs(l["intensity"] - intensity) < 1e-6 and pos is None:
                return l
        return None

    project = {
        "hemi": "TableScene.cs:157 ThreeEnvironment.Apply(..., theme.Sky, #0a0a14, 0.45): özel ambient probe (ortam ×0.75 + yarıküre), Gradient değil",
        "key": "TableScene.cs:167-170 Directional #fff4e2, 1.7×(1/π), konum (1.1,4.2,2.0) → hedef (0,TY,0); yumuşak gölge, VeryHigh, bias 1.0 / normal 0.8 (URP texel birimi)",
        "spot": "TableScene.cs:171-173 Spot #ffffff, 9 decay 1.6 → URP şiddeti çuhada eşlenir, menzil 7, açı 2·0.62 rad, iç = 0.3·dış",
        "back": "TableScene.cs:174 Point theme.A2, 5 decay 1.4 → çuhada eşlenir, menzil 7, konum (0,1.5,-2.3)",
        "fill": "TableScene.cs:175 Point #9fb6ff, 1.6 decay 1.5 → çuhada eşlenir, menzil 6, konum (0,1.6,2.2)",
        "nukeLight": "TableFx.cs:204-205 Point #FFB25A, menzil 4.5, 90·e^(−3t) (ThreeLocalIntensity(90,1.4,0.3)), konum iniş + 0.25 m",
        "winSpot": "TableFx.cs:66-67, 237, 290 Spot #FFE7A3, menzil 5, açı 2·0.32 rad, iç 0.4·dış, 1.8 m yukarıda, 40·sin(...) 3.4 sn",
    }
    rows = []
    for L in lighting["lights"]:
        name = L["name"]
        pos = L.get("position") if isinstance(L.get("position"), list) else None
        kind = {"Directional": "directional", "Spot": "spot", "Point": "point", "Hemisphere": None}[L["type"]]
        g = None
        if kind == "point" and name == "nukeLight":
            g = next((l for l in by_type.get("point", []) if abs(l.get("range", 0) - 4.5) < 1e-6), None)
        elif kind == "spot" and name == "winSpot":
            g = next((l for l in by_type.get("spot", []) if abs(l["spot"]["outerConeAngle"] - 0.32) < 1e-6), None)
        elif kind:
            g = pick(kind, pos)
        glb = None
        if g:
            glb = {"node": g["path"], "type": g["type"], "color_linear": g["color"], "color_hex": linear_to_hex(g["color"]),
                   "intensity": g["intensity"], "range": g.get("range"), "spot": g.get("spot"),
                   "position": g["world_pos"], "forward": g["forward"]}
            # Per theme: only colour can change (back = glow).
            glb["color_by_theme"] = {}
            for t, f in sorted(scenes.items()):
                for l in f["lights"]:
                    if l["type"] == g["type"] and max(abs(a - b) for a, b in zip(l["world_pos"], g["world_pos"])) < 1e-6 and abs(l["intensity"] - g["intensity"]) < 1e-6 and l.get("range") == g.get("range"):
                        glb["color_by_theme"][t] = linear_to_hex(l["color"])
        checks = []
        if g:
            def add(prop, pv, gv, ok, note=""):
                checks.append({"prop": prop, "package": pv, "glb": gv, "status": "var" if ok else "farklı", **({"note": note} if note else {})})
            col = L.get("color", "")
            hexcol = re.match(r"#[0-9A-Fa-f]{6}", col)
            if hexcol:
                base_ok = close(g["color"], hex_to_linear(hexcol.group(0)))
                theme_ok = name == "back" and glb["color_hex"].upper() == tok["saturn"]["glow"].upper()
                add("color", col, glb["color_hex"], base_ok or theme_ok, "Satürn glow (= a2, G101-Masa.dc.html:885)" if theme_ok and not base_ok else "")
            if isinstance(L.get("intensity"), (int, float)):
                add("intensity", L["intensity"], g["intensity"], abs(L["intensity"] - g["intensity"]) < 1e-6)
            else:
                add("intensity", L.get("intensity"), g["intensity"], g["intensity"] == 0, "çalışma anında canlanır; dışa aktarımda dinlenme değeri (0)")
            if "distance" in L:
                add("distance→range", L["distance"], g.get("range"), abs(L["distance"] - (g.get("range") or 0)) < 1e-6)
            if "angle" in L:
                add("angle→outerConeAngle", L["angle"], g["spot"]["outerConeAngle"], abs(L["angle"] - g["spot"]["outerConeAngle"]) < 1e-4)
                inner = (1 - L["penumbra"]) * L["angle"]
                add("penumbra→innerConeAngle", f"(1−{L['penumbra']})·{L['angle']} = {round(inner, 4)}", g["spot"]["innerConeAngle"], abs(inner - g["spot"]["innerConeAngle"]) < 1e-3)
            if "decay" in L:
                checks.append({"prop": "decay", "package": L["decay"], "glb": None, "status": "glTF'e geçmez", "note": "KHR_lights_punctual yalnız ters kare azalma tanır"})
            if pos:
                add("position", pos, g["world_pos"], max(abs(a - b) for a, b in zip(pos, g["world_pos"])) < 1e-4)
            elif "position" in L:
                checks.append({"prop": "position", "package": L["position"], "glb": g["world_pos"], "status": "çalışma anı", "note": "dinamik; dinlenme konumunda dışa aktarılmış"})
            if "target" in L:
                want = [L["target"][i] - (pos or g["world_pos"])[i] for i in range(3)]
                n = math.sqrt(sum(x * x for x in want)) or 1
                want = [x / n for x in want]
                add("target→direction", L["target"], [round(x, 4) for x in g["forward"]], close([round(x, 3) for x in g["forward"]], [round(x, 3) for x in want]),
                    "GLTFExporter hedefi taşımaz: düğüm dönüşü birim, ışık −Z yönüne bakar; hedef ayrı boş düğüm")
            if "shadow" in L:
                checks.append({"prop": "shadow", "package": L["shadow"], "glb": None, "status": "glTF'e geçmez"})
        status = "pakette (GLB'ye aktarılamaz)" if L["type"] == "Hemisphere" else ("farklı" if any(c["status"] == "farklı" for c in checks) else "var") if g else "eksik"
        rows.append({"id": f"lights.{name}", "source": f"lighting.json lights[{lighting['lights'].index(L)}] ({name})", "package": L,
                     "sahne_tam": glb, "checks": checks, "status_v4": status, "project": project.get(name)})
    # renderer / environment / ambient
    project_settings = {
        "renderer.toneMapping": "ThreeToneMappingFeature.cs:10-55 three.js ACESFilmic geçişi; TableScene.cs:183-189 URP Tonemapping None, Bloom 0, Vignette 0",
        "renderer.exposure": "ThreeToneMapping.cs:13 DesignExposure = 1.05",
        "renderer.outputColorSpace": "Unity doğrusal renk uzayı, sRGB çıkış (ProjectSettings)",
        "renderer.shadows": "TableScene.cs:137 shadowDistance 4.5, 1 cascade, MSAA 4; Assets/Settings/PC_RPAsset.asset ana ışık 2048 yumuşak; Mobile_RPAsset.asset 1024, yumuşak kapalı",
        "renderer.pixelRatio": "Table/Scripts içinde masaya özel değil (render ölçeği proje ayarı)",
        "renderer.clear": "TableScene.cs BuildCamera: SolidColor theme.Bg3 (opak); satır 207: galaksi arka planı için şeffaf mod",
        "environment": "ThreeEnvironment.cs:7-69 koyu 12 m oda #10131f + 4 panel, GGX ön-süzgeçli yansıma küpü, yoğunluk 0.75, ambient probe",
        "ambient.led": "TableFx.cs:84 LED malzemelerini (led* / *_led*) toplar, emisyonu canlandırır",
        "ambient.holo": "TableFx.cs:76-80 holo_halka: dönüş ve opaklık, additive karışım",
        "ambient.cameraSway": "TableCameraRig.cs (boşta salınım; tween3d.json camera.idleSway)",
    }
    settings = []
    for k, v in lighting["renderer"].items():
        settings.append({"id": f"renderer.{k}", "source": f"lighting.json renderer.{k}", "package": v, "sahne_tam": None,
                         "status_v4": "glTF'e geçmez", "project": project_settings.get(f"renderer.{k}")})
    settings.append({"id": "environment", "source": "lighting.json environment", "package": lighting["environment"], "sahne_tam": None,
                     "status_v4": "glTF'e geçmez", "project": project_settings["environment"]})
    for k, v in lighting["ambient"].items():
        settings.append({"id": f"ambient.{k}", "source": f"lighting.json ambient.{k}", "package": v, "sahne_tam": None,
                         "status_v4": "glTF'e geçmez (animasyon)", "project": project_settings.get(f"ambient.{k}")})
    settings.append({"id": "glbNote", "source": "lighting.json glbNote", "package": lighting["glbNote"],
                     "sahne_tam": {"lights": len(sat["lights"]), "extension": "KHR_lights_punctual" in sat["extensionsUsed"]},
                     "status_v4": "var", "project": None})
    # Nodes in sahne_tam that are not lights but belong to them (targets, the hemisphere light as an empty node).
    parents = {n.get("parent") for n in sat["nodes"]}
    empties = [n for n in sat["nodes"] if n.get("parent") == 0 and not n.get("name") and "mesh" not in n and "light" not in n and n["i"] not in parents]
    return rows, settings, [{"node": f"sahne_tam/#{n['i']}", "world_pos": n.get("world_pos")} for n in empties]


# ---------- layout ----------
def unity(p):
    return [-p[0], p[1], p[2]] if p else p


def layout(new_by_file, tween3d):
    f = next(x for x in new_by_file.values() if x["kind"] == "scene" and x["theme"] == "saturn")
    nodes = {n["i"]: n for n in f["nodes"]}
    lay = {l["i"]: l for l in f["layout"]}
    TY, RZ, TILT = 0.76, 0.8, -0.36  # TableConstants.cs:8 (project)
    out = {"source": "saturn/galaxy101_sahne_tam_saturn.glb (layout identical in all five themes; only colours differ)",
           "space": "gltf = three.js (right-handed, Y up, m); unity = glTFast import (x mirrored)",
           "scene_skins": {"table": "andromeda", "rack": "andromeda", "tiles": "marble (deste, 106 instances)",
                           "why": "Araç - 3D Varlık Dışa Aktarma.html exports sahne_tam after the table/rack/tile loops; table3d.js exportAsset('scene') clones the live scene"},
           "parts": [], "seats": [], "lights": [], "checks": []}
    for i, l in sorted(lay.items()):
        n = nodes[i]
        if "light" in n:
            continue
        entry = {"node": l["name"] or f"#{i}", "gltf": {"translation": l["translation"], "rotation_euler_xyz": l["rotation_euler_xyz"]},
                 "unity": {"position": unity(l["translation"])}, "bounds_gltf": l["bounds"]}
        if l["scale"] != [1, 1, 1]:
            entry["gltf"]["scale"] = l["scale"]
        if not l["name"] and any(nodes[c]["name"] and nodes[c]["name"].startswith(("istaka", "koltuk")) for c in [k for k, x in nodes.items() if x.get("parent") == i]):
            continue
        out["parts"].append(entry)
    # Seats: unnamed groups whose children are istakaN / koltukN.
    for i, n in sorted(nodes.items()):
        kids = [nodes[k] for k in nodes if nodes[k].get("parent") == i]
        rack = next((k for k in kids if (k.get("name") or "").startswith("istaka")), None)
        if n.get("parent") == 0 and rack:
            chair = next((k for k in kids if (k.get("name") or "").startswith("koltuk")), None)
            yaw = lay[i]["rotation_euler_xyz"][1] if abs(lay[i]["rotation_euler_xyz"][0]) < 1e-3 else math.pi  # (π, 0, π) = yaw π
            m = rack["matrix"]; tilt = math.atan2(m[6], m[5])
            seat = {"seat": int(rack["name"][-1]), "yaw_rad": round(yaw, 5), "rack": {"node": rack["name"], "local_translation": m[12:15], "local_tilt_x_rad": round(tilt, 5),
                    "world_gltf": rack["world_pos"], "world_unity": unity(rack["world_pos"])}}
            if chair:
                seat["chair"] = {"node": chair["name"], "local_translation": chair["matrix"][12:15], "world_gltf": chair["world_pos"], "world_unity": unity(chair["world_pos"])}
            out["seats"].append(seat)
    for l in f["lights"]:
        out["lights"].append({"node": l["path"], "type": l["type"], "gltf": l["world_pos"], "unity": unity(l["world_pos"]), "forward_gltf": l["forward"]})
    # Checks against the project's constants (Assets/Galaxy101/Table/Scripts/Core/TableConstants.cs).
    def chk(what, project, glb, ok, src):
        out["checks"].append({"what": what, "project": project, "sahne_tam": glb, "status": "var" if ok else "farklı", "project_source": src})
    cuha = next(p for p in out["parts"] if p["node"] == "cuha")
    chk("masa yüzeyi TY (cuha y)", TY, cuha["gltf"]["translation"][1], abs(cuha["gltf"]["translation"][1] - TY) < 1e-4, "TableConstants.cs:8 TY")
    for s in out["seats"]:
        want_yaw = [0, math.pi / 2, math.pi, -math.pi / 2][s["seat"]]
        chk(f"koltuk {s['seat']} yönü (SeatYaw)", round(want_yaw, 5), s["yaw_rad"], abs(abs(s["yaw_rad"]) - abs(want_yaw)) < 1e-3 and (want_yaw == math.pi or abs(s["yaw_rad"] - want_yaw) < 1e-3), "TableConstants.cs:10 SeatYaw")
        lt = s["rack"]["local_translation"]
        chk(f"ıstaka {s['seat']} RackLocal", [0, round(TY + 0.033, 4), RZ], lt, close(lt, [0, TY + 0.033, RZ]), "TableConstants.cs:19 RackLocal")
        chk(f"ıstaka {s['seat']} TILT", TILT, s["rack"]["local_tilt_x_rad"], abs(s["rack"]["local_tilt_x_rad"] - TILT) < 1e-3, "TableConstants.cs:8 TILT")
        if "chair" in s:
            chk(f"koltuk {s['seat']} ChairLocal", [0, 0, 1.32], s["chair"]["local_translation"], close(s["chair"]["local_translation"], [0, 0, 1.32]), "TableConstants.cs:20 ChairLocal")
    seat0_chair = any("chair" in s for s in out["seats"] if s["seat"] == 0)
    chk("oyuncu koltuğu (seat 0) yok", "TableScene.cs:246-248 koltuk yalnız rakiplere", "seat 0: yalnız ıstaka" if not seat0_chair else "seat 0: koltuk var", not seat0_chair, "TableScene.cs:246")
    zone = next(p for p in out["parts"] if p["node"] == "atma_halka" and p["gltf"]["translation"][0] > 0)
    chk("atma halkası DiscardLocal + TY+0.0012", [0.6, 0.7612, 0.6], zone["gltf"]["translation"], close(zone["gltf"]["translation"], [0.6, 0.7612, 0.6]), "TableConstants.cs:21 DiscardLocal, TableScene.cs:330")
    side = next(p for p in out["parts"] if p["node"] == "atma_halka" and p["gltf"]["translation"][0] < 0)
    chk("soldan alma halkası (seat 3 discard)", [-0.6, 0.7612, 0.6], side["gltf"]["translation"], close(side["gltf"]["translation"], [-0.6, 0.7612, 0.6]), "TableScene.cs:450 DiscardCenter(3)")
    out["camera"] = {"in_glb": bool(f["cameras"]), "note": "sahne_tam has no camera (table3d.js camera is not a scene child); views from tween3d.json",
                     "fov": tween3d["camera"]["fov"], "views": tween3d["camera"]["views"],
                     "project": {"FieldOfView": 40, "PlayerView": "seat", "TopView": "top", "DealView": "deal", "LobbyView": "over",
                                 "source": "TableConstants.cs:24-30", "missing_in_project": ["mid"]}}
    return out


def main():
    new_path, proj_path, pkg, out_dir = sys.argv[1:5]
    new = json.load(open(new_path, encoding="utf-8"))["files"]
    proj = json.load(open(proj_path, encoding="utf-8"))["files"]
    mj = json.load(open(os.path.join(pkg, "assets", "materials.json"), encoding="utf-8"))
    lighting = json.load(open(os.path.join(pkg, "assets", "lighting.json"), encoding="utf-8"))
    themes = json.load(open(os.path.join(pkg, "assets", "themes.json"), encoding="utf-8"))
    tween3d = json.load(open(os.path.join(pkg, "anim", "tween3d.json"), encoding="utf-8"))
    tok = theme_tokens(themes)
    rows, extras = material_map(new, proj, mj, tok)
    by_file = {f["file"]: f for f in new}
    lights, settings, empties = light_map(by_file, lighting, tok)
    lay = layout(by_file, tween3d)
    os.makedirs(out_dir, exist_ok=True)
    summary = {
        "package_entries": len(rows),
        "v4": {s: sum(1 for r in rows if r["v4"]["status"] == s) for s in ("var", "farklı", "eksik")},
        "project": {s: sum(1 for r in rows if r["project"]["status"] == s) for s in ("var", "farklı", "eksik")},
        "glb_names_not_in_package": len(extras),
        "theme_tokens": tok,
    }
    json.dump({"tool": "tools/blender/material_light_map.py", "summary": summary, "materials": rows, "not_in_package": extras},
              open(os.path.join(out_dir, "material_map.json"), "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=1)
    json.dump({"tool": "tools/blender/material_light_map.py", "lights": lights, "settings": settings, "sahne_tam_empty_nodes": empties},
              open(os.path.join(out_dir, "light_map.json"), "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=1)
    json.dump(lay, open(os.path.join(out_dir, "sahne_tam_layout.json"), "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=1)
    print("MATERIALS", json.dumps(summary["v4"]), "project", json.dumps(summary["project"]), "extra names", len(extras))
    print("LIGHTS", [(l["id"], l["status_v4"]) for l in lights])
    print("LAYOUT checks", sum(1 for c in lay["checks"] if c["status"] == "var"), "/", len(lay["checks"]))


if __name__ == "__main__":
    main()
