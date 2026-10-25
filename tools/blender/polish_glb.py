"""Galaxy 101 table assets: Blender polish pass (spec/16 §B).

For every GLB in <src>:
  * Tile sets / single tile: copied unchanged (spec: tile bodies are fixed, faces become a Unity atlas).
  * Names, hierarchy, origins and transforms are preserved (no Apply Transform).
  * Edges: vertices welded, custom normals cleared, shade-by-angle 30°, Bevel 0.5–0.7 mm × 2 segments
    (angle limited, clamp overlap, harden normals) + Weighted Normal. Flat cards, hair-thin parts and
    sub-3 mm details are left alone so profiles and tile seating surfaces do not move.
  * Textures: every canvas texture is replaced by its 2K re-bake (same generator, same UVs and
    KHR_texture_transform); patterned materials gain Normal + MetallicRoughness maps.
  * LOD1: racks, tables, chair and treats/gifts get "<name>_LOD1" siblings (50 % Decimate, no bevel).
  * Export: GLB, modifiers applied, materials + images embedded, tangents when normal maps exist.
  * Verification: exported file is re-imported and compared with the source (names, bounds,
    material factors, horizontal-surface heights of the rack profile).

Usage: blender --background --factory-startup --python polish_glb.py -- <src> <textures_2k> <out> <report.json> [file filter]
"""
import bpy, bmesh, sys, os, json, math, shutil
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
SRC, TEX, OUT, REPORT = args[:4]
ONLY = args[4] if len(args) > 4 else None
TEXTURES = json.load(open(os.path.join(TEX, "textures.json"), encoding="utf-8"))
ANGLE = math.radians(30)
LOD_PREFIXES = ("galaxy101_istaka_", "galaxy101_masa_", "galaxy101_koltuk", "galaxy101_hediye_", "galaxy101_ikram_")
# Surfaces tiles stand on: keep their corners crisp to stay inside the ±0.5 mm seating tolerance.
RACK_SEATING = {"istaka_govde"}
SEATING_TOLERANCE = 0.0005
# Rhombus baklava slices have ~40° tips; an angle-limited bevel overshoots them by ~0.08 mm, so they get normals only.
NO_BEVEL_PREFIXES = ("dilim_govde",)
os.makedirs(OUT, exist_ok=True)


def mesh_objects():
    return [o for o in bpy.data.objects if o.type == "MESH"]


def world_bounds(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    return [min(p[i] for p in pts) for i in range(3)], [max(p[i] for p in pts) for i in range(3)]


def snapshot():
    data = {"objects": {}, "materials": {}}
    dg = bpy.context.evaluated_depsgraph_get()
    for o in mesh_objects():
        lo, hi = world_bounds(o.evaluated_get(dg))
        data["objects"][o.name] = {"min": lo, "max": hi, "parent": o.parent.name if o.parent else None,
                                   "loc": list(o.location), "mats": [m.name if m else None for m in o.data.materials]}
    for m in bpy.data.materials:
        b = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if m.use_nodes else None
        if b:
            entry = {k: (None if b.inputs[k].is_linked else round(float(b.inputs[k].default_value), 4))
                     for k in ("Metallic", "Roughness", "Coat Weight", "Alpha")}
            # The exporter may fold strength into the colour (KHR_materials_emissive_strength): compare the product.
            if not b.inputs["Emission Color"].is_linked and not b.inputs["Emission Strength"].is_linked:
                col, st = b.inputs["Emission Color"].default_value, b.inputs["Emission Strength"].default_value
                for i, ch in enumerate("RGB"):
                    entry["Emission" + ch] = round(float(col[i] * st), 4)
            data["materials"][m.name] = entry
    return data


def profile_heights(objname):
    """Ray-cast straight down across the rack depth at three x stations; keep hits on horizontal faces."""
    o = bpy.data.objects.get(objname)
    if not o:
        return []
    dg = bpy.context.evaluated_depsgraph_get(); ev = o.evaluated_get(dg); inv = ev.matrix_world.inverted()
    lo, hi = world_bounds(ev); hits = []
    for x in (lo[0] + (hi[0] - lo[0]) * f for f in (0.25, 0.5, 0.75)):
        y = lo[1]
        while y <= hi[1]:
            origin = inv @ Vector((x, y, hi[2] + 0.05)); direction = (inv.to_3x3() @ Vector((0, 0, -1))).normalized()
            ok, loc, nrm, _ = ev.ray_cast(origin, direction)
            if ok:
                wn = (ev.matrix_world.to_3x3() @ nrm).normalized()
                if wn.z > 0.999:
                    hits.append((round(x, 4), round(y, 5), (ev.matrix_world @ loc).z))
            y += 0.0005
    return hits


def weld_and_shade(o):
    me = o.data
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    bm.to_mesh(me); bm.free()
    if me.has_custom_normals:
        with bpy.context.temp_override(object=o, active_object=o, selected_objects=[o]):
            bpy.ops.mesh.customdata_custom_splitnormals_clear()
    me.shade_smooth()
    me.set_sharp_from_angle(angle=ANGLE)


def polish_geometry(o, report):
    lo, hi = world_bounds(o); size = sorted(hi[i] - lo[i] for i in range(3))
    flat, tiny = size[0] < 0.0002, size[2] < 0.003
    if flat or tiny or len(o.data.polygons) < 4:
        report["skipped"].append(o.name); return False
    weld_and_shade(o)
    # Bevels read on hard-surface parts; on small organic pieces (seeds, leaves, slices) they only erode the shape.
    width = 0 if size[2] < 0.025 or o.name.startswith(NO_BEVEL_PREFIXES) else 0.0005 if o.name in RACK_SEATING else 0.0007
    width = min(width, size[0] * 0.2)  # never chew through thin plates and strips
    if width >= 0.0002:
        bev = o.modifiers.new("Polish_Bevel", "BEVEL")
        bev.width, bev.segments, bev.limit_method, bev.angle_limit = width, 2, "ANGLE", ANGLE
        bev.use_clamp_overlap, bev.harden_normals = True, True
    wn = o.modifiers.new("Polish_WeightedNormal", "WEIGHTED_NORMAL"); wn.keep_sharp = True; wn.weight = 50
    report["beveled" if width >= 0.0002 else "normals_only"].append(o.name)
    return True


def socket_target(node):
    """Which BSDF input an image ultimately feeds (through Mix / Math helpers)."""
    stack, seen = [node], set()
    while stack:
        n = stack.pop()
        if n.name in seen:
            continue
        seen.add(n.name)
        for out in n.outputs:
            for link in out.links:
                if link.to_node.type == "BSDF_PRINCIPLED":
                    return link.to_socket.name
                if link.to_node.type in ("MIX", "MIX_RGB", "MATH", "MIX_SHADER", "EMISSION"):
                    if link.to_node.type == "EMISSION":
                        return "Emission Color"
                    stack.append(link.to_node)
    return "Base Color"


def load_image(path, non_color=False):
    name = os.path.splitext(os.path.basename(path))[0]
    img = bpy.data.images.get(name) or bpy.data.images.load(path)
    img.name = name
    img.colorspace_settings.name = "Non-Color" if non_color else "sRGB"
    img.pack()
    return img


def polish_textures(asset, report):
    sets = TEXTURES.get(asset, {})
    for m in bpy.data.materials:
        tex = sets.get(m.name)
        if not tex or not m.use_nodes:
            continue
        nt = m.node_tree; bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        for n in [n for n in nt.nodes if n.type == "TEX_IMAGE" and n.image]:
            kind = "Emission" if socket_target(n) == "Emission Color" and "Emission" in tex else "BaseColor"
            if kind not in tex:
                kind = "BaseColor" if "BaseColor" in tex else kind
            old = n.image
            n.image = load_image(os.path.join(TEX, asset, tex[kind]))
            report["textures"].append(f"{m.name}: {old.name} {old.size[0]}x{old.size[1]} -> {n.image.name} {n.image.size[0]}x{n.image.size[1]}")
        if bsdf is None or "Normal" not in tex:
            continue
        # The colour map's UV transform drives the new maps so they stay registered (and export as KHR_texture_transform).
        colour = next((n for n in nt.nodes if n.type == "TEX_IMAGE" and socket_target(n) in ("Base Color", "Emission Color")), None)
        vector = colour.inputs["Vector"].links[0].from_socket if colour and colour.inputs["Vector"].is_linked else None
        x = colour.location.x if colour else -600
        mr = nt.nodes.new("ShaderNodeTexImage"); mr.image = load_image(os.path.join(TEX, asset, tex["MetallicRoughness"]), True); mr.location = (x, -300)
        sep = nt.nodes.new("ShaderNodeSeparateColor"); sep.location = (x + 300, -300)
        nt.links.new(mr.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Green"], bsdf.inputs["Roughness"]); nt.links.new(sep.outputs["Blue"], bsdf.inputs["Metallic"])
        nm = nt.nodes.new("ShaderNodeTexImage"); nm.image = load_image(os.path.join(TEX, asset, tex["Normal"]), True); nm.location = (x, -600)
        nmap = nt.nodes.new("ShaderNodeNormalMap"); nmap.location = (x + 300, -600)
        nt.links.new(nm.outputs["Color"], nmap.inputs["Color"]); nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
        if colour:
            for node in (mr, nm):
                node.extension, node.interpolation = colour.extension, colour.interpolation
                if vector:
                    nt.links.new(vector, node.inputs["Vector"])
        report["pbr"].append(m.name)


def add_lods(report):
    for o in list(mesh_objects()):
        lod = o.copy(); lod.name = o.name + "_LOD1"
        for c in o.users_collection:
            c.objects.link(lod)
        lod.parent = o.parent; lod.matrix_parent_inverse = o.matrix_parent_inverse.copy()
        for mod in list(lod.modifiers):
            if mod.name == "Polish_Bevel":
                lod.modifiers.remove(mod)
        if len(o.data.vertices) > 120:
            lod.data = o.data.copy(); lod.data.name = o.data.name + "_LOD1"
            dec = lod.modifiers.new("Polish_Decimate", "DECIMATE"); dec.ratio = 0.5; dec.use_collapse_triangulate = False
            if "Polish_WeightedNormal" in lod.modifiers:  # decimate before re-weighting normals
                lod.modifiers.move(lod.modifiers.find("Polish_Decimate"), 0)
        report["lod1"].append(lod.name)


def seating_diffs(before, after):
    """Height change of every seating sample (same x station in order, the nearest y within 0.25 mm), and how many
    samples found no counterpart at all (a lost station or surface is a failure, not a skipped comparison)."""
    def stations(points):
        return [sorted((p for p in points if p[0] == x), key=lambda p: p[1]) for x in sorted({p[0] for p in points})]
    rows_before, rows_after = stations(before), stations(after)
    diffs, lost = [], sum(len(row) for row in rows_before[len(rows_after):])
    for row_before, row_after in zip(rows_before, rows_after):
        for _, y, z in row_before:
            near = min(row_after, key=lambda p: abs(p[1] - y), default=None)
            if near is not None and abs(near[1] - y) <= 0.00025:
                diffs.append(abs(near[2] - z))
            else:
                lost += 1
    return diffs, lost


def export(path, tangents):
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_apply=True, export_materials="EXPORT",
                              export_image_format="AUTO", export_yup=True, export_tangents=tangents,
                              export_normals=True, export_extras=True, export_cameras=False, export_lights=False)


def import_file(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)


results = {}
for name in sorted(os.listdir(SRC)):
    if not name.endswith(".glb") or (ONLY and ONLY not in name):
        continue
    src, dst = os.path.join(SRC, name), os.path.join(OUT, name)
    asset = name[:-4].replace("_saturn", "")
    if "_tas_" in name:
        shutil.copyfile(src, dst); results[name] = {"action": "copied unchanged (tile)"}; continue
    report = {"skipped": [], "beveled": [], "normals_only": [], "textures": [], "pbr": [], "lod1": []}
    import_file(src)
    before = snapshot(); seat_before = profile_heights("istaka_govde") if "istaka" in name else []
    for o in mesh_objects():
        if o.data.users > 1:
            o.data = o.data.copy()
        polish_geometry(o, report)
    polish_textures(asset, report)
    if name.startswith(LOD_PREFIXES):
        add_lods(report)
    export(dst, tangents=bool(report["pbr"]))
    # ---- verification on the exported file ----
    import_file(dst)
    after = snapshot(); issues = []
    # Seating is measured on the re-imported file: what Unity will load, not the pre-export modifier stack.
    seat_after = profile_heights("istaka_govde") if "istaka" in name else []
    for obj, b in before["objects"].items():
        a = after["objects"].get(obj)
        if not a:
            issues.append(f"missing object {obj}"); continue
        # A bevel may only pull extremes inwards, never past its width; any growth is an error.
        grow = max(max(b["min"][i] - a["min"][i], a["max"][i] - b["max"][i]) for i in range(3))
        shrink = max(max(a["min"][i] - b["min"][i], b["max"][i] - a["max"][i]) for i in range(3))
        if grow > 0.00005 or shrink > 0.0008:
            issues.append(f"{obj}: bounds grew {grow * 1000:.2f} mm / shrank {shrink * 1000:.2f} mm")
        if a["mats"] != b["mats"]:
            issues.append(f"{obj}: materials {b['mats']} -> {a['mats']}")
        if a["parent"] != b["parent"]:
            issues.append(f"{obj}: parent {b['parent']} -> {a['parent']}")
    for mat, f in before["materials"].items():
        g = after["materials"].get(mat)
        if g is None:
            issues.append(f"missing material {mat}"); continue
        for k, v in f.items():
            if v is not None and g.get(k) is not None and abs(g[k] - v) > 0.01:
                issues.append(f"{mat}.{k}: {v} -> {g[k]}")
    seat = None
    if seat_before:
        diffs, lost = seating_diffs(seat_before, seat_after)
        seat = {"samples": len(diffs), "lost": lost, "max_mm": round(max(diffs) * 1000, 3) if diffs else None,
                "heights_mm": sorted({round(z * 1000, 1) for _, _, z in seat_before})}
        if lost:
            issues.append(f"rack seating: {lost} of {len(seat_before)} samples have no counterpart after re-import")
        if not diffs:
            issues.append("rack seating: no comparable samples after re-import")
        elif max(diffs) > SEATING_TOLERANCE:
            issues.append(f"rack seating moved {max(diffs) * 1000:.2f} mm (tolerance {SEATING_TOLERANCE * 1000:.1f} mm)")
    report.update(issues=issues, seating=seat, size_kb=os.path.getsize(dst) // 1024, src_kb=os.path.getsize(src) // 1024,
                  objects=len(after["objects"]))
    results[name] = report
    print("POLISHED", name, "issues", len(issues))
json.dump(results, open(REPORT, "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print("DONE", len(results))
# A quality gate: any issue (bounds, materials, hierarchy, seating) fails the run.
failed = [n for n, r in results.items() if r.get("issues")]
if failed:
    print("FAILED", len(failed), ":", ", ".join(failed))
    sys.exit(1)
