"""Save every embedded texture of each GLB as PNG, named <glb>__<material>__<slot>.png.

Usage: blender --background --factory-startup --python extract_textures.py -- <glb_dir> <out_dir> [skip_substring]
"""
import bpy, sys, os

args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]
skip = args[2] if len(args) > 2 else None
os.makedirs(out, exist_ok=True)
count = 0
for name in sorted(os.listdir(src)):
    if not name.endswith(".glb") or (skip and skip in name):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(src, name))
    for m in bpy.data.materials:
        if not m.use_nodes:
            continue
        for n in m.node_tree.nodes:
            if n.type != "TEX_IMAGE" or not n.image:
                continue
            slot = "+".join(sorted({l.to_socket.name for o in n.outputs for l in o.links}))
            img = n.image
            path = os.path.join(out, f"{name[:-4]}__{m.name}__{slot}.png".replace(" ", ""))
            img.filepath_raw = path
            img.file_format = "PNG"
            img.save()
            count += 1
print("EXTRACTED", count)
