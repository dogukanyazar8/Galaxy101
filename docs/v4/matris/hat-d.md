# Fark matrisi · Hat D (veri varlıkları)

Paket: `Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/` (aşağıda dosya adları buna göre). "Projede ne vardı" v4 öncesi taban commit `753287e` (`Assets/Galaxy101/Resources/Galaxy101/` altı, aşağıda kısaltılmış). "v4 sonrası" bu dalın ürettiği dosya. Üretici: `tools/v4/hat_d_matrix.py` (elle düzenleme yok; yeniden üret). Kayıtlar: `scripts/import-v4-data.py` → `docs/v4/hat-d/import_report.json`.

## Özet

| Bölüm | Durum sayıları |
|---|---|
| Temalar | var 395, eksik 6, farklı 3 |
| Fontlar | farklı 7, eksik 5, var 6, pakette yok 1 |
| İkonlar | farklı 3, var 35, eksik 31, pakette yok 4 |
| Lig rozetleri | eksik 75 |
| İmleçler | eksik 11 |
| Ses | eksik 34, farklı 8, var 3 |
| Metinler | var 691, farklı 40, eksik 284, boş (pakette bilinçli) 2, pakette yok (masa anahtarı) 85 |

## Temalar

Her gezegen × her anahtar. `ui.ro` (yükleme ekranındaki halka opaklığı) ile `rings.ro` (menü gezegen halkası) pakette aynı adlı iki ayrı değer; projede yalnız ikincisi vardı (`cssVars.ro`), ilki `loadingRingOpacity` olarak eklendi.

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| `tema.Satürn.ad.tr` | `Satürn` (strings.json menu.STR.pn) | `Satürn` (themes.json name_tr) | var | `Satürn` (name_tr) |
| `tema.Satürn.ad.en` | `Saturn` (strings.json menu.STR.pn) | `Saturn` (themes.json name_en) | var | `Saturn` (name_en) |
| `tema.Satürn.menu.desc` | `Altın halkalar` (themes.json planets.Satürn.menu.desc) | `Altın halkalar` (themes.json desc_tr) | var | `Altın halkalar` (desc_tr) |
| `tema.Satürn.menu.desc.en` | `Golden rings` (strings.json menu.STR.pd) | `Golden rings` (themes.json desc_en) | var | `Golden rings` (desc_en) |
| `tema.Satürn.menu.orb` | `#FBEBC4, #D9B06A, #8A6430` (themes.json planets.Satürn.menu.orb) | `#FBEBC4, #D9B06A, #8A6430` (themes.json pickerOrb) | var | `#FBEBC4, #D9B06A, #8A6430` (pickerOrb) |
| `tema.Satürn.ui.bg1` | `#2A2F6E` (themes.json planets.Satürn.ui.bg1) | `#2A2F6E` (themes.json cssVars.bg1) | var | `#2A2F6E` (cssVars.bg1) |
| `tema.Satürn.ui.bg2` | `#161A45` (themes.json planets.Satürn.ui.bg2) | `#161A45` (themes.json cssVars.bg2) | var | `#161A45` (cssVars.bg2) |
| `tema.Satürn.ui.bg3` | `#0A0C28` (themes.json planets.Satürn.ui.bg3) | `#0A0C28` (themes.json cssVars.bg3) | var | `#0A0C28` (cssVars.bg3) |
| `tema.Satürn.ui.pn` | `#232A66` (themes.json planets.Satürn.ui.pn) | `#232A66` (themes.json cssVars.pn) | var | `#232A66` (cssVars.pn) |
| `tema.Satürn.ui.pnH` | `#2D3680` (themes.json planets.Satürn.ui.pnH) | `#2D3680` (themes.json cssVars.pnH) | var | `#2D3680` (cssVars.pnH) |
| `tema.Satürn.ui.pnS` | `#0B0D2B` (themes.json planets.Satürn.ui.pnS) | `#0B0D2B` (themes.json cssVars.pnS) | var | `#0B0D2B` (cssVars.pnS) |
| `tema.Satürn.ui.dp` | `#0B0E2E` (themes.json planets.Satürn.ui.dp) | `#0B0E2E` (themes.json cssVars.dp) | var | `#0B0E2E` (cssVars.dp) |
| `tema.Satürn.ui.sd` | `#10133A` (themes.json planets.Satürn.ui.sd) | `#10133A` (themes.json cssVars.sd) | var | `#10133A` (cssVars.sd) |
| `tema.Satürn.ui.bd` | `#2E3680` (themes.json planets.Satürn.ui.bd) | `#2E3680` (themes.json cssVars.bd) | var | `#2E3680` (cssVars.bd) |
| `tema.Satürn.ui.a1` | `#FFD964` (themes.json planets.Satürn.ui.a1) | `#FFD964` (themes.json cssVars.a1) | var | `#FFD964` (cssVars.a1) |
| `tema.Satürn.ui.a2` | `#F5A623` (themes.json planets.Satürn.ui.a2) | `#F5A623` (themes.json cssVars.a2) | var | `#F5A623` (cssVars.a2) |
| `tema.Satürn.ui.aS` | `#B86E0F` (themes.json planets.Satürn.ui.aS) | `#B86E0F` (themes.json cssVars.aS) | var | `#B86E0F` (cssVars.aS) |
| `tema.Satürn.ui.aT` | `#3A1F00` (themes.json planets.Satürn.ui.aT) | `#3A1F00` (themes.json cssVars.aT) | var | `#3A1F00` (cssVars.aT) |
| `tema.Satürn.ui.lS` | `#8A4E06` (themes.json planets.Satürn.ui.lS) | `#8A4E06` (themes.json cssVars.lS) | var | `#8A4E06` (cssVars.lS) |
| `tema.Satürn.ui.gl` | `rgba(255,201,74,.45)` (themes.json planets.Satürn.ui.gl) | `rgba(255,201,74,.45)` (themes.json cssVars.gl) | var | `rgba(255,201,74,.45)` (cssVars.gl) |
| `tema.Satürn.ui.mt` | `#A9AEDD` (themes.json planets.Satürn.ui.mt) | `#A9AEDD` (themes.json cssVars.mt) | var | `#A9AEDD` (cssVars.mt) |
| `tema.Satürn.ui.ic` | `#D3D6FF` (themes.json planets.Satürn.ui.ic) | `#D3D6FF` (themes.json cssVars.ic) | var | `#D3D6FF` (cssVars.ic) |
| `tema.Satürn.ui.p1` | `#F3E1B5` (themes.json planets.Satürn.ui.p1) | `#F3E1B5` (themes.json cssVars.p1) | var | `#F3E1B5` (cssVars.p1) |
| `tema.Satürn.ui.p2` | `#D9B77A` (themes.json planets.Satürn.ui.p2) | `#D9B77A` (themes.json cssVars.p2) | var | `#D9B77A` (cssVars.p2) |
| `tema.Satürn.ui.p3` | `#EBD3A0` (themes.json planets.Satürn.ui.p3) | `#EBD3A0` (themes.json cssVars.p3) | var | `#EBD3A0` (cssVars.p3) |
| `tema.Satürn.ui.p4` | `#B98F55` (themes.json planets.Satürn.ui.p4) | `#B98F55` (themes.json cssVars.p4) | var | `#B98F55` (cssVars.p4) |
| `tema.Satürn.ui.rg` | `#E8CF98` (themes.json planets.Satürn.ui.rg) | `#E8CF98` (themes.json cssVars.rg) | var | `#E8CF98` (cssVars.rg) |
| `tema.Satürn.ui.pg` | `rgba(255,214,140,.35)` (themes.json planets.Satürn.ui.pg) | `rgba(255,214,140,.35)` (themes.json cssVars.pg) | var | `rgba(255,214,140,.35)` (cssVars.pg) |
| `tema.Satürn.ui.st` | `#FFE7A3` (themes.json planets.Satürn.ui.st) | `#FFE7A3` (themes.json cssVars.st) | var | `#FFE7A3` (cssVars.st) |
| `tema.Satürn.ui.lg1` | `#FFF4C8` (themes.json planets.Satürn.ui.lg1) | `#FFF4C8` (themes.json cssVars.lg1) | var | `#FFF4C8` (cssVars.lg1) |
| `tema.Satürn.ui.tf1` | `#FFFDF6` (themes.json planets.Satürn.ui.tf1) | `#FFFDF6` (themes.json cssVars.tf1) | var | `#FFFDF6` (cssVars.tf1) |
| `tema.Satürn.ui.tf2` | `#EEE2C6` (themes.json planets.Satürn.ui.tf2) | `#EEE2C6` (themes.json cssVars.tf2) | var | `#EEE2C6` (cssVars.tf2) |
| `tema.Satürn.ui.te` | `#D9C9A2` (themes.json planets.Satürn.ui.te) | `#D9C9A2` (themes.json cssVars.te) | var | `#D9C9A2` (cssVars.te) |
| `tema.Satürn.ui.te2` | `#A8925F` (themes.json planets.Satürn.ui.te2) | `#A8925F` (themes.json cssVars.te2) | var | `#A8925F` (cssVars.te2) |
| `tema.Satürn.ui.t1` | `#C8462E` (themes.json planets.Satürn.ui.t1) | `#C8462E` (themes.json cssVars.t1) | var | `#C8462E` (cssVars.t1) |
| `tema.Satürn.ui.t2` | `#2B5DA8` (themes.json planets.Satürn.ui.t2) | `#2B5DA8` (themes.json cssVars.t2) | var | `#2B5DA8` (cssVars.t2) |
| `tema.Satürn.ui.t3` | `#D9951A` (themes.json planets.Satürn.ui.t3) | `#D9951A` (themes.json cssVars.t3) | var | `#D9951A` (cssVars.t3) |
| `tema.Satürn.ui.atm` | `rgba(255,226,175,.55)` (themes.json planets.Satürn.ui.atm) | `rgba(255,226,175,.55)` (themes.json cssVars.atm) | var | `rgba(255,226,175,.55)` (cssVars.atm) |
| `tema.Satürn.ui.pf` | `1.1` (themes.json planets.Satürn.ui.pf) | `1.1` (themes.json planetFlattening) | var | `1.1` (planetFlattening) |
| `tema.Satürn.ui.ro` | `0.85` (themes.json planets.Satürn.ui.ro (yükleme ekranı halka opaklığı)) | — (themes.json (cssVars.ro = menü halkası, ayrı değer)) | eksik | `0.85` (loadingRingOpacity) |
| `tema.Satürn.rings.rgA` | `rgba(236,214,172,.95)` (themes.json planets.Satürn.rings.rgA) | `rgba(236,214,172,.95)` (themes.json cssVars.rgA) | var | `rgba(236,214,172,.95)` (cssVars.rgA) |
| `tema.Satürn.rings.rgB` | `rgba(190,170,135,.35)` (themes.json planets.Satürn.rings.rgB) | `rgba(190,170,135,.35)` (themes.json cssVars.rgB) | var | `rgba(190,170,135,.35)` (cssVars.rgB) |
| `tema.Satürn.rings.rgC` | `rgba(212,190,150,.8)` (themes.json planets.Satürn.rings.rgC) | `rgba(212,190,150,.8)` (themes.json cssVars.rgC) | var | `rgba(212,190,150,.8)` (cssVars.rgC) |
| `tema.Satürn.rings.rgM` | `rgba(200,176,132,.7)` (themes.json planets.Satürn.rings.rgM) | `rgba(200,176,132,.7)` (themes.json cssVars.rgM) | var | `rgba(200,176,132,.7)` (cssVars.rgM) |
| `tema.Satürn.rings.ro` | `1` (themes.json planets.Satürn.rings.ro) | `1` (themes.json cssVars.ro) | var | `1` (cssVars.ro) |
| `tema.Satürn.atmosphere` | `rgba(255,226,175,.55)` (themes.json planets.Satürn.atmosphere) | `rgba(255,226,175,.55)` (themes.json cssVars.atm) | var | `rgba(255,226,175,.55)` (cssVars.atm) |
| `tema.Satürn.logo.lg1` | `#FFF4C8` (themes.json planets.Satürn.logo.lg1) | `#FFF4C8` (themes.json cssVars.lg1) | var | `#FFF4C8` (cssVars.lg1) |
| `tema.Satürn.logo.tf1` | `#FFFDF6` (themes.json planets.Satürn.logo.tf1) | `#FFFDF6` (themes.json cssVars.tf1) | var | `#FFFDF6` (cssVars.tf1) |
| `tema.Satürn.logo.tf2` | `#EEE2C6` (themes.json planets.Satürn.logo.tf2) | `#EEE2C6` (themes.json cssVars.tf2) | var | `#EEE2C6` (cssVars.tf2) |
| `tema.Satürn.logo.te` | `#D9C9A2` (themes.json planets.Satürn.logo.te) | `#D9C9A2` (themes.json cssVars.te) | var | `#D9C9A2` (cssVars.te) |
| `tema.Satürn.logo.te2` | `#A8925F` (themes.json planets.Satürn.logo.te2) | `#A8925F` (themes.json cssVars.te2) | var | `#A8925F` (cssVars.te2) |
| `tema.Satürn.logo.t1` | `#C8462E` (themes.json planets.Satürn.logo.t1) | `#C8462E` (themes.json cssVars.t1) | var | `#C8462E` (cssVars.t1) |
| `tema.Satürn.logo.t2` | `#2B5DA8` (themes.json planets.Satürn.logo.t2) | `#2B5DA8` (themes.json cssVars.t2) | var | `#2B5DA8` (cssVars.t2) |
| `tema.Satürn.logo.t3` | `#D9951A` (themes.json planets.Satürn.logo.t3) | `#D9951A` (themes.json cssVars.t3) | var | `#D9951A` (cssVars.t3) |
| `tema.Satürn.table3d.bg1` | `#2A2F6E` (themes.json planets.Satürn.table3d.bg1) | `#2A2F6E` (Table/table_content.json planets_table.Satürn.bg1) | var | `#2A2F6E` (table_content planets_table.bg1) |
| `tema.Satürn.table3d.bg2` | `#161A45` (themes.json planets.Satürn.table3d.bg2) | `#161A45` (Table/table_content.json planets_table.Satürn.bg2) | var | `#161A45` (table_content planets_table.bg2) |
| `tema.Satürn.table3d.bg3` | `#0A0C28` (themes.json planets.Satürn.table3d.bg3) | `#0A0C28` (Table/table_content.json planets_table.Satürn.bg3) | var | `#0A0C28` (table_content planets_table.bg3) |
| `tema.Satürn.table3d.pn` | `#232A66` (themes.json planets.Satürn.table3d.pn) | `#232A66` (Table/table_content.json planets_table.Satürn.pn) | var | `#232A66` (table_content planets_table.pn) |
| `tema.Satürn.table3d.pnH` | `#2D3680` (themes.json planets.Satürn.table3d.pnH) | `#2D3680` (Table/table_content.json planets_table.Satürn.pnH) | var | `#2D3680` (table_content planets_table.pnH) |
| `tema.Satürn.table3d.pnS` | `#0B0D2B` (themes.json planets.Satürn.table3d.pnS) | `#0B0D2B` (Table/table_content.json planets_table.Satürn.pnS) | var | `#0B0D2B` (table_content planets_table.pnS) |
| `tema.Satürn.table3d.dp` | `#0B0E2E` (themes.json planets.Satürn.table3d.dp) | `#0B0E2E` (Table/table_content.json planets_table.Satürn.dp) | var | `#0B0E2E` (table_content planets_table.dp) |
| `tema.Satürn.table3d.sd` | `#10133A` (themes.json planets.Satürn.table3d.sd) | `#10133A` (Table/table_content.json planets_table.Satürn.sd) | var | `#10133A` (table_content planets_table.sd) |
| `tema.Satürn.table3d.bd` | `#2E3680` (themes.json planets.Satürn.table3d.bd) | `#2E3680` (Table/table_content.json planets_table.Satürn.bd) | var | `#2E3680` (table_content planets_table.bd) |
| `tema.Satürn.table3d.a1` | `#FFD964` (themes.json planets.Satürn.table3d.a1) | `#FFD964` (Table/table_content.json planets_table.Satürn.a1) | var | `#FFD964` (table_content planets_table.a1) |
| `tema.Satürn.table3d.a2` | `#F5A623` (themes.json planets.Satürn.table3d.a2) | `#F5A623` (Table/table_content.json planets_table.Satürn.a2) | var | `#F5A623` (table_content planets_table.a2) |
| `tema.Satürn.table3d.aS` | `#B86E0F` (themes.json planets.Satürn.table3d.aS) | `#B86E0F` (Table/table_content.json planets_table.Satürn.aS) | var | `#B86E0F` (table_content planets_table.aS) |
| `tema.Satürn.table3d.aT` | `#3A1F00` (themes.json planets.Satürn.table3d.aT) | `#3A1F00` (Table/table_content.json planets_table.Satürn.aT) | var | `#3A1F00` (table_content planets_table.aT) |
| `tema.Satürn.table3d.lS` | `#8A4E06` (themes.json planets.Satürn.table3d.lS) | `#8A4E06` (Table/table_content.json planets_table.Satürn.lS) | var | `#8A4E06` (table_content planets_table.lS) |
| `tema.Satürn.table3d.gl` | `rgba(255,201,74,.45)` (themes.json planets.Satürn.table3d.gl) | `rgba(255,201,74,.45)` (Table/table_content.json planets_table.Satürn.gl) | var | `rgba(255,201,74,.45)` (table_content planets_table.gl) |
| `tema.Satürn.table3d.mt` | `#A9AEDD` (themes.json planets.Satürn.table3d.mt) | `#A9AEDD` (Table/table_content.json planets_table.Satürn.mt) | var | `#A9AEDD` (table_content planets_table.mt) |
| `tema.Satürn.table3d.ic` | `#D3D6FF` (themes.json planets.Satürn.table3d.ic) | `#D3D6FF` (Table/table_content.json planets_table.Satürn.ic) | var | `#D3D6FF` (table_content planets_table.ic) |
| `tema.Satürn.table3d.pg` | `rgba(255,214,140,.30)` (themes.json planets.Satürn.table3d.pg) | `rgba(255,214,140,.30)` (Table/table_content.json planets_table.Satürn.pg) | var | `rgba(255,214,140,.30)` (table_content planets_table.pg) |
| `tema.Satürn.table3d.st` | `#FFE7A3` (themes.json planets.Satürn.table3d.st) | `#FFE7A3` (Table/table_content.json planets_table.Satürn.st) | var | `#FFE7A3` (table_content planets_table.st) |
| `tema.Satürn.table3d.lg1` | `#FFF4C8` (themes.json planets.Satürn.table3d.lg1) | `#FFF4C8` (Table/table_content.json planets_table.Satürn.lg1) | var | `#FFF4C8` (table_content planets_table.lg1) |
| `tema.Satürn.table3d.tf1` | `#FFFDF6` (themes.json planets.Satürn.table3d.tf1) | `#FFFDF6` (Table/table_content.json planets_table.Satürn.tf1) | var | `#FFFDF6` (table_content planets_table.tf1) |
| `tema.Satürn.table3d.tf2` | `#EEE2C6` (themes.json planets.Satürn.table3d.tf2) | `#EEE2C6` (Table/table_content.json planets_table.Satürn.tf2) | var | `#EEE2C6` (table_content planets_table.tf2) |
| `tema.Satürn.table3d.te` | `#D9C9A2` (themes.json planets.Satürn.table3d.te) | `#D9C9A2` (Table/table_content.json planets_table.Satürn.te) | var | `#D9C9A2` (table_content planets_table.te) |
| `tema.Satürn.table3d.f1` | `#2C3790` (themes.json planets.Satürn.table3d.f1) | `#2C3790` (Table/table_content.json planets_table.Satürn.f1) | var | `#2C3790` (table_content planets_table.f1) |
| `tema.Satürn.table3d.f2` | `#11153F` (themes.json planets.Satürn.table3d.f2) | `#11153F` (Table/table_content.json planets_table.Satürn.f2) | var | `#11153F` (table_content planets_table.f2) |
| `tema.Satürn.table3d.rim` | `#4a5080` (themes.json planets.Satürn.table3d.rim) | `#4a5080` (Table/table_content.json planets_table.Satürn.rim) | var | `#4a5080` (table_content planets_table.rim) |
| `tema.Mars.ad.tr` | `Mars` (strings.json menu.STR.pn) | `Mars` (themes.json name_tr) | var | `Mars` (name_tr) |
| `tema.Mars.ad.en` | `Mars` (strings.json menu.STR.pn) | `Mars` (themes.json name_en) | var | `Mars` (name_en) |
| `tema.Mars.menu.desc` | `Kızıl çöl` (themes.json planets.Mars.menu.desc) | `Kızıl çöl` (themes.json desc_tr) | var | `Kızıl çöl` (desc_tr) |
| `tema.Mars.menu.desc.en` | `Red desert` (strings.json menu.STR.pd) | `Red desert` (themes.json desc_en) | var | `Red desert` (desc_en) |
| `tema.Mars.menu.orb` | `#FFB08A, #D2532E, #7A2412` (themes.json planets.Mars.menu.orb) | `#FFB08A, #D2532E, #7A2412` (themes.json pickerOrb) | var | `#FFB08A, #D2532E, #7A2412` (pickerOrb) |
| `tema.Mars.ui.bg1` | `#5A1F17` (themes.json planets.Mars.ui.bg1) | `#5A1F17` (themes.json cssVars.bg1) | var | `#5A1F17` (cssVars.bg1) |
| `tema.Mars.ui.bg2` | `#2C0E0B` (themes.json planets.Mars.ui.bg2) | `#2C0E0B` (themes.json cssVars.bg2) | var | `#2C0E0B` (cssVars.bg2) |
| `tema.Mars.ui.bg3` | `#140605` (themes.json planets.Mars.ui.bg3) | `#140605` (themes.json cssVars.bg3) | var | `#140605` (cssVars.bg3) |
| `tema.Mars.ui.pn` | `#4A1E19` (themes.json planets.Mars.ui.pn) | `#4A1E19` (themes.json cssVars.pn) | var | `#4A1E19` (cssVars.pn) |
| `tema.Mars.ui.pnH` | `#5C2620` (themes.json planets.Mars.ui.pnH) | `#5C2620` (themes.json cssVars.pnH) | var | `#5C2620` (cssVars.pnH) |
| `tema.Mars.ui.pnS` | `#1C0907` (themes.json planets.Mars.ui.pnS) | `#1C0907` (themes.json cssVars.pnS) | var | `#1C0907` (cssVars.pnS) |
| `tema.Mars.ui.dp` | `#1E0A08` (themes.json planets.Mars.ui.dp) | `#1E0A08` (themes.json cssVars.dp) | var | `#1E0A08` (cssVars.dp) |
| `tema.Mars.ui.sd` | `#2A0F0C` (themes.json planets.Mars.ui.sd) | `#2A0F0C` (themes.json cssVars.sd) | var | `#2A0F0C` (cssVars.sd) |
| `tema.Mars.ui.bd` | `#6A2A22` (themes.json planets.Mars.ui.bd) | `#6A2A22` (themes.json cssVars.bd) | var | `#6A2A22` (cssVars.bd) |
| `tema.Mars.ui.a1` | `#FFA26B` (themes.json planets.Mars.ui.a1) | `#FFA26B` (themes.json cssVars.a1) | var | `#FFA26B` (cssVars.a1) |
| `tema.Mars.ui.a2` | `#E8552E` (themes.json planets.Mars.ui.a2) | `#E8552E` (themes.json cssVars.a2) | var | `#E8552E` (cssVars.a2) |
| `tema.Mars.ui.aS` | `#8E2A12` (themes.json planets.Mars.ui.aS) | `#8E2A12` (themes.json cssVars.aS) | var | `#8E2A12` (cssVars.aS) |
| `tema.Mars.ui.aT` | `#2A0800` (themes.json planets.Mars.ui.aT) | `#2A0800` (themes.json cssVars.aT) | var | `#2A0800` (cssVars.aT) |
| `tema.Mars.ui.lS` | `#7A1E0C` (themes.json planets.Mars.ui.lS) | `#7A1E0C` (themes.json cssVars.lS) | var | `#7A1E0C` (cssVars.lS) |
| `tema.Mars.ui.gl` | `rgba(255,120,70,.45)` (themes.json planets.Mars.ui.gl) | `rgba(255,120,70,.45)` (themes.json cssVars.gl) | var | `rgba(255,120,70,.45)` (cssVars.gl) |
| `tema.Mars.ui.mt` | `#E3B2A4` (themes.json planets.Mars.ui.mt) | `#E3B2A4` (themes.json cssVars.mt) | var | `#E3B2A4` (cssVars.mt) |
| `tema.Mars.ui.ic` | `#FFD8CC` (themes.json planets.Mars.ui.ic) | `#FFD8CC` (themes.json cssVars.ic) | var | `#FFD8CC` (cssVars.ic) |
| `tema.Mars.ui.p1` | `#E0754A` (themes.json planets.Mars.ui.p1) | `#E0754A` (themes.json cssVars.p1) | var | `#E0754A` (cssVars.p1) |
| `tema.Mars.ui.p2` | `#B8482A` (themes.json planets.Mars.ui.p2) | `#B8482A` (themes.json cssVars.p2) | var | `#B8482A` (cssVars.p2) |
| `tema.Mars.ui.p3` | `#D9633C` (themes.json planets.Mars.ui.p3) | `#D9633C` (themes.json cssVars.p3) | var | `#D9633C` (cssVars.p3) |
| `tema.Mars.ui.p4` | `#8E3420` (themes.json planets.Mars.ui.p4) | `#8E3420` (themes.json cssVars.p4) | var | `#8E3420` (cssVars.p4) |
| `tema.Mars.ui.rg` | `transparent` (themes.json planets.Mars.ui.rg) | `transparent` (themes.json cssVars.rg) | var | `transparent` (cssVars.rg) |
| `tema.Mars.ui.pg` | `rgba(255,110,60,.35)` (themes.json planets.Mars.ui.pg) | `rgba(255,110,60,.35)` (themes.json cssVars.pg) | var | `rgba(255,110,60,.35)` (cssVars.pg) |
| `tema.Mars.ui.st` | `#FFC9B0` (themes.json planets.Mars.ui.st) | `#FFC9B0` (themes.json cssVars.st) | var | `#FFC9B0` (cssVars.st) |
| `tema.Mars.ui.lg1` | `#FFE2CC` (themes.json planets.Mars.ui.lg1) | `#FFE2CC` (themes.json cssVars.lg1) | var | `#FFE2CC` (cssVars.lg1) |
| `tema.Mars.ui.tf1` | `#FFF7F0` (themes.json planets.Mars.ui.tf1) | `#FFF7F0` (themes.json cssVars.tf1) | var | `#FFF7F0` (cssVars.tf1) |
| `tema.Mars.ui.tf2` | `#F0D8C6` (themes.json planets.Mars.ui.tf2) | `#F0D8C6` (themes.json cssVars.tf2) | var | `#F0D8C6` (cssVars.tf2) |
| `tema.Mars.ui.te` | `#DDB8A0` (themes.json planets.Mars.ui.te) | `#DDB8A0` (themes.json cssVars.te) | var | `#DDB8A0` (cssVars.te) |
| `tema.Mars.ui.te2` | `#A3705A` (themes.json planets.Mars.ui.te2) | `#A3705A` (themes.json cssVars.te2) | var | `#A3705A` (cssVars.te2) |
| `tema.Mars.ui.t1` | `#B3261E` (themes.json planets.Mars.ui.t1) | `#B3261E` (themes.json cssVars.t1) | var | `#B3261E` (cssVars.t1) |
| `tema.Mars.ui.t2` | `#E0612E` (themes.json planets.Mars.ui.t2) | `#E0612E` (themes.json cssVars.t2) | var | `#E0612E` (cssVars.t2) |
| `tema.Mars.ui.t3` | `#6E1A10` (themes.json planets.Mars.ui.t3) | `#6E1A10` (themes.json cssVars.t3) | var | `#6E1A10` (cssVars.t3) |
| `tema.Mars.ui.atm` | `rgba(255,150,110,.45)` (themes.json planets.Mars.ui.atm) | `rgba(255,150,110,.45)` (themes.json cssVars.atm) | var | `rgba(255,150,110,.45)` (cssVars.atm) |
| `tema.Mars.ui.pf` | `1` (themes.json planets.Mars.ui.pf) | `1` (themes.json planetFlattening) | var | `1` (planetFlattening) |
| `tema.Mars.ui.ro` | `0` (themes.json planets.Mars.ui.ro (yükleme ekranı halka opaklığı)) | — (themes.json (cssVars.ro = menü halkası, ayrı değer)) | eksik | `0` (loadingRingOpacity) |
| `tema.Mars.rings.rgA` | `transparent` (themes.json planets.Mars.rings.rgA) | `transparent` (themes.json cssVars.rgA) | var | `transparent` (cssVars.rgA) |
| `tema.Mars.rings.rgB` | `transparent` (themes.json planets.Mars.rings.rgB) | `transparent` (themes.json cssVars.rgB) | var | `transparent` (cssVars.rgB) |
| `tema.Mars.rings.rgC` | `transparent` (themes.json planets.Mars.rings.rgC) | `transparent` (themes.json cssVars.rgC) | var | `transparent` (cssVars.rgC) |
| `tema.Mars.rings.rgM` | `transparent` (themes.json planets.Mars.rings.rgM) | `transparent` (themes.json cssVars.rgM) | var | `transparent` (cssVars.rgM) |
| `tema.Mars.rings.ro` | `0` (themes.json planets.Mars.rings.ro) | `0` (themes.json cssVars.ro) | var | `0` (cssVars.ro) |
| `tema.Mars.atmosphere` | `rgba(255,150,110,.45)` (themes.json planets.Mars.atmosphere) | `rgba(255,150,110,.45)` (themes.json cssVars.atm) | var | `rgba(255,150,110,.45)` (cssVars.atm) |
| `tema.Mars.logo.lg1` | `#FFE2CC` (themes.json planets.Mars.logo.lg1) | `#FFE2CC` (themes.json cssVars.lg1) | var | `#FFE2CC` (cssVars.lg1) |
| `tema.Mars.logo.tf1` | `#FFF7F0` (themes.json planets.Mars.logo.tf1) | `#FFF7F0` (themes.json cssVars.tf1) | var | `#FFF7F0` (cssVars.tf1) |
| `tema.Mars.logo.tf2` | `#F0D8C6` (themes.json planets.Mars.logo.tf2) | `#F0D8C6` (themes.json cssVars.tf2) | var | `#F0D8C6` (cssVars.tf2) |
| `tema.Mars.logo.te` | `#DDB8A0` (themes.json planets.Mars.logo.te) | `#DDB8A0` (themes.json cssVars.te) | var | `#DDB8A0` (cssVars.te) |
| `tema.Mars.logo.te2` | `#A3705A` (themes.json planets.Mars.logo.te2) | `#A3705A` (themes.json cssVars.te2) | var | `#A3705A` (cssVars.te2) |
| `tema.Mars.logo.t1` | `#B3261E` (themes.json planets.Mars.logo.t1) | `#B3261E` (themes.json cssVars.t1) | var | `#B3261E` (cssVars.t1) |
| `tema.Mars.logo.t2` | `#E0612E` (themes.json planets.Mars.logo.t2) | `#E0612E` (themes.json cssVars.t2) | var | `#E0612E` (cssVars.t2) |
| `tema.Mars.logo.t3` | `#6E1A10` (themes.json planets.Mars.logo.t3) | `#6E1A10` (themes.json cssVars.t3) | var | `#6E1A10` (cssVars.t3) |
| `tema.Mars.table3d.bg1` | `#5A1F17` (themes.json planets.Mars.table3d.bg1) | `#5A1F17` (Table/table_content.json planets_table.Mars.bg1) | var | `#5A1F17` (table_content planets_table.bg1) |
| `tema.Mars.table3d.bg2` | `#2C0E0B` (themes.json planets.Mars.table3d.bg2) | `#2C0E0B` (Table/table_content.json planets_table.Mars.bg2) | var | `#2C0E0B` (table_content planets_table.bg2) |
| `tema.Mars.table3d.bg3` | `#140605` (themes.json planets.Mars.table3d.bg3) | `#140605` (Table/table_content.json planets_table.Mars.bg3) | var | `#140605` (table_content planets_table.bg3) |
| `tema.Mars.table3d.pn` | `#4A1E19` (themes.json planets.Mars.table3d.pn) | `#4A1E19` (Table/table_content.json planets_table.Mars.pn) | var | `#4A1E19` (table_content planets_table.pn) |
| `tema.Mars.table3d.pnH` | `#5C2620` (themes.json planets.Mars.table3d.pnH) | `#5C2620` (Table/table_content.json planets_table.Mars.pnH) | var | `#5C2620` (table_content planets_table.pnH) |
| `tema.Mars.table3d.pnS` | `#1C0907` (themes.json planets.Mars.table3d.pnS) | `#1C0907` (Table/table_content.json planets_table.Mars.pnS) | var | `#1C0907` (table_content planets_table.pnS) |
| `tema.Mars.table3d.dp` | `#1E0A08` (themes.json planets.Mars.table3d.dp) | `#1E0A08` (Table/table_content.json planets_table.Mars.dp) | var | `#1E0A08` (table_content planets_table.dp) |
| `tema.Mars.table3d.sd` | `#2A0F0C` (themes.json planets.Mars.table3d.sd) | `#2A0F0C` (Table/table_content.json planets_table.Mars.sd) | var | `#2A0F0C` (table_content planets_table.sd) |
| `tema.Mars.table3d.bd` | `#6A2A22` (themes.json planets.Mars.table3d.bd) | `#6A2A22` (Table/table_content.json planets_table.Mars.bd) | var | `#6A2A22` (table_content planets_table.bd) |
| `tema.Mars.table3d.a1` | `#FFA26B` (themes.json planets.Mars.table3d.a1) | `#FFA26B` (Table/table_content.json planets_table.Mars.a1) | var | `#FFA26B` (table_content planets_table.a1) |
| `tema.Mars.table3d.a2` | `#E8552E` (themes.json planets.Mars.table3d.a2) | `#E8552E` (Table/table_content.json planets_table.Mars.a2) | var | `#E8552E` (table_content planets_table.a2) |
| `tema.Mars.table3d.aS` | `#8E2A12` (themes.json planets.Mars.table3d.aS) | `#8E2A12` (Table/table_content.json planets_table.Mars.aS) | var | `#8E2A12` (table_content planets_table.aS) |
| `tema.Mars.table3d.aT` | `#2A0800` (themes.json planets.Mars.table3d.aT) | `#2A0800` (Table/table_content.json planets_table.Mars.aT) | var | `#2A0800` (table_content planets_table.aT) |
| `tema.Mars.table3d.lS` | `#7A1E0C` (themes.json planets.Mars.table3d.lS) | `#7A1E0C` (Table/table_content.json planets_table.Mars.lS) | var | `#7A1E0C` (table_content planets_table.lS) |
| `tema.Mars.table3d.gl` | `rgba(255,120,70,.45)` (themes.json planets.Mars.table3d.gl) | `rgba(255,120,70,.45)` (Table/table_content.json planets_table.Mars.gl) | var | `rgba(255,120,70,.45)` (table_content planets_table.gl) |
| `tema.Mars.table3d.mt` | `#E3B2A4` (themes.json planets.Mars.table3d.mt) | `#E3B2A4` (Table/table_content.json planets_table.Mars.mt) | var | `#E3B2A4` (table_content planets_table.mt) |
| `tema.Mars.table3d.ic` | `#FFD8CC` (themes.json planets.Mars.table3d.ic) | `#FFD8CC` (Table/table_content.json planets_table.Mars.ic) | var | `#FFD8CC` (table_content planets_table.ic) |
| `tema.Mars.table3d.pg` | `rgba(255,110,60,.30)` (themes.json planets.Mars.table3d.pg) | `rgba(255,110,60,.30)` (Table/table_content.json planets_table.Mars.pg) | var | `rgba(255,110,60,.30)` (table_content planets_table.pg) |
| `tema.Mars.table3d.st` | `#FFC9B0` (themes.json planets.Mars.table3d.st) | `#FFC9B0` (Table/table_content.json planets_table.Mars.st) | var | `#FFC9B0` (table_content planets_table.st) |
| `tema.Mars.table3d.lg1` | `#FFE2CC` (themes.json planets.Mars.table3d.lg1) | `#FFE2CC` (Table/table_content.json planets_table.Mars.lg1) | var | `#FFE2CC` (table_content planets_table.lg1) |
| `tema.Mars.table3d.tf1` | `#FFF7F0` (themes.json planets.Mars.table3d.tf1) | `#FFF7F0` (Table/table_content.json planets_table.Mars.tf1) | var | `#FFF7F0` (table_content planets_table.tf1) |
| `tema.Mars.table3d.tf2` | `#F0D8C6` (themes.json planets.Mars.table3d.tf2) | `#F0D8C6` (Table/table_content.json planets_table.Mars.tf2) | var | `#F0D8C6` (table_content planets_table.tf2) |
| `tema.Mars.table3d.te` | `#DDB8A0` (themes.json planets.Mars.table3d.te) | `#DDB8A0` (Table/table_content.json planets_table.Mars.te) | var | `#DDB8A0` (table_content planets_table.te) |
| `tema.Mars.table3d.f1` | `#74291E` (themes.json planets.Mars.table3d.f1) | `#74291E` (Table/table_content.json planets_table.Mars.f1) | var | `#74291E` (table_content planets_table.f1) |
| `tema.Mars.table3d.f2` | `#2A0D09` (themes.json planets.Mars.table3d.f2) | `#2A0D09` (Table/table_content.json planets_table.Mars.f2) | var | `#2A0D09` (table_content planets_table.f2) |
| `tema.Mars.table3d.rim` | `#6e3e36` (themes.json planets.Mars.table3d.rim) | `#6e3e36` (Table/table_content.json planets_table.Mars.rim) | var | `#6e3e36` (table_content planets_table.rim) |
| `tema.Neptün.ad.tr` | `Neptün` (strings.json menu.STR.pn) | `Neptün` (themes.json name_tr) | var | `Neptün` (name_tr) |
| `tema.Neptün.ad.en` | `Neptune` (strings.json menu.STR.pn) | `Neptune` (themes.json name_en) | var | `Neptune` (name_en) |
| `tema.Neptün.menu.desc` | `Buz mavisi` (themes.json planets.Neptün.menu.desc) | `Buz mavisi` (themes.json desc_tr) | var | `Buz mavisi` (desc_tr) |
| `tema.Neptün.menu.desc.en` | `Ice blue` (strings.json menu.STR.pd) | `Ice blue` (themes.json desc_en) | var | `Ice blue` (desc_en) |
| `tema.Neptün.menu.orb` | `#A8D4FF, #3A7BEA, #14357F` (themes.json planets.Neptün.menu.orb) | `#A8D4FF, #3A7BEA, #14357F` (themes.json pickerOrb) | var | `#A8D4FF, #3A7BEA, #14357F` (pickerOrb) |
| `tema.Neptün.ui.bg1` | `#0F3C80` (themes.json planets.Neptün.ui.bg1) | `#0F3C80` (themes.json cssVars.bg1) | var | `#0F3C80` (cssVars.bg1) |
| `tema.Neptün.ui.bg2` | `#081F52` (themes.json planets.Neptün.ui.bg2) | `#081F52` (themes.json cssVars.bg2) | var | `#081F52` (cssVars.bg2) |
| `tema.Neptün.ui.bg3` | `#03102E` (themes.json planets.Neptün.ui.bg3) | `#03102E` (themes.json cssVars.bg3) | var | `#03102E` (cssVars.bg3) |
| `tema.Neptün.ui.pn` | `#12407F` (themes.json planets.Neptün.ui.pn) | `#12407F` (themes.json cssVars.pn) | var | `#12407F` (cssVars.pn) |
| `tema.Neptün.ui.pnH` | `#185099` (themes.json planets.Neptün.ui.pnH) | `#185099` (themes.json cssVars.pnH) | var | `#185099` (cssVars.pnH) |
| `tema.Neptün.ui.pnS` | `#04163A` (themes.json planets.Neptün.ui.pnS) | `#04163A` (themes.json cssVars.pnS) | var | `#04163A` (cssVars.pnS) |
| `tema.Neptün.ui.dp` | `#061A40` (themes.json planets.Neptün.ui.dp) | `#061A40` (themes.json cssVars.dp) | var | `#061A40` (cssVars.dp) |
| `tema.Neptün.ui.sd` | `#071C46` (themes.json planets.Neptün.ui.sd) | `#071C46` (themes.json cssVars.sd) | var | `#071C46` (cssVars.sd) |
| `tema.Neptün.ui.bd` | `#1F5AA8` (themes.json planets.Neptün.ui.bd) | `#1F5AA8` (themes.json cssVars.bd) | var | `#1F5AA8` (cssVars.bd) |
| `tema.Neptün.ui.a1` | `#8CEBFF` (themes.json planets.Neptün.ui.a1) | `#8CEBFF` (themes.json cssVars.a1) | var | `#8CEBFF` (cssVars.a1) |
| `tema.Neptün.ui.a2` | `#2FB4E8` (themes.json planets.Neptün.ui.a2) | `#2FB4E8` (themes.json cssVars.a2) | var | `#2FB4E8` (cssVars.a2) |
| `tema.Neptün.ui.aS` | `#11699A` (themes.json planets.Neptün.ui.aS) | `#11699A` (themes.json cssVars.aS) | var | `#11699A` (cssVars.aS) |
| `tema.Neptün.ui.aT` | `#021C2E` (themes.json planets.Neptün.ui.aT) | `#021C2E` (themes.json cssVars.aT) | var | `#021C2E` (cssVars.aT) |
| `tema.Neptün.ui.lS` | `#0A4F75` (themes.json planets.Neptün.ui.lS) | `#0A4F75` (themes.json cssVars.lS) | var | `#0A4F75` (cssVars.lS) |
| `tema.Neptün.ui.gl` | `rgba(100,220,255,.45)` (themes.json planets.Neptün.ui.gl) | `rgba(100,220,255,.45)` (themes.json cssVars.gl) | var | `rgba(100,220,255,.45)` (cssVars.gl) |
| `tema.Neptün.ui.mt` | `#A6CDF0` (themes.json planets.Neptün.ui.mt) | `#A6CDF0` (themes.json cssVars.mt) | var | `#A6CDF0` (cssVars.mt) |
| `tema.Neptün.ui.ic` | `#D2EEFF` (themes.json planets.Neptün.ui.ic) | `#D2EEFF` (themes.json cssVars.ic) | var | `#D2EEFF` (cssVars.ic) |
| `tema.Neptün.ui.p1` | `#5FA8FF` (themes.json planets.Neptün.ui.p1) | `#5FA8FF` (themes.json cssVars.p1) | var | `#5FA8FF` (cssVars.p1) |
| `tema.Neptün.ui.p2` | `#2F6FE0` (themes.json planets.Neptün.ui.p2) | `#2F6FE0` (themes.json cssVars.p2) | var | `#2F6FE0` (cssVars.p2) |
| `tema.Neptün.ui.p3` | `#4A8EF5` (themes.json planets.Neptün.ui.p3) | `#4A8EF5` (themes.json cssVars.p3) | var | `#4A8EF5` (cssVars.p3) |
| `tema.Neptün.ui.p4` | `#1F4FB8` (themes.json planets.Neptün.ui.p4) | `#1F4FB8` (themes.json cssVars.p4) | var | `#1F4FB8` (cssVars.p4) |
| `tema.Neptün.ui.rg` | `#9CC8FF` (themes.json planets.Neptün.ui.rg) | `#9CC8FF` (themes.json cssVars.rg) | var | `#9CC8FF` (cssVars.rg) |
| `tema.Neptün.ui.pg` | `rgba(80,170,255,.4)` (themes.json planets.Neptün.ui.pg) | `rgba(80,170,255,.4)` (themes.json cssVars.pg) | var | `rgba(80,170,255,.4)` (cssVars.pg) |
| `tema.Neptün.ui.st` | `#CDEBFF` (themes.json planets.Neptün.ui.st) | `#CDEBFF` (themes.json cssVars.st) | var | `#CDEBFF` (cssVars.st) |
| `tema.Neptün.ui.lg1` | `#E6FBFF` (themes.json planets.Neptün.ui.lg1) | `#E6FBFF` (themes.json cssVars.lg1) | var | `#E6FBFF` (cssVars.lg1) |
| `tema.Neptün.ui.tf1` | `#F8FCFF` (themes.json planets.Neptün.ui.tf1) | `#F8FCFF` (themes.json cssVars.tf1) | var | `#F8FCFF` (cssVars.tf1) |
| `tema.Neptün.ui.tf2` | `#DCE8F4` (themes.json planets.Neptün.ui.tf2) | `#DCE8F4` (themes.json cssVars.tf2) | var | `#DCE8F4` (cssVars.tf2) |
| `tema.Neptün.ui.te` | `#B8CADC` (themes.json planets.Neptün.ui.te) | `#B8CADC` (themes.json cssVars.te) | var | `#B8CADC` (cssVars.te) |
| `tema.Neptün.ui.te2` | `#7C93AE` (themes.json planets.Neptün.ui.te2) | `#7C93AE` (themes.json cssVars.te2) | var | `#7C93AE` (cssVars.te2) |
| `tema.Neptün.ui.t1` | `#1F5FBF` (themes.json planets.Neptün.ui.t1) | `#1F5FBF` (themes.json cssVars.t1) | var | `#1F5FBF` (cssVars.t1) |
| `tema.Neptün.ui.t2` | `#0E9CC4` (themes.json planets.Neptün.ui.t2) | `#0E9CC4` (themes.json cssVars.t2) | var | `#0E9CC4` (cssVars.t2) |
| `tema.Neptün.ui.t3` | `#2D3B9A` (themes.json planets.Neptün.ui.t3) | `#2D3B9A` (themes.json cssVars.t3) | var | `#2D3B9A` (cssVars.t3) |
| `tema.Neptün.ui.atm` | `rgba(150,210,255,.8)` (themes.json planets.Neptün.ui.atm) | `rgba(150,210,255,.8)` (themes.json cssVars.atm) | var | `rgba(150,210,255,.8)` (cssVars.atm) |
| `tema.Neptün.ui.pf` | `1.02` (themes.json planets.Neptün.ui.pf) | `1.02` (themes.json planetFlattening) | var | `1.02` (planetFlattening) |
| `tema.Neptün.ui.ro` | `0.35` (themes.json planets.Neptün.ui.ro (yükleme ekranı halka opaklığı)) | — (themes.json (cssVars.ro = menü halkası, ayrı değer)) | eksik | `0.35` (loadingRingOpacity) |
| `tema.Neptün.rings.rgA` | `transparent` (themes.json planets.Neptün.rings.rgA) | `transparent` (themes.json cssVars.rgA) | var | `transparent` (cssVars.rgA) |
| `tema.Neptün.rings.rgB` | `transparent` (themes.json planets.Neptün.rings.rgB) | `transparent` (themes.json cssVars.rgB) | var | `transparent` (cssVars.rgB) |
| `tema.Neptün.rings.rgC` | `transparent` (themes.json planets.Neptün.rings.rgC) | `transparent` (themes.json cssVars.rgC) | var | `transparent` (cssVars.rgC) |
| `tema.Neptün.rings.rgM` | `transparent` (themes.json planets.Neptün.rings.rgM) | `transparent` (themes.json cssVars.rgM) | var | `transparent` (cssVars.rgM) |
| `tema.Neptün.rings.ro` | `0` (themes.json planets.Neptün.rings.ro) | `0` (themes.json cssVars.ro) | var | `0` (cssVars.ro) |
| `tema.Neptün.atmosphere` | `rgba(150,210,255,.8)` (themes.json planets.Neptün.atmosphere) | `rgba(150,210,255,.8)` (themes.json cssVars.atm) | var | `rgba(150,210,255,.8)` (cssVars.atm) |
| `tema.Neptün.logo.lg1` | `#E6FBFF` (themes.json planets.Neptün.logo.lg1) | `#E6FBFF` (themes.json cssVars.lg1) | var | `#E6FBFF` (cssVars.lg1) |
| `tema.Neptün.logo.tf1` | `#F8FCFF` (themes.json planets.Neptün.logo.tf1) | `#F8FCFF` (themes.json cssVars.tf1) | var | `#F8FCFF` (cssVars.tf1) |
| `tema.Neptün.logo.tf2` | `#DCE8F4` (themes.json planets.Neptün.logo.tf2) | `#DCE8F4` (themes.json cssVars.tf2) | var | `#DCE8F4` (cssVars.tf2) |
| `tema.Neptün.logo.te` | `#B8CADC` (themes.json planets.Neptün.logo.te) | `#B8CADC` (themes.json cssVars.te) | var | `#B8CADC` (cssVars.te) |
| `tema.Neptün.logo.te2` | `#7C93AE` (themes.json planets.Neptün.logo.te2) | `#7C93AE` (themes.json cssVars.te2) | var | `#7C93AE` (cssVars.te2) |
| `tema.Neptün.logo.t1` | `#1F5FBF` (themes.json planets.Neptün.logo.t1) | `#1F5FBF` (themes.json cssVars.t1) | var | `#1F5FBF` (cssVars.t1) |
| `tema.Neptün.logo.t2` | `#0E9CC4` (themes.json planets.Neptün.logo.t2) | `#0E9CC4` (themes.json cssVars.t2) | var | `#0E9CC4` (cssVars.t2) |
| `tema.Neptün.logo.t3` | `#2D3B9A` (themes.json planets.Neptün.logo.t3) | `#2D3B9A` (themes.json cssVars.t3) | var | `#2D3B9A` (cssVars.t3) |
| `tema.Neptün.table3d.bg1` | `#0F3C80` (themes.json planets.Neptün.table3d.bg1) | `#0F3C80` (Table/table_content.json planets_table.Neptün.bg1) | var | `#0F3C80` (table_content planets_table.bg1) |
| `tema.Neptün.table3d.bg2` | `#081F52` (themes.json planets.Neptün.table3d.bg2) | `#081F52` (Table/table_content.json planets_table.Neptün.bg2) | var | `#081F52` (table_content planets_table.bg2) |
| `tema.Neptün.table3d.bg3` | `#03102E` (themes.json planets.Neptün.table3d.bg3) | `#03102E` (Table/table_content.json planets_table.Neptün.bg3) | var | `#03102E` (table_content planets_table.bg3) |
| `tema.Neptün.table3d.pn` | `#12407F` (themes.json planets.Neptün.table3d.pn) | `#12407F` (Table/table_content.json planets_table.Neptün.pn) | var | `#12407F` (table_content planets_table.pn) |
| `tema.Neptün.table3d.pnH` | `#185099` (themes.json planets.Neptün.table3d.pnH) | `#185099` (Table/table_content.json planets_table.Neptün.pnH) | var | `#185099` (table_content planets_table.pnH) |
| `tema.Neptün.table3d.pnS` | `#04163A` (themes.json planets.Neptün.table3d.pnS) | `#04163A` (Table/table_content.json planets_table.Neptün.pnS) | var | `#04163A` (table_content planets_table.pnS) |
| `tema.Neptün.table3d.dp` | `#061A40` (themes.json planets.Neptün.table3d.dp) | `#061A40` (Table/table_content.json planets_table.Neptün.dp) | var | `#061A40` (table_content planets_table.dp) |
| `tema.Neptün.table3d.sd` | `#071C46` (themes.json planets.Neptün.table3d.sd) | `#071C46` (Table/table_content.json planets_table.Neptün.sd) | var | `#071C46` (table_content planets_table.sd) |
| `tema.Neptün.table3d.bd` | `#1F5AA8` (themes.json planets.Neptün.table3d.bd) | `#1F5AA8` (Table/table_content.json planets_table.Neptün.bd) | var | `#1F5AA8` (table_content planets_table.bd) |
| `tema.Neptün.table3d.a1` | `#8CEBFF` (themes.json planets.Neptün.table3d.a1) | `#8CEBFF` (Table/table_content.json planets_table.Neptün.a1) | var | `#8CEBFF` (table_content planets_table.a1) |
| `tema.Neptün.table3d.a2` | `#2FB4E8` (themes.json planets.Neptün.table3d.a2) | `#2FB4E8` (Table/table_content.json planets_table.Neptün.a2) | var | `#2FB4E8` (table_content planets_table.a2) |
| `tema.Neptün.table3d.aS` | `#11699A` (themes.json planets.Neptün.table3d.aS) | `#11699A` (Table/table_content.json planets_table.Neptün.aS) | var | `#11699A` (table_content planets_table.aS) |
| `tema.Neptün.table3d.aT` | `#021C2E` (themes.json planets.Neptün.table3d.aT) | `#021C2E` (Table/table_content.json planets_table.Neptün.aT) | var | `#021C2E` (table_content planets_table.aT) |
| `tema.Neptün.table3d.lS` | `#0A4F75` (themes.json planets.Neptün.table3d.lS) | `#0A4F75` (Table/table_content.json planets_table.Neptün.lS) | var | `#0A4F75` (table_content planets_table.lS) |
| `tema.Neptün.table3d.gl` | `rgba(100,220,255,.45)` (themes.json planets.Neptün.table3d.gl) | `rgba(100,220,255,.45)` (Table/table_content.json planets_table.Neptün.gl) | var | `rgba(100,220,255,.45)` (table_content planets_table.gl) |
| `tema.Neptün.table3d.mt` | `#A6CDF0` (themes.json planets.Neptün.table3d.mt) | `#A6CDF0` (Table/table_content.json planets_table.Neptün.mt) | var | `#A6CDF0` (table_content planets_table.mt) |
| `tema.Neptün.table3d.ic` | `#D2EEFF` (themes.json planets.Neptün.table3d.ic) | `#D2EEFF` (Table/table_content.json planets_table.Neptün.ic) | var | `#D2EEFF` (table_content planets_table.ic) |
| `tema.Neptün.table3d.pg` | `rgba(80,170,255,.34)` (themes.json planets.Neptün.table3d.pg) | `rgba(80,170,255,.34)` (Table/table_content.json planets_table.Neptün.pg) | var | `rgba(80,170,255,.34)` (table_content planets_table.pg) |
| `tema.Neptün.table3d.st` | `#CDEBFF` (themes.json planets.Neptün.table3d.st) | `#CDEBFF` (Table/table_content.json planets_table.Neptün.st) | var | `#CDEBFF` (table_content planets_table.st) |
| `tema.Neptün.table3d.lg1` | `#E6FBFF` (themes.json planets.Neptün.table3d.lg1) | `#E6FBFF` (Table/table_content.json planets_table.Neptün.lg1) | var | `#E6FBFF` (table_content planets_table.lg1) |
| `tema.Neptün.table3d.tf1` | `#F8FCFF` (themes.json planets.Neptün.table3d.tf1) | `#F8FCFF` (Table/table_content.json planets_table.Neptün.tf1) | var | `#F8FCFF` (table_content planets_table.tf1) |
| `tema.Neptün.table3d.tf2` | `#DCE8F4` (themes.json planets.Neptün.table3d.tf2) | `#DCE8F4` (Table/table_content.json planets_table.Neptün.tf2) | var | `#DCE8F4` (table_content planets_table.tf2) |
| `tema.Neptün.table3d.te` | `#B8CADC` (themes.json planets.Neptün.table3d.te) | `#B8CADC` (Table/table_content.json planets_table.Neptün.te) | var | `#B8CADC` (table_content planets_table.te) |
| `tema.Neptün.table3d.f1` | `#164FA4` (themes.json planets.Neptün.table3d.f1) | `#164FA4` (Table/table_content.json planets_table.Neptün.f1) | var | `#164FA4` (table_content planets_table.f1) |
| `tema.Neptün.table3d.f2` | `#061A40` (themes.json planets.Neptün.table3d.f2) | `#061A40` (Table/table_content.json planets_table.Neptün.f2) | var | `#061A40` (table_content planets_table.f2) |
| `tema.Neptün.table3d.rim` | `#3e5e8e` (themes.json planets.Neptün.table3d.rim) | `#3e5e8e` (Table/table_content.json planets_table.Neptün.rim) | var | `#3e5e8e` (table_content planets_table.rim) |
| `tema.Jüpiter.ad.tr` | `Jüpiter` (strings.json menu.STR.pn) | `Jüpiter` (themes.json name_tr) | var | `Jüpiter` (name_tr) |
| `tema.Jüpiter.ad.en` | `Jupiter` (strings.json menu.STR.pn) | `Jupiter` (themes.json name_en) | var | `Jupiter` (name_en) |
| `tema.Jüpiter.menu.desc` | `Fırtına kuşakları` (themes.json planets.Jüpiter.menu.desc) | `Fırtına kuşakları` (themes.json desc_tr) | var | `Fırtına kuşakları` (desc_tr) |
| `tema.Jüpiter.menu.desc.en` | `Storm belts` (strings.json menu.STR.pd) | `Storm belts` (themes.json desc_en) | var | `Storm belts` (desc_en) |
| `tema.Jüpiter.menu.orb` | `#F7E2C6, #D0905E, #7A4520` (themes.json planets.Jüpiter.menu.orb) | `#F7E2C6, #D0905E, #7A4520` (themes.json pickerOrb) | var | `#F7E2C6, #D0905E, #7A4520` (pickerOrb) |
| `tema.Jüpiter.ui.bg1` | `#4E3522` (themes.json planets.Jüpiter.ui.bg1) | `#4E3522` (themes.json cssVars.bg1) | var | `#4E3522` (cssVars.bg1) |
| `tema.Jüpiter.ui.bg2` | `#26180E` (themes.json planets.Jüpiter.ui.bg2) | `#26180E` (themes.json cssVars.bg2) | var | `#26180E` (cssVars.bg2) |
| `tema.Jüpiter.ui.bg3` | `#120A05` (themes.json planets.Jüpiter.ui.bg3) | `#120A05` (themes.json cssVars.bg3) | var | `#120A05` (cssVars.bg3) |
| `tema.Jüpiter.ui.pn` | `#47321F` (themes.json planets.Jüpiter.ui.pn) | `#47321F` (themes.json cssVars.pn) | var | `#47321F` (cssVars.pn) |
| `tema.Jüpiter.ui.pnH` | `#5A3F28` (themes.json planets.Jüpiter.ui.pnH) | `#5A3F28` (themes.json cssVars.pnH) | var | `#5A3F28` (cssVars.pnH) |
| `tema.Jüpiter.ui.pnS` | `#1A1008` (themes.json planets.Jüpiter.ui.pnS) | `#1A1008` (themes.json cssVars.pnS) | var | `#1A1008` (cssVars.pnS) |
| `tema.Jüpiter.ui.dp` | `#1E140A` (themes.json planets.Jüpiter.ui.dp) | `#1E140A` (themes.json cssVars.dp) | var | `#1E140A` (cssVars.dp) |
| `tema.Jüpiter.ui.sd` | `#2A1B0F` (themes.json planets.Jüpiter.ui.sd) | `#2A1B0F` (themes.json cssVars.sd) | var | `#2A1B0F` (cssVars.sd) |
| `tema.Jüpiter.ui.bd` | `#6E4E30` (themes.json planets.Jüpiter.ui.bd) | `#6E4E30` (themes.json cssVars.bd) | var | `#6E4E30` (cssVars.bd) |
| `tema.Jüpiter.ui.a1` | `#FFC88A` (themes.json planets.Jüpiter.ui.a1) | `#FFC88A` (themes.json cssVars.a1) | var | `#FFC88A` (cssVars.a1) |
| `tema.Jüpiter.ui.a2` | `#E58B45` (themes.json planets.Jüpiter.ui.a2) | `#E58B45` (themes.json cssVars.a2) | var | `#E58B45` (cssVars.a2) |
| `tema.Jüpiter.ui.aS` | `#8F4C18` (themes.json planets.Jüpiter.ui.aS) | `#8F4C18` (themes.json cssVars.aS) | var | `#8F4C18` (cssVars.aS) |
| `tema.Jüpiter.ui.aT` | `#2E1405` (themes.json planets.Jüpiter.ui.aT) | `#2E1405` (themes.json cssVars.aT) | var | `#2E1405` (cssVars.aT) |
| `tema.Jüpiter.ui.lS` | `#7A3C10` (themes.json planets.Jüpiter.ui.lS) | `#7A3C10` (themes.json cssVars.lS) | var | `#7A3C10` (cssVars.lS) |
| `tema.Jüpiter.ui.gl` | `rgba(255,170,100,.45)` (themes.json planets.Jüpiter.ui.gl) | `rgba(255,170,100,.45)` (themes.json cssVars.gl) | var | `rgba(255,170,100,.45)` (cssVars.gl) |
| `tema.Jüpiter.ui.mt` | `#DCC2A6` (themes.json planets.Jüpiter.ui.mt) | `#DCC2A6` (themes.json cssVars.mt) | var | `#DCC2A6` (cssVars.mt) |
| `tema.Jüpiter.ui.ic` | `#F7E3CC` (themes.json planets.Jüpiter.ui.ic) | `#F7E3CC` (themes.json cssVars.ic) | var | `#F7E3CC` (cssVars.ic) |
| `tema.Jüpiter.ui.p1` | `#F2DCC0` (themes.json planets.Jüpiter.ui.p1) | `#F2DCC0` (themes.json cssVars.p1) | var | `#F2DCC0` (cssVars.p1) |
| `tema.Jüpiter.ui.p2` | `#C98A5A` (themes.json planets.Jüpiter.ui.p2) | `#C98A5A` (themes.json cssVars.p2) | var | `#C98A5A` (cssVars.p2) |
| `tema.Jüpiter.ui.p3` | `#E8C49C` (themes.json planets.Jüpiter.ui.p3) | `#E8C49C` (themes.json cssVars.p3) | var | `#E8C49C` (cssVars.p3) |
| `tema.Jüpiter.ui.p4` | `#A8643A` (themes.json planets.Jüpiter.ui.p4) | `#A8643A` (themes.json cssVars.p4) | var | `#A8643A` (cssVars.p4) |
| `tema.Jüpiter.ui.rg` | `#D9B48A` (themes.json planets.Jüpiter.ui.rg) | `#D9B48A` (themes.json cssVars.rg) | var | `#D9B48A` (cssVars.rg) |
| `tema.Jüpiter.ui.pg` | `rgba(255,190,130,.3)` (themes.json planets.Jüpiter.ui.pg) | `rgba(255,190,130,.3)` (themes.json cssVars.pg) | var | `rgba(255,190,130,.3)` (cssVars.pg) |
| `tema.Jüpiter.ui.st` | `#FFE2C2` (themes.json planets.Jüpiter.ui.st) | `#FFE2C2` (themes.json cssVars.st) | var | `#FFE2C2` (cssVars.st) |
| `tema.Jüpiter.ui.lg1` | `#FFF0DC` (themes.json planets.Jüpiter.ui.lg1) | `#FFF0DC` (themes.json cssVars.lg1) | var | `#FFF0DC` (cssVars.lg1) |
| `tema.Jüpiter.ui.tf1` | `#FFFAF2` (themes.json planets.Jüpiter.ui.tf1) | `#FFFAF2` (themes.json cssVars.tf1) | var | `#FFFAF2` (cssVars.tf1) |
| `tema.Jüpiter.ui.tf2` | `#F0E0CA` (themes.json planets.Jüpiter.ui.tf2) | `#F0E0CA` (themes.json cssVars.tf2) | var | `#F0E0CA` (cssVars.tf2) |
| `tema.Jüpiter.ui.te` | `#DCC4A4` (themes.json planets.Jüpiter.ui.te) | `#DCC4A4` (themes.json cssVars.te) | var | `#DCC4A4` (cssVars.te) |
| `tema.Jüpiter.ui.te2` | `#A08060` (themes.json planets.Jüpiter.ui.te2) | `#A08060` (themes.json cssVars.te2) | var | `#A08060` (cssVars.te2) |
| `tema.Jüpiter.ui.t1` | `#B4532A` (themes.json planets.Jüpiter.ui.t1) | `#B4532A` (themes.json cssVars.t1) | var | `#B4532A` (cssVars.t1) |
| `tema.Jüpiter.ui.t2` | `#6E4A2E` (themes.json planets.Jüpiter.ui.t2) | `#6E4A2E` (themes.json cssVars.t2) | var | `#6E4A2E` (cssVars.t2) |
| `tema.Jüpiter.ui.t3` | `#D08A3A` (themes.json planets.Jüpiter.ui.t3) | `#D08A3A` (themes.json cssVars.t3) | var | `#D08A3A` (cssVars.t3) |
| `tema.Jüpiter.ui.atm` | `rgba(255,220,180,.5)` (themes.json planets.Jüpiter.ui.atm) | `rgba(255,220,180,.5)` (themes.json cssVars.atm) | var | `rgba(255,220,180,.5)` (cssVars.atm) |
| `tema.Jüpiter.ui.pf` | `1.07` (themes.json planets.Jüpiter.ui.pf) | `1.07` (themes.json planetFlattening) | var | `1.07` (planetFlattening) |
| `tema.Jüpiter.ui.ro` | `0.25` (themes.json planets.Jüpiter.ui.ro (yükleme ekranı halka opaklığı)) | — (themes.json (cssVars.ro = menü halkası, ayrı değer)) | eksik | `0.25` (loadingRingOpacity) |
| `tema.Jüpiter.rings.rgA` | `transparent` (themes.json planets.Jüpiter.rings.rgA) | `transparent` (themes.json cssVars.rgA) | var | `transparent` (cssVars.rgA) |
| `tema.Jüpiter.rings.rgB` | `transparent` (themes.json planets.Jüpiter.rings.rgB) | `transparent` (themes.json cssVars.rgB) | var | `transparent` (cssVars.rgB) |
| `tema.Jüpiter.rings.rgC` | `transparent` (themes.json planets.Jüpiter.rings.rgC) | `transparent` (themes.json cssVars.rgC) | var | `transparent` (cssVars.rgC) |
| `tema.Jüpiter.rings.rgM` | `transparent` (themes.json planets.Jüpiter.rings.rgM) | `transparent` (themes.json cssVars.rgM) | var | `transparent` (cssVars.rgM) |
| `tema.Jüpiter.rings.ro` | `0` (themes.json planets.Jüpiter.rings.ro) | `0` (themes.json cssVars.ro) | var | `0` (cssVars.ro) |
| `tema.Jüpiter.atmosphere` | `rgba(255,220,180,.5)` (themes.json planets.Jüpiter.atmosphere) | `rgba(255,220,180,.5)` (themes.json cssVars.atm) | var | `rgba(255,220,180,.5)` (cssVars.atm) |
| `tema.Jüpiter.logo.lg1` | `#FFF0DC` (themes.json planets.Jüpiter.logo.lg1) | `#FFF0DC` (themes.json cssVars.lg1) | var | `#FFF0DC` (cssVars.lg1) |
| `tema.Jüpiter.logo.tf1` | `#FFFAF2` (themes.json planets.Jüpiter.logo.tf1) | `#FFFAF2` (themes.json cssVars.tf1) | var | `#FFFAF2` (cssVars.tf1) |
| `tema.Jüpiter.logo.tf2` | `#F0E0CA` (themes.json planets.Jüpiter.logo.tf2) | `#F0E0CA` (themes.json cssVars.tf2) | var | `#F0E0CA` (cssVars.tf2) |
| `tema.Jüpiter.logo.te` | `#DCC4A4` (themes.json planets.Jüpiter.logo.te) | `#DCC4A4` (themes.json cssVars.te) | var | `#DCC4A4` (cssVars.te) |
| `tema.Jüpiter.logo.te2` | `#A08060` (themes.json planets.Jüpiter.logo.te2) | `#A08060` (themes.json cssVars.te2) | var | `#A08060` (cssVars.te2) |
| `tema.Jüpiter.logo.t1` | `#B4532A` (themes.json planets.Jüpiter.logo.t1) | `#B4532A` (themes.json cssVars.t1) | var | `#B4532A` (cssVars.t1) |
| `tema.Jüpiter.logo.t2` | `#6E4A2E` (themes.json planets.Jüpiter.logo.t2) | `#6E4A2E` (themes.json cssVars.t2) | var | `#6E4A2E` (cssVars.t2) |
| `tema.Jüpiter.logo.t3` | `#D08A3A` (themes.json planets.Jüpiter.logo.t3) | `#D08A3A` (themes.json cssVars.t3) | var | `#D08A3A` (cssVars.t3) |
| `tema.Jüpiter.table3d.bg1` | `#4E3522` (themes.json planets.Jüpiter.table3d.bg1) | `#4E3522` (Table/table_content.json planets_table.Jüpiter.bg1) | var | `#4E3522` (table_content planets_table.bg1) |
| `tema.Jüpiter.table3d.bg2` | `#26180E` (themes.json planets.Jüpiter.table3d.bg2) | `#26180E` (Table/table_content.json planets_table.Jüpiter.bg2) | var | `#26180E` (table_content planets_table.bg2) |
| `tema.Jüpiter.table3d.bg3` | `#120A05` (themes.json planets.Jüpiter.table3d.bg3) | `#120A05` (Table/table_content.json planets_table.Jüpiter.bg3) | var | `#120A05` (table_content planets_table.bg3) |
| `tema.Jüpiter.table3d.pn` | `#47321F` (themes.json planets.Jüpiter.table3d.pn) | `#47321F` (Table/table_content.json planets_table.Jüpiter.pn) | var | `#47321F` (table_content planets_table.pn) |
| `tema.Jüpiter.table3d.pnH` | `#5A3F28` (themes.json planets.Jüpiter.table3d.pnH) | `#5A3F28` (Table/table_content.json planets_table.Jüpiter.pnH) | var | `#5A3F28` (table_content planets_table.pnH) |
| `tema.Jüpiter.table3d.pnS` | `#1A1008` (themes.json planets.Jüpiter.table3d.pnS) | `#1A1008` (Table/table_content.json planets_table.Jüpiter.pnS) | var | `#1A1008` (table_content planets_table.pnS) |
| `tema.Jüpiter.table3d.dp` | `#1E140A` (themes.json planets.Jüpiter.table3d.dp) | `#1E140A` (Table/table_content.json planets_table.Jüpiter.dp) | var | `#1E140A` (table_content planets_table.dp) |
| `tema.Jüpiter.table3d.sd` | `#2A1B0F` (themes.json planets.Jüpiter.table3d.sd) | `#2A1B0F` (Table/table_content.json planets_table.Jüpiter.sd) | var | `#2A1B0F` (table_content planets_table.sd) |
| `tema.Jüpiter.table3d.bd` | `#6E4E30` (themes.json planets.Jüpiter.table3d.bd) | `#6E4E30` (Table/table_content.json planets_table.Jüpiter.bd) | var | `#6E4E30` (table_content planets_table.bd) |
| `tema.Jüpiter.table3d.a1` | `#FFC88A` (themes.json planets.Jüpiter.table3d.a1) | `#FFC88A` (Table/table_content.json planets_table.Jüpiter.a1) | var | `#FFC88A` (table_content planets_table.a1) |
| `tema.Jüpiter.table3d.a2` | `#E58B45` (themes.json planets.Jüpiter.table3d.a2) | `#E58B45` (Table/table_content.json planets_table.Jüpiter.a2) | var | `#E58B45` (table_content planets_table.a2) |
| `tema.Jüpiter.table3d.aS` | `#8F4C18` (themes.json planets.Jüpiter.table3d.aS) | `#8F4C18` (Table/table_content.json planets_table.Jüpiter.aS) | var | `#8F4C18` (table_content planets_table.aS) |
| `tema.Jüpiter.table3d.aT` | `#2E1405` (themes.json planets.Jüpiter.table3d.aT) | `#2E1405` (Table/table_content.json planets_table.Jüpiter.aT) | var | `#2E1405` (table_content planets_table.aT) |
| `tema.Jüpiter.table3d.lS` | `#7A3C10` (themes.json planets.Jüpiter.table3d.lS) | `#7A3C10` (Table/table_content.json planets_table.Jüpiter.lS) | var | `#7A3C10` (table_content planets_table.lS) |
| `tema.Jüpiter.table3d.gl` | `rgba(255,170,100,.45)` (themes.json planets.Jüpiter.table3d.gl) | `rgba(255,170,100,.45)` (Table/table_content.json planets_table.Jüpiter.gl) | var | `rgba(255,170,100,.45)` (table_content planets_table.gl) |
| `tema.Jüpiter.table3d.mt` | `#DCC2A6` (themes.json planets.Jüpiter.table3d.mt) | `#DCC2A6` (Table/table_content.json planets_table.Jüpiter.mt) | var | `#DCC2A6` (table_content planets_table.mt) |
| `tema.Jüpiter.table3d.ic` | `#F7E3CC` (themes.json planets.Jüpiter.table3d.ic) | `#F7E3CC` (Table/table_content.json planets_table.Jüpiter.ic) | var | `#F7E3CC` (table_content planets_table.ic) |
| `tema.Jüpiter.table3d.pg` | `rgba(255,190,130,.26)` (themes.json planets.Jüpiter.table3d.pg) | `rgba(255,190,130,.26)` (Table/table_content.json planets_table.Jüpiter.pg) | var | `rgba(255,190,130,.26)` (table_content planets_table.pg) |
| `tema.Jüpiter.table3d.st` | `#FFE2C2` (themes.json planets.Jüpiter.table3d.st) | `#FFE2C2` (Table/table_content.json planets_table.Jüpiter.st) | var | `#FFE2C2` (table_content planets_table.st) |
| `tema.Jüpiter.table3d.lg1` | `#FFF0DC` (themes.json planets.Jüpiter.table3d.lg1) | `#FFF0DC` (Table/table_content.json planets_table.Jüpiter.lg1) | var | `#FFF0DC` (table_content planets_table.lg1) |
| `tema.Jüpiter.table3d.tf1` | `#FFFAF2` (themes.json planets.Jüpiter.table3d.tf1) | `#FFFAF2` (Table/table_content.json planets_table.Jüpiter.tf1) | var | `#FFFAF2` (table_content planets_table.tf1) |
| `tema.Jüpiter.table3d.tf2` | `#F0E0CA` (themes.json planets.Jüpiter.table3d.tf2) | `#F0E0CA` (Table/table_content.json planets_table.Jüpiter.tf2) | var | `#F0E0CA` (table_content planets_table.tf2) |
| `tema.Jüpiter.table3d.te` | `#DCC4A4` (themes.json planets.Jüpiter.table3d.te) | `#DCC4A4` (Table/table_content.json planets_table.Jüpiter.te) | var | `#DCC4A4` (table_content planets_table.te) |
| `tema.Jüpiter.table3d.f1` | `#66462A` (themes.json planets.Jüpiter.table3d.f1) | `#66462A` (Table/table_content.json planets_table.Jüpiter.f1) | var | `#66462A` (table_content planets_table.f1) |
| `tema.Jüpiter.table3d.f2` | `#22150A` (themes.json planets.Jüpiter.table3d.f2) | `#22150A` (Table/table_content.json planets_table.Jüpiter.f2) | var | `#22150A` (table_content planets_table.f2) |
| `tema.Jüpiter.table3d.rim` | `#6c5538` (themes.json planets.Jüpiter.table3d.rim) | `#6c5538` (Table/table_content.json planets_table.Jüpiter.rim) | var | `#6c5538` (table_content planets_table.rim) |
| `tema.Venüs.ad.tr` | `Venüs` (strings.json menu.STR.pn) | `Venüs` (themes.json name_tr) | var | `Venüs` (name_tr) |
| `tema.Venüs.ad.en` | `Venus` (strings.json menu.STR.pn) | `Venus` (themes.json name_en) | var | `Venus` (name_en) |
| `tema.Venüs.menu.desc` | `Pembe sis` (themes.json planets.Venüs.menu.desc) | `Pembe sis` (themes.json desc_tr) | var | `Pembe sis` (desc_tr) |
| `tema.Venüs.menu.desc.en` | `Pink haze` (strings.json menu.STR.pd) | `Pink haze` (themes.json desc_en) | var | `Pink haze` (desc_en) |
| `tema.Venüs.menu.orb` | `#FFF0D2, #EBC07E, #9A6A34` (themes.json planets.Venüs.menu.orb) | `#FFF0D2, #EBC07E, #9A6A34` (themes.json pickerOrb) | var | `#FFF0D2, #EBC07E, #9A6A34` (pickerOrb) |
| `tema.Venüs.ui.bg1` | `#5A2A4E` (themes.json planets.Venüs.ui.bg1) | `#5A2A4E` (themes.json cssVars.bg1) | var | `#5A2A4E` (cssVars.bg1) |
| `tema.Venüs.ui.bg2` | `#2C1428` (themes.json planets.Venüs.ui.bg2) | `#2C1428` (themes.json cssVars.bg2) | var | `#2C1428` (cssVars.bg2) |
| `tema.Venüs.ui.bg3` | `#140812` (themes.json planets.Venüs.ui.bg3) | `#140812` (themes.json cssVars.bg3) | var | `#140812` (cssVars.bg3) |
| `tema.Venüs.ui.pn` | `#4C2444` (themes.json planets.Venüs.ui.pn) | `#4C2444` (themes.json cssVars.pn) | var | `#4C2444` (cssVars.pn) |
| `tema.Venüs.ui.pnH` | `#5E2E54` (themes.json planets.Venüs.ui.pnH) | `#5E2E54` (themes.json cssVars.pnH) | var | `#5E2E54` (cssVars.pnH) |
| `tema.Venüs.ui.pnS` | `#1C0A19` (themes.json planets.Venüs.ui.pnS) | `#1C0A19` (themes.json cssVars.pnS) | var | `#1C0A19` (cssVars.pnS) |
| `tema.Venüs.ui.dp` | `#1F0C1C` (themes.json planets.Venüs.ui.dp) | `#1F0C1C` (themes.json cssVars.dp) | var | `#1F0C1C` (cssVars.dp) |
| `tema.Venüs.ui.sd` | `#2B1127` (themes.json planets.Venüs.ui.sd) | `#2B1127` (themes.json cssVars.sd) | var | `#2B1127` (cssVars.sd) |
| `tema.Venüs.ui.bd` | `#7A3A6A` (themes.json planets.Venüs.ui.bd) | `#7A3A6A` (themes.json cssVars.bd) | var | `#7A3A6A` (cssVars.bd) |
| `tema.Venüs.ui.a1` | `#FFD3A8` (themes.json planets.Venüs.ui.a1) | `#FFD3A8` (themes.json cssVars.a1) | var | `#FFD3A8` (cssVars.a1) |
| `tema.Venüs.ui.a2` | `#F28AA0` (themes.json planets.Venüs.ui.a2) | `#F28AA0` (themes.json cssVars.a2) | var | `#F28AA0` (cssVars.a2) |
| `tema.Venüs.ui.aS` | `#A33F5E` (themes.json planets.Venüs.ui.aS) | `#A33F5E` (themes.json cssVars.aS) | var | `#A33F5E` (cssVars.aS) |
| `tema.Venüs.ui.aT` | `#3A0A18` (themes.json planets.Venüs.ui.aT) | `#3A0A18` (themes.json cssVars.aT) | var | `#3A0A18` (cssVars.aT) |
| `tema.Venüs.ui.lS` | `#8A2E48` (themes.json planets.Venüs.ui.lS) | `#8A2E48` (themes.json cssVars.lS) | var | `#8A2E48` (cssVars.lS) |
| `tema.Venüs.ui.gl` | `rgba(255,170,190,.45)` (themes.json planets.Venüs.ui.gl) | `rgba(255,170,190,.45)` (themes.json cssVars.gl) | var | `rgba(255,170,190,.45)` (cssVars.gl) |
| `tema.Venüs.ui.mt` | `#E2B9D2` (themes.json planets.Venüs.ui.mt) | `#E2B9D2` (themes.json cssVars.mt) | var | `#E2B9D2` (cssVars.mt) |
| `tema.Venüs.ui.ic` | `#FFE0EE` (themes.json planets.Venüs.ui.ic) | `#FFE0EE` (themes.json cssVars.ic) | var | `#FFE0EE` (cssVars.ic) |
| `tema.Venüs.ui.p1` | `#FBE7C2` (themes.json planets.Venüs.ui.p1) | `#FBE7C2` (themes.json cssVars.p1) | var | `#FBE7C2` (cssVars.p1) |
| `tema.Venüs.ui.p2` | `#EFC98E` (themes.json planets.Venüs.ui.p2) | `#EFC98E` (themes.json cssVars.p2) | var | `#EFC98E` (cssVars.p2) |
| `tema.Venüs.ui.p3` | `#F7DAB0` (themes.json planets.Venüs.ui.p3) | `#F7DAB0` (themes.json cssVars.p3) | var | `#F7DAB0` (cssVars.p3) |
| `tema.Venüs.ui.p4` | `#DDAE72` (themes.json planets.Venüs.ui.p4) | `#DDAE72` (themes.json cssVars.p4) | var | `#DDAE72` (cssVars.p4) |
| `tema.Venüs.ui.rg` | `transparent` (themes.json planets.Venüs.ui.rg) | `transparent` (themes.json cssVars.rg) | var | `transparent` (cssVars.rg) |
| `tema.Venüs.ui.pg` | `rgba(255,200,170,.35)` (themes.json planets.Venüs.ui.pg) | `rgba(255,200,170,.35)` (themes.json cssVars.pg) | var | `rgba(255,200,170,.35)` (cssVars.pg) |
| `tema.Venüs.ui.st` | `#FFE0C8` (themes.json planets.Venüs.ui.st) | `#FFE0C8` (themes.json cssVars.st) | var | `#FFE0C8` (cssVars.st) |
| `tema.Venüs.ui.lg1` | `#FFF3E6` (themes.json planets.Venüs.ui.lg1) | `#FFF3E6` (themes.json cssVars.lg1) | var | `#FFF3E6` (cssVars.lg1) |
| `tema.Venüs.ui.tf1` | `#FFFBF6` (themes.json planets.Venüs.ui.tf1) | `#FFFBF6` (themes.json cssVars.tf1) | var | `#FFFBF6` (cssVars.tf1) |
| `tema.Venüs.ui.tf2` | `#F4E4D8` (themes.json planets.Venüs.ui.tf2) | `#F4E4D8` (themes.json cssVars.tf2) | var | `#F4E4D8` (cssVars.tf2) |
| `tema.Venüs.ui.te` | `#E0C6B8` (themes.json planets.Venüs.ui.te) | `#E0C6B8` (themes.json cssVars.te) | var | `#E0C6B8` (cssVars.te) |
| `tema.Venüs.ui.te2` | `#AC8878` (themes.json planets.Venüs.ui.te2) | `#AC8878` (themes.json cssVars.te2) | var | `#AC8878` (cssVars.te2) |
| `tema.Venüs.ui.t1` | `#C2416A` (themes.json planets.Venüs.ui.t1) | `#C2416A` (themes.json cssVars.t1) | var | `#C2416A` (cssVars.t1) |
| `tema.Venüs.ui.t2` | `#D98A2B` (themes.json planets.Venüs.ui.t2) | `#D98A2B` (themes.json cssVars.t2) | var | `#D98A2B` (cssVars.t2) |
| `tema.Venüs.ui.t3` | `#8E3A7A` (themes.json planets.Venüs.ui.t3) | `#8E3A7A` (themes.json cssVars.t3) | var | `#8E3A7A` (cssVars.t3) |
| `tema.Venüs.ui.atm` | `rgba(255,238,205,.9)` (themes.json planets.Venüs.ui.atm) | `rgba(255,238,205,.9)` (themes.json cssVars.atm) | var | `rgba(255,238,205,.9)` (cssVars.atm) |
| `tema.Venüs.ui.pf` | `1` (themes.json planets.Venüs.ui.pf) | `1` (themes.json planetFlattening) | var | `1` (planetFlattening) |
| `tema.Venüs.ui.ro` | `0` (themes.json planets.Venüs.ui.ro (yükleme ekranı halka opaklığı)) | — (themes.json (cssVars.ro = menü halkası, ayrı değer)) | eksik | `0` (loadingRingOpacity) |
| `tema.Venüs.rings.rgA` | `transparent` (themes.json planets.Venüs.rings.rgA) | `transparent` (themes.json cssVars.rgA) | var | `transparent` (cssVars.rgA) |
| `tema.Venüs.rings.rgB` | `transparent` (themes.json planets.Venüs.rings.rgB) | `transparent` (themes.json cssVars.rgB) | var | `transparent` (cssVars.rgB) |
| `tema.Venüs.rings.rgC` | `transparent` (themes.json planets.Venüs.rings.rgC) | `transparent` (themes.json cssVars.rgC) | var | `transparent` (cssVars.rgC) |
| `tema.Venüs.rings.rgM` | `transparent` (themes.json planets.Venüs.rings.rgM) | `transparent` (themes.json cssVars.rgM) | var | `transparent` (cssVars.rgM) |
| `tema.Venüs.rings.ro` | `0` (themes.json planets.Venüs.rings.ro) | `0` (themes.json cssVars.ro) | var | `0` (cssVars.ro) |
| `tema.Venüs.atmosphere` | `rgba(255,238,205,.9)` (themes.json planets.Venüs.atmosphere) | `rgba(255,238,205,.9)` (themes.json cssVars.atm) | var | `rgba(255,238,205,.9)` (cssVars.atm) |
| `tema.Venüs.logo.lg1` | `#FFF3E6` (themes.json planets.Venüs.logo.lg1) | `#FFF3E6` (themes.json cssVars.lg1) | var | `#FFF3E6` (cssVars.lg1) |
| `tema.Venüs.logo.tf1` | `#FFFBF6` (themes.json planets.Venüs.logo.tf1) | `#FFFBF6` (themes.json cssVars.tf1) | var | `#FFFBF6` (cssVars.tf1) |
| `tema.Venüs.logo.tf2` | `#F4E4D8` (themes.json planets.Venüs.logo.tf2) | `#F4E4D8` (themes.json cssVars.tf2) | var | `#F4E4D8` (cssVars.tf2) |
| `tema.Venüs.logo.te` | `#E0C6B8` (themes.json planets.Venüs.logo.te) | `#E0C6B8` (themes.json cssVars.te) | var | `#E0C6B8` (cssVars.te) |
| `tema.Venüs.logo.te2` | `#AC8878` (themes.json planets.Venüs.logo.te2) | `#AC8878` (themes.json cssVars.te2) | var | `#AC8878` (cssVars.te2) |
| `tema.Venüs.logo.t1` | `#C2416A` (themes.json planets.Venüs.logo.t1) | `#C2416A` (themes.json cssVars.t1) | var | `#C2416A` (cssVars.t1) |
| `tema.Venüs.logo.t2` | `#D98A2B` (themes.json planets.Venüs.logo.t2) | `#D98A2B` (themes.json cssVars.t2) | var | `#D98A2B` (cssVars.t2) |
| `tema.Venüs.logo.t3` | `#8E3A7A` (themes.json planets.Venüs.logo.t3) | `#8E3A7A` (themes.json cssVars.t3) | var | `#8E3A7A` (cssVars.t3) |
| `tema.Venüs.table3d.bg1` | `#5A2A4E` (themes.json planets.Venüs.table3d.bg1) | `#5A2A4E` (Table/table_content.json planets_table.Venüs.bg1) | var | `#5A2A4E` (table_content planets_table.bg1) |
| `tema.Venüs.table3d.bg2` | `#2C1428` (themes.json planets.Venüs.table3d.bg2) | `#2C1428` (Table/table_content.json planets_table.Venüs.bg2) | var | `#2C1428` (table_content planets_table.bg2) |
| `tema.Venüs.table3d.bg3` | `#140812` (themes.json planets.Venüs.table3d.bg3) | `#140812` (Table/table_content.json planets_table.Venüs.bg3) | var | `#140812` (table_content planets_table.bg3) |
| `tema.Venüs.table3d.pn` | `#4C2444` (themes.json planets.Venüs.table3d.pn) | `#4C2444` (Table/table_content.json planets_table.Venüs.pn) | var | `#4C2444` (table_content planets_table.pn) |
| `tema.Venüs.table3d.pnH` | `#5E2E54` (themes.json planets.Venüs.table3d.pnH) | `#5E2E54` (Table/table_content.json planets_table.Venüs.pnH) | var | `#5E2E54` (table_content planets_table.pnH) |
| `tema.Venüs.table3d.pnS` | `#1C0A19` (themes.json planets.Venüs.table3d.pnS) | `#1C0A19` (Table/table_content.json planets_table.Venüs.pnS) | var | `#1C0A19` (table_content planets_table.pnS) |
| `tema.Venüs.table3d.dp` | `#1F0C1C` (themes.json planets.Venüs.table3d.dp) | `#1F0C1C` (Table/table_content.json planets_table.Venüs.dp) | var | `#1F0C1C` (table_content planets_table.dp) |
| `tema.Venüs.table3d.sd` | `#2B1127` (themes.json planets.Venüs.table3d.sd) | `#2B1127` (Table/table_content.json planets_table.Venüs.sd) | var | `#2B1127` (table_content planets_table.sd) |
| `tema.Venüs.table3d.bd` | `#7A3A6A` (themes.json planets.Venüs.table3d.bd) | `#7A3A6A` (Table/table_content.json planets_table.Venüs.bd) | var | `#7A3A6A` (table_content planets_table.bd) |
| `tema.Venüs.table3d.a1` | `#FFD3A8` (themes.json planets.Venüs.table3d.a1) | `#FFD3A8` (Table/table_content.json planets_table.Venüs.a1) | var | `#FFD3A8` (table_content planets_table.a1) |
| `tema.Venüs.table3d.a2` | `#F28AA0` (themes.json planets.Venüs.table3d.a2) | `#F28AA0` (Table/table_content.json planets_table.Venüs.a2) | var | `#F28AA0` (table_content planets_table.a2) |
| `tema.Venüs.table3d.aS` | `#A33F5E` (themes.json planets.Venüs.table3d.aS) | `#A33F5E` (Table/table_content.json planets_table.Venüs.aS) | var | `#A33F5E` (table_content planets_table.aS) |
| `tema.Venüs.table3d.aT` | `#3A0A18` (themes.json planets.Venüs.table3d.aT) | `#3A0A18` (Table/table_content.json planets_table.Venüs.aT) | var | `#3A0A18` (table_content planets_table.aT) |
| `tema.Venüs.table3d.lS` | `#8A2E48` (themes.json planets.Venüs.table3d.lS) | `#8A2E48` (Table/table_content.json planets_table.Venüs.lS) | var | `#8A2E48` (table_content planets_table.lS) |
| `tema.Venüs.table3d.gl` | `rgba(255,170,190,.45)` (themes.json planets.Venüs.table3d.gl) | `rgba(255,170,190,.45)` (Table/table_content.json planets_table.Venüs.gl) | var | `rgba(255,170,190,.45)` (table_content planets_table.gl) |
| `tema.Venüs.table3d.mt` | `#E2B9D2` (themes.json planets.Venüs.table3d.mt) | `#E2B9D2` (Table/table_content.json planets_table.Venüs.mt) | var | `#E2B9D2` (table_content planets_table.mt) |
| `tema.Venüs.table3d.ic` | `#FFE0EE` (themes.json planets.Venüs.table3d.ic) | `#FFE0EE` (Table/table_content.json planets_table.Venüs.ic) | var | `#FFE0EE` (table_content planets_table.ic) |
| `tema.Venüs.table3d.pg` | `rgba(255,200,170,.30)` (themes.json planets.Venüs.table3d.pg) | `rgba(255,200,170,.30)` (Table/table_content.json planets_table.Venüs.pg) | var | `rgba(255,200,170,.30)` (table_content planets_table.pg) |
| `tema.Venüs.table3d.st` | `#FFE0C8` (themes.json planets.Venüs.table3d.st) | `#FFE0C8` (Table/table_content.json planets_table.Venüs.st) | var | `#FFE0C8` (table_content planets_table.st) |
| `tema.Venüs.table3d.lg1` | `#FFF3E6` (themes.json planets.Venüs.table3d.lg1) | `#FFF3E6` (Table/table_content.json planets_table.Venüs.lg1) | var | `#FFF3E6` (table_content planets_table.lg1) |
| `tema.Venüs.table3d.tf1` | `#FFFBF6` (themes.json planets.Venüs.table3d.tf1) | `#FFFBF6` (Table/table_content.json planets_table.Venüs.tf1) | var | `#FFFBF6` (table_content planets_table.tf1) |
| `tema.Venüs.table3d.tf2` | `#F4E4D8` (themes.json planets.Venüs.table3d.tf2) | `#F4E4D8` (Table/table_content.json planets_table.Venüs.tf2) | var | `#F4E4D8` (table_content planets_table.tf2) |
| `tema.Venüs.table3d.te` | `#E0C6B8` (themes.json planets.Venüs.table3d.te) | `#E0C6B8` (Table/table_content.json planets_table.Venüs.te) | var | `#E0C6B8` (table_content planets_table.te) |
| `tema.Venüs.table3d.f1` | `#713464` (themes.json planets.Venüs.table3d.f1) | `#713464` (Table/table_content.json planets_table.Venüs.f1) | var | `#713464` (table_content planets_table.f1) |
| `tema.Venüs.table3d.f2` | `#260E23` (themes.json planets.Venüs.table3d.f2) | `#260E23` (Table/table_content.json planets_table.Venüs.f2) | var | `#260E23` (table_content planets_table.f2) |
| `tema.Venüs.table3d.rim` | `#704668` (themes.json planets.Venüs.table3d.rim) | `#704668` (Table/table_content.json planets_table.Venüs.rim) | var | `#704668` (table_content planets_table.rim) |
| `tema.vars.fL` | logo yazı tipi (themes.json vars.fL; değer gezegenlerde yok, fonts.json used.*.var) | — (USS --fL/--fH/--fB yok; MenuView Font("Montserrat-700"/"Baloo2-700")) | farklı | değişmedi; fontlar fonts.json rollerine göre (bölüm Fontlar) |
| `tema.vars.fH` | başlık yazı tipi (themes.json vars.fH; değer gezegenlerde yok, fonts.json used.*.var) | — (USS --fL/--fH/--fB yok; MenuView Font("Montserrat-700"/"Baloo2-700")) | farklı | değişmedi; fontlar fonts.json rollerine göre (bölüm Fontlar) |
| `tema.vars.fB` | gövde yazı tipi (themes.json vars.fB; değer gezegenlerde yok, fonts.json used.*.var) | — (USS --fL/--fH/--fB yok; MenuView Font("Montserrat-700"/"Baloo2-700")) | farklı | değişmedi; fontlar fonts.json rollerine göre (bölüm Fontlar) |
| `tema.transition` | Tema değişimi: CSS değişkenleri 0,6 sn; gezegen konumu 1 sn cubic-bezier(.6,0,.2,1) (anim/easings.json cssPlanetMove) (themes.json transition) | — (themes.json) | eksik | themes.json gezegen sözlüğünde yer yok; süre ve eğri Faz 2 animasyonunda (anim/easings.json cssPlanetMove) |

## Fontlar

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| `fonts/Montserrat-SemiBold.ttf` | Montserrat 600, Version 9.000, 2747 glif, sha b4e1563393d7 (fonts.json files.Montserrat) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-600.ttf`: ad tablosu "Montserrat Thin", ağırlık 600, Version 9.000, 2747 glif, sha e97bbfa2f3a1; 85 örnek karakterde ilerleme farkı 0, çizim farkı 77 | farklı | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-600.ttf` ← paket dosyası (.meta/GUID korunur); FontAsset yeniden üretilecek (RAPOR §Unity) |
| `fonts/Montserrat-Bold.ttf` | Montserrat 700, Version 9.000, 2747 glif, sha bc6e854971ce (fonts.json files.Montserrat) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-700.ttf`: ad tablosu "Montserrat Thin", ağırlık 700, Version 9.000, 2747 glif, sha 69a8c83122a4; 85 örnek karakterde ilerleme farkı 0, çizim farkı 75 | farklı | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-700.ttf` ← paket dosyası (.meta/GUID korunur); FontAsset yeniden üretilecek (RAPOR §Unity) |
| `fonts/Montserrat-ExtraBold.ttf` | Montserrat 800, Version 9.000, 2747 glif, sha d3ac6a843d3b (fonts.json files.Montserrat) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-800.ttf`: ad tablosu "Montserrat Thin", ağırlık 800, Version 9.000, 2747 glif, sha d929927560a8; 85 örnek karakterde ilerleme farkı 0, çizim farkı 77 | farklı | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-800.ttf` ← paket dosyası (.meta/GUID korunur); FontAsset yeniden üretilecek (RAPOR §Unity) |
| `fonts/Montserrat-Black.ttf` | Montserrat 900, Version 9.000, 2747 glif, sha ea67b37f8776 (fonts.json files.Montserrat) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-900.ttf`: ad tablosu "Montserrat Thin", ağırlık 900, Version 9.000, 2747 glif, sha c10273bf9ea9; 85 örnek karakterde ilerleme farkı 0, çizim farkı 69 | farklı | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-900.ttf` ← paket dosyası (.meta/GUID korunur); FontAsset yeniden üretilecek (RAPOR §Unity) |
| `fonts/Montserrat-Variable.ttf` | Montserrat 100, Version 9.000, 2747 glif, sha 0f7b311b2f32 (fonts.json files.Montserrat), değişken font | — (dosya projede yok; eski sabit ağırlıklar bu dosyadan kesilmişti: scripts/import-galaxy-ui-assets.py Montserrat-Variable.ttf sha 0f7b311b… (PINNED)) | eksik | kopyalanmadı: istemci sabit ağırlıklı TTF + FontAsset kullanır (fonts.json license notu) |
| `fonts/Baloo2-SemiBold.ttf` | Baloo 2 600, Version 1.700, 1585 glif, sha b2e326e489d1 (fonts.json files.Baloo 2) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Baloo2-600.ttf`: ad tablosu "Baloo 2 Regular", ağırlık 600, Version 1.700, 1601 glif, sha 6e0947263ecf; 85 örnek karakterde ilerleme farkı 0, çizim farkı 0 | farklı | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Baloo2-600.ttf` ← paket dosyası (.meta/GUID korunur); FontAsset yeniden üretilecek (RAPOR §Unity) |
| `fonts/Baloo2-Bold.ttf` | Baloo 2 700, Version 1.700, 1585 glif, sha e1bc2e47cfd8 (fonts.json files.Baloo 2) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Baloo2-700.ttf`: ad tablosu "Baloo 2 Regular", ağırlık 700, Version 1.700, 1601 glif, sha 49d1d02239a7; 85 örnek karakterde ilerleme farkı 0, çizim farkı 0 | farklı | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Baloo2-700.ttf` ← paket dosyası (.meta/GUID korunur); FontAsset yeniden üretilecek (RAPOR §Unity) |
| `fonts/Baloo2-ExtraBold.ttf` | Baloo 2 800, Version 1.700, 1585 glif, sha f9f24a56b1ed (fonts.json files.Baloo 2) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Baloo2-800.ttf`: ad tablosu "Baloo 2 Regular", ağırlık 800, Version 1.700, 1601 glif, sha 4acfa2878727; 85 örnek karakterde ilerleme farkı 0, çizim farkı 0 | farklı | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Baloo2-800.ttf` ← paket dosyası (.meta/GUID korunur); FontAsset yeniden üretilecek (RAPOR §Unity) |
| `fonts/Baloo2-Variable.ttf` | Baloo 2 400, Version 1.700, 1601 glif, sha d47a68525480 (fonts.json files.Baloo 2), değişken font | — (dosya projede yok; eski sabit ağırlıklar bu dosyadan kesilmişti: scripts/import-galaxy-ui-assets.py Baloo2-Variable.ttf sha d47a6852… (PINNED)) | eksik | kopyalanmadı: istemci sabit ağırlıklı TTF + FontAsset kullanır (fonts.json license notu) |
| `fonts/Nunito-Bold.ttf` | Nunito 700, Version 3.602, 1098 glif, sha ad443e47990c (fonts.json files.Nunito) | — (yok) | eksik | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Nunito-700.ttf` ← paket dosyası (.meta/GUID korunur); FontAsset yeniden üretilecek (RAPOR §Unity) |
| `fonts/Nunito-Variable.ttf` | Nunito 200, Version 3.602, 1103 glif, sha bb55a5ca5c20 (fonts.json files.Nunito), değişken font | — (dosya projede yok; Nunito-900.ttf aynı glif sayılı bir değişken fonttan kesilmiş) | eksik | kopyalanmadı: istemci sabit ağırlıklı TTF + FontAsset kullanır (fonts.json license notu) |
| `fonts/OFL-Montserrat.txt` | OFL 1.1 metni, sha256 (LF) 8b7141c03fa4 (fonts.json files.license) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-OFL.txt` sha256 (LF) 8b7141c03fa4 | var | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Montserrat-OFL.txt` (aynı) |
| `fonts/OFL-Baloo2.txt` | OFL 1.1 metni, sha256 (LF) ad09b05dc8bc (fonts.json files.license) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Baloo2-OFL.txt` sha256 (LF) ad09b05dc8bc | var | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Baloo2-OFL.txt` (aynı) |
| `fonts/OFL-Nunito.txt` | OFL 1.1 metni, sha256 (LF) 580df76c95a1 (fonts.json files.license) | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Nunito-OFL.txt` sha256 (LF) 580df76c95a1 | var | `Assets/Galaxy101/Resources/Galaxy101/Fonts/Nunito-OFL.txt` (aynı) |
| `font.rol.logo` | Montserrat 700/800 · --fL (fonts.json used.logo) | Montserrat ağırlıkları projede: [700, 800] (Fonts/) | var | tüm ağırlıklar Fonts/ altında |
| `font.rol.heading` | Montserrat 600/700/800/900 · --fH (fonts.json used.heading) | Montserrat ağırlıkları projede: [600, 700, 800, 900] (Fonts/) | var | tüm ağırlıklar Fonts/ altında |
| `font.rol.body` | Baloo 2 600/700/800 · --fB (fonts.json used.body) | Baloo 2 ağırlıkları projede: [600, 700, 800] (Fonts/) | var | tüm ağırlıklar Fonts/ altında |
| `font.rol.versionTag` | Nunito 700 (fonts.json used.versionTag) | Nunito ağırlıkları projede: yok (Fonts/) | eksik | tüm ağırlıklar Fonts/ altında (Nunito-700 yeni) |
| `Fonts/Nunito-900.ttf` | — (fonts.json yalnız Nunito 700 · versionTag; 900 yalnız "optional" URL listesinde) | Nunito 900 sha 99da492eb108 — MenuView.Main.cs indirim rozeti | pakette yok | dokunulmadı (kullanan kod Faz 2'de v4 menüye göre değişecek) |

## İkonlar

Eşleme geometriyle (yol/şekil öznitelikleri aynı): aynı geometri + aynı çizgi → var; aynı geometri, farklı çizgi kalınlığı → farklı. Paket ikonları kullanım yerine göre ayrı dosya (aynı çizim farklı `stroke-width` ile birden çok kez var).

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| `app-splash-tip-box` | viewBox 0 0 24 24, stroke 2.4; app.splash.tipBox, load.bar (icons/app-splash-tip-box.svg) | `tip-bulb` (Icons/*.txt, aynı geometri; stroke 2.2) | farklı | `Icons/v4/app-splash-tip-box.txt` + `Icons/v4/svg/app-splash-tip-box.svg`; GalaxyIcon çizer |
| `auth-guest` | viewBox 0 0 24 24, stroke 2.2; auth.guest (icons/auth-guest.svg) | `auth-guest` (Icons/*.txt, aynı geometri) | var | `Icons/v4/auth-guest.txt` + `Icons/v4/svg/auth-guest.svg`; GalaxyIcon çizer |
| `auth-login` | viewBox 0 0 24 24, stroke 2.2; auth.login (icons/auth-login.svg) | `auth-login` (Icons/*.txt, aynı geometri) | var | `Icons/v4/auth-login.txt` + `Icons/v4/svg/auth-login.svg`; GalaxyIcon çizer |
| `auth-register` | viewBox 0 0 24 24, stroke 2.2; auth.register (icons/auth-register.svg) | `auth-register` (Icons/*.txt, aynı geometri) | var | `Icons/v4/auth-register.txt` + `Icons/v4/svg/auth-register.svg`; GalaxyIcon çizer |
| `form-back` | viewBox 0 0 24 24, stroke 2.8; form.back, lobby.back, ranked.back (icons/form-back.svg) | — | eksik | `Icons/v4/form-back.txt` + `Icons/v4/svg/form-back.svg`; GalaxyIcon çizer |
| `form-field-eye` | viewBox 0 0 24 24, stroke 2.2; form.field[{{ $index }}].eye, lobby.b.go[{{ $index }}] (icons/form-field-eye.svg) | — | eksik | `Icons/v4/form-field-eye.txt` + `Icons/v4/svg/form-field-eye.svg`; GalaxyIcon çizer |
| `form-field-eye-7` | viewBox 0 0 24 24, stroke 3.6; form.field[{{ $index }}].eye, form.terms, form.errorText +7 (icons/form-field-eye-7.svg) | `tick` (Icons/*.txt, aynı geometri) | var | `Icons/v4/form-field-eye-7.txt` + `Icons/v4/svg/form-field-eye-7.svg`; GalaxyIcon çizer |
| `form-guest-chips` | viewBox 0 0 24 24, stroke 2.2; form.guest.chips (icons/form-guest-chips.svg) | `phone` (Icons/*.txt, aynı geometri) | var | `Icons/v4/form-guest-chips.txt` + `Icons/v4/svg/form-guest-chips.svg`; GalaxyIcon çizer |
| `load-fail` | viewBox 0 0 24 24, stroke 2.4; load.fail (icons/load-fail.svg) | — | eksik | `Icons/v4/load-fail.txt` + `Icons/v4/svg/load-fail.svg`; GalaxyIcon çizer |
| `menu-open-settings` | viewBox 0 0 24 24, stroke 2.2; menu.openSettings, menu.closeSettings, table.openSettings +1 (icons/menu-open-settings.svg) | `settings` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-open-settings.txt` + `Icons/v4/svg/menu-open-settings.svg`; GalaxyIcon çizer |
| `menu-open-shop` | viewBox 0 0 24 24, stroke 1.9; menu.openShop (icons/menu-open-shop.svg) | `shop` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-open-shop.txt` + `Icons/v4/svg/menu-open-shop.svg`; GalaxyIcon çizer |
| `menu-open-friends` | viewBox 0 0 24 24, stroke 1.9; menu.openFriends (icons/menu-open-friends.svg) | `friends, table-full` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-open-friends.txt` + `Icons/v4/svg/menu-open-friends.svg`; GalaxyIcon çizer |
| `menu-open-leader` | viewBox 0 0 24 24, stroke 1.9; menu.openLeader (icons/menu-open-leader.svg) | `leader` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-open-leader.txt` + `Icons/v4/svg/menu-open-leader.svg`; GalaxyIcon çizer |
| `menu-open-daily` | viewBox 0 0 24 24, stroke 1.9; menu.openDaily (icons/menu-open-daily.svg) | `daily, table-gift` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-open-daily.txt` + `Icons/v4/svg/menu-open-daily.svg`; GalaxyIcon çizer |
| `menu-open-rules` | viewBox 0 0 24 24, stroke 1.9; menu.openRules (icons/menu-open-rules.svg) | `rules` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-open-rules.txt` + `Icons/v4/svg/menu-open-rules.svg`; GalaxyIcon çizer |
| `menu-ask-quit` | viewBox 0 0 24 24, stroke 2.4; menu.askQuit (icons/menu-ask-quit.svg) | — | eksik | `Icons/v4/menu-ask-quit.txt` + `Icons/v4/svg/menu-ask-quit.svg`; GalaxyIcon çizer |
| `menu-close-settings` | viewBox 0 0 24 24, stroke 2.8; menu.closeSettings#2, menu.closePanel#2, menu.fr.clearQ +13 (icons/menu-close-settings.svg) | `close` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-close-settings.txt` + `Icons/v4/svg/menu-close-settings.svg`; GalaxyIcon çizer |
| `menu-sec-cur` | viewBox 0 0 24 24, stroke 3; menu.sec.cur[{{ $index }}], lobby.cf.rulesSum (icons/menu-sec-cur.svg) | `chevron-down` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-sec-cur.txt` + `Icons/v4/svg/menu-sec-cur.svg`; GalaxyIcon çizer |
| `menu-v-pick` | viewBox 0 0 96 62, stroke 1.5; menu.v.pick[{{ $index }}] (icons/menu-v-pick.svg) | — | eksik | `Icons/v4/menu-v-pick.txt` + `Icons/v4/svg/menu-v-pick.svg`; GalaxyIcon çizemez: <g> → SVG içe aktarma |
| `icon-20` | viewBox 0 0 96 62, stroke 1.5; usedBy boş (icons/icon-20.svg) | — | eksik | `Icons/v4/icon-20.txt` + `Icons/v4/svg/icon-20.svg`; GalaxyIcon çizemez: <g> → SVG içe aktarma |
| `icon-21` | viewBox 0 0 96 62, stroke 1.5; usedBy boş (icons/icon-21.svg) | — | eksik | `Icons/v4/icon-21.txt` + `Icons/v4/svg/icon-21.svg`; GalaxyIcon çizer |
| `menu-close-panel` | viewBox 0 0 24 24, stroke 1.9; menu.closePanel (icons/menu-close-panel.svg) | `profile` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-close-panel.txt` + `Icons/v4/svg/menu-close-panel.svg`; GalaxyIcon çizer |
| `menu-open-profile` | viewBox 0 0 24 24, stroke 2.4; menu.openProfile#2 (icons/menu-open-profile.svg) | `edit` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-open-profile.txt` + `Icons/v4/svg/menu-open-profile.svg`; GalaxyIcon çizer |
| `menu-open-share` | viewBox 0 0 24 24, stroke 2.2; menu.openShare, lobby.w.share, lobby.d.share (icons/menu-open-share.svg) | `share` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-open-share.txt` + `Icons/v4/svg/menu-open-share.svg`; GalaxyIcon çizer |
| `menu-shop-deal-title` | viewBox 0 0 24 24, stroke 2.4; menu.shop.dealTitle, menu.dl.doneCount (icons/menu-shop-deal-title.svg) | `clock` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-shop-deal-title.txt` + `Icons/v4/svg/menu-shop-deal-title.svg`; GalaxyIcon çizer |
| `icon-26` | viewBox 0 0 60 100, stroke 14; usedBy boş (icons/icon-26.svg) | — | eksik | `Icons/v4/icon-26.txt` + `Icons/v4/svg/icon-26.svg`; GalaxyIcon çizer |
| `menu-i-price` | viewBox 0 0 24 24, stroke 2.4; menu.i.price[{{ $index }}] (icons/menu-i-price.svg) | `search` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-i-price.txt` + `Icons/v4/svg/menu-i-price.svg`; GalaxyIcon çizer |
| `menu-af-copy` | viewBox 0 0 24 24, stroke 2.2; menu.af.copy#2, menu.af.copy#3 (icons/menu-af-copy.svg) | `copy` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-af-copy.txt` + `Icons/v4/svg/menu-af-copy.svg`; GalaxyIcon çizer |
| `menu-open-add-friend` | viewBox 0 0 24 24, stroke 2.4; menu.openAddFriend (icons/menu-open-add-friend.svg) | `add` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-open-add-friend.txt` + `Icons/v4/svg/menu-open-add-friend.svg`; GalaxyIcon çizer |
| `icon-30` | viewBox 0 0 34 24, stroke None; usedBy boş (icons/icon-30.svg) | `crown` (Icons/*.txt, aynı geometri; stroke None) | farklı | `Icons/v4/icon-30.txt` + `Icons/v4/svg/icon-30.svg`; GalaxyIcon çizer |
| `lobby-t-name` | viewBox 0 0 24 24, stroke 3; lobby.t.name[{{ $index }}], lobby.cf.passD, lobby.b.label[{{ $index }}] +3 (icons/lobby-t-name.svg) | `lock` (Icons/*.txt, aynı geometri; stroke 2.6) | farklı | `Icons/v4/lobby-t-name.txt` + `Icons/v4/svg/lobby-t-name.svg`; GalaxyIcon çizer |
| `icon-32` | viewBox 0 0 24 24, stroke 2.2; usedBy boş (icons/icon-32.svg) | `logout` (Icons/*.txt, aynı geometri) | var | `Icons/v4/icon-32.txt` + `Icons/v4/svg/icon-32.svg`; GalaxyIcon çizer |
| `icon-33` | viewBox 0 0 24 24, stroke 2.2; usedBy boş (icons/icon-33.svg) | `delete` (Icons/*.txt, aynı geometri) | var | `Icons/v4/icon-33.txt` + `Icons/v4/svg/icon-33.svg`; GalaxyIcon çizer |
| `menu-sh-link-label` | viewBox 0 0 24 24, stroke 2.4; menu.sh.linkLabel (icons/menu-sh-link-label.svg) | `link` (Icons/*.txt, aynı geometri) | var | `Icons/v4/menu-sh-link-label.txt` + `Icons/v4/svg/menu-sh-link-label.svg`; GalaxyIcon çizer |
| `lobby-open-code` | viewBox 0 0 24 24, stroke 2.2; lobby.openCode, lobby.d.submitPin (icons/lobby-open-code.svg) | `key` (Icons/*.txt, aynı geometri) | var | `Icons/v4/lobby-open-code.txt` + `Icons/v4/svg/lobby-open-code.svg`; GalaxyIcon çizer |
| `lobby-open-create` | viewBox 0 0 24 24, stroke 3; lobby.openCreate, lobby.closeCreate, ranked.openInvite[{{ $index }}] (icons/lobby-open-create.svg) | `plus` (Icons/*.txt, aynı geometri) | var | `Icons/v4/lobby-open-create.txt` + `Icons/v4/svg/lobby-open-create.svg`; GalaxyIcon çizer |
| `lobby-o-pick` | viewBox 0 0 24 24, stroke 2.4; lobby.o.pick[{{ $index }}], lobby.g.label[{{ $index }}], lobby.b.n[{{ $index }}] (icons/lobby-o-pick.svg) | — | eksik | `Icons/v4/lobby-o-pick.txt` + `Icons/v4/svg/lobby-o-pick.svg`; GalaxyIcon çizer |
| `lobby-w-leave` | viewBox 0 0 24 24, stroke 2.2; lobby.w.leave, table.askExit, table.stay (icons/lobby-w-leave.svg) | `leave, table-exit` (Icons/*.txt, aynı geometri) | var | `Icons/v4/lobby-w-leave.txt` + `Icons/v4/svg/lobby-w-leave.svg`; GalaxyIcon çizer |
| `lobby-w-open-inv` | viewBox 0 0 24 24, stroke 2.4; lobby.w.openInv (icons/lobby-w-open-inv.svg) | `invite` (Icons/*.txt, aynı geometri) | var | `Icons/v4/lobby-w-open-inv.txt` + `Icons/v4/svg/lobby-w-open-inv.svg`; GalaxyIcon çizer |
| `lobby-z-ini` | viewBox 0 0 26 20, stroke None; lobby.z.ini[{{ $index }}] (icons/lobby-z-ini.svg) | — | eksik | `Icons/v4/lobby-z-ini.txt` + `Icons/v4/svg/lobby-z-ini.svg`; GalaxyIcon çizer |
| `lobby-z-invite` | viewBox 0 0 24 24, stroke 2.4; lobby.z.invite[{{ $index }}] (icons/lobby-z-invite.svg) | — | eksik | `Icons/v4/lobby-z-invite.txt` + `Icons/v4/svg/lobby-z-invite.svg`; GalaxyIcon çizer |
| `lobby-z-add-bot` | viewBox 0 0 24 24, stroke 2.2; lobby.z.addBot[{{ $index }}] (icons/lobby-z-add-bot.svg) | `bot` (Icons/*.txt, aynı geometri) | var | `Icons/v4/lobby-z-add-bot.txt` + `Icons/v4/svg/lobby-z-add-bot.svg`; GalaxyIcon çizer |
| `wait-summary-prize` | viewBox 0 0 24 24, stroke 2; wait.summary.prize (icons/wait-summary-prize.svg) | `trophy` (Icons/*.txt, aynı geometri) | var | `Icons/v4/wait-summary-prize.txt` + `Icons/v4/svg/wait-summary-prize.svg`; GalaxyIcon çizer |
| `lobby-iv-close` | viewBox 0 0 24 24, stroke 2.4; lobby.iv.close (icons/lobby-iv-close.svg) | — | eksik | `Icons/v4/lobby-iv-close.txt` + `Icons/v4/svg/lobby-iv-close.svg`; GalaxyIcon çizer |
| `lobby-w-share` | viewBox 0 0 24 24, stroke 2.2; lobby.w.share#2 (icons/lobby-w-share.svg) | `info` (Icons/*.txt, aynı geometri) | var | `Icons/v4/lobby-w-share.txt` + `Icons/v4/svg/lobby-w-share.svg`; GalaxyIcon çizer |
| `lobby-cf-toggle-rules` | viewBox 0 0 24 24, stroke 2.2; lobby.cf.toggleRules (icons/lobby-cf-toggle-rules.svg) | `sliders` (Icons/*.txt, aynı geometri) | var | `Icons/v4/lobby-cf-toggle-rules.txt` + `Icons/v4/svg/lobby-cf-toggle-rules.svg`; GalaxyIcon çizer |
| `icon-47` | viewBox 0 0 24 24, stroke 2.2; usedBy boş (icons/icon-47.svg) | `friends, table-full` (Icons/*.txt, aynı geometri) | var | `Icons/v4/icon-47.txt` + `Icons/v4/svg/icon-47.svg`; GalaxyIcon çizer |
| `icon-48` | viewBox 0 0 24 24, stroke 2.2; usedBy boş (icons/icon-48.svg) | `table-closed` (Icons/*.txt, aynı geometri) | var | `Icons/v4/icon-48.txt` + `Icons/v4/svg/icon-48.svg`; GalaxyIcon çizer |
| `lobby-d-copy` | viewBox 0 0 24 24, stroke 2.2; lobby.d.copy (icons/lobby-d-copy.svg) | — | eksik | `Icons/v4/lobby-d-copy.txt` + `Icons/v4/svg/lobby-d-copy.svg`; GalaxyIcon çizer |
| `icon-50` | viewBox 0 0 40 40, stroke None; usedBy boş (icons/icon-50.svg) | — | eksik | `Icons/v4/icon-50.txt` + `Icons/v4/svg/icon-50.svg`; GalaxyIcon çizemez: <defs>, <g>, circle fill gradyan, kök fill gradyan, path fill gradyan, path stroke gradyan, rect fill gradyan, rect stroke gradyan, rect@transform → SVG içe aktarma |
| `ranked-me-next` | viewBox 0 0 24 24, stroke 2.4; ranked.me.next (icons/ranked-me-next.svg) | — | eksik | `Icons/v4/ranked-me-next.txt` + `Icons/v4/svg/ranked-me-next.svg`; GalaxyIcon çizer |
| `ranked-to-progress` | viewBox 0 0 24 24, stroke 2.4; ranked.toProgress (icons/ranked-to-progress.svg) | — | eksik | `Icons/v4/ranked-to-progress.txt` + `Icons/v4/svg/ranked-to-progress.svg`; GalaxyIcon çizer |
| `ranked-m-pick` | viewBox 0 0 24 24, stroke None; ranked.m.pick[{{ $index }}] (icons/ranked-m-pick.svg) | — | eksik | `Icons/v4/ranked-m-pick.txt` + `Icons/v4/svg/ranked-m-pick.svg`; GalaxyIcon çizer |
| `ranked-table-sub` | viewBox 0 0 24 24, stroke 2.4; ranked.table.sub (icons/ranked-table-sub.svg) | — | eksik | `Icons/v4/ranked-table-sub.txt` + `Icons/v4/svg/ranked-table-sub.svg`; GalaxyIcon çizer |
| `ranked-range-short` | viewBox 0 0 24 24, stroke 2.4; ranked.range.short[{{ $index }}] (icons/ranked-range-short.svg) | — | eksik | `Icons/v4/ranked-range-short.txt` + `Icons/v4/svg/ranked-range-short.svg`; GalaxyIcon çizer |
| `ranked-close-invite` | viewBox 0 0 24 24, stroke 2.4; ranked.closeInvite (icons/ranked-close-invite.svg) | — | eksik | `Icons/v4/ranked-close-invite.txt` + `Icons/v4/svg/ranked-close-invite.svg`; GalaxyIcon çizer |
| `table-open-chat` | viewBox 0 0 24 24, stroke 2; table.openChat (icons/table-open-chat.svg) | `table-chat` (Icons/*.txt, aynı geometri) | var | `Icons/v4/table-open-chat.txt` + `Icons/v4/svg/table-open-chat.svg`; GalaxyIcon çizer |
| `table-open-gift-any` | viewBox 0 0 24 24, stroke 2; table.openGiftAny, table.closeGift (icons/table-open-gift-any.svg) | `daily, table-gift` (Icons/*.txt, aynı geometri) | var | `Icons/v4/table-open-gift-any.txt` + `Icons/v4/svg/table-open-gift-any.svg`; GalaxyIcon çizer |
| `table-do-undo` | viewBox 0 0 24 24, stroke 2.4; table.doUndo (icons/table-do-undo.svg) | — | eksik | `Icons/v4/table-do-undo.txt` + `Icons/v4/svg/table-do-undo.svg`; GalaxyIcon çizer |
| `table-do-return` | viewBox 0 0 24 24, stroke 2.4; table.doReturn (icons/table-do-return.svg) | — | eksik | `Icons/v4/table-do-return.txt` + `Icons/v4/svg/table-do-return.svg`; GalaxyIcon çizer |
| `table-do-bomb` | viewBox 0 0 24 24, stroke None; table.doBomb (icons/table-do-bomb.svg) | — | eksik | `Icons/v4/table-do-bomb.txt` + `Icons/v4/svg/table-do-bomb.svg`; GalaxyIcon çizer |
| `table-t-indicator` | viewBox 0 0 18 40, stroke 2.4; table.t.indicator#2 (icons/table-t-indicator.svg) | — | eksik | `Icons/v4/table-t-indicator.txt` + `Icons/v4/svg/table-t-indicator.svg`; GalaxyIcon çizer |
| `table-dh-roll` | viewBox 0 0 26 26, stroke None; table.dh.roll (icons/table-dh-roll.svg) | — | eksik | `Icons/v4/table-dh-roll.txt` + `Icons/v4/svg/table-dh-roll.svg`; GalaxyIcon çizer |
| `table-close-chat` | viewBox 0 0 24 24, stroke 2; table.closeChat (icons/table-close-chat.svg) | — | eksik | `Icons/v4/table-close-chat.txt` + `Icons/v4/svg/table-close-chat.svg`; GalaxyIcon çizer |
| `table-send-draft` | viewBox 0 0 24 24, stroke 2.4; table.sendDraft (icons/table-send-draft.svg) | — | eksik | `Icons/v4/table-send-draft.txt` + `Icons/v4/svg/table-send-draft.svg`; GalaxyIcon çizer |
| `table-v-pick` | viewBox 0 0 96 62, stroke 1.5; table.v.pick[{{ $index }}] (icons/table-v-pick.svg) | — | eksik | `Icons/v4/table-v-pick.txt` + `Icons/v4/svg/table-v-pick.svg`; GalaxyIcon çizemez: <g> → SVG içe aktarma |
| `icon-67` | viewBox 0 0 96 62, stroke 1.5; usedBy boş (icons/icon-67.svg) | — | eksik | `Icons/v4/icon-67.txt` + `Icons/v4/svg/icon-67.svg`; GalaxyIcon çizemez: <g> → SVG içe aktarma |
| `table-t-in-use` | viewBox 0 0 60 100, stroke 15; table.t.inUse[{{ $index }}] (icons/table-t-in-use.svg) | — | eksik | `Icons/v4/table-t-in-use.txt` + `Icons/v4/svg/table-t-in-use.svg`; GalaxyIcon çizer |
| `icon-69` | viewBox 0 0 76 62, stroke 6; usedBy boş (icons/icon-69.svg) | — | eksik | `Icons/v4/icon-69.txt` + `Icons/v4/svg/icon-69.svg`; GalaxyIcon çizemez: style:animation → SVG içe aktarma |
| `Icons/back.txt` | — (69 paket ikonundan hiçbirinin geometrisi aynı değil) | `back` (Icons/back.txt) | pakette yok | dokunulmadı; GalaxyIcon çağıranlar bu adı kullanıyor (Faz 2 v4 ikonlarına geçer) |
| `Icons/eye.txt` | — (69 paket ikonundan hiçbirinin geometrisi aynı değil) | `eye` (Icons/eye.txt) | pakette yok | dokunulmadı; GalaxyIcon çağıranlar bu adı kullanıyor (Faz 2 v4 ikonlarına geçer) |
| `Icons/table-tip.txt` | — (69 paket ikonundan hiçbirinin geometrisi aynı değil) | `table-tip` (Icons/table-tip.txt) | pakette yok | dokunulmadı; GalaxyIcon çağıranlar bu adı kullanıyor (Faz 2 v4 ikonlarına geçer) |
| `Icons/table-wifi-off.txt` | — (69 paket ikonundan hiçbirinin geometrisi aynı değil) | `table-wifi-off` (Icons/table-wifi-off.txt) | pakette yok | dokunulmadı; GalaxyIcon çağıranlar bu adı kullanıyor (Faz 2 v4 ikonlarına geçer) |

## Lig rozetleri

Dosya adı `rank/<lig>[_<I|II|III>]_<24|52|160>.svg` · `medal_<lig>_<16|24>.svg` (ranks.json files). Kademe taşı sayısı g101-rank.js `emblem()`: n = 4 − kademe.

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| `bronze_160` | rozet bronze, 160 px (assets/rank/bronze_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_160.png` + `Galaxy101/Rank/bronze_160@2x.png` + `Galaxy101/Rank/svg/bronze_160.svg` |
| `bronze_24` | rozet bronze, 24 px (assets/rank/bronze_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_24.png` + `Galaxy101/Rank/bronze_24@2x.png` + `Galaxy101/Rank/svg/bronze_24.svg` |
| `bronze_52` | rozet bronze, 52 px (assets/rank/bronze_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_52.png` + `Galaxy101/Rank/bronze_52@2x.png` + `Galaxy101/Rank/svg/bronze_52.svg` |
| `bronze_I_160` | rozet bronze I, 160 px, kademe taşı 3 (assets/rank/bronze_I_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_I_160.png` + `Galaxy101/Rank/bronze_I_160@2x.png` + `Galaxy101/Rank/svg/bronze_I_160.svg` |
| `bronze_I_24` | rozet bronze I, 24 px, kademe taşı 3 (assets/rank/bronze_I_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_I_24.png` + `Galaxy101/Rank/bronze_I_24@2x.png` + `Galaxy101/Rank/svg/bronze_I_24.svg` |
| `bronze_I_52` | rozet bronze I, 52 px, kademe taşı 3 (assets/rank/bronze_I_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_I_52.png` + `Galaxy101/Rank/bronze_I_52@2x.png` + `Galaxy101/Rank/svg/bronze_I_52.svg` |
| `bronze_II_160` | rozet bronze II, 160 px, kademe taşı 2 (assets/rank/bronze_II_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_II_160.png` + `Galaxy101/Rank/bronze_II_160@2x.png` + `Galaxy101/Rank/svg/bronze_II_160.svg` |
| `bronze_II_24` | rozet bronze II, 24 px, kademe taşı 2 (assets/rank/bronze_II_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_II_24.png` + `Galaxy101/Rank/bronze_II_24@2x.png` + `Galaxy101/Rank/svg/bronze_II_24.svg` |
| `bronze_II_52` | rozet bronze II, 52 px, kademe taşı 2 (assets/rank/bronze_II_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_II_52.png` + `Galaxy101/Rank/bronze_II_52@2x.png` + `Galaxy101/Rank/svg/bronze_II_52.svg` |
| `bronze_III_160` | rozet bronze III, 160 px, kademe taşı 1 (assets/rank/bronze_III_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_III_160.png` + `Galaxy101/Rank/bronze_III_160@2x.png` + `Galaxy101/Rank/svg/bronze_III_160.svg` |
| `bronze_III_24` | rozet bronze III, 24 px, kademe taşı 1 (assets/rank/bronze_III_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_III_24.png` + `Galaxy101/Rank/bronze_III_24@2x.png` + `Galaxy101/Rank/svg/bronze_III_24.svg` |
| `bronze_III_52` | rozet bronze III, 52 px, kademe taşı 1 (assets/rank/bronze_III_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/bronze_III_52.png` + `Galaxy101/Rank/bronze_III_52@2x.png` + `Galaxy101/Rank/svg/bronze_III_52.svg` |
| `diamond_160` | rozet diamond, 160 px (assets/rank/diamond_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_160.png` + `Galaxy101/Rank/diamond_160@2x.png` + `Galaxy101/Rank/svg/diamond_160.svg` |
| `diamond_24` | rozet diamond, 24 px (assets/rank/diamond_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_24.png` + `Galaxy101/Rank/diamond_24@2x.png` + `Galaxy101/Rank/svg/diamond_24.svg` |
| `diamond_52` | rozet diamond, 52 px (assets/rank/diamond_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_52.png` + `Galaxy101/Rank/diamond_52@2x.png` + `Galaxy101/Rank/svg/diamond_52.svg` |
| `diamond_I_160` | rozet diamond I, 160 px, kademe taşı 3 (assets/rank/diamond_I_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_I_160.png` + `Galaxy101/Rank/diamond_I_160@2x.png` + `Galaxy101/Rank/svg/diamond_I_160.svg` |
| `diamond_I_24` | rozet diamond I, 24 px, kademe taşı 3 (assets/rank/diamond_I_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_I_24.png` + `Galaxy101/Rank/diamond_I_24@2x.png` + `Galaxy101/Rank/svg/diamond_I_24.svg` |
| `diamond_I_52` | rozet diamond I, 52 px, kademe taşı 3 (assets/rank/diamond_I_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_I_52.png` + `Galaxy101/Rank/diamond_I_52@2x.png` + `Galaxy101/Rank/svg/diamond_I_52.svg` |
| `diamond_II_160` | rozet diamond II, 160 px, kademe taşı 2 (assets/rank/diamond_II_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_II_160.png` + `Galaxy101/Rank/diamond_II_160@2x.png` + `Galaxy101/Rank/svg/diamond_II_160.svg` |
| `diamond_II_24` | rozet diamond II, 24 px, kademe taşı 2 (assets/rank/diamond_II_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_II_24.png` + `Galaxy101/Rank/diamond_II_24@2x.png` + `Galaxy101/Rank/svg/diamond_II_24.svg` |
| `diamond_II_52` | rozet diamond II, 52 px, kademe taşı 2 (assets/rank/diamond_II_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_II_52.png` + `Galaxy101/Rank/diamond_II_52@2x.png` + `Galaxy101/Rank/svg/diamond_II_52.svg` |
| `diamond_III_160` | rozet diamond III, 160 px, kademe taşı 1 (assets/rank/diamond_III_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_III_160.png` + `Galaxy101/Rank/diamond_III_160@2x.png` + `Galaxy101/Rank/svg/diamond_III_160.svg` |
| `diamond_III_24` | rozet diamond III, 24 px, kademe taşı 1 (assets/rank/diamond_III_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_III_24.png` + `Galaxy101/Rank/diamond_III_24@2x.png` + `Galaxy101/Rank/svg/diamond_III_24.svg` |
| `diamond_III_52` | rozet diamond III, 52 px, kademe taşı 1 (assets/rank/diamond_III_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/diamond_III_52.png` + `Galaxy101/Rank/diamond_III_52@2x.png` + `Galaxy101/Rank/svg/diamond_III_52.svg` |
| `galaxy_160` | rozet galaxy, 160 px (assets/rank/galaxy_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/galaxy_160.png` + `Galaxy101/Rank/galaxy_160@2x.png` + `Galaxy101/Rank/svg/galaxy_160.svg` |
| `galaxy_24` | rozet galaxy, 24 px (assets/rank/galaxy_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/galaxy_24.png` + `Galaxy101/Rank/galaxy_24@2x.png` + `Galaxy101/Rank/svg/galaxy_24.svg` |
| `galaxy_52` | rozet galaxy, 52 px (assets/rank/galaxy_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/galaxy_52.png` + `Galaxy101/Rank/galaxy_52@2x.png` + `Galaxy101/Rank/svg/galaxy_52.svg` |
| `gold_160` | rozet gold, 160 px (assets/rank/gold_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_160.png` + `Galaxy101/Rank/gold_160@2x.png` + `Galaxy101/Rank/svg/gold_160.svg` |
| `gold_24` | rozet gold, 24 px (assets/rank/gold_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_24.png` + `Galaxy101/Rank/gold_24@2x.png` + `Galaxy101/Rank/svg/gold_24.svg` |
| `gold_52` | rozet gold, 52 px (assets/rank/gold_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_52.png` + `Galaxy101/Rank/gold_52@2x.png` + `Galaxy101/Rank/svg/gold_52.svg` |
| `gold_I_160` | rozet gold I, 160 px, kademe taşı 3 (assets/rank/gold_I_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_I_160.png` + `Galaxy101/Rank/gold_I_160@2x.png` + `Galaxy101/Rank/svg/gold_I_160.svg` |
| `gold_I_24` | rozet gold I, 24 px, kademe taşı 3 (assets/rank/gold_I_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_I_24.png` + `Galaxy101/Rank/gold_I_24@2x.png` + `Galaxy101/Rank/svg/gold_I_24.svg` |
| `gold_I_52` | rozet gold I, 52 px, kademe taşı 3 (assets/rank/gold_I_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_I_52.png` + `Galaxy101/Rank/gold_I_52@2x.png` + `Galaxy101/Rank/svg/gold_I_52.svg` |
| `gold_II_160` | rozet gold II, 160 px, kademe taşı 2 (assets/rank/gold_II_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_II_160.png` + `Galaxy101/Rank/gold_II_160@2x.png` + `Galaxy101/Rank/svg/gold_II_160.svg` |
| `gold_II_24` | rozet gold II, 24 px, kademe taşı 2 (assets/rank/gold_II_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_II_24.png` + `Galaxy101/Rank/gold_II_24@2x.png` + `Galaxy101/Rank/svg/gold_II_24.svg` |
| `gold_II_52` | rozet gold II, 52 px, kademe taşı 2 (assets/rank/gold_II_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_II_52.png` + `Galaxy101/Rank/gold_II_52@2x.png` + `Galaxy101/Rank/svg/gold_II_52.svg` |
| `gold_III_160` | rozet gold III, 160 px, kademe taşı 1 (assets/rank/gold_III_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_III_160.png` + `Galaxy101/Rank/gold_III_160@2x.png` + `Galaxy101/Rank/svg/gold_III_160.svg` |
| `gold_III_24` | rozet gold III, 24 px, kademe taşı 1 (assets/rank/gold_III_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_III_24.png` + `Galaxy101/Rank/gold_III_24@2x.png` + `Galaxy101/Rank/svg/gold_III_24.svg` |
| `gold_III_52` | rozet gold III, 52 px, kademe taşı 1 (assets/rank/gold_III_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/gold_III_52.png` + `Galaxy101/Rank/gold_III_52@2x.png` + `Galaxy101/Rank/svg/gold_III_52.svg` |
| `master_160` | rozet master, 160 px (assets/rank/master_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_160.png` + `Galaxy101/Rank/master_160@2x.png` + `Galaxy101/Rank/svg/master_160.svg` |
| `master_24` | rozet master, 24 px (assets/rank/master_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_24.png` + `Galaxy101/Rank/master_24@2x.png` + `Galaxy101/Rank/svg/master_24.svg` |
| `master_52` | rozet master, 52 px (assets/rank/master_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_52.png` + `Galaxy101/Rank/master_52@2x.png` + `Galaxy101/Rank/svg/master_52.svg` |
| `master_I_160` | rozet master I, 160 px, kademe taşı 3 (assets/rank/master_I_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_I_160.png` + `Galaxy101/Rank/master_I_160@2x.png` + `Galaxy101/Rank/svg/master_I_160.svg` |
| `master_I_24` | rozet master I, 24 px, kademe taşı 3 (assets/rank/master_I_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_I_24.png` + `Galaxy101/Rank/master_I_24@2x.png` + `Galaxy101/Rank/svg/master_I_24.svg` |
| `master_I_52` | rozet master I, 52 px, kademe taşı 3 (assets/rank/master_I_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_I_52.png` + `Galaxy101/Rank/master_I_52@2x.png` + `Galaxy101/Rank/svg/master_I_52.svg` |
| `master_II_160` | rozet master II, 160 px, kademe taşı 2 (assets/rank/master_II_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_II_160.png` + `Galaxy101/Rank/master_II_160@2x.png` + `Galaxy101/Rank/svg/master_II_160.svg` |
| `master_II_24` | rozet master II, 24 px, kademe taşı 2 (assets/rank/master_II_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_II_24.png` + `Galaxy101/Rank/master_II_24@2x.png` + `Galaxy101/Rank/svg/master_II_24.svg` |
| `master_II_52` | rozet master II, 52 px, kademe taşı 2 (assets/rank/master_II_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_II_52.png` + `Galaxy101/Rank/master_II_52@2x.png` + `Galaxy101/Rank/svg/master_II_52.svg` |
| `master_III_160` | rozet master III, 160 px, kademe taşı 1 (assets/rank/master_III_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_III_160.png` + `Galaxy101/Rank/master_III_160@2x.png` + `Galaxy101/Rank/svg/master_III_160.svg` |
| `master_III_24` | rozet master III, 24 px, kademe taşı 1 (assets/rank/master_III_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_III_24.png` + `Galaxy101/Rank/master_III_24@2x.png` + `Galaxy101/Rank/svg/master_III_24.svg` |
| `master_III_52` | rozet master III, 52 px, kademe taşı 1 (assets/rank/master_III_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/master_III_52.png` + `Galaxy101/Rank/master_III_52@2x.png` + `Galaxy101/Rank/svg/master_III_52.svg` |
| `medal_bronze_16` | madalya bronze, 16 px (assets/rank/medal_bronze_16.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_bronze_16.png` + `Galaxy101/Rank/medal_bronze_16@2x.png` + `Galaxy101/Rank/svg/medal_bronze_16.svg` |
| `medal_bronze_24` | madalya bronze, 24 px (assets/rank/medal_bronze_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_bronze_24.png` + `Galaxy101/Rank/medal_bronze_24@2x.png` + `Galaxy101/Rank/svg/medal_bronze_24.svg` |
| `medal_diamond_16` | madalya diamond, 16 px (assets/rank/medal_diamond_16.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_diamond_16.png` + `Galaxy101/Rank/medal_diamond_16@2x.png` + `Galaxy101/Rank/svg/medal_diamond_16.svg` |
| `medal_diamond_24` | madalya diamond, 24 px (assets/rank/medal_diamond_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_diamond_24.png` + `Galaxy101/Rank/medal_diamond_24@2x.png` + `Galaxy101/Rank/svg/medal_diamond_24.svg` |
| `medal_galaxy_16` | madalya galaxy, 16 px (assets/rank/medal_galaxy_16.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_galaxy_16.png` + `Galaxy101/Rank/medal_galaxy_16@2x.png` + `Galaxy101/Rank/svg/medal_galaxy_16.svg` |
| `medal_galaxy_24` | madalya galaxy, 24 px (assets/rank/medal_galaxy_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_galaxy_24.png` + `Galaxy101/Rank/medal_galaxy_24@2x.png` + `Galaxy101/Rank/svg/medal_galaxy_24.svg` |
| `medal_gold_16` | madalya gold, 16 px (assets/rank/medal_gold_16.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_gold_16.png` + `Galaxy101/Rank/medal_gold_16@2x.png` + `Galaxy101/Rank/svg/medal_gold_16.svg` |
| `medal_gold_24` | madalya gold, 24 px (assets/rank/medal_gold_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_gold_24.png` + `Galaxy101/Rank/medal_gold_24@2x.png` + `Galaxy101/Rank/svg/medal_gold_24.svg` |
| `medal_master_16` | madalya master, 16 px (assets/rank/medal_master_16.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_master_16.png` + `Galaxy101/Rank/medal_master_16@2x.png` + `Galaxy101/Rank/svg/medal_master_16.svg` |
| `medal_master_24` | madalya master, 24 px (assets/rank/medal_master_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_master_24.png` + `Galaxy101/Rank/medal_master_24@2x.png` + `Galaxy101/Rank/svg/medal_master_24.svg` |
| `medal_silver_16` | madalya silver, 16 px (assets/rank/medal_silver_16.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_silver_16.png` + `Galaxy101/Rank/medal_silver_16@2x.png` + `Galaxy101/Rank/svg/medal_silver_16.svg` |
| `medal_silver_24` | madalya silver, 24 px (assets/rank/medal_silver_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/medal_silver_24.png` + `Galaxy101/Rank/medal_silver_24@2x.png` + `Galaxy101/Rank/svg/medal_silver_24.svg` |
| `silver_160` | rozet silver, 160 px (assets/rank/silver_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_160.png` + `Galaxy101/Rank/silver_160@2x.png` + `Galaxy101/Rank/svg/silver_160.svg` |
| `silver_24` | rozet silver, 24 px (assets/rank/silver_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_24.png` + `Galaxy101/Rank/silver_24@2x.png` + `Galaxy101/Rank/svg/silver_24.svg` |
| `silver_52` | rozet silver, 52 px (assets/rank/silver_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_52.png` + `Galaxy101/Rank/silver_52@2x.png` + `Galaxy101/Rank/svg/silver_52.svg` |
| `silver_I_160` | rozet silver I, 160 px, kademe taşı 3 (assets/rank/silver_I_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_I_160.png` + `Galaxy101/Rank/silver_I_160@2x.png` + `Galaxy101/Rank/svg/silver_I_160.svg` |
| `silver_I_24` | rozet silver I, 24 px, kademe taşı 3 (assets/rank/silver_I_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_I_24.png` + `Galaxy101/Rank/silver_I_24@2x.png` + `Galaxy101/Rank/svg/silver_I_24.svg` |
| `silver_I_52` | rozet silver I, 52 px, kademe taşı 3 (assets/rank/silver_I_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_I_52.png` + `Galaxy101/Rank/silver_I_52@2x.png` + `Galaxy101/Rank/svg/silver_I_52.svg` |
| `silver_II_160` | rozet silver II, 160 px, kademe taşı 2 (assets/rank/silver_II_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_II_160.png` + `Galaxy101/Rank/silver_II_160@2x.png` + `Galaxy101/Rank/svg/silver_II_160.svg` |
| `silver_II_24` | rozet silver II, 24 px, kademe taşı 2 (assets/rank/silver_II_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_II_24.png` + `Galaxy101/Rank/silver_II_24@2x.png` + `Galaxy101/Rank/svg/silver_II_24.svg` |
| `silver_II_52` | rozet silver II, 52 px, kademe taşı 2 (assets/rank/silver_II_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_II_52.png` + `Galaxy101/Rank/silver_II_52@2x.png` + `Galaxy101/Rank/svg/silver_II_52.svg` |
| `silver_III_160` | rozet silver III, 160 px, kademe taşı 1 (assets/rank/silver_III_160.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_III_160.png` + `Galaxy101/Rank/silver_III_160@2x.png` + `Galaxy101/Rank/svg/silver_III_160.svg` |
| `silver_III_24` | rozet silver III, 24 px, kademe taşı 1 (assets/rank/silver_III_24.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_III_24.png` + `Galaxy101/Rank/silver_III_24@2x.png` + `Galaxy101/Rank/svg/silver_III_24.svg` |
| `silver_III_52` | rozet silver III, 52 px, kademe taşı 1 (assets/rank/silver_III_52.svg; ranks.json) | — (lig işareti kodla çizilen döndürülmüş kare: PanelUi.cs, MenuView.Main.cs rank-diamond) | eksik | `Galaxy101/Rank/silver_III_52.png` + `Galaxy101/Rank/silver_III_52@2x.png` + `Galaxy101/Rank/svg/silver_III_52.svg` |

## İmleçler

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| `default` | 32 px, sıcak nokta [4, 3] (assets/cursors/default.svg; cursors.json hotspots.default) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/default.png` (Texture Type Cursor) + `Galaxy101/Cursors/default@2x.png` sıcak nokta [8, 6]; Cursors/cursors.json |
| `pointer` | 32 px, sıcak nokta [4, 3] (assets/cursors/pointer.svg; cursors.json hotspots.pointer) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/pointer.png` (Texture Type Cursor) + `Galaxy101/Cursors/pointer@2x.png` sıcak nokta [8, 6]; Cursors/cursors.json |
| `grab` | 32 px, sıcak nokta [16, 15] (assets/cursors/grab.svg; cursors.json hotspots.grab) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/grab.png` (Texture Type Cursor) + `Galaxy101/Cursors/grab@2x.png` sıcak nokta [32, 30]; Cursors/cursors.json |
| `grabbing` | 32 px, sıcak nokta [16, 16] (assets/cursors/grabbing.svg; cursors.json hotspots.grabbing) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/grabbing.png` (Texture Type Cursor) + `Galaxy101/Cursors/grabbing@2x.png` sıcak nokta [32, 32]; Cursors/cursors.json |
| `drop` | 32 px, sıcak nokta [16, 16] (assets/cursors/drop.svg; cursors.json hotspots.drop) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/drop.png` (Texture Type Cursor) + `Galaxy101/Cursors/drop@2x.png` sıcak nokta [32, 32]; Cursors/cursors.json |
| `text` | 32 px, sıcak nokta [16, 16] (assets/cursors/text.svg; cursors.json hotspots.text) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/text.png` (Texture Type Cursor) + `Galaxy101/Cursors/text@2x.png` sıcak nokta [32, 32]; Cursors/cursors.json |
| `no` | 32 px, sıcak nokta [4, 3] (assets/cursors/no.svg; cursors.json hotspots.no) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/no.png` (Texture Type Cursor) + `Galaxy101/Cursors/no@2x.png` sıcak nokta [8, 6]; Cursors/cursors.json |
| `wait` | 32 px, sıcak nokta [16, 16] (assets/cursors/wait.svg; cursors.json hotspots.wait) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/wait.png` (Texture Type Cursor) + `Galaxy101/Cursors/wait@2x.png` sıcak nokta [32, 32]; Cursors/cursors.json |
| `progress` | 32 px, sıcak nokta [4, 3] (assets/cursors/progress.svg; cursors.json hotspots.progress) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/progress.png` (Texture Type Cursor) + `Galaxy101/Cursors/progress@2x.png` sıcak nokta [8, 6]; Cursors/cursors.json |
| `ew` | 32 px, sıcak nokta [16, 16] (assets/cursors/ew.svg; cursors.json hotspots.ew) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/ew.png` (Texture Type Cursor) + `Galaxy101/Cursors/ew@2x.png` sıcak nokta [32, 32]; Cursors/cursors.json |
| `ns` | 32 px, sıcak nokta [16, 16] (assets/cursors/ns.svg; cursors.json hotspots.ns) | — (projede özel imleç yok; işletim sistemi imleci) | eksik | `Galaxy101/Cursors/ns.png` (Texture Type Cursor) + `Galaxy101/Cursors/ns@2x.png` sıcak nokta [32, 32]; Cursors/cursors.json |

## Ses

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| `ui.tap` | ui · öncelik 1 · poly 2 · 45 ms · sfx/ui_tap.wav — İmleci pointer olan her öğeye basış (düğme, sekme, kart) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/ui_tap.wav` (0.645 sn) + Audio/sound_events.json events.ui.tap |
| `ui.open` | ui · öncelik 2 · poly 1 · 160 ms · sfx/ui_panel_open.wav — Panel/diyalog açılır (masa: hediye, sohbet, ayarlar, kalk onayı) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/ui_panel_open.wav` (0.76 sn) + Audio/sound_events.json events.ui.open |
| `ui.close` | ui · öncelik 1 · poly 1 · 120 ms · sfx/ui_panel_close.wav — Panel/diyalog kapanır (events.json) | — | eksik | `Galaxy101/Audio/Sfx/ui_panel_close.wav` (0.72 sn) + Audio/sound_events.json events.ui.close |
| `ui.screen` | ui · öncelik 2 · poly 1 · 260 ms · sfx/ui_screen.wav — Ekran geçişi (açılış → giriş → menü → lobi → masa) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/ui_screen.wav` (0.86 sn) + Audio/sound_events.json events.ui.screen |
| `ui.error` | ui · öncelik 3 · poly 1 · 180 ms · sfx/ui_error.wav — Hata tostu / kırmızı sistem satırı (protocol ui: toast, shake, inline) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/ui_error.wav` (0.78 sn) + Audio/sound_events.json events.ui.error |
| `ui.reward` | ui · öncelik 3 · poly 1 · 700 ms · sfx/ui_reward_coins.wav — Ödül alındı (günlük ödül, görev, maç kazancı; altın tost) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/ui_reward_coins.wav` (1.3 sn) + Audio/sound_events.json events.ui.reward |
| `turn.mine` | game · öncelik 4 · poly 1 · 420 ms · sfx/turn_mine.wav — turn olayı, seat = ben (dağıtım bitince) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/turn_mine.wav` (1.02 sn) + Audio/sound_events.json events.turn.mine |
| `turn.warn` | game · öncelik 4 · poly 1 · 300 ms · sfx/turn_warn_tick.wav — Tur sayacı uyarı eşiğine girdi (ayar warn, vars. 5 sn) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/turn_warn_tick.wav` (0.9 sn) + Audio/sound_events.json events.turn.warn |
| `count.tick` | game · öncelik 3 · poly 1 · 120 ms · sfx/count_tick.wav — Geri sayım rakamı 3, 2, 1 (events.json) | — | eksik | `Galaxy101/Audio/Sfx/count_tick.wav` (0.72 sn) + Audio/sound_events.json events.count.tick |
| `count.go` | game · öncelik 3 · poly 1 · 380 ms · sfx/count_go.wav — Geri sayım bitti, dağıtım başlıyor (events.json) | — | eksik | `Galaxy101/Audio/Sfx/count_go.wav` (0.98 sn) + Audio/sound_events.json events.count.go |
| `deal.shuffle` | tile · öncelik 3 · poly 1 · 900 ms · sfx/deal_shuffle.wav — dealStart: balyalar diziliyor (events.json) | — | eksik | `Galaxy101/Audio/Sfx/deal_shuffle.wav` (1.5 sn) + Audio/sound_events.json events.deal.shuffle |
| `dice.roll` | tile · öncelik 3 · poly 1 · 1450 ms · sfx/dice_roll.wav — dealDie: zar yuvarlanıyor (1450 ms, 3 sekme) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/dice_roll.wav` (2.05 sn) + Audio/sound_events.json events.dice.roll |
| `deal.indicator` | tile · öncelik 3 · poly 1 · 400 ms · sfx/deal_indicator.wav — dealStep:indicator: gösterge çevrildi (events.json) | — | eksik | `Galaxy101/Audio/Sfx/deal_indicator.wav` (1.0 sn) + Audio/sound_events.json events.deal.indicator |
| `deal.distribute` | tile · öncelik 2 · poly 1 · 2400 ms · sfx/deal_distribute.wav — dealStep:distribute: balyalar oyunculara kayıyor (events.json) | — | eksik | `Galaxy101/Audio/Sfx/deal_distribute.wav` (3.0 sn) + Audio/sound_events.json events.deal.distribute |
| `tile.draw` | tile · öncelik 2 · poly 2 · 90 ms · sfx/tile_draw.wav — game.drew (ben ya da rakip; rakipte %70 ses) (events.json) | draw.wav (TableAudio Draw) — Assets/Okey101/Art/Audio, kayıttan kesilmiş domino sesi, tek kanal, ses 0.38 | farklı | `Galaxy101/Audio/Sfx/tile_draw.wav` (0.69 sn) + Audio/sound_events.json events.tile.draw |
| `tile.discard` | tile · öncelik 2 · poly 2 · 110 ms · sfx/tile_discard.wav — game.discarded (taş halkaya/yığına iner) (events.json) | discard.wav (TableAudio Discard) — Assets/Okey101/Art/Audio, kayıttan kesilmiş domino sesi, tek kanal, ses 0.38 | farklı | `Galaxy101/Audio/Sfx/tile_discard.wav` (0.71 sn) + Audio/sound_events.json events.tile.discard |
| `tile.slide` | tile · öncelik 1 · poly 2 · 60 ms · sfx/tile_slide.wav — Istakada taş yer değiştirdi (yerel) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/tile_slide.wav` (0.66 sn) + Audio/sound_events.json events.tile.slide |
| `meld.lay` | tile · öncelik 3 · poly 1 · 260 ms · sfx/meld_lay.wav — game.opened / game.laid (perler masaya) (events.json) | place.wav (TableAudio GroupsOpened) — Assets/Okey101/Art/Audio, kayıttan kesilmiş domino sesi, tek kanal, ses 0.38 | farklı | `Galaxy101/Audio/Sfx/meld_lay.wav` (0.86 sn) + Audio/sound_events.json events.meld.lay |
| `meld.add` | tile · öncelik 2 · poly 1 · 120 ms · sfx/meld_add.wav — game.added (işleme) (events.json) | place.wav (TableAudio GroupAdded/GroupExtended) — Assets/Okey101/Art/Audio, kayıttan kesilmiş domino sesi, tek kanal, ses 0.38 | farklı | `Galaxy101/Audio/Sfx/meld_add.wav` (0.72 sn) + Audio/sound_events.json events.meld.add |
| `okey.swap` | tile · öncelik 3 · poly 1 · 380 ms · sfx/okey_swap.wav — game.okeySwapped (events.json) | place.wav (TableAudio JokerSwapped) — Assets/Okey101/Art/Audio, kayıttan kesilmiş domino sesi, tek kanal, ses 0.38 | farklı | `Galaxy101/Audio/Sfx/okey_swap.wav` (0.98 sn) + Audio/sound_events.json events.okey.swap |
| `penalty` | game · öncelik 5 · poly 1 · 520 ms · sfx/penalty.wav — game.penalty (R-40…R-44) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/penalty.wav` (1.12 sn) + Audio/sound_events.json events.penalty |
| `finish` | game · öncelik 6 · poly 1 · 1200 ms · sfx/finish_fanfare.wav — game.finished (biri bitirdi) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/finish_fanfare.wav` (1.8 sn) + Audio/sound_events.json events.finish |
| `finish.nuke` | game · öncelik 6 · poly 1 · 1750 ms · sfx/finish_nuke.wav — Okeyle / elden bitiş fırlatması (nuke) iniş anı (events.json) | — | eksik | `Galaxy101/Audio/Sfx/finish_nuke.wav` (2.35 sn) + Audio/sound_events.json events.finish.nuke |
| `hand.end` | game · öncelik 4 · poly 1 · 600 ms · sfx/hand_end.wav — game.handEnd: el sonu tablosu açıldı (events.json) | — | eksik | `Galaxy101/Audio/Sfx/hand_end.wav` (1.2 sn) + Audio/sound_events.json events.hand.end |
| `gift.send` | chat · öncelik 2 · poly 2 · 400 ms · sfx/gift_whoosh.wav — gift: ikram/hediye uçuşu başladı (events.json) | — | eksik | `Galaxy101/Audio/Sfx/gift_whoosh.wav` (1.0 sn) + Audio/sound_events.json events.gift.send |
| `gift.receive` | chat · öncelik 3 · poly 1 · 600 ms · sfx/gift_sparkle.wav — Sana ikram geldi (sağ üst kart) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/gift_sparkle.wav` (1.2 sn) + Audio/sound_events.json events.gift.receive |
| `gift.rocket` | chat · öncelik 3 · poly 1 · 5600 ms · sfx/gift_rocket.wav — Roket uçuşu (5,6 sn) (events.json) | — | eksik | `Galaxy101/Audio/Sfx/gift_rocket.wav` (6.2 sn) + Audio/sound_events.json events.gift.rocket |
| `chat.message` | chat · öncelik 1 · poly 1 · 140 ms · sfx/chat_pop.wav — chat: rakipten mesaj/balon (events.json) | — | eksik | `Galaxy101/Audio/Sfx/chat_pop.wav` (0.74 sn) + Audio/sound_events.json events.chat.message |
| `music.menu` | music/music_menu_loop.wav, döngü 32 sn, açılış, giriş, menü, lobi, dereceli (events.json music) | — (müzik yok) | eksik | `Galaxy101/Audio/Music/music_menu_loop.wav` (32.0 sn) |
| `music.table` | music/music_table_loop.wav, döngü 40 sn, masa (yükleme bitince → maç sonu) (events.json music) | — (müzik yok) | eksik | `Galaxy101/Audio/Music/music_table_loop.wav` (40.0 sn) |
| `ses.ayar.master` | varsayılan 80 (g101-sound.js S(); events.json volume) | content.json settingsDefaults.master = 80 · Table/table_content.json settingsDefaults.master = 80 | var | sound_events.json volume.defaults.master = 80; ayar dosyalarına dokunulmadı (EK-402) |
| `ses.ayar.sfx` | varsayılan 90 (g101-sound.js S(); events.json volume) | content.json settingsDefaults.sfx = 90 · Table/table_content.json settingsDefaults.sfx = 90 | var | sound_events.json volume.defaults.sfx = 90; ayar dosyalarına dokunulmadı (EK-402) |
| `ses.ayar.music` | varsayılan 55 (g101-sound.js S(); events.json volume) | content.json settingsDefaults.music = 60 · Table/table_content.json settingsDefaults.music = 55 | farklı | sound_events.json volume.defaults.music = 55; ayar dosyalarına dokunulmadı (EK-402) |
| `ses.ayar.tileSnd` | varsayılan True (g101-sound.js S(); events.json volume) | content.json settingsDefaults.tileSnd = True · Table/table_content.json settingsDefaults.tileSnd = True | var | sound_events.json volume.defaults.tileSnd = True; ayar dosyalarına dokunulmadı (EK-402) |
| `ses.ayar.chatSnd` | varsayılan True (g101-sound.js S(); events.json volume) | content.json settingsDefaults.chatSnd = — · Table/table_content.json settingsDefaults.chatSnd = True | farklı | sound_events.json volume.defaults.chatSnd = True; ayar dosyalarına dokunulmadı (EK-402) |
| `ses.ayar.notifSnd` | varsayılan True (g101-sound.js S(); events.json volume) | content.json settingsDefaults.notifSnd = True · Table/table_content.json settingsDefaults.notifSnd = — | farklı | sound_events.json volume.defaults.notifSnd = True; ayar dosyalarına dokunulmadı (EK-402) |
| `ses.kural.kanal` | efekt kategorileri sfx, müzik music kaydırıcısı; tile → tileSnd, chat → chatSnd ve notifSnd (g101-sound.js CAT, gainFor) (events.json / README.md / g101-sound.js) | — (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok) | eksik | Audio/sound_events.json channels, toggles |
| `ses.kural.kazanç` | kazanç = master/100 × (müzikse music, değilse sfx)/100; kategori anahtarı kapalıysa 0. Varsayılan master 80, sfx 90, music 55. (events.json / README.md / g101-sound.js) | — (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok) | eksik | Audio/sound_events.json volume.rule |
| `ses.kural.kısma` | prio ≥ 5 (penalty, finish, finish.nuke) çalınca müzik %40'a iner (0,3 sn), 2,4 sn'de geri gelir (events.json / README.md / g101-sound.js) | — (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok) | eksik | Audio/sound_events.json mixing.duck |
| `ses.kural.poly` | aynı olaydan eş zamanlı en çok poly kopya (events.json / README.md / g101-sound.js) | — (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok) | eksik | Audio/sound_events.json events[].poly |
| `ses.kural.geçiş` | menü ↔ masa müziği 1,2 sn geçiş (g101-sound.js: eski 1,2 sn söner, yeni 1,5 sn açılır) (events.json / README.md / g101-sound.js) | — (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok) | eksik | Audio/sound_events.json mixing.crossfadeOutSeconds, fadeInSeconds |
| `ses.kural.eşzaman` | dice.roll 1450 ms, nuke 1750 ms, rocket 5600 ms (anim/ ve protocol/timings.md ile aynı) (events.json / README.md / g101-sound.js) | — (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok) | eksik | Audio/sound_events.json mixing.sync |
| `ses.kural.rakip` | rakibin taş çekişi %70 ses (G101-Masa sfx()) (events.json / README.md / g101-sound.js) | — (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok) | eksik | Audio/sound_events.json volume.opponentDrawVolume |
| `ses.kural.motor` | motor olayı → ses tablosu (sound/README.md) (events.json / README.md / g101-sound.js) | — (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok) | eksik | Audio/sound_events.json engineEvents |
| `ses.kural.ekran` | masada masa müziği, diğer ekranlarda menü müziği; açılış dışındaki her ekran geçişinde ui.screen (events.json / README.md / g101-sound.js) | — (TableAudio yalnız taş sesi; karıştırma, kanal, müzik yok) | eksik | Audio/sound_events.json screenMusic, screenChange |

## Metinler

Paket: 751 tablo + 241 satır içi + 49 hata metni (94 hata kodu; 45 sessiz/resync, metinsiz). Tekil anahtar: 1017 (23 tekrarlı satır içi anahtar #2/#3 aldı; aynı metni gösteren 24 hata kodu oyun metninin anahtarını kullanır). Karşılaştırma: `table.STR.<k>` için masa anahtarı `<k>` (Table/Strings/*.csv); diğerleri için TR+EN metin çifti (strings_tr_en.csv, additional_strings.csv, content/themes/supplemental/table_content.json ve C# `L("tr","en")` / `Text("tr","en")` çağrıları). Menü yerelleştirmesi anahtar değil Türkçe metinle çalıştığı için (LocalizationBridge) metin eşleşmesi kullanıldı. Tüm anahtarlar ve durumları: `docs/v4/hat-d/strings_compare.csv`.

| Grup | Anahtar | var | farklı | eksik (yeni) | boş (pakette bilinçli) |
|---|---|---|---|---|---|
| table | 751 | 580 | 33 | 136 | 2 |
| inline | 241 | 111 | 4 | 126 | 0 |
| error | 25 | 0 | 3 | 22 | 0 |
| toplam | 1017 | 691 | 40 | 284 | 2 |

Masa HUD tabloları (Table/Strings): pakette 258 `table.STR` anahtarı; projede 198 vardı; 0 metin güncellendi, 8 dizi kaydı ve 2 koşullu metin eski haliyle kaldı, 0 yer tutucu uyuşmazlığı, 60 anahtar eklendi; 85 masa anahtarı pakette yok.

### Ek A · farklı metinler

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| `app.STR.tag` | TR Masalar dolu, taşlar hazır.<br>Arkadaşlarınla otur, 101 açıp eli bitir.<br>EN Tables are full and the tiles are ready.<br>Sit with friends, open 101 and finish the hand. | TR Masalar dolu, taşlar hazır.<br>Arkadaşlarınla otur, 101 açıp eli bitir.<br>EN Tables are full, tiles are racked.<br>Sit with your friends, open 101 and finish. (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.howCont` | TR Nasıl devam etmek istersin?<br>EN How do you want to continue? | TR Nasıl devam etmek istersin?<br>EN How would you like to continue? (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.orEmail` | TR ya da e-postayla<br>EN or with email | TR ya da e-postayla<br>EN or with e-mail (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.terms` | TR Devam ederek Kullanım Koşulları’nı ve Gizlilik Politikası’nı kabul etmiş olursun. Oyun sanal çiple oynanır; gerçek para kazandırmaz.<br>EN By continuing you accept the Terms of Use and Privacy Policy. The game uses virtual chips and pays out no real money. | TR Devam ederek Kullanım Koşulları’nı ve Gizlilik Politikası’nı kabul etmiş olursun. Oyun sanal çiple oynanır; gerçek para kazandırmaz.<br>EN By continuing you accept the Terms of Use and the Privacy Policy. The game is played with virtual chips; it pays no real money. (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.eExists` | TR Bu e-postayla zaten hesap var<br>EN An account with this email exists | TR Bu e-postayla zaten hesap var<br>EN An account with this e-mail already exists (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.noAcct` | TR Bu e-postayla kayıtlı hesap bulunamadı.<br>EN No account found for this email. | TR Bu e-postayla kayıtlı hesap bulunamadı.<br>EN No account with this e-mail. (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.tips.0` | TR İpucu: Göstergenin bir üstü okeydir ve her taşın yerine geçer.<br>EN Tip: The tile above the indicator is the okey and stands in for any tile. | TR İpucu: Göstergenin bir üstü okeydir ve her taşın yerine geçer.<br>EN Tip: The tile one above the indicator is the okey; it stands for any tile. (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.tips.1` | TR İpucu: Elini açmak için 101 puanlık per ya da 5 çift gerekir.<br>EN Tip: You need 101 points in sets or 5 pairs to open. | TR İpucu: Elini açmak için 101 puanlık per ya da 5 çift gerekir.<br>EN Tip: Opening needs sets worth 101 or 5 pairs. (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.tips.2` | TR İpucu: Bir pere basılı tutarak toplu taşıyabilirsin.<br>EN Tip: Press and hold a set to move it as a group. | TR İpucu: Bir pere basılı tutarak toplu taşıyabilirsin.<br>EN Tip: Hold a set to move it as one. (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.tips.3` | TR İpucu: İşlek taşı atarsan +101 ceza yazılır.<br>EN Tip: Discarding a playable tile costs +101. | TR İpucu: İşlek taşı atarsan +101 ceza yazılır.<br>EN Tip: Throwing a playable tile costs +101. (Scripts/Screens/AuthFlow.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.failTitle` | TR Masa açılamadı<br>EN Couldn’t open the table | TR Masa açılamadı<br>EN Couldn't open the table (Table/Strings/table_strings_ek.csv ldErrTitle; eşleşme: TR metni) | farklı | strings_v4.csv |
| `app.STR.failBody` | TR Bağlantı kurulamadı. İnternetini kontrol edip yeniden dene.<br>EN We couldn’t connect. Check your connection and try again. | TR Bağlantı kurulamadı. İnternetini kontrol edip yeniden dene.<br>EN We couldn't connect. Check your connection and try again. (Table/Strings/table_strings_ek.csv ldErrBody; eşleşme: TR metni) | farklı | strings_v4.csv |
| `menu.SET_EN.Tam_ekran_çerçevesiz_pencere_veya_pencer` | TR Tam ekran, çerçevesiz pencere veya pencere<br>EN Fullscreen, borderless or windowed | TR Tam ekran, çerçevesiz pencere veya pencere<br>EN Fullscreen, borderless window or window (Scripts/Screens/SettingsController.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `lobby.STR.tierOk` | TR Uygun<br>EN Available | TR Kullanılabilir<br>EN Available (Scripts/Screens/AuthFlow.cs; eşleşme: EN metni) | farklı | strings_v4.csv |
| `ranked.STR.cancel` | TR İptal<br>EN Cancel | TR Vazgeç<br>EN Cancel (strings_tr_en.csv ui.cancel; eşleşme: EN metni) | farklı | strings_v4.csv |
| `table.STR.diceCap` | TR ZAR<br>EN DIE | TR ZAR<br>EN DICE (Table/Scripts/Hud/TableHud.Deal.cs; eşleşme: TR metni) | farklı | strings_v4.csv; Table/Strings eklendi |
| `table.STR.flows.4` | TR Elden bitiş<br>EN Bomb finish | TR Hediye<br>EN Gifts (Table/Strings/table_strings.csv flows.4; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (dizi kaydı → flows.5) |
| `table.STR.flows.5` | TR Hediye<br>EN Gifts | TR Sohbet<br>EN Chat (Table/Strings/table_strings.csv flows.5; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (dizi kaydı → flows.6) |
| `table.STR.flows.6` | TR Sohbet<br>EN Chat | TR El sonu<br>EN Hand over (Table/Strings/table_strings.csv flows.6; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (dizi kaydı → flows.7) |
| `table.STR.flows.7` | TR El sonu<br>EN Hand over | TR Çıkış<br>EN Leave (Table/Strings/table_strings.csv flows.7; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (dizi kaydı → flows.8) |
| `table.STR.flows.8` | TR Çıkış<br>EN Leave | TR Bağlantı koptu<br>EN Disconnected (Table/Strings/table_strings.csv flows.8; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (dizi kaydı → flows.9) |
| `table.STR.flows.9` | TR Bağlantı koptu<br>EN Disconnected | TR Ayarlar<br>EN Settings (Table/Strings/table_strings.csv flows.9; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (dizi kaydı → flows.10) |
| `table.STR.views.1.0` | TR Yarı yüksek<br>EN Mid view | TR Kuşbakışı<br>EN Top view (Table/Strings/table_strings.csv views.1.0; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (dizi kaydı → views.2.0) |
| `table.STR.views.1.1` | TR Orta açı, ıstaka ve masa dengeli<br>EN Medium angle, rack and table balanced | TR Yüksek açı, tüm masa görünür<br>EN High angle, whole table visible (Table/Strings/table_strings.csv views.1.1; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (dizi kaydı → views.2.1) |
| `table.STR.undone` | TR Taşlar ıstakana geri toplandı<br>EN Tiles returned to your rack | TR Taşlar ıstakana döndü<br>EN Tiles returned to your rack (Table/Strings/table_strings_ek.csv pen.returned; eşleşme: EN metni) | farklı | strings_v4.csv; Table/Strings eklendi |
| `table.STR.shortTip` | TR Sayı yetmiyor · açarsan hatalı açma cezası alırsın<br>EN Not enough points · opening now is a wrong-open penalty | TR Sayı yetmiyor · açarsan hatalı açma cezası alırsın<br>EN Not enough points · opening now costs a wrong-opening penalty (Table/Scripts/Hud/TableHud.cs; eşleşme: TR metni) | farklı | strings_v4.csv; Table/Strings eklendi |
| `table.STR.penCode.P_OKEY_DISCARD` | TR okey attı<br>EN discarded the okey | TR okey attı<br>EN threw okey (Table/Strings/table_strings_ek.csv why.okey; eşleşme: TR metni) | farklı | strings_v4.csv; Table/Strings eklendi |
| `table.STR.bombBtn` | TR Elden bit<br>EN Bomb out | TR Elden bit<br>EN Finish from hand (Table/Scripts/Hud/TableHud.cs; eşleşme: TR metni) | farklı | strings_v4.csv; Table/Strings eklendi |
| `table.STR.bombTitle` | TR ELDEN BİTTİN!<br>EN BOMBED OUT! | TR ELDEN BİTTİN!<br>EN FINISHED FROM THE HAND! (Table/Scripts/Game/TableGame.cs; eşleşme: TR metni) | farklı | strings_v4.csv; Table/Strings eklendi |
| `table.STR.tagSide` | TR YERDEN<br>EN FROM LEFT | TR YERDEN<br>EN FROM PILE (Table/Strings/table_strings_ek.csv pileTag; eşleşme: TR metni) | farklı | strings_v4.csv; Table/Strings eklendi |
| `table.STR.gave` | TR {0}, {1} için {2} gönderdin<br>EN {0} sent {1} {2} | TR {0}, {1} için {2} ısmarladı<br>EN {0} sent {1} {2} (Table/Strings/table_strings.csv gave; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (koşullu fonksiyon, EK-412) |
| `table.STR.gaveAll` | TR {0} herkese {1} gönderdin<br>EN {0} sent everyone {1} | TR {0} herkese {1} gönderdi<br>EN {0} sent everyone {1} (Table/Strings/table_strings.csv gaveAll; eşleşme: anahtar) | farklı | strings_v4.csv; Table/Strings eski metin kaldı (koşullu fonksiyon, EK-412) |
| `shared.rank.diamond` | TR Elmas<br>EN Diamond | TR Elmas<br>EN Gems (strings_tr_en.csv inline-list; eşleşme: TR metni) | farklı | strings_v4.csv |
| `menu.inline.x@1348#7` | TR Taş görünümü<br>EN Tile style | TR Taş görünümü<br>EN Tile skin (strings_tr_en.csv inline; eşleşme: TR metni) | farklı | strings_v4.csv |
| `ranked.inline.againLabel@546` | TR Tekrar ara<br>EN Play again | TR Tekrar oyna<br>EN Play again (Table/Strings/table_strings_ek.csv msAgain; eşleşme: EN metni) | farklı | strings_v4.csv |
| `table.inline.sec@1991` | TR  sn<br>EN s | TR  sn<br>EN  s (additional_strings.csv source.151; eşleşme: TR metni) | farklı | strings_v4.csv |
| `table.inline.chipsCap@2071` | TR EL<br>EN HAND | TR EL<br>EN HANDS (strings_tr_en.csv inline; eşleşme: TR metni) | farklı | strings_v4.csv |
| `err.E_TABLE_FULL` | TR Masa doldu<br>EN The table is full | TR Masa doldu<br>EN Table is full (Scripts/Screens/LobbyScreen.Room.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `err.E_TABLE_PLAYING` | TR Bu masada oyun başladı<br>EN A game is already under way at this table | TR Bu masada oyun başladı<br>EN The game at this table has started (Scripts/Screens/LobbyScreen.cs; eşleşme: TR metni) | farklı | strings_v4.csv |
| `err.E_SPECTATE_OFF` | TR Bu masa izleyiciye kapalı<br>EN Spectating is off at this table | TR Bu masa izleyiciye kapalı<br>EN This table is closed to spectators (Scripts/Screens/LobbyScreen.cs; eşleşme: TR metni) | farklı | strings_v4.csv |

### Ek B · boş metinler (pakette bilinçli boş)

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| `menu.RULES.1.2` | TR "" · EN "" (strings.json tables; G101-Menu.dc.html:1033 RULES örneği "") | — | boş (pakette bilinçli) | strings_v4.csv (boş; örneği olmayan kural bölümü) |
| `menu.RULES.5.2` | TR "" · EN "" (strings.json tables; G101-Menu.dc.html:1033 RULES örneği "") | — | boş (pakette bilinçli) | strings_v4.csv (boş; örneği olmayan kural bölümü) |

### Ek C · masa anahtarları: pakette yok

Projenin `Table/Strings/*.csv` dosyalarında olup pakette `table.STR.<k>` karşılığı olmayan anahtarlar (silinmedi; HUD kodu kullanıyor).

`botBadge`, `botRoom`, `botStake`, `botTag`, `color.B`, `color.K`, `color.R`, `color.Y`, `drawDeckOnly`, `ev.okeyPen`, `ev.opened`, `ev.pairs`, `ev.penFed`, `ev.penOkeyTaken`, `ev.penPlayable`, `ev.penShort`, `exitBodyBot`, `handCounter`, `heCPen`, `ldErrBody`, `ldErrTitle`, `ldKick`, `ldRetry`, `ldTip.0`, `lenLong`, `lenNormal`, `lenQuick`, `msAgain`, `msByHand`, `msHandCol`, `msKick`, `msLevelUp`, `msLobby`, `msLoseSub`, `msPlace`, `msRank`, `msWin`, `msWinSub`, `msXp`, `msXp1`, `msXp2`, `msXp3`, `msXpLeft`, `msXpOther`, `msXpWins`, `msg.canPutBack`, `msg.cantDiscardPicked`, `msg.cantIsle`, `msg.groupMove`, `msg.lastTile`, `msg.okeyOnlyWith`, `msg.openFirst`, `msg.pairsOnly`, `msg.timeUp`, `msg.timeWarn`, `mulHand`, `mulOkey`, `mulPairs`, `noteFinPairs`, `notePairs`, `okStandsFor`, `okStandsForAuto`, `pairsHand`, `pen.fed`, `pen.okey`, `pen.okeyTaken`, `pen.playable`, `pen.returned`, `pen.short`, `pen.shortPairs`, `penBubbleOkey`, `penBubbleTile`, `pileTag`, `putBack`, `putBackHint`, `takeOkeyBanner`, `takeOkeyHint`, `unscored`, `unscoredSub`, `why.fed`, `why.okey`, `why.okeyTaken`, `why.playable`, `why.short`, `xpPill`

### Ek D · masa metinleri: eski metin kaldı

Dizi kayıtları HUD'da konumla okunur (`TableHud.Windows.cs` `views.{i}`); koşullu fonksiyonlardan düzleşmiş metinler HUD'da kodla kurulur (`TableHud.Gifts.cs` `GiftSentence`).

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| `flows.4` | TR Elden bitiş · EN Bomb finish (table.STR.flows.4) | TR Hediye · EN Gifts (table_strings.csv) | farklı | eski metin kaldı; pakette bu metin `flows.5` sırasında (EK-403) |
| `flows.5` | TR Hediye · EN Gifts (table.STR.flows.5) | TR Sohbet · EN Chat (table_strings.csv) | farklı | eski metin kaldı; pakette bu metin `flows.6` sırasında (EK-403) |
| `flows.6` | TR Sohbet · EN Chat (table.STR.flows.6) | TR El sonu · EN Hand over (table_strings.csv) | farklı | eski metin kaldı; pakette bu metin `flows.7` sırasında (EK-403) |
| `flows.7` | TR El sonu · EN Hand over (table.STR.flows.7) | TR Çıkış · EN Leave (table_strings.csv) | farklı | eski metin kaldı; pakette bu metin `flows.8` sırasında (EK-403) |
| `flows.8` | TR Çıkış · EN Leave (table.STR.flows.8) | TR Bağlantı koptu · EN Disconnected (table_strings.csv) | farklı | eski metin kaldı; pakette bu metin `flows.9` sırasında (EK-403) |
| `flows.9` | TR Bağlantı koptu · EN Disconnected (table.STR.flows.9) | TR Ayarlar · EN Settings (table_strings.csv) | farklı | eski metin kaldı; pakette bu metin `flows.10` sırasında (EK-403) |
| `views.1.0` | TR Yarı yüksek · EN Mid view (table.STR.views.1.0) | TR Kuşbakışı · EN Top view (table_strings.csv) | farklı | eski metin kaldı; pakette bu metin `views.2.0` sırasında (EK-403) |
| `views.1.1` | TR Orta açı, ıstaka ve masa dengeli · EN Medium angle, rack and table balanced (table.STR.views.1.1) | TR Yüksek açı, tüm masa görünür · EN High angle, whole table visible (table_strings.csv) | farklı | eski metin kaldı; pakette bu metin `views.2.1` sırasında (EK-403) |
| `gave` | TR {0}, {1} için {2} gönderdin · EN {0} sent {1} {2} (table.STR.gave; kullanılmayan parametre tab, me) | TR {0}, {1} için {2} ısmarladı · EN {0} sent {1} {2} (table_strings.csv) | farklı | eski metin kaldı; paket metni G101-Masa.dc.html fonksiyonunun tek dalı (EK-412) |
| `gaveAll` | TR {0} herkese {1} gönderdin · EN {0} sent everyone {1} (table.STR.gaveAll; kullanılmayan parametre tab, me) | TR {0} herkese {1} gönderdi · EN {0} sent everyone {1} (table_strings.csv) | farklı | eski metin kaldı; paket metni G101-Masa.dc.html fonksiyonunun tek dalı (EK-412) |
