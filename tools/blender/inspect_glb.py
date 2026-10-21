"""Inventory every GLB in a folder: objects, mesh stats, dimensions, materials and textures.

Usage: blender --background --factory-startup --python inspect_glb.py -- <glb_dir> <out_json>
"""
import bpy, sys, json, os
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]
report = {}
for name in sorted(os.listdir(src)):
    if not name.lower().endswith(".glb"):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(src, name))
    objects = []
    for ob in bpy.data.objects:
        entry = {"name": ob.name, "type": ob.type, "parent": ob.parent.name if ob.parent else None,
                 "loc": [round(v, 4) for v in ob.location], "rot": [round(v, 4) for v in ob.rotation_euler],
                 "scale": [round(v, 4) for v in ob.scale]}
        if ob.type == "MESH":
            me = ob.data
            corners = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
            entry.update({"verts": len(me.vertices), "faces": len(me.polygons), "uv": [u.name for u in me.uv_layers],
                          "dims_mm": [round((max(c[i] for c in corners) - min(c[i] for c in corners)) * 1000, 2) for i in range(3)],
                          "materials": [m.name if m else None for m in me.materials],
                          "smooth": sum(p.use_smooth for p in me.polygons)})
        objects.append(entry)
    mats = {}
    for m in bpy.data.materials:
        info = {}
        if m.use_nodes:
            bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
            if bsdf:
                for key in ("Base Color", "Metallic", "Roughness", "Emission Color", "Emission Strength", "Alpha", "Transmission Weight", "Coat Weight"):
                    if key in bsdf.inputs:
                        sock = bsdf.inputs[key]
                        info[key] = "linked" if sock.is_linked else (list(round(x, 3) for x in sock.default_value) if hasattr(sock.default_value, "__len__") else round(sock.default_value, 3))
            info["images"] = [n.image.name + f" {n.image.size[0]}x{n.image.size[1]}" for n in m.node_tree.nodes if n.type == "TEX_IMAGE" and n.image]
        info["blend"] = getattr(m, "surface_render_method", "")
        mats[m.name] = info
    report[name] = {"objects": objects, "materials": mats}
with open(out, "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=1)
print("INSPECTED", len(report))
