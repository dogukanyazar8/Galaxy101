"""Galaxy 101 · v4 Hat C: compare GLB inventories (tools/blender/gltf_inventory.py output).

  * new v4 set (103) vs the project's Assets/Galaxy101/Table/Models (polished Satürn + shared, 29);
  * new v4 set vs Design/old/assets_src/glb (the raw three.js export the project models were polished from):
    separates design changes from the Blender polish (bevel, LOD1, 2K textures);
  * the same asset across the five themes: what a theme changes (materials, images, geometry).

Usage:
  python tools/blender/gltf_compare.py <new.json> <project.json> <old_raw.json> <out.json>
"""
import json
import sys

LOD = "_LOD1"


def load(path):
    return {f["file"].split("/")[-1]: f for f in json.load(open(path, encoding="utf-8"))["files"]}


def names(seq, strip_lod=False):
    out = {}
    for n in seq:
        if n is None:
            continue
        if strip_lod and n.endswith(LOD):
            continue
        out[n] = out.get(n, 0) + 1
    return out


def multiset_diff(a, b):
    added = {k: b[k] - a.get(k, 0) for k in b if b[k] > a.get(k, 0)}
    removed = {k: a[k] - b.get(k, 0) for k in a if a[k] > b.get(k, 0)}
    return added, removed


def close(x, y, tol=1e-3):
    if isinstance(x, (int, float)) and isinstance(y, (int, float)):
        return abs(x - y) <= tol
    if isinstance(x, list) and isinstance(y, list) and len(x) == len(y):
        return all(close(a, b, tol) for a, b in zip(x, y))
    if isinstance(x, dict) and isinstance(y, dict):
        return set(x) == set(y) and all(close(x[k], y[k], tol) for k in x)
    return x == y


def flatten(d, prefix=""):
    out = {}
    for k, v in (d or {}).items():
        key = prefix + k
        if isinstance(v, dict) and not ("image" in v or "sampler" in v):
            out.update(flatten(v, key + "."))
        else:
            out[key] = v
    return out


def material_view(m):
    """Factors and flags only; textures reduced to slot -> image (size, sha1) and transform."""
    flat = flatten({k: v for k, v in m.items() if k not in ("index", "name")})
    factors, textures = {}, {}
    for k, v in flat.items():
        if isinstance(v, dict) and "image" in v:
            img = v.get("image") or {}
            textures[k] = {"size": img.get("size"), "sha1": img.get("sha1"),
                           "transform": (v.get("extensions") or {}).get("KHR_texture_transform")}
        else:
            factors[k] = v
    return factors, textures


def material_diff(a, b):
    ma = {m.get("name"): m for m in a["materials"]}
    mb = {m.get("name"): m for m in b["materials"]}
    changes, tex = {}, {}
    for name in sorted(set(ma) & set(mb), key=str):
        fa, ta = material_view(ma[name]); fb, tb = material_view(mb[name])
        d = {k: [fa.get(k), fb.get(k)] for k in sorted(set(fa) | set(fb)) if not close(fa.get(k), fb.get(k))}
        if d:
            changes[name] = d
        t = {}
        for slot in sorted(set(ta) | set(tb)):
            x, y = ta.get(slot), tb.get(slot)
            if x is None or y is None:
                t[slot] = ["-" if x is None else x["size"], "-" if y is None else y["size"]]
            elif x["sha1"] != y["sha1"] or not close(x["transform"], y["transform"]):
                t[slot] = {"size": [x["size"], y["size"]], "same_pixels": x["sha1"] == y["sha1"],
                           **({"transform": [x["transform"], y["transform"]]} if not close(x["transform"], y["transform"]) else {})}
        if t:
            tex[name] = t
    return changes, tex


def node_diff(a, b, strip_lod):
    """World position change of same-named nodes (first occurrence), in millimetres."""
    pa = {}
    for n in a["nodes"]:
        if n.get("name") and n["name"] not in pa and "world_pos" in n:
            pa[n["name"]] = n["world_pos"]
    moved = []
    seen = set()
    for n in b["nodes"]:
        nm = n.get("name")
        if not nm or nm in seen or (strip_lod and nm.endswith(LOD)) or nm not in pa or "world_pos" not in n:
            continue
        seen.add(nm)
        d = max(abs(x - y) for x, y in zip(pa[nm], n["world_pos"])) * 1000
        if d > 0.01:
            moved.append([nm, round(d, 3)])
    moved.sort(key=lambda x: -x[1])
    return moved


def bounds_delta(a, b):
    if not a["bounds"] or not b["bounds"]:
        return None
    return round(max(max(abs(x - y) for x, y in zip(a["bounds"][k], b["bounds"][k])) for k in ("min", "max")) * 1000, 3)


def diff(a, b, strip_lod=False):
    """a = reference (new v4), b = other file. Returns a compact description of what differs."""
    if a["sha256"] == b["sha256"]:
        return {"same_file": True}
    na, nb = names(a["node_names"]), names(b["node_names"], strip_lod)
    added, removed = multiset_diff(na, nb)
    ma, mb = names(a["material_names"]), names(b["material_names"], strip_lod)
    madd, mrem = multiset_diff(ma, mb)
    changes, tex = material_diff(a, b)
    roots = [{k: r[k] for k in ("name", "translation", "rotation_euler_xyz", "scale")} for r in a["roots"]]
    roots_b = [{k: r[k] for k in ("name", "translation", "rotation_euler_xyz", "scale")} for r in b["roots"]]
    out = {
        "same_file": False,
        "generator": [a["generator"], b["generator"]],
        "triangles": [a["counts"]["triangles"], b["counts"]["triangles"]],
        "nodes": [a["counts"]["nodes"], b["counts"]["nodes"]],
        "lod1_nodes_in_other": sum(1 for n in b["node_names"] if n and n.endswith(LOD)),
        "nodes_only_in_new": removed, "nodes_only_in_other": added,
        "materials_only_in_new": mrem, "materials_only_in_other": madd,
        "material_factor_changes": changes, "texture_changes": tex,
        "images": [len(a["images"]), len(b["images"])],
        "extensions": {"only_new": sorted(set(a["extensionsUsed"]) - set(b["extensionsUsed"])),
                       "only_other": sorted(set(b["extensionsUsed"]) - set(a["extensionsUsed"]))},
        "bounds_max_delta_mm": bounds_delta(a, b),
        "roots_equal": close(roots, roots_b, 1e-4),
        "moved_nodes_mm": node_diff(a, b, strip_lod)[:12],
    }
    if not out["roots_equal"]:
        out["roots"] = [roots, roots_b]
    geometry_same = out["bounds_max_delta_mm"] is not None and out["bounds_max_delta_mm"] < 0.01 and not out["moved_nodes_mm"]
    kinds = []
    if added or removed: kinds.append("düğüm adları")
    if madd or mrem: kinds.append("malzeme adları")
    if changes: kinds.append("malzeme değerleri")
    if tex: kinds.append("dokular")
    if not geometry_same: kinds.append("geometri/konum")
    if out["triangles"][0] != out["triangles"][1]: kinds.append("üçgen sayısı")
    if out["extensions"]["only_new"] or out["extensions"]["only_other"]: kinds.append("uzantılar")
    out["what"] = kinds or ["yalnız ikili düzen (içerik aynı)"]
    return out


def main():
    new, proj, raw, out_path = load(sys.argv[1]), load(sys.argv[2]), load(sys.argv[3]), sys.argv[4]
    vs_project, vs_raw = {}, {}
    for name, f in sorted(new.items()):
        p = proj.get(name)
        vs_project[f["file"]] = {"status": "yeni", "project_file": None} if p is None else {
            "status": "aynı" if p["sha256"] == f["sha256"] else "farklı", "project_file": "Assets/Galaxy101/Table/Models/" + name,
            "diff": diff(f, p, strip_lod=True)}
        o = raw.get(name)
        if o is not None:
            vs_raw[f["file"]] = {"status": "aynı" if o["sha256"] == f["sha256"] else "farklı", "diff": diff(f, o)}
    missing = sorted(set(proj) - set(new))
    # The same asset across themes: group by stem without the theme suffix.
    groups = {}
    for name, f in new.items():
        if f["theme"]:
            groups.setdefault(f["stem"][: -len(f["theme"]) - 1], {})[f["theme"]] = f
    themes = {}
    for g, by in sorted(groups.items()):
        ref = by.get("saturn")
        entry = {}
        for t, f in sorted(by.items()):
            if t == "saturn":
                continue
            d = diff(ref, f)
            entry[t] = {"what": d.get("what", ["aynı dosya"]), "materials": sorted(d.get("material_factor_changes", {}).keys()),
                        "textures": sorted(d.get("texture_changes", {}).keys()), "bounds_max_delta_mm": d.get("bounds_max_delta_mm")}
        themes[g] = entry
    summary = {
        "new_files": len(new), "project_files": len(proj),
        "vs_project": {s: sum(1 for v in vs_project.values() if v["status"] == s) for s in ("aynı", "farklı", "yeni")},
        "project_only": missing,
        "vs_old_raw": {s: sum(1 for v in vs_raw.values() if v["status"] == s) for s in ("aynı", "farklı")},
    }
    doc = {"tool": "tools/blender/gltf_compare.py", "reference": "new = Design/Galaxy101_ClaudeCode/galaxy101_3d_tum_temalar",
           "summary": summary, "vs_project": vs_project, "vs_old_raw": vs_raw, "across_themes": themes}
    with open(out_path, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(doc, fh, ensure_ascii=False, indent=1)
    print("COMPARE", json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
