"""Galaxy 101 · v4 Hat C: write docs/v4/matris/hat-c.md from the JSON reports in docs/v4/hat-c.

One row per GLB (103), per materials.json entry, per GLB material name the package does not list, per lighting.json
light / setting and per sahne_tam layout check. Columns (Turkish, as the v4 matrices):
| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum | v4 sonrası |

Usage:
  python tools/blender/hat_c_matrix.py docs/v4/hat-c docs/v4/matris/hat-c.md
"""
import json
import os
import re
import sys

KIND_TR = {"table": "masa", "rack": "ıstaka", "tile_set": "taş seti", "tile": "tek taş", "markers": "oyun işaretleri",
           "scene": "tam sahne", "die": "zar", "chair": "koltuk", "room": "oda", "treat": "ikram", "gift": "hediye"}
THEME_TR = {"saturn": "Satürn", "mars": "Mars", "neptun": "Neptün", "jupiter": "Jüpiter", "venus": "Venüs", None: "ortak"}
PKG = "galaxy101_3d_tum_temalar"


def cell(v):
    s = v if isinstance(v, str) else json.dumps(v, ensure_ascii=False)
    return s.replace("|", "\\|").replace("\n", " ")


def num(n):
    return f"{n:,}".replace(",", " ")


def size(b):
    return "×".join(f"{x:.3f}".rstrip("0").rstrip(".") for x in b["size"]) + " m" if b else "—"


def group(name):
    m = re.fullmatch(r"(tas_yuz_[a-z]+)_\w+", name)
    return m.group(1) + "_*" if m else name


def collapse_tex(changes):
    out = {}
    for name, slots in changes.items():
        g = group(name); out.setdefault(g, [0, set()]); out[g][0] += 1
        for slot, v in slots.items():
            if isinstance(v, dict) and v.get("size") and v["size"][0] != v["size"][1]:
                out[g][1].add(f"{v['size'][1][0]}→{v['size'][0][0]} px")
    return [f"{g}{f' ({n})' if n > 1 else ''}{(' ' + ', '.join(sorted(sz))) if sz else ''}" for g, (n, sz) in sorted(out.items())]


def collapse_mat(changes):
    out = {}
    for name, props in changes.items():
        g = group(name)
        for prop, (new, old) in props.items():
            key = prop.split(".")[-1]
            out.setdefault(g, {}).setdefault(key, set()).add(f"{old}→{new}")
    res = []
    for g, props in sorted(out.items()):
        res.append(g + " " + "; ".join(f"{k} {', '.join(sorted(v))}" for k, v in props.items()))
    return res


def glb_rows(inv, proj, cmp, contract, placement):
    by_dest = {p["source"].split(PKG + "/", 1)[1]: p for p in placement["files"]}
    pfiles = {f["stem"]: f for f in proj["files"]}
    issues = {}
    for r in contract["results"]:
        if r["status"] == "uymaz":
            issues.setdefault(r["file"], []).append(r["check"] + (f" ({r['detail']})" if isinstance(r["detail"], str) and r["detail"] else ""))
    rows = []
    for f in inv["files"]:
        c = f["counts"]
        pkg = (f"{KIND_TR[f['kind']]}{' ' + f['variant'] if f['variant'] else ''} · {THEME_TR[f['theme']]}; "
               f"{c['nodes']} düğüm, {c['materials']} malzeme, {num(c['triangles'])} üçgen"
               + (f" ({num(c['triangles_drawn'])} çizilen, {c['instances']} örnek)" if c["instances"] else "")
               + f", {c['images']} doku, {size(f['bounds'])}"
               + (f", {c['lights']} ışık" if c["lights"] else "")
               + f" (`{PKG}/{f['file']}`)")
        vp = cmp["vs_project"][f["file"]]
        p = pfiles.get(f["stem"])
        if p:
            pc = p["counts"]
            lod = sum(1 for n in p["node_names"] if n and n.endswith("_LOD1"))
            proj_txt = (f"`Models/{f['stem']}.glb` ({(p['generator'] or '').split(' ')[0]}; {num(pc['triangles'])} üçgen"
                        + (f", {lod} LOD1" if lod else "") + f", {pc['images']} doku)")
        else:
            proj_txt = "yok"
        raw = cmp["vs_old_raw"].get(f["file"])
        if vp["status"] == "yeni":
            durum = "eksik"
            if f["theme"] and f["theme"] != "saturn":
                th = cmp["across_themes"].get(f["stem"][: -len(f["theme"]) - 1], {}).get(f["theme"], {})
                what = th.get("what", [])
                if what == ["aynı dosya"]:
                    durum += " (Satürn dosyasıyla bayt bayt aynı)"
                elif th.get("materials") or th.get("textures"):
                    durum += " (Satürn'den farkı yalnız malzeme rengi/doku: " + ", ".join((th.get("materials") or []) + [t + " dokusu" for t in th.get("textures") or []]) + ")"
                elif what:
                    durum += " (Satürn'den farkı: " + ", ".join(what) + ")"
        else:
            d = vp["diff"]
            if f["kind"] == "die":
                durum = "farklı (başka model: v4 three.js zarı, 6 dokulu yüz; proje make_die.py zarı)"
            elif raw and (raw["status"] == "aynı" or raw["diff"]["what"] == ["yalnız ikili düzen (içerik aynı)"]):
                durum = "farklı (yalnız Blender cilası: bevel, LOD1, 2K doku; içerik ham dışa aktarımla aynı)" if p and p["generator"].startswith("Khronos") else "var (içerik aynı)"
            elif raw:
                rd = raw["diff"]
                det = []
                if rd["texture_changes"]:
                    det.append("doku: " + ", ".join(collapse_tex(rd["texture_changes"])))
                if rd["material_factor_changes"]:
                    det.append("değer (eski ham → v4): " + ", ".join(collapse_mat(rd["material_factor_changes"])))
                pre = "farklı (cila + v4 içerik farkı: " if p and p["generator"].startswith("Khronos") else "farklı (v4 içerik farkı: "
                durum = pre + "; ".join(det) + ")"
            else:
                durum = "farklı (" + ", ".join(d.get("what", [])) + ")"
        dest = by_dest[f["file"]]
        after = f"`{dest['dest'].split('Table/', 1)[1]}` (guid `{dest['guid'][:8]}…`)"
        k = f["kind"]
        if k == "table":
            after += "; holo_halka, cuha, LED adları ✓"
        elif k == "rack":
            after += "; kök TILT −0.36 ✓"
        elif k == "tile_set":
            after += "; tas_R1/tas_govde/tas_yuz ✓"
        elif k == "markers":
            after += "; referans (Unity işaretleri kodla üretir), simge_* dokuları yeni"
        elif k == "scene":
            after += "; yerleşim/ışık referansı (sahne_tam_layout.json, light_map.json); ışık yönleri GLB'de kayıp"
        elif k == "die":
            after += "; prefab için kullanılmaz (2↔5 ayna, pivot alt yüzde) — EK-306"
        if f["file"] in issues:
            after += "; ⚠ " + "; ".join(sorted(set(issues[f["file"]])))
        rows.append([f"`{f['file']}`", pkg, proj_txt, durum, after])
    return rows


def material_rows(mm):
    rows = []
    for r in mm["materials"]:
        pk = ", ".join(f"{k}: {v}" for k, v in r["package"].items() if k != "name")
        pkg = f"`{r['name']}` {r['type']} {{{pk}}} ({r['source']})"
        p = r["project"]
        if p["files"]:
            bad = [f"{c['prop']} {c['glb'][0] if isinstance(c['glb'], list) and c['glb'] else c['glb']}" for c in p["checks"] if c["status"] == "farklı"]
            proj = f"{p['files']} proje GLB'sinde ({', '.join(p['kinds'])})" + (f"; farklı: {', '.join(bad)}" if bad else "")
        else:
            proj = "GLB'de yok"
        if r.get("project_code"):
            proj += f"; kod: {r['project_code']}"
        durum = p["status"] if p["files"] else ("eksik (kodda üretilir)" if r.get("project_code") and "kodda" in r["project_code"] else "eksik")
        v = r["v4"]
        if v["files"]:
            notes = sorted({c["status"] for c in v["checks"] if c["status"] not in ("var", "—")})
            bad = [f"{c['prop']} {c['package']} → {c['glb']}" for c in v["checks"] if c["status"] == "farklı"]
            after = f"v4: {v['files']} dosyada, {v['status']}" + (f" ({'; '.join(bad)})" if bad else "") + (f"; notlar: {', '.join(notes)}" if notes else "")
        else:
            after = "v4 GLB'lerinde yok"
        if r.get("alias"):
            after += f"; GLB adı `{r['alias']['glb_name']}` ({r['alias']['note']})"
        if r["id"].startswith("tableSkins.r"):
            after += "; paket tutarsızlığı: table3d.js:872-874 tanımı kullanılmıyor, ıstakalar racks3d.js:88-112 ile üretiliyor (EK-301)"
        rows.append([f"`{r['id']}`", pkg, proj, durum, after])
    for x in mm["not_in_package"]:
        proj = "proje GLB'lerinde var" if x["in_project"] else "proje GLB'lerinde yok"
        rows.append([f"`{x['name']}`", "pakette yok (materials.json listelemiyor)", proj, "pakette yok",
                     f"v4: {x['files']} dosyada ({', '.join(x['kinds'])})"])
    return rows


def light_rows(lm):
    rows = []
    for l in lm["lights"]:
        P = l["package"]
        pk = ", ".join(f"{k}: {v}" for k, v in P.items() if k not in ("name", "glb"))
        if P.get("glb"):
            pk += f"; {P['glb']}"
        g = l["sahne_tam"]
        bad = [c for c in l["checks"] if c["status"] == "farklı"]
        if g:
            after = (f"sahne_tam: {g['type']} {g['color_hex']} şiddet {g['intensity']}" + (f", menzil {g['range']}" if g.get("range") else "")
                     + (f", koni {g['spot']['outerConeAngle']}/{g['spot']['innerConeAngle']}" if g.get("spot") else "")
                     + f", konum {g['position']}")
            if bad:
                after += "; farklı: " + "; ".join(f"{c['prop']} ({c.get('note', '')})" for c in bad)
            if any(c["prop"] == "decay" for c in l["checks"]):
                after += "; decay glTF'e geçmez"
            if len(set(g.get("color_by_theme", {}).values())) > 1:
                after += "; tema rengi " + ", ".join(f"{t}: {c}" for t, c in g["color_by_theme"].items())
            after += f" → durum {l['status_v4']}"
        else:
            after = l["status_v4"] + "; Unity değeri lighting.json'daki gibi kurulur"
        durum = "var" if l["project"] else "eksik"
        if l["id"] == "lights.hemi":
            durum = "farklı (yöntem: Gradient yerine özel ambient probe)"
        if l["id"] == "lights.nukeLight":
            durum = "farklı (konum iniş + 0.25 m; paket 0.3 m)"
        rows.append([f"`{l['id']}`", f"{pk} ({l['source']})", cell(l["project"] or "—"), durum, after])
    for s in lm["settings"]:
        durum = "var" if s["project"] else "eksik"
        if s["id"] == "renderer.shadows":
            durum = "farklı (Mobile_RPAsset 1024, yumuşak gölge kapalı)"
        if s["id"] == "renderer.clear":
            durum = "farklı (düz renk theme.Bg3; şeffaf mod ayrı)"
        if s["id"] == "renderer.pixelRatio":
            durum = "pakette var, masa kodunda yok"
        if s["id"] == "glbNote":
            durum = "—"
        after = s["status_v4"] + (f"; sahne_tam: {s['sahne_tam']}" if s.get("sahne_tam") else "")
        rows.append([f"`{s['id']}`", f"{cell(s['package'])} ({s['source']})", cell(s["project"] or "—"), durum, after])
    return rows


def layout_rows(lay):
    rows = []
    for c in lay["checks"]:
        rows.append([f"yerleşim: {c['what']}", f"sahne_tam: {c['sahne_tam']}", f"{c['project']} ({c['project_source']})", c["status"], "sahne_tam_layout.json"])
    cam = lay["camera"]
    for name, v in cam["views"].items():
        proj = {"seat": "PlayerView", "top": "TopView", "deal": "DealView", "over": "LobbyView"}.get(name)
        rows.append([f"kamera: {name}", f"pos {v['pos']} → {v['target']}, fov {cam['fov']} (anim/tween3d.json camera.views.{name})",
                     f"TableConstants.cs:24-30 {proj}" if proj else "yok", "var" if proj else "eksik", "sahne_tam'da kamera yok; değer tween3d.json'dan"])
    return rows


def table(header, rows):
    out = ["| " + " | ".join(header) + " |", "|" + "---|" * len(header)]
    out += ["| " + " | ".join(cell(x) for x in r) + " |" for r in rows]
    return "\n".join(out)


def main():
    d, out = sys.argv[1], sys.argv[2]
    J = lambda n: json.load(open(os.path.join(d, n), encoding="utf-8"))
    inv, proj, cmp, mm, lm, lay, con, pl = (J("glb_inventory.json"), J("glb_inventory_proje.json"), J("compare.json"), J("material_map.json"),
                                            J("light_map.json"), J("sahne_tam_layout.json"), J("unity_contract.json"), J("placement.json"))
    H = ["Kimlik", "Pakette ne diyor (kaynak)", "Projede ne vardı (dosya)", "Durum", "v4 sonrası"]
    g, m, l, y = glb_rows(inv, proj, cmp, con, pl), material_rows(mm), light_rows(lm), layout_rows(lay)
    count = lambda rows: {s: sum(1 for r in rows if r[3].startswith(s)) for s in ("var", "farklı", "eksik", "pakette yok")}
    parts = [
        "# v4 fark matrisi · Hat C · 3D varlıklar",
        "",
        "Kaynak: `Design/Galaxy101_ClaudeCode/galaxy101_3d_tum_temalar/` (103 GLB) ve `prototip/galaxy101_handoff_v4/` (aşağıda `pkg/`): "
        "`assets/materials.json`, `assets/lighting.json`, `assets/themes.json`, `anim/tween3d.json`. Proje: `Assets/Galaxy101/Table/Models/*.glb` "
        "(Satürn + ortak, Blender cilalı) ve `Assets/Galaxy101/Table/Scripts/**` (salt okunur). Bu dosya `tools/blender/hat_c_matrix.py` ile "
        "`docs/v4/hat-c/*.json` raporlarından üretilir; elle düzenlemeyin, betiği yeniden çalıştırın.",
        "",
        "Durum: **var** paketle aynı · **farklı** projede var ama değer/içerik farklı · **eksik** pakette var, projede yok · **pakette yok** projede/GLB'de var, pakette tanımı yok. "
        "\"v4 sonrası\" sütunu bu hattın yerleştirdiği dosyayı (`Models/v4/...`) ve Unity aşamasının yapacağını söyler. Ayrıntı: `docs/v4/hat-c/RAPOR.md`, eksikler: `docs/v4/hat-c/EKSIKLER.md`.",
        "",
        f"Özet: GLB {len(g)} satır {count(g)} · malzeme {len(m)} satır {count(m)} · ışık/ayar {len(l)} satır {count(l)} · yerleşim/kamera {len(y)} satır {count(y)}.",
        "",
        "## 1 · GLB dosyaları (103)",
        "",
        table(H, g),
        "",
        "## 2 · Malzemeler (`pkg/assets/materials.json`) ve paketin listelemediği GLB malzeme adları",
        "",
        "three.js → glTF eşlemesi: renkler doğrusal (hex sRGB), opacity = baseColorFactor[3], emissiveIntensity = KHR_materials_emissive_strength, "
        "clearcoat* = KHR_materials_clearcoat, iridescence* = KHR_materials_iridescence, MeshBasicMaterial = KHR_materials_unlit. "
        "\"tema\" = applyLook()/rackKit.theme() o temanın rengini koyuyor (G101-Masa.dc.html:885 tableTheme eşlemesi); \"animasyon anı\" = LED nabzı (lighting.json ambient.led) dışa aktarım anında yakalanmış.",
        "",
        table(H, m),
        "",
        "## 3 · Işıklar ve görüntü ayarları (`pkg/assets/lighting.json`)",
        "",
        table(H, l),
        "",
        "## 4 · Yerleşim (sahne_tam) ve kamera görünümleri",
        "",
        table(H, y),
        "",
    ]
    os.makedirs(os.path.dirname(out), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        fh.write("\n".join(parts))
    print("MATRIX", out, "glb", len(g), count(g), "materials", len(m), count(m), "lights", len(l), count(l), "layout", len(y), count(y))


if __name__ == "__main__":
    main()
