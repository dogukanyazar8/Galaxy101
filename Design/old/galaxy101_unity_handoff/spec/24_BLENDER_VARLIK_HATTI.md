# 24 — Blender varlık hattı (Claude Code + Blender)

Amaç: prototipteki 3D modelleri Blender'da üretim kalitesine getirip Unity'ye aktarmak. Claude Code, Blender'ı **komut satırından Python ile** (`blender -b -P script.py`) ya da kuruluysa **Blender MCP** sunucusu üzerinden kullanabilir.

## Kaynaklar
1. `reference/game/Galaxy 101 - 3D Varlik Disa Aktarma.html` — yerel sunucuda açın; gezegen temasını seçip **Tümünü GLB olarak indir** ile masa, ıstakalar, taşlar, koltuklar, ikramlar GLB olarak iner (`spec/16`).
2. Ölçüler `spec/09` ve `spec/data/table_scene.json` (TY 0.76, TW 0.05, TH 0.07, TD 0.010, SL 0.053, masa yarı boy 0.86, çuha 0.76).

## Blender'da yapılacaklar
| Varlık | Not |
|---|---|
| Taş | Yuvarlatılmış kutu 50×70×10 mm, bevel 2.8 mm / 2 segment; ayrı yüz malzemesi (UV 0–1 tek düzlem); gövde PBR (pürüzlülük .36, clearcoat .7). 5 görünüm: klasik, galaksi, altın-oniks, neon, mermer (`tiles3d.js`). |
| Zar | 30 mm, köşe yarıçapı 5 mm; 6 yüz için ayrı UV adası; nokta dokusu 256²; ya da noktaları gerçek çukur (boolean) yapın. |
| Istaka | 6 tema profili (`racks3d.js`): profil eğrisi → extrude 0.98 m, uç kapakları; 2 basamak, eğim −0.36 rad. |
| Masa | yuvarlatılmış kare plaka + kenar halkası + LED bandı + çuha (ayrı malzeme), ayak, taban; 4 tema (station, wood, holo, andromeda). |
| Koltuk | 3 adet, deri malzeme, sırt LED şeridi (emissive). |
| İkramlar | 10 model (`gifts3d.js`). |

Kurallar: gerçek ölçek (1 birim = 1 m), Y yukarı, uygulanmış ölçek/dönüş, isimler prototipteki gibi (`tas_govde`, `tas_yuz`, `istaka0`, `masa_govde`, `cuha`, `zar`…), üçgen sayısı: taş ≤ 300, ıstaka ≤ 4k, masa ≤ 15k.

## Örnek Python (taş)
```python
import bpy, bmesh
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.mesh.primitive_cube_add(size=1)
o = bpy.context.object; o.name = "tas_govde"; o.scale = (0.05, 0.010, 0.07)
bpy.ops.object.transform_apply(scale=True)
b = o.modifiers.new("bevel", "BEVEL"); b.width = 0.0028; b.segments = 2
bpy.ops.object.modifier_apply(modifier="bevel")
m = bpy.data.materials.new("tas"); m.use_nodes = True
p = m.node_tree.nodes["Principled BSDF"]; p.inputs["Roughness"].default_value = 0.36
p.inputs["Coat Weight"].default_value = 0.7; o.data.materials.append(m)
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 0.0056, 0), rotation=(1.5708, 0, 0))
f = bpy.context.object; f.name = "tas_yuz"; f.scale = (0.046, 0.0644, 1); bpy.ops.object.transform_apply(scale=True)
bpy.ops.export_scene.gltf(filepath="//tas.glb", export_format="GLB")
```
Aynı şablonla zar ve diğer parçalar üretilir. FBX gerekiyorsa `bpy.ops.export_scene.fbx(apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y')`.

## Unity'ye aktarma
`Assets/Art/Models/` altına; glTFast paketi ya da FBX. Taş yüzleri **çalışma zamanında** dokudan atanır (atlas: 4 renk × 13 + sahte okey + boş okey = 54 hücre). Masada yatan taş yüzü için Unlit malzeme varyantı (`spec/23`).
