# v4 · Hat C · 3D varlıklar · rapor

Dal `v4/assets3d` (worktree `C:/GD/Okey101-v4/assets3d`, taban `753287e`). Unity çalıştırılmadı; bu hat dosyaları ve fark raporunu hazırlar, prefab/katalog üretimi Faz 2'de ana kopyada yapılır. Eksikler: `EKSIKLER.md` (EK-301…EK-311). Matris: `docs/v4/matris/hat-c.md`.

## 1 · Özet

| | Sonuç |
|---|---|
| GLB | 103 dosya (5 tema × 18 + 13 ortak, 84 585 424 bayt, 408 022 üçgen) `Assets/Galaxy101/Table/Models/v4/<tema>/` ve `v4/shared/` altına bayt bayt kopyalandı; 103 GLB + 7 klasör `.meta` (glTFast içe aktarıcı bloğu `galaxy101_zar.glb.meta`'dan, yeni GUID'ler). Eski `Models/*.glb` yerinde. |
| Proje ile karşılaştırma | Projede 29 GLB var (Satürn + ortak; 28'i `Design/old/assets_src/blender_out` ile bayt bayt aynı, zar `make_die.py`). v4'ün 29 karşılığı "farklı" (cila), 74'ü projede yok (4 tema × 18 = 72 tema dosyası + Satürn `isaretler` ve `sahne_tam`). Projede olup v4'te olmayan dosya yok. |
| Tasarım farkı (cilasız) | Cilanın yapıldığı eski ham set (`Design/old/assets_src/glb`, 28) ile: 10 dosya bayt bayt aynı, 7 dosya içerik aynı (yalnız ikili düzen), 11 dosyada doku/değer farkı (aşağıda §3.2). Geometri, düğüm ve malzeme adları 28'inde de aynı. |
| Tema varyantları | Geometri Satürn ile birebir (sınır farkı 0 mm); yalnız malzeme renkleri (+ çuha dokusu) değişiyor. `istaka_andromeda/ceviz/sedef` ve `tas_tek` beş temada bayt bayt aynı. |
| Malzemeler | `materials.json` 46 kayıt: v4 GLB'lerinde **31 var**, **3 farklı**, **12 eksik** (3'ü çalışma anı malzemesi, 9'u kullanılmayan ıstaka tanımı, EK-301). GLB'de olup pakette olmayan 94 malzeme adı (hediye/ikram, racks3d.js ıstakaları, taş seti başına gövde/yüz). Proje GLB'leri: 26 var, 4 farklı, 16 eksik. |
| Işıklar | `lighting.json` 7 ışık: sahne_tam'da 6'sı KHR_lights_punctual (hemi glTF'e geçmez). Renk, şiddet, menzil, koni ve konumlar 6/6 aynı; `key` ve `spot` yönü GLB'de kayıp (EK-303); decay geçmez. Projenin Unity ışıkları (TableScene/TableFx) paketle aynı, tek fark nukeLight konumu 0.25 m (paket 0.3 m). |
| Yerleşim | sahne_tam'dan çıkarılan masa/ıstaka/koltuk/atma halkası konumları projenin `TableConstants` değerleriyle 19/19 aynı. Kamera sahne_tam'da yok; tween3d.json'daki `mid` görünümü projede yok. |
| Unity sözleşmesi | Kodun ada göre aradığı her şey v4'te var (tas_R1/tas_govde/tas_yuz, holo_halka, cuha, LED, ıstaka TILT, hediye düğümleri, SkinService'in 16 malzemesi). Uymayanlar: 26 dosyada glTFast'in desteklemediği uzantı (iridescence/sheen, EK-304), zar (EK-306). |

## 2 · Yapılanlar ve komutlar

Bütün raporlar tek komutla yeniden üretilir: `sh tools/blender/hat_c_run.sh` (Blender ve Unity gerekmez; Python 3.12 + `uv`). Betikler `tools/blender/` altında, çıktılar `docs/v4/hat-c/` altında.

| # | Komut | Çıktı | Özet |
|---|---|---|---|
| 1 | `uv run --with numpy python tools/blender/gltf_inventory.py Design/Galaxy101_ClaudeCode/galaxy101_3d_tum_temalar docs/v4/hat-c/glb_inventory.json --label v4_tum_temalar` | `glb_inventory.json` (3 MB, satır başına bir düğüm/malzeme) | `INVENTORY v4_tum_temalar files 103 kinds {gift 4, treat 6, chair 1, room 1, die 1, markers 5, rack 30, table 20, scene 5, tile_set 25, tile 5}`. GLB'yi doğrudan ayrıştırır: dosya özeti, düğüm ağacı ve dünya konumları, gerçek sınırlar (EXT_mesh_gpu_instancing örnekleri dahil), malzemeler (uzantı değerleriyle), doku boyutları, ışıklar, kameralar. |
| 2 | aynı betik `Assets/Galaxy101/Table/Models … --flat` ve `Design/old/assets_src/glb` (geçici) | `glb_inventory_proje.json` | 29 proje GLB'si (Khronos Blender çıktısı, `_LOD1` kardeşleri) |
| 3 | `python tools/blender/gltf_compare.py <v4> <proje> <eski ham> docs/v4/hat-c/compare.json` | `compare.json` | `vs_project {aynı 0, farklı 29, yeni 74}`, `vs_old_raw {aynı 10, farklı 18}` (18'in 7'si yalnız ikili düzen), `across_themes` |
| 4 | `python tools/blender/material_light_map.py <v4> <proje> <pkg> docs/v4/hat-c` | `material_map.json`, `light_map.json`, `sahne_tam_layout.json` | `MATERIALS v4 {var 31, farklı 3, eksik 12} project {var 26, farklı 4, eksik 16} extra names 94`; `LIGHTS hemi: GLB'ye aktarılamaz, key/spot: farklı (yön), back/fill/nukeLight/winSpot: var`; `LAYOUT checks 19/19` |
| 5 | `uv run --with numpy python tools/blender/unity_contract_check.py <v4> <kaynak klasör> docs/v4/hat-c/unity_contract.json` | `unity_contract.json` | `checks 193, uyar 165, uymaz 28` (26 desteklenmeyen uzantı + 2 zar), SkinService eksik malzeme yok |
| 6 | `python tools/blender/place_v4_models.py Design/Galaxy101_ClaudeCode/galaxy101_3d_tum_temalar . docs/v4/hat-c/placement.json` | `Models/v4/**`, `placement.json` | `PLACED 103 files, 7 folders, new metas 110`; ikinci çalıştırma `new metas 0` (GUID'ler korunur), SHA-256 kaynakla aynı |
| 7 | `python tools/blender/hat_c_matrix.py docs/v4/hat-c docs/v4/matris/hat-c.md` | matris | `glb 103 {farklı 29, eksik 74}`, `materials 140 {var 26, farklı 4, eksik 16, pakette yok 94}`, `lights 18 {var 12, farklı 4}`, `layout 24 {var 23, eksik 1}` |
| 8 | `uv run --with playwright --with pillow python tools/blender/bake_tile_faces.py Design/Galaxy101_ClaudeCode/prototip <geçici>` | (geçici klasör, commit edilmedi) | `ATLAS classic 0, galaxy 0.18, gold 0.1, neon 0.35, marble 0` (emisyon). Atlas karşılaştırması: klasik ve mermer projedekiyle bayt bayt aynı; galaksi/altın/neon farklı (EK-311). Betik v4 dışa aktarma sayfasını bulacak şekilde güncellendi. |
| 9 | `uv run --with numpy python tools/blender/extract_felt_from_glb.py Assets/Galaxy101/Table/Models/v4 <geçici> Assets/Galaxy101/Resources/Galaxy101/Table/Felt` | (geçici) | 16 çuha dokusu, hepsi 1024², yön projeyle aynı; piksel farkı ortalama 0.04/255 (en çok 31, 89 piksel) |

Blender ile cila yapılmadı: v4 GLB'leri pakette tanımlı haliyle (bayt bayt) yerleştirildi. Malzeme adları paketle uyuşuyor (uyuşmayanlar paketin kendi tutarsızlığı, EK-301), bu yüzden GLB'ye dokunmayı gerektiren bir düzeltme yok.

## 3 · Bulgular

### 3.1 Projedeki modeller ve Unity kodunun onları kullanışı

- `Models/*.glb` (29): `TableAssetBuilder.cs:21` `Models` sabiti; `:25-27` masa/ıstaka/taş seti dosya adları (`galaxy101_<ad>_saturn`); `:36-42` her biri `MakePrefab` (`:63-65` `Models + "/" + model + ".glb"`) ile prefab olur, `TableMaterialConverter` glTFast malzemelerini URP Lit / Complex Lit / Unlit'e çevirir (`KHR_materials_emissive_strength` değerini GLB JSON'undan okur), `_LOD1` adlı renderer'lar `AddLodGroup` (`:79`) ile LODGroup olur, ıstakalara 30 `Slot_r_i` eklenir. `TableAssetProbe.cs:15` yalnız `Models/*.glb` üst klasörünü okur (v4 alt klasörü etkilemez). Başka kod model yolu kullanmıyor.
- Çalışma anında ada göre aranan şeyler: `TableScene.cs:268` ve `TileFactory.cs:33-34,67` taş setinde `tas_R1` → `tas_govde`, `tas_yuz`; `TableScene.cs:305` `cuha`; `TableScene.cs:317` ıstaka kökünün TILT dönüşü; `TableFx.cs:76` `holo_halka*`, `:84` `led*`/`*_led*` malzemeleri; `TableGifts.cs:77-81` `demet`, `kupa_govde`, `alev*`, `kabarcik*`; `SkinService.cs:56-79` 16 malzeme adı; `TableCeremony.cs:225` zar yüz yönleri.
- v4'te hepsi var (`unity_contract.json`). Cilalı proje dosyalarındaki `.001` ekli adlar (`istaka_led_uc.001`, `tabak.001`…) ve `Node_N`/`Mesh_N` adları Blender'ın eklediği adlar; v4'te temiz ad var, kod bunları aramıyor.

### 3.2 v4 ile eski ham dışa aktarım arasındaki gerçek tasarım farkları (Satürn + ortak)

| Dosya | Fark |
|---|---|
| `masa_istasyon/ceviz/hologram/andromeda` | `holo` dokusu 512 → 1024 px; `cuha` dokusu (andromeda hariç) ≈ aynı; `led` emisyon gücü 2.447 → 2.444 (LED nabzının dışa aktarım anı, EK-307) |
| `oda_istasyon` | `led` emisyon gücü (aynı neden) |
| `tas_seti_altin/galaksi/neon` | 53 yüz dokusu yeniden çizildi; yüz emisyonu 0.25 → 0.1 / 0.55 → 0.18 / 1.6 → 0.35 (`tiles3d.js:92-96` FACE) |
| `tas_seti_klasik/mermer`, `tas_tek` | sahte okey yüz dokusu; mermer gövde dokusu (`tas_marble`) |
| 6 ıstaka, koltuk, baklava, çekirdek, simit | bayt bayt aynı |
| 4 hediye, kahve, kola, çay | içerik aynı (yalnız ikili düzen) |

`table3d.js` eski referansla karşılaştırıldığında 3D geometri ve malzeme kodu değişmemiş; yeni olanlar ıstaka taşı simgeleri (`simge_side`/`simge_islek`, `table3d.js:241-261`), `mid` kamera görünümü, soldan halkasının parlaması ve zar/işaret/sahne dışa aktarımı.

### 3.3 Malzemeler (`material_map.json`)

- Masa, oda, koltuk, taş gövdesi ve işaret malzemelerinin hepsi paket değerleriyle ya da applyLook()'un tema değeriyle aynı (31). Tema değerleri G101-Masa eşlemesinden (EK-302): Satürn `a1 #FFD964`, `a2 #F5A623`, `rim #4a5080`, `tile #FFFDF6`, `leather #232A66`, `glow #F5A623`, `sky #FFE7A3` (5 temanın tamamı `material_map.json` `summary.theme_tokens`).
- Farklı 3 + eksik 9: kullanılmayan ıstaka tanımları (EK-301). Eksik 3: `gecerli`, `yeni_tas`, `*_duz` yalnız çalışma anında oluşur; projede `TileFactory.cs:48,159-160` kodda üretiyor.
- Paketin listelemediği 94 ad: ıstakalar (racks3d.js, 24), taş setleri (`tas_<skin>`, `tas_kenar_<skin>`, `tas_yuz_<skin>_*`), hediye/ikram malzemeleri (`gifts3d.js`), zar (`zar_1…6`), adsız işaret malzemeleri.
- Proje (cilalı) GLB'lerinde farklı 4: `masa_andromeda_kenar` ve `istaka_andromeda_uc` iridescence kalınlığı Blender'da kaybolmuş (glTFast zaten desteklemiyor); `istaka_ceviz`, `istaka_pirinc` EK-301.
- Kodda paketten farklı davranış (Unity aşaması için): `SkinService.cs:68` `holo`'yu her masada A1 yapıyor; pakette andromeda masada `holo #B79CFF`, `hale #7F9CFF` (`materials.json` tableSkinApply). `masa_hale` (masa altı hale, `table3d.js:186`) ve `balya_isaret` (`table3d.js:379-381`) Unity'de yok.

### 3.4 Işıklar ve sahne_tam (`light_map.json`, `sahne_tam_layout.json`)

- sahne_tam'da 6 ışık (adsız düğüm `#98…#106`): nukeLight (#FFB25A, 0, menzil 4.5), key (#FFF4E2, 1.7, (1.1, 4.2, 2.0)), spot (#FFFFFF, 9, menzil 7, koni 0.62/0.186, (0, 3.2, 0.3)), back (tema glow = a2, 5, menzil 7, (0, 1.5, −2.3)), fill (#9FB6FF, 1.6, menzil 6, (0, 1.6, 2.2)), winSpot (#FFE7A3, 0, menzil 5, koni 0.32/0.128). Hemi yok (boş düğüm `#99`).
- Yerleşim (glTF → Unity: x işareti değişir): masa yüzeyi y 0.76; ıstakalar koltuk çerçevesinde (0, 0.793, 0.8), X −0.36 rad; koltuklar (0, 0, 1.32), oyuncunun (seat 0) koltuğu yok; koltuk yönleri 0, π/2, π, −π/2; atma halkası (0.6, 0.7612, 0.6), soldan halkası (−0.6, 0.7612, 0.6); deste 106 taş (mermer, GPU örnekleme); oda 7 × 3.2 × 5.5 m.
- Projedeki Unity değerleri (`TableScene.cs:152-189`, `TableFx.cs:66-67, 204-205`, `ThreeEnvironment.cs`, `ThreeToneMapping.cs:13`) lighting.json ile aynı; π ve decay dönüşümü (`ThreeToUrp`, `ThreeLocalIntensity`) çuha merkezinde eşleme yapıyor. Farklar: nukeLight 0.25 m (paket 0.3 m), Mobile URP gölgesi 1024 / yumuşak kapalı, kamera temizliği opak `Bg3`.

### 3.5 Diğer

- glTFast 6.20 `EXT_mesh_gpu_instancing`'i destekliyor: çekirdek (112), simit (240), baklava (63) ve sahne_tam destesi (106) her örnek için `<mesh>_i<n>` çocuk nesnesi olarak kurulur (`GameObjectInstantiator.AddPrimitiveInstanced`). `TableGifts` hediyeleri malzemeye göre zaten birleştiriyor.
- `KHR_materials_emissive_strength` glTFast'te desteklenmiyor ama `TableMaterialConverter` GLB JSON'undan okuyor (mevcut davranış).

## 4 · Commitler

| Commit | İçerik |
|---|---|
| `97e5c0d` | 103 GLB + 110 `.meta` `Models/v4/` altında; `gltf_inventory.py`, `gltf_compare.py`, `material_light_map.py`, `unity_contract_check.py`, `place_v4_models.py`, `hat_c_run.sh`; `docs/v4/hat-c/*.json` |
| `cac659e` | `hat_c_matrix.py` ve `docs/v4/matris/hat-c.md`; `bake_tile_faces.py` v4 sayfası; `extract_felt_from_glb.py`; Türkçe notlar |
| son commit | `RAPOR.md`, `EKSIKLER.md`, güncel JSON'lar ve matris |

## 5 · Unity aşaması: adım adım (ana kopya, Faz 2)

1. **Birleştir ve içe aktar.** `v4/assets3d` birleşince Unity `Assets/Galaxy101/Table/Models/v4/**` altındaki 103 GLB'yi glTFast ile içe aktarır (~81 MB, LFS). Beklenen uyarılar (bugünküyle aynı tür, `code 20`): `KHR_materials_iridescence` (26), `KHR_materials_sheen` (1), `KHR_materials_emissive_strength`. Hata beklenmez; `TableAssetProbe` yalnız üst klasörü okur.
2. **Prefab kaynağını seç (EK-305).** Önerilen (A): `TableAssetBuilder.cs` değişmez, prefablar cilalı `Models/*.glb`'den üretilir (v4 içeriği aynı; fark yalnız §3.2). (B) v4'e geçilecekse: `TableAssetBuilder.cs:21` `Models`'i ikiye ayır — temalı dosyalar `Models/v4/saturn/<ad>.glb`, ortaklar `Models/v4/shared/<ad>.glb`; zar `Models/galaxy101_zar.glb`'de kalır (EK-306). (B)'de `_LOD1` olmadığından `AddLodGroup` LODGroup kurmaz, bevel ve 2K dokular kaybolur. (C) cilayı v4'e uygulamak: `blender --background --factory-startup --python tools/blender/polish_glb.py -- Assets/Galaxy101/Table/Models/v4/<seçilen> Design/old/assets_src/textures_2k <çıktı> <rapor.json>` (holo dokusu için `bake_canvas_textures.py` yeni prototiple yeniden pişirilmeli).
3. **`Galaxy 101 > Build Table Assets`.** Beklenen günlük satırı `GALAXY101_TABLE_ASSETS_READY prefabs=28` (4 masa, 6 ıstaka, 5 taş seti, 10 hediye, koltuk, oda, zar). Katalog `Assets/Galaxy101/Resources/Galaxy101/Table/TableAssetCatalog.asset`.
4. **Taş yüz atlası (EK-311).** `uv run --with playwright --with pillow python tools/blender/bake_tile_faces.py Design/Galaxy101_ClaudeCode/prototip Assets/Galaxy101/Resources/Galaxy101/Table/TileFaces` → galaksi/altın/neon PNG'leri ve `tile_faces.json` (emisyon 0.18 / 0.1 / 0.35) güncellenir; klasik ve mermer değişmez. Sonra Build Table Assets atlas içe aktarma ayarlarını kurar.
5. **Çuha dokuları (isteğe bağlı).** v4 ile fark ihmal edilebilir. Yenilenecekse: `uv run --with numpy python tools/blender/extract_felt_from_glb.py Assets/Galaxy101/Table/Models/v4 Assets/Galaxy101/Resources/Galaxy101/Table/Felt` (ad ve yön `TableScene.ApplyFelt` ile aynı). `tools/felt_textures.py` silinmiş `Design/galaxy101_unity_handoff` yolunu okuyor; Hat D'nin işi.
6. **Malzeme/renk (kod, Unity aşaması).** Tema renklerini v4 tema GLB'leriyle doğrula: her `Models/v4/<tema>/` dosyası o temanın applyLook() rengini taşıyor (`material_map.json` checks `tema`). Paketle farklı kod: `SkinService.cs:68` andromeda masada `holo #B79CFF` ve `hale #7F9CFF` olmalı; `masa_hale` (geometri `isaretler` GLB'sinde, 3.6 m düzlem, y 0.004; opaklık ceviz 0.12 / holo 0.4 / diğer 0.25) ve `balya_isaret` eklenmeli; ıstaka taşı simgeleri (`simge_side`, `simge_islek`, 128² doku `isaretler` GLB'sinde) isteniyorsa 3D rozet olarak eklenmeli.
7. **Işıklar.** Oyun ışıkları `TableScene.BuildLighting` olarak kalır (değerler lighting.json ile aynı). sahne_tam'ın ışıkları kullanılmaz (EK-303); sahne_tam bir referans sahnesine konacaksa ışık nesneleri silinmeli ya da `key`/`spot` (0, 0.76, 0)'a çevrilmeli (Unity'de de (0, 0.76, 0)). Düzeltilecek tek değer: `TableFx.cs:204` nukeLight `Vector3.up * 0.25f` → `0.3f` (paket "iniş noktası + 0,3 m"). Mobile URP gölge çözünürlüğü (1024) bilinçli bir karar olarak kayda geçmeli.
8. **Kamera.** `TableConstants.cs:24-30`'a `mid` görünümü eklenmeli: pos (0, 2.08, 1.62) → hedef (0, 0.6, −0.06), fov 40 (`tween3d.json` camera.views.mid). Diğer 4 görünüm aynı.
9. **Zar.** `Models/galaxy101_zar.glb` kalır. v4 zarı kullanılmak istenirse `TableCeremony.cs:225` `FaceNormal`'da 2 ↔ 5 yer değişmeli ve modelin y'si −0.015 kaydırılmalı (EK-306).
10. **Doğrulama.** `sahne_tam_layout.json` değerleri (glTF ve Unity koordinatları) ve `light_map.json` ışıkları referans karesiyle karşılaştırılır; bir model değişirse `sh tools/blender/hat_c_run.sh` raporları yeniden üretir.

## 6 · Dosyalar

- `tools/blender/gltf_inventory.py`, `gltf_compare.py`, `material_light_map.py`, `unity_contract_check.py`, `place_v4_models.py`, `hat_c_matrix.py`, `extract_felt_from_glb.py`, `hat_c_run.sh`; güncellenen `bake_tile_faces.py`.
- `docs/v4/hat-c/glb_inventory.json`, `glb_inventory_proje.json`, `compare.json`, `material_map.json`, `light_map.json`, `sahne_tam_layout.json`, `unity_contract.json`, `placement.json`, `RAPOR.md`, `EKSIKLER.md`.
- `docs/v4/matris/hat-c.md`.
- `Assets/Galaxy101/Table/Models/v4.meta`, `Models/v4/{saturn,mars,neptun,jupiter,venus,shared}/` (103 GLB + `.meta`).
