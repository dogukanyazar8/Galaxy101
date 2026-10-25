"""Galaxy 101 · v4 Hat C: check the v4 GLBs against what the project's Unity table code looks up.

The table code (read-only references) finds models, nodes and materials by name. This script checks every v4 GLB
against those lookups, so the Unity phase knows what keeps working when it switches to Models/v4/...:

  TableAssetBuilder.cs:22-29   Models/<galaxy101_..._saturn>.glb file names, prefab per skin / gift / chair / room / die
  TableScene.cs:268, TileFactory.cs:33-34,67   tile set child "tas_R1" with "tas_govde" and "tas_yuz"
  TableScene.cs:305            material "cuha" on the table (felt texture from Resources)
  TableScene.cs:317            rack GLB root carries TILT (rotation X = -0.36 rad)
  TableFx.cs:76, 84            renderer "holo_halka*"; LED materials ("led*" / "*_led*")
  TableGifts.cs:77-81          gift nodes "demet", "kupa_govde", "alev*", "kabarcik*"
  SkinService.cs:57-79         material names recoloured per theme
  TableCeremony.cs:225         die faces in Unity space: +y 1, +x 2, +z 3, -z 4, -x 5, -y 6
  glTFast 6.20 Extensions      supported extensions (Library/PackageCache/com.unity.cloud.gltfast/Runtime/Scripts/GltfImport.cs)

Usage:
  uv run --with numpy python tools/blender/unity_contract_check.py <inventory.json> <glb root> <out.json>
"""
import json
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gltf_inventory as gi  # noqa: E402

TILT = -0.36
SKIN_SERVICE = ["led", "istaka_led", "istaka_karbon_dikis", "istaka_neon_tup1", "masa_holo_led", "istaka_neon_tup2", "duvar",
                "masa_cam_kenar", "led_cizgi", "masa_holo_cizgi", "holo", "masa_kenar", "deri", "istaka_eloksal", "tas_classic", "cuha"]
GIFT_NODES = {"flower": ["demet"], "trophy": ["kupa_govde"], "rocket": ["alev"], "cake": ["alev"], "cola": ["kabarcik"]}
# glTFast 6.20 GltfImport.cs supported list (Draco/KTX/meshopt only with their packages).
GLTFAST_SUPPORTED = {"KHR_materials_pbrSpecularGlossiness", "KHR_materials_unlit", "KHR_materials_variants", "KHR_texture_transform",
                     "KHR_mesh_quantization", "KHR_materials_transmission", "EXT_mesh_gpu_instancing", "KHR_lights_punctual",
                     "KHR_materials_clearcoat"}
# The project's converter reads this one from the GLB JSON itself (TableMaterialConverter.cs EmissiveStrength).
HANDLED_BY_PROJECT = {"KHR_materials_emissive_strength"}
# TableCeremony.cs:225 FaceNormal: index = pip value.
FACE_UNITY = {1: (0, 1, 0), 2: (1, 0, 0), 3: (0, 0, 1), 4: (0, 0, -1), 5: (-1, 0, 0), 6: (0, -1, 0)}


def children(f, i):
    return [n for n in f["nodes"] if n.get("parent") == i]


def die_faces(path):
    """Pip value -> dominant normal axis in Unity space (glTFast mirrors X)."""
    gltf, binary, _, _ = gi.read_glb(path)
    out = {}
    for mesh in gltf["meshes"]:
        for p in mesh["primitives"]:
            name = gltf["materials"][p["material"]]["name"]
            if not name.startswith("zar_"):
                continue
            nor = gi.accessor(gltf, binary, p["attributes"]["NORMAL"])
            idx = np.unique(gi.accessor(gltf, binary, p["indices"]).ravel()) if "indices" in p else np.arange(len(nor))
            m = nor[idx].mean(axis=0)
            axis = int(np.argmax(np.abs(m))); v = [0, 0, 0]; v[axis] = int(np.sign(m[axis]))
            out[int(name.split("_")[1])] = {"gltf": tuple(v), "unity": (-v[0], v[1], v[2])}
    return out


def main():
    inv_path, root, out_path = sys.argv[1:4]
    files = json.load(open(inv_path, encoding="utf-8"))["files"]
    results = []

    def add(file, check, ok, detail, source):
        results.append({"file": file, "check": check, "status": "uyar" if ok else "uymaz", "detail": detail, "source": source})

    for f in files:
        names = set(f["unique_node_names"]); mats = set(n for n in f["material_names"] if n)
        if f["kind"] in ("tile_set", "tile"):
            root_i = next(n["i"] for n in f["nodes"] if n.get("parent") is None)
            r1 = next((n for n in children(f, root_i) if n.get("name") == "tas_R1"), None)
            if f["kind"] == "tile":
                r1 = next((n for n in f["nodes"] if n.get("parent") is None), None)
                add(f["file"], "tek taş kökü", True, f"kök {r1.get('name')} (TableAssetBuilder tas_tek kullanmaz)", "TableAssetBuilder.cs:27-29")
                continue
            kids = {n.get("name") for n in children(f, r1["i"])} if r1 else set()
            add(f["file"], "tas_R1/tas_govde + tas_yuz", bool(r1) and {"tas_govde", "tas_yuz"} <= kids,
                f"tas_R1 {'var' if r1 else 'yok'}, çocuklar {sorted(k for k in kids if k)}", "TableScene.cs:268, TileFactory.cs:33-34,67")
            tiles = [n for n in children(f, root_i) if (n.get("name") or "").startswith("tas_")]
            add(f["file"], "53 taş (52 + sahte okey) + arka", len(tiles) == 54, f"{len(tiles)} tas_* grubu", "table3d.js:1039-1043 exportAsset('tiles')")
        if f["kind"] in ("table", "scene"):
            add(f["file"], "holo_halka düğümü", any(n.startswith("holo_halka") for n in names), "", "TableFx.cs:76")
            add(f["file"], "cuha malzemesi", "cuha" in mats, "", "TableScene.cs:305")
            leds = sorted(m for m in mats if m.startswith("led") or "_led" in m)
            add(f["file"], "LED malzemeleri", bool(leds), ", ".join(leds), "TableFx.cs:84")
        if f["kind"] == "rack":
            r = f["roots"][0]
            ok = abs(r["rotation_euler_xyz"][0] - TILT) < 1e-3 and all(abs(x) < 1e-6 for x in r["translation"])
            add(f["file"], "kök dönüşü TILT", ok, f"kök {r['name']} rot {r['rotation_euler_xyz']} konum {r['translation']}", "TableScene.cs:317")
        if f["kind"] in ("gift", "treat") and f["variant"] in GIFT_NODES:
            for want in GIFT_NODES[f["variant"]]:
                hit = sorted(n for n in names if n == want or (want in ("alev", "kabarcik") and n.startswith(want)))
                add(f["file"], f"hediye düğümü {want}", bool(hit), ", ".join(hit[:6]) + (" …" if len(hit) > 6 else ""), "TableGifts.cs:77-81")
        if f["kind"] == "die":
            faces = die_faces(os.path.join(root, f["file"]))
            wrong = {v: {"unity": list(fc["unity"]), "beklenen": list(FACE_UNITY[v])} for v, fc in faces.items() if tuple(fc["unity"]) != FACE_UNITY[v]}
            add(f["file"], "zar yüzleri (Unity uzayı)", not wrong, {"yanlış": wrong, "gltf": {v: list(fc["gltf"]) for v, fc in faces.items()}}, "TableCeremony.cs:225")
            b = f["bounds"]
            add(f["file"], "zar pivotu merkezde", all(abs(c) < 1e-4 for c in b["center"]), f"sınır merkezi {b['center']} (proje zarı merkezde)", "TableCeremony.cs:229-231, tools/blender/make_die.py")
        unsupported = sorted(set(f["extensionsUsed"]) - GLTFAST_SUPPORTED - HANDLED_BY_PROJECT)
        if unsupported:
            add(f["file"], "glTFast 6.20 desteklemediği uzantılar", False, ", ".join(unsupported), "glTFast GltfImport.cs; mevcut .meta reportItems code 20")
    # SkinService names across the set.
    allmats = {}
    for f in files:
        for n in f["material_names"]:
            if n:
                allmats.setdefault(n, set()).add(f["kind"])
    skin = [{"material": m, "found_in": sorted(allmats.get(m, [])), "status": "uyar" if m in allmats else "uymaz"} for m in SKIN_SERVICE]
    summary = {"checks": len(results), "uyar": sum(1 for r in results if r["status"] == "uyar"), "uymaz": sum(1 for r in results if r["status"] == "uymaz"),
               "skin_service_missing": [s["material"] for s in skin if s["status"] == "uymaz"]}
    by_check = {}
    for r in results:
        k = by_check.setdefault(r["check"], {"uyar": 0, "uymaz": 0}); k[r["status"]] += 1
    summary["by_check"] = by_check
    json.dump({"tool": "tools/blender/unity_contract_check.py", "summary": summary, "skin_service": skin, "results": results},
              open(out_path, "w", encoding="utf-8", newline="\n"), ensure_ascii=False, indent=1)
    print("CONTRACT", json.dumps(summary, ensure_ascii=False))


if __name__ == "__main__":
    main()
