# Blender cilalama (spec/16 §B) — Satürn

Ham: `glb/` (28, `Saturn GLB/` kopyası) · Cilalı: `blender_out/` (28, aynı adlar) · 2K dokular: `textures_2k/` · Rapor: `blender_out/polish_report.json`

Yeniden üretim (Blender 5.2, hepsi `tools/blender/` altında):
1. `uv run --with playwright python bake_canvas_textures.py <reference/table> <hires>` — prototipin kendi canvas üreticileri 4–8× çözünürlükte (cvs() yaması); orijinal gömülü dokularla ortalama fark < 3/255.
2. `python prepare_textures.py <hires> <glb_inventory.json> textures_2k` — dikey çevirme (glTF yönü), `<malzeme>_BaseColor/_Emission/_Normal/_MetallicRoughness.png`.
3. `blender --background --factory-startup --python polish_glb.py -- glb textures_2k blender_out blender_out/polish_report.json`
4. Kontrol: `render_compare.py` (ham/cilalı EEVEE karşılaştırma).

Yapılanlar
- Nesne/malzeme adları, hiyerarşi, orijin ve dönüşümler korunur (Apply Transform yok). Dışa aktarımda yeniden içe alınıp doğrulanır: ad, ebeveyn, malzeme yuvası, sınır kutusu (büyüme 0, içe kırpma ≤ 0.8 mm), malzeme faktörleri (emission renk×şiddet).
- Kenarlar: köşe birleştirme, açıya göre yumuşak gölgeleme (30°), Bevel 0.7 mm × 2 (ıstaka gövdesi 0.5 mm) + Weighted Normal. Düz kartlar, < 3 mm detaylar, < 25 mm organik parçalar ve dar açılı baklava dilimleri yalnız normal düzeltmesi alır.
- Istaka raf/dudak yükseklikleri: yatay yüzey ışın testinde değişim 0.000 mm (ceviz, sedef, alüminyum, andromeda).
- Dokular: 34 canvas dokusu 2K'ya (hediyeler 1K) yükseltildi; UV ve KHR_texture_transform aynı kaldı. Ahşap, fırçalı metal, karbon, sedef panel, keçe, zemin, duvar, kese kâğıdı için Normal + MetallicRoughness eklendi.
- Taş setleri ve tek taş dokunulmadan kopyalandı (spec: taş gövdesi sabit, yüzler Unity atlası).
- LOD1: ıstaka, masa, koltuk, ikram/hediye — her parçaya `<ad>_LOD1` kardeşi (Decimate %50, bevelsiz, ≤120 köşeli parçalar mesh paylaşır). Oda LOD'suz.

Spec'ten sapmalar
- Smart UV Project yapılmadı: dokular tekrar eden (tiling) ve orijinal UV + transform ile birebir hizalı; tekil UV'ye pişirmek çözünürlüğü düşürürdü. Damar yönü orijinaldeki gibi x boyunca.
- LOD adlandırması: LOD0 nesneleri yeniden adlandırılmadı (kod bu adlara bağlı); yalnız `_LOD1` kopyaları eklendi. Unity'de `X` + `X_LOD1` → LODGroup.
