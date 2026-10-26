"""Galaxy 101 · v4 Hat C: take the table felt textures straight out of the v4 table GLBs.

The project draws the felt per table skin and planet into Assets/Galaxy101/Resources/Galaxy101/Table/Felt/
<skin>_<planet>.png (tools/felt_textures.py, which still reads the deleted Design/galaxy101_unity_handoff path).
The v4 GLBs already embed the design's felt (material "cuha", baseColorTexture, 1024×1024, rows as the glTF exporter
wrote them, the orientation the project's PNGs use), so the Unity phase can copy these instead of re-rendering.
Names follow TableScene.cs ApplyFelt: andromeda.png (one felt for every planet), else <skin>_<planet>.png with planet
ids saturn / mars / neptune / jupiter / venus.

Usage:
  python tools/blender/extract_felt_from_glb.py <v4 GLB root> <out dir> [<compare with dir>]
"""
import hashlib
import json
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gltf_inventory as gi  # noqa: E402

SKINS = {"istasyon": "station", "ceviz": "wood", "hologram": "holo", "andromeda": "andromeda"}
PLANETS = {"saturn": "saturn", "mars": "mars", "neptun": "neptune", "jupiter": "jupiter", "venus": "venus"}


def felt_png(path):
    gltf, binary, _, _ = gi.read_glb(path)
    mat = next(m for m in gltf["materials"] if m.get("name") == "cuha")
    tex = gltf["textures"][mat["pbrMetallicRoughness"]["baseColorTexture"]["index"]]
    raw, _ = gi.view_bytes(gltf, binary, gltf["images"][tex["source"]]["bufferView"])
    return raw


def png_size(raw):
    return list(struct.unpack(">II", raw[16:24]))


def main():
    root, out = sys.argv[1], sys.argv[2]
    cmp_dir = sys.argv[3] if len(sys.argv) > 3 else None
    os.makedirs(out, exist_ok=True)
    report = []
    for theme in sorted(PLANETS):
        for variant, skin in SKINS.items():
            src = os.path.join(root, theme, f"galaxy101_masa_{variant}_{theme}.glb")
            raw = felt_png(src)
            name = "andromeda.png" if skin == "andromeda" else f"{skin}_{PLANETS[theme]}.png"
            dst = os.path.join(out, name)
            if skin == "andromeda" and os.path.exists(dst) and open(dst, "rb").read() != raw:
                report.append({"file": name, "warning": f"andromeda felt differs in {theme}"})
                continue
            open(dst, "wb").write(raw)
            entry = {"file": name, "from": os.path.relpath(src, root).replace("\\", "/"), "size": png_size(raw), "sha1": hashlib.sha1(raw).hexdigest()[:12]}
            if cmp_dir:
                old = os.path.join(cmp_dir, name)
                if os.path.exists(old):
                    o = open(old, "rb").read()
                    entry["project"] = {"size": png_size(o), "same_bytes": o == raw}
                else:
                    entry["project"] = None
            report.append(entry)
    # keep one entry per output file
    seen, uniq = set(), []
    for e in report:
        if e.get("file") in seen and "warning" not in e:
            continue
        seen.add(e.get("file")); uniq.append(e)
    print(json.dumps(uniq, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
