"""Turn the high-resolution canvas bakes into glTF-ready PBR sets.

* Flips every image vertically (three.js GLTFExporter stores CanvasTextures flipped; the GLB UVs expect that).
* Names files after their material: <material>_BaseColor.png, <material>_Emission.png,
  <material>_Normal.png (OpenGL / +Y) and <material>_MetallicRoughness.png (G = roughness, B = metallic).
* Normal and roughness detail is derived from the material's own base pattern (wood grain, brushing,
  carbon weave, felt fibre, floor/wall seams) so it stays registered with the colour map.

Usage: python prepare_textures.py <hires dir> <inventory.json> <out dir>
"""
import json, os, sys
import numpy as np
from PIL import Image, ImageFilter

HIRES, INVENTORY, OUT = sys.argv[1:4]
manifest = json.load(open(os.path.join(HIRES, "manifest.json"), encoding="utf-8"))
inventory = json.load(open(INVENTORY, encoding="utf-8"))

# material -> (pattern, normal strength, roughness variation, height blur px)
DETAIL = {
    "istaka_ceviz": ("wood", 2.2, 0.22, 1.2), "istaka_ceviz_uc": ("wood", 2.2, 0.22, 1.2),
    "istaka_gulagaci": ("wood", 2.0, 0.20, 1.2), "istaka_gulagaci_uc": ("wood", 2.0, 0.20, 1.2),
    "masa_ceviz_govde": ("wood", 2.0, 0.20, 1.2), "masa_ceviz_kenar": ("wood", 2.0, 0.20, 1.2),
    "ahsap": ("wood", 1.6, 0.18, 1.0),
    "istaka_aluminyum": ("brushed", 1.4, 0.30, 0.6), "istaka_eloksal": ("brushed", 1.4, 0.30, 0.6),
    "istaka_karbon": ("weave", 3.0, 0.18, 2.0),
    "istaka_sedef_panel": ("inlay", 2.4, 0.10, 1.5),
    "zemin": ("seams", 3.5, 0.25, 1.5), "duvar": ("seams", 2.5, 0.15, 1.5),
    "kese_kagidi": ("paper", 1.4, 0.12, 1.0),
}
FELT = ("felt", 0.5, 0.0, 0.8)  # station / walnut felt only; holo and andromeda felts are emissive art


def material_factors(asset, material):
    for name, data in inventory.items():
        if name.startswith(asset) and material in data["materials"]:
            info = data["materials"][material]
            return float(info.get("Metallic", 0) or 0), float(info.get("Roughness", 0.5) or 0.5)
    return 0.0, 0.5


def height_from(img, blur):
    lum = np.asarray(img.convert("L"), np.float32) / 255.0
    h = Image.fromarray((lum * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(blur))
    h = np.asarray(h, np.float32) / 255.0
    return (h - h.mean()) / (h.std() + 1e-6)


def normal_map(height, strength):
    # Stored orientation: row 0 is the top of the file = v=1, so +V (green) points to decreasing rows.
    dx = np.roll(height, -1, 1) - np.roll(height, 1, 1)
    dy = np.roll(height, 1, 0) - np.roll(height, -1, 0)
    scale = strength * 0.1 * (2048.0 / max(height.shape))
    n = np.dstack([-dx * scale, -dy * scale, np.ones_like(height)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).round().astype(np.uint8), "RGB")


def mr_map(height, metallic, roughness, variation):
    rough = np.clip(roughness * (1.0 - variation * np.tanh(height * 0.8)), 0.02, 1.0)
    rgb = np.dstack([np.ones_like(rough), rough, np.full_like(rough, metallic)])
    return Image.fromarray((rgb * 255).round().astype(np.uint8), "RGB")


report = {}
for asset, textures in manifest.items():
    folder = os.path.join(OUT, asset); os.makedirs(folder, exist_ok=True)
    for key, meta in textures.items():
        material, slot = key.split("__")
        img = Image.open(os.path.join(HIRES, asset, key + ".png")).transpose(Image.FLIP_TOP_BOTTOM)
        kind = "Emission" if slot == "emissiveMap" else "BaseColor"
        has_alpha = img.mode == "RGBA" and np.asarray(img)[..., 3].min() < 255
        img = img if has_alpha else img.convert("RGB")
        img.save(os.path.join(folder, f"{material}_{kind}.png"), optimize=True)
        entry = report.setdefault(asset, {}).setdefault(material, {"size": list(img.size)})
        entry[kind] = f"{material}_{kind}.png"
        detail = DETAIL.get(material) or (FELT if material == "cuha" and asset.split("_")[-1] in ("istasyon", "ceviz") else None)
        if slot != "map" or not detail:
            continue
        pattern, strength, variation, blur = detail
        height = height_from(img.convert("RGB"), blur * img.size[0] / 2048.0 * 2)
        if pattern == "felt":  # the felt art is a smooth gradient: give it a fine, seeded fibre nap instead
            rng = np.random.default_rng(101)
            noise = Image.fromarray((rng.random(img.size[::-1]) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.4))
            noise = np.asarray(noise, np.float32); height = (noise - noise.mean()) / (noise.std() + 1e-6)
        if pattern == "seams":  # grid lines are lighter than the panel: they read as grooves
            height = -height
        normal_map(height, strength).save(os.path.join(folder, f"{material}_Normal.png"), optimize=True)
        metallic, roughness = material_factors(asset, material)
        mr_map(height, metallic, roughness, variation).save(os.path.join(folder, f"{material}_MetallicRoughness.png"), optimize=True)
        entry.update(Normal=f"{material}_Normal.png", MetallicRoughness=f"{material}_MetallicRoughness.png",
                     pattern=pattern, metallic=metallic, roughness=roughness)
json.dump(report, open(os.path.join(OUT, "textures.json"), "w", encoding="utf-8"), indent=1, ensure_ascii=False)
print("PREPARED", sum(len(v) for v in report.values()), "materials")
