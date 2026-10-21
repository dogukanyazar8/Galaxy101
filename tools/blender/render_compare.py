"""Render the same GLB from raw and polished folders with identical camera/lights (EEVEE).

Usage: blender --background --factory-startup --python render_compare.py -- <raw dir> <polished dir> <out dir> <file filter> [closeup]
Writes <out>/<name>__raw.png and <out>/<name>__polished.png. LOD1 objects are hidden.
"""
import bpy, sys, os, math
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
RAW, POL, OUT, FILTER = args[:4]
CLOSE = len(args) > 4 and args[4] == "closeup"
os.makedirs(OUT, exist_ok=True)


def setup(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    for o in bpy.data.objects:
        if o.name.endswith("_LOD1"):
            o.hide_render = True
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = 1200, 760
    sc.view_settings.view_transform = "AgX"
    world = bpy.data.worlds.new("w"); sc.world = world; world.use_nodes = True
    bg = world.node_tree.nodes["Background"]; bg.inputs["Color"].default_value = (0.035, 0.04, 0.08, 1); bg.inputs["Strength"].default_value = 1.0
    pts = [o.matrix_world @ Vector(c) for o in bpy.data.objects if o.type == "MESH" and not o.hide_render for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    center, size = (lo + hi) / 2, (hi - lo).length
    if CLOSE:  # corner of the object nearest the camera, tight framing
        center = Vector((lo.x + (hi.x - lo.x) * 0.1, (lo.y + hi.y) / 2, (lo.z + hi.z) / 2)); size *= 0.28
    cam_data = bpy.data.cameras.new("c"); cam_data.lens = 60; cam = bpy.data.objects.new("c", cam_data); sc.collection.objects.link(cam)
    direction = Vector((-0.55, -1.0, 0.62)).normalized(); cam.location = center + direction * size * 1.55
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler(); sc.camera = cam
    for i, (d, e, s) in enumerate([((-1, -1.2, 1.5), 4.0, 1.0), ((1.4, -0.4, 0.8), 1.6, 0.6), ((0.2, 1.4, 1.2), 2.5, 0.8)]):
        ld = bpy.data.lights.new(f"l{i}", "AREA"); ld.energy = e * 18 * size * size; ld.size = size * s
        lo_ = bpy.data.objects.new(f"l{i}", ld); sc.collection.objects.link(lo_)
        lo_.location = center + Vector(d).normalized() * size * 1.4
        lo_.rotation_euler = (center - lo_.location).to_track_quat("-Z", "Y").to_euler()


for name in sorted(os.listdir(POL)):
    if not name.endswith(".glb") or FILTER not in name:
        continue
    for tag, folder in (("raw", RAW), ("polished", POL)):
        setup(os.path.join(folder, name))
        bpy.context.scene.render.filepath = os.path.join(OUT, f"{name[:-4]}{'_close' if CLOSE else ''}__{tag}.png")
        bpy.ops.render.render(write_still=True)
    print("RENDERED", name)
