"""Galaxy 101 die (spec/21, spec/24): 30 mm cube, 5 mm rounded corners, real dimpled pips.

Ivory body (clearcoat look), dark navy pips, the single pip red and larger. Opposite faces add up to 7; the faces are
placed so that after the glTF Y-up export and glTFast's X mirror the Unity faces are +y 1, −y 6, +x 2, −x 5, +z 3,
−z 4 (TableCeremony.FaceNormal). Blender: +Z 1, −Z 6, −X 2, +X 5, −Y 3, +Y 4.

Usage: blender --background --factory-startup --python make_die.py -- <out.glb>
"""
import bpy, bmesh, sys
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1]
SIZE, RADIUS = 0.030, 0.005
bpy.ops.wm.read_factory_settings(use_empty=True)

def material(name, rgb, rough, coat=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*rgb, 1); p.inputs["Roughness"].default_value = rough
    if coat: p.inputs["Coat Weight"].default_value = coat
    return m

ivory = material("zar_fildisi", (0.93, 0.89, 0.80), 0.32, 0.7)
navy = material("zar_nokta", (0.012, 0.018, 0.05), 0.85)
red = material("zar_bir", (0.70, 0.03, 0.05), 0.8)

bpy.ops.mesh.primitive_cube_add(size=SIZE)
die = bpy.context.object; die.name = "zar"
die.data.materials.append(ivory); die.data.materials.append(navy); die.data.materials.append(red)
bevel = die.modifiers.new("kose", "BEVEL"); bevel.width = RADIUS; bevel.segments = 5; bevel.limit_method = "NONE"
bpy.ops.object.modifier_apply(modifier="kose")

# Pip centres in each face's own (u, v) plane, 7.8 mm apart.
LAYOUT = {1: [(0, 0)], 2: [(-1, -1), (1, 1)], 3: [(-1, -1), (0, 0), (1, 1)], 4: [(-1, -1), (1, 1), (-1, 1), (1, -1)],
          5: [(-1, -1), (1, 1), (-1, 1), (1, -1), (0, 0)], 6: [(-1, -1), (1, 1), (-1, 1), (1, -1), (-1, 0), (1, 0)]}
FACES = {1: (Vector((0, 0, 1)), Vector((1, 0, 0)), Vector((0, 1, 0))), 6: (Vector((0, 0, -1)), Vector((1, 0, 0)), Vector((0, 1, 0))),
         2: (Vector((-1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))), 5: (Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))),
         3: (Vector((0, -1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1))), 4: (Vector((0, 1, 0)), Vector((1, 0, 0)), Vector((0, 0, 1)))}
STEP = 0.0078
cutters = []
for value, (normal, u, v) in FACES.items():
    r = 0.0040 if value == 1 else 0.0027
    for (a, b) in LAYOUT[value]:
        centre = normal * (SIZE / 2 + r * 0.45) + u * (a * STEP) + v * (b * STEP)
        bpy.ops.mesh.primitive_uv_sphere_add(radius=r, location=centre, segments=20, ring_count=10)
        s = bpy.context.object; s.data.materials.append(red if value == 1 else navy); cutters.append(s)

# One boolean against all cutters; the dimple walls keep the cutters' materials.
bpy.ops.object.select_all(action="DESELECT")
for c in cutters: c.select_set(True)
bpy.context.view_layer.objects.active = cutters[0]
bpy.ops.object.join(); pips = bpy.context.object; pips.name = "zar_kesici"
boolean = die.modifiers.new("cukurlar", "BOOLEAN"); boolean.operation = "DIFFERENCE"; boolean.object = pips; boolean.solver = "EXACT"; boolean.material_mode = "TRANSFER"
bpy.context.view_layer.objects.active = die; bpy.ops.object.modifier_apply(modifier="cukurlar")
bpy.data.objects.remove(pips, do_unlink=True)
bpy.ops.object.shade_smooth_by_angle(angle=0.6)

tris = sum(len(p.vertices) - 2 for p in die.data.polygons)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=False, export_apply=True)
print("DIE_READY", OUT, "triangles", tris, "bounds", [round(x, 4) for x in die.dimensions])
