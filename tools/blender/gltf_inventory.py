"""Galaxy 101 · v4 Hat C: inventory of GLB files without Blender.

Parses GLB containers directly (stdlib + numpy) so it runs while another session holds Unity:
for every *.glb under <root> it records
  * file facts: relative path, size, sha256, generator, extensionsUsed;
  * classification from the file name (theme, kind, package skin key);
  * node tree: names, parents, local TRS, world position, mesh / light / camera references;
  * meshes: vertex and triangle counts, primitive modes, materials per primitive;
  * exact bounds in glTF space (every POSITION transformed by its node's world matrix), per file and
    per top-level node (the layout of sahne_tam);
  * materials as stored (factors + extensions), with texture references resolved to image size/mime;
  * KHR_lights_punctual lights and cameras with world position and forward direction.

glTF space is three.js space (right-handed, Y up, metres). glTFast mirrors X on import, so a Unity
position is (-x, y, z) (Assets/Galaxy101/Table/Scripts/Core/ThreeSpace.cs).

Usage:
  uv run --with numpy python tools/blender/gltf_inventory.py <root> <out.json> [--label NAME] [--flat]
"""
import base64
import hashlib
import json
import math
import os
import re
import struct
import sys

import numpy as np

COMPONENT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT2": 4, "MAT3": 9, "MAT4": 16}
MODES = {0: "POINTS", 1: "LINES", 2: "LINE_LOOP", 3: "LINE_STRIP", 4: "TRIANGLES", 5: "TRIANGLE_STRIP", 6: "TRIANGLE_FAN"}
THEMES = ("saturn", "mars", "neptun", "jupiter", "venus")
# File stem part -> (kind, package skin key). Package keys: materials.json / TableAssetCatalog skins.
TABLES = {"istasyon": "station", "ceviz": "wood", "hologram": "holo", "andromeda": "andromeda"}
RACKS = {"ceviz": "walnut", "sedef": "sedef", "aluminyum": "chrome", "karbon": "carbon", "neon": "neon", "andromeda": "andromeda"}
TILESETS = {"klasik": "classic", "galaksi": "galaxy", "altin": "gold", "neon": "neon", "mermer": "marble"}
GIFTS = ("tea", "coffee", "cola", "simit", "baklava", "seeds", "flower", "cake", "trophy", "rocket")


def r(v, n=5):
    """Round floats (recursively) so the JSON stays readable and diff-friendly."""
    if isinstance(v, float):
        x = round(v, n)
        return 0.0 if x == 0 else x
    if isinstance(v, (list, tuple)):
        return [r(x, n) for x in v]
    if isinstance(v, dict):
        return {k: r(x, n) for k, x in v.items()}
    return v


def classify(stem):
    """galaxy101_<kind>_<variant>_<theme> -> dict(theme, kind, variant, key)."""
    s = stem[len("galaxy101_"):] if stem.startswith("galaxy101_") else stem
    theme = next((t for t in THEMES if s.endswith("_" + t)), None)
    if theme:
        s = s[: -len(theme) - 1]
    if s.startswith("masa_"):
        v = s[5:]; return dict(theme=theme, kind="table", variant=v, key=TABLES.get(v))
    if s.startswith("istaka_"):
        v = s[7:]; return dict(theme=theme, kind="rack", variant=v, key=RACKS.get(v))
    if s.startswith("tas_seti_"):
        v = s[9:]; return dict(theme=theme, kind="tile_set", variant=v, key=TILESETS.get(v))
    if s == "tas_tek":
        return dict(theme=theme, kind="tile", variant=None, key=None)
    if s == "isaretler":
        return dict(theme=theme, kind="markers", variant=None, key=None)
    if s == "sahne_tam":
        return dict(theme=theme, kind="scene", variant=None, key=None)
    if s == "zar":
        return dict(theme=theme, kind="die", variant=None, key=None)
    if s == "koltuk":
        return dict(theme=theme, kind="chair", variant=None, key=None)
    if s.startswith("oda_"):
        return dict(theme=theme, kind="room", variant=s[4:], key=None)
    if s.startswith("ikram_") or s.startswith("hediye_"):
        v = s.split("_", 1)[1]; return dict(theme=theme, kind="treat" if s.startswith("ikram_") else "gift", variant=v, key=v if v in GIFTS else None)
    return dict(theme=theme, kind="other", variant=s, key=None)


def read_glb(path):
    data = open(path, "rb").read()
    magic, version, length = struct.unpack_from("<4sII", data, 0)
    if magic != b"glTF":
        raise ValueError(f"{path}: not a GLB (magic {magic!r}); a Git LFS pointer?")
    off, gltf, binary = 12, None, b""
    while off < length:
        clen, ctype = struct.unpack_from("<II", data, off)
        chunk = data[off + 8: off + 8 + clen]
        if ctype == 0x4E4F534A:
            gltf = json.loads(chunk.decode("utf-8"))
        elif ctype == 0x004E4942:
            binary = chunk
        off += 8 + clen
    return gltf, binary, data, version


def buffer_bytes(gltf, binary, index):
    buf = gltf["buffers"][index]
    uri = buf.get("uri")
    if uri is None:
        return binary
    if uri.startswith("data:"):
        return base64.b64decode(uri.split(",", 1)[1])
    raise ValueError("external buffer " + uri)


def view_bytes(gltf, binary, view_index):
    bv = gltf["bufferViews"][view_index]
    b = buffer_bytes(gltf, binary, bv["buffer"])
    start = bv.get("byteOffset", 0)
    return b[start: start + bv["byteLength"]], bv.get("byteStride")


def accessor(gltf, binary, index):
    acc = gltf["accessors"][index]
    dtype, width, count = np.dtype(COMPONENT[acc["componentType"]]).newbyteorder("<"), WIDTH[acc["type"]], acc["count"]
    if "bufferView" in acc:
        raw, stride = view_bytes(gltf, binary, acc["bufferView"])
        item = dtype.itemsize * width
        stride = stride or item
        arr = np.ndarray((count, width), dtype=dtype, buffer=raw, offset=acc.get("byteOffset", 0), strides=(stride, dtype.itemsize)).copy()
    else:
        arr = np.zeros((count, width), dtype=dtype)
    sparse = acc.get("sparse")
    if sparse:
        ii = sparse["indices"]; vv = sparse["values"]
        idx_raw, _ = view_bytes(gltf, binary, ii["bufferView"])
        idx = np.frombuffer(idx_raw, dtype=np.dtype(COMPONENT[ii["componentType"]]).newbyteorder("<"), count=sparse["count"], offset=ii.get("byteOffset", 0))
        val_raw, _ = view_bytes(gltf, binary, vv["bufferView"])
        vals = np.frombuffer(val_raw, dtype=dtype, count=sparse["count"] * width, offset=vv.get("byteOffset", 0)).reshape(-1, width)
        arr[idx] = vals
    if acc.get("normalized") and dtype.kind in "iu":
        info = np.iinfo(dtype); arr = np.maximum(arr.astype(np.float64) / info.max, -1.0)
    return arr.astype(np.float64) if dtype.kind == "f" or acc.get("normalized") else arr


def quat_matrix(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def local_matrix(node):
    if "matrix" in node:
        return np.array(node["matrix"], dtype=np.float64).reshape(4, 4).T  # column-major
    m = np.eye(4)
    m[:3, :3] = quat_matrix(node.get("rotation", [0, 0, 0, 1])) * np.array(node.get("scale", [1, 1, 1]))
    m[:3, 3] = node.get("translation", [0, 0, 0])
    return m


def euler_xyz(m3):
    """three.js Euler order XYZ from a rotation matrix (radians), for readable root rotations."""
    m13 = max(-1.0, min(1.0, m3[0, 2]))
    y = math.asin(m13)
    if abs(m13) < 0.9999999:
        x = math.atan2(-m3[1, 2], m3[2, 2]); z = math.atan2(-m3[0, 1], m3[0, 0])
    else:
        x = math.atan2(m3[2, 1], m3[1, 1]); z = 0.0
    return [x, y, z]


def image_size(raw, mime):
    if raw[:8] == b"\x89PNG\r\n\x1a\n":
        return list(struct.unpack(">II", raw[16:24]))
    if raw[:2] == b"\xff\xd8":
        i = 2
        while i < len(raw):
            if raw[i] != 0xFF:
                i += 1; continue
            marker = raw[i + 1]
            if marker in (0xC0, 0xC1, 0xC2):
                h, w = struct.unpack(">HH", raw[i + 5: i + 9]); return [w, h]
            i += 2 + struct.unpack(">H", raw[i + 2: i + 4])[0]
    if raw[:4] == b"RIFF" and raw[8:12] == b"WEBP":
        return None
    return None


def inventory_file(path, root):
    gltf, binary, data, version = read_glb(path)
    stem = os.path.splitext(os.path.basename(path))[0]
    rel = os.path.relpath(path, root).replace("\\", "/")
    nodes = gltf.get("nodes", [])
    parent = {}
    for i, n in enumerate(nodes):
        for c in n.get("children", []):
            parent[c] = i
    scene_index = gltf.get("scene", 0)
    roots = gltf.get("scenes", [{}])[scene_index].get("nodes", []) if gltf.get("scenes") else [i for i in range(len(nodes)) if i not in parent]
    world, top = {}, {}

    def walk(i, pm, top_index):
        world[i] = pm @ local_matrix(nodes[i]); top[i] = top_index
        for c in nodes[i].get("children", []):
            walk(c, world[i], top_index)
    for ri in roots:
        walk(ri, np.eye(4), ri)

    def path_of(i):
        names = []
        while i is not None:
            names.append(nodes[i].get("name", f"#{i}")); i = parent.get(i)
        return "/".join(reversed(names))

    images = []
    for im in gltf.get("images", []):
        entry = {"name": im.get("name"), "mime": im.get("mimeType")}
        if "bufferView" in im:
            raw, _ = view_bytes(gltf, binary, im["bufferView"])
            entry.update(bytes=len(raw), size=image_size(raw, im.get("mimeType")), sha1=hashlib.sha1(raw).hexdigest()[:12])
        elif "uri" in im:
            entry["uri"] = im["uri"][:40]
        images.append(entry)
    textures = gltf.get("textures", [])
    samplers = gltf.get("samplers", [])

    def tex_ref(t):
        if not isinstance(t, dict) or "index" not in t:
            return t
        tex = textures[t["index"]]
        out = {k: v for k, v in t.items() if k != "index"}
        src = tex.get("source")
        if src is not None and src < len(images):
            out["image"] = {k: images[src].get(k) for k in ("name", "mime", "size", "sha1")}
        if "sampler" in tex:
            out["sampler"] = samplers[tex["sampler"]]
        return out

    def resolve(obj):
        if isinstance(obj, dict):
            return {k: (tex_ref(v) if k.endswith("Texture") else resolve(v)) for k, v in obj.items()}
        if isinstance(obj, list):
            return [resolve(x) for x in obj]
        return obj

    materials = []
    for mi, m in enumerate(gltf.get("materials", [])):
        mm = resolve(m); mm["index"] = mi
        materials.append(r(mm))

    meshes = []
    mesh_points = {}
    for mi, mesh in enumerate(gltf.get("meshes", [])):
        verts = tris = lines = points = 0
        prims, pts = [], []
        for p in mesh.get("primitives", []):
            mode = p.get("mode", 4)
            pos = accessor(gltf, binary, p["attributes"]["POSITION"])[:, :3] if "POSITION" in p["attributes"] else np.zeros((0, 3))
            n = gltf["accessors"][p["indices"]]["count"] if "indices" in p else len(pos)
            verts += len(pos); pts.append(pos)
            if mode == 4: tris += n // 3
            elif mode in (5, 6): tris += max(0, n - 2)
            elif mode == 1: lines += n // 2
            elif mode == 3: lines += max(0, n - 1)
            elif mode == 2: lines += n
            else: points += n
            mat = p.get("material")
            prims.append({"mode": MODES.get(mode, mode), "material": gltf["materials"][mat].get("name") if mat is not None else None,
                          "attributes": sorted(p["attributes"].keys()), "targets": len(p.get("targets", []))})
        mesh_points[mi] = np.concatenate(pts) if pts else np.zeros((0, 3))
        meshes.append({"name": mesh.get("name"), "vertices": verts, "triangles": tris, "lines": lines, "points": points, "primitives": prims})

    # EXT_mesh_gpu_instancing: a node draws its mesh once per instance (TRS attributes), e.g. the seeds' unit spheres.
    instances = {}
    for i, n in enumerate(nodes):
        inst = n.get("extensions", {}).get("EXT_mesh_gpu_instancing")
        if inst:
            attrs = {k: accessor(gltf, binary, v) for k, v in inst.get("attributes", {}).items()}
            count = max((len(a) for a in attrs.values()), default=0)
            mats = []
            for j in range(count):
                m = np.eye(4)
                q = attrs["ROTATION"][j] if "ROTATION" in attrs else [0, 0, 0, 1]
                sc = attrs["SCALE"][j] if "SCALE" in attrs else [1, 1, 1]
                m[:3, :3] = quat_matrix(q) * np.array(sc)
                m[:3, 3] = attrs["TRANSLATION"][j] if "TRANSLATION" in attrs else [0, 0, 0]
                mats.append(m)
            instances[i] = mats

    def world_points(k):
        p = mesh_points[nodes[k]["mesh"]]
        if not len(p):
            return p
        mats = [world[k] @ m for m in instances[k]] if k in instances else [world[k]]
        return np.concatenate([p @ m[:3, :3].T + m[:3, 3] for m in mats])

    lights_def = gltf.get("extensions", {}).get("KHR_lights_punctual", {}).get("lights", [])
    node_out, lights, cameras = [], [], []
    lo_all, hi_all = np.full(3, np.inf), np.full(3, -np.inf)
    top_bounds = {}
    for i, n in enumerate(nodes):
        e = {"i": i, "name": n.get("name"), "parent": parent.get(i)}
        if "mesh" in n: e["mesh"] = n["mesh"]
        if i in instances: e["instances"] = len(instances[i])
        for k in ("translation", "rotation", "scale", "matrix"):
            if k in n: e[k] = r(n[k], 6)
        if i in world:
            e["world_pos"] = r(world[i][:3, 3].tolist(), 5)
        ext = n.get("extensions", {})
        if "KHR_lights_punctual" in ext:
            li = ext["KHR_lights_punctual"]["light"]; e["light"] = li
            if i in world:
                w = world[i]; fwd = w[:3, :3] @ np.array([0, 0, -1.0]); fwd = fwd / (np.linalg.norm(fwd) or 1)
                lights.append(r({"node": n.get("name"), "path": path_of(i), **lights_def[li], "world_pos": w[:3, 3].tolist(), "forward": fwd.tolist()}, 5))
        if "camera" in n:
            e["camera"] = n["camera"]
            if i in world:
                w = world[i]; fwd = w[:3, :3] @ np.array([0, 0, -1.0])
                cameras.append(r({"node": n.get("name"), **gltf["cameras"][n["camera"]], "world_pos": w[:3, 3].tolist(), "forward": fwd.tolist()}, 5))
        if n.get("extras"): e["extras"] = n["extras"]
        node_out.append(e)
        if "mesh" in n and i in world:
            wp = world_points(i)
            if len(wp):
                lo, hi = wp.min(axis=0), wp.max(axis=0)
                lo_all, hi_all = np.minimum(lo_all, lo), np.maximum(hi_all, hi)
                t = top[i]; b = top_bounds.setdefault(t, [np.full(3, np.inf), np.full(3, -np.inf)])
                b[0] = np.minimum(b[0], lo); b[1] = np.maximum(b[1], hi)

    def bbox(lo, hi):
        if not np.isfinite(lo).all():
            return None
        return r({"min": lo.tolist(), "max": hi.tolist(), "size": (hi - lo).tolist(), "center": ((lo + hi) / 2).tolist()}, 5)

    root_info = []
    for ri in roots:
        n = nodes[ri]; w = world[ri]
        sc = np.linalg.norm(w[:3, :3], axis=0)
        root_info.append(r({"name": n.get("name"), "children": len(n.get("children", [])),
                            "translation": w[:3, 3].tolist(), "rotation_euler_xyz": euler_xyz(w[:3, :3] / sc), "scale": sc.tolist(),
                            "bounds": bbox(*top_bounds[ri]) if ri in top_bounds else None}, 5))
    # Children of the single root (e.g. sahne_tam/<part>) are the layout pieces worth comparing.
    layout = []
    # Tile sets: 53 tiles in a grid, their positions are in "nodes"; the layout matters for scenes, tables, racks...
    if len(roots) == 1 and classify(stem)["kind"] != "tile_set":
        for c in nodes[roots[0]].get("children", []):
            lo, hi = np.full(3, np.inf), np.full(3, -np.inf)
            stack = [c]
            while stack:
                k = stack.pop(); stack.extend(nodes[k].get("children", []))
                if "mesh" in nodes[k] and k in world and len(mesh_points[nodes[k]["mesh"]]):
                    wp = world_points(k)
                    lo, hi = np.minimum(lo, wp.min(axis=0)), np.maximum(hi, wp.max(axis=0))
            w = world[c]; sc = np.linalg.norm(w[:3, :3], axis=0)
            entry = {"i": c, "name": nodes[c].get("name"), "translation": w[:3, 3].tolist(), "rotation_euler_xyz": euler_xyz(w[:3, :3] / np.where(sc == 0, 1, sc)),
                     "scale": sc.tolist(), "bounds": bbox(lo, hi)}
            if "extensions" in nodes[c] and "KHR_lights_punctual" in nodes[c]["extensions"]:
                entry["light"] = nodes[c]["extensions"]["KHR_lights_punctual"]["light"]
            layout.append(r(entry, 5))

    mat_names = [m.get("name") for m in gltf.get("materials", [])]
    node_names = [n.get("name") for n in nodes]
    return {
        "file": rel, "stem": stem, **classify(stem),
        "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(), "gltf_version": version,
        "generator": gltf.get("asset", {}).get("generator"),
        "extensionsUsed": sorted(gltf.get("extensionsUsed", [])), "extensionsRequired": sorted(gltf.get("extensionsRequired", [])),
        "counts": {"nodes": len(nodes), "meshes": len(meshes), "mesh_nodes": sum(1 for n in nodes if "mesh" in n),
                   "materials": len(mat_names), "textures": len(textures), "images": len(images),
                   "vertices": sum(m["vertices"] for m in meshes), "triangles": sum(m["triangles"] for m in meshes),
                   "triangles_drawn": sum(meshes[n["mesh"]]["triangles"] * len(instances.get(i, [None])) for i, n in enumerate(nodes) if "mesh" in n),
                   "instanced_nodes": len(instances), "instances": sum(len(v) for v in instances.values()),
                   "line_segments": sum(m["lines"] for m in meshes), "points": sum(m["points"] for m in meshes),
                   "lights": len(lights), "cameras": len(cameras), "animations": len(gltf.get("animations", [])), "skins": len(gltf.get("skins", []))},
        "bounds": bbox(lo_all, hi_all),
        "roots": root_info,
        "layout": layout,
        "node_names": node_names,
        "unique_node_names": sorted(set(x for x in node_names if x)),
        "material_names": mat_names,
        "materials": materials,
        "meshes": meshes,
        "images": images,
        "lights": lights,
        "cameras": cameras,
        "nodes": node_out,
    }


def dump(doc, fh):
    """JSON with one line per list item (node, material, mesh...): readable diffs without a 6 MB indented file."""
    one = lambda v: json.dumps(v, ensure_ascii=False, separators=(", ", ": "))
    nl = "\n"
    fh.write("{" + nl)
    for k in (k for k in doc if k != "files"):
        fh.write(" " + one(k) + ": " + one(doc[k]) + "," + nl)
    fh.write(' "files": [' + nl)
    files = doc.get("files", [])
    for fi, f in enumerate(files):
        fh.write("  {" + nl)
        items = list(f.items())
        for ki, (k, v) in enumerate(items):
            end = "," if ki < len(items) - 1 else ""
            if isinstance(v, list) and v and isinstance(v[0], dict):
                fh.write("   " + one(k) + ": [" + nl + ("," + nl).join("    " + one(x) for x in v) + nl + "   ]" + end + nl)
            else:
                fh.write("   " + one(k) + ": " + one(v) + end + nl)
        fh.write("  }" + ("," if fi < len(files) - 1 else "") + nl)
    fh.write(" ]" + nl + "}" + nl)


def scan(root, flat=False):
    out = []
    for dirpath, dirs, files in os.walk(root):
        if flat:
            dirs[:] = []  # only <root>/*.glb (Models/ without its v4/ copies)
        for f in sorted(files):
            if f.lower().endswith(".glb"):
                out.append(os.path.join(dirpath, f))
    return sorted(out, key=lambda p: os.path.relpath(p, root).replace("\\", "/"))


def main():
    args = sys.argv[1:]
    label = None
    if "--label" in args:
        k = args.index("--label"); label = args[k + 1]; del args[k: k + 2]
    flat = "--flat" in args
    args = [a for a in args if a != "--flat"]
    root, out = args[0], args[1]
    files = [inventory_file(p, root) for p in scan(root, flat)]
    by_kind, by_theme = {}, {}
    for f in files:
        by_kind[f["kind"]] = by_kind.get(f["kind"], 0) + 1
        by_theme[f["theme"] or "shared"] = by_theme.get(f["theme"] or "shared", 0) + 1
    doc = {
        "label": label or os.path.basename(os.path.normpath(root)),
        "root": root.replace("\\", "/"),
        "tool": "tools/blender/gltf_inventory.py",
        "space": "glTF (three.js): right-handed, Y up, metres. Unity (glTFast) position = (-x, y, z), rotation = (x, -y, -z, w).",
        "summary": {"files": len(files), "bytes": sum(f["bytes"] for f in files), "by_kind": by_kind, "by_theme": by_theme,
                    "triangles": sum(f["counts"]["triangles"] for f in files)},
        "files": files,
    }
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as fh:
        dump(doc, fh)
    print("INVENTORY", doc["label"], "files", len(files), "kinds", json.dumps(by_kind, ensure_ascii=False), "->", out)


if __name__ == "__main__":
    main()
