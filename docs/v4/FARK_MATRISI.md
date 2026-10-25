# v4 · fark matrisi

Paket (`Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/`) ↔ proje. Her satır: kimlik, pakette ne diyor (kaynak), projede ne vardı (dosya:satır), durum (**var / farklı / eksik / pakette yok**), v4 sonrası. Sonraki fazların iş listesi bu matristir. Bölümler büyük olduğu için ayrı dosyalarda; bu dosya dizin ve özet.

| Bölüm | Kapsam | Dosya | Sahibi |
|---|---|---|---|
| Kurallar | R-01…R-98 (kural motoru ve botlar), kural kararları D-01…D-21, D-39, D-43 | `matris/hat-a.md` | Hat A |
| Sunucu | protocol.json C2S/S2C mesajları + 94 hata kodu, timings.md süreleri, R-59…R-66 sunucu tarafı, R-90…R-98, D-11, D-12, D-20, D-22…D-38, D-40…D-42, "pakette yok" sunucu özellikleri | `matris/hat-b.md`, `PROTOKOL_ESLEME.md` | Hat B |
| 3D | 103 GLB, `materials.json`, `lighting.json`, `sahne_tam` | `matris/hat-c.md` | Hat C |
| Veri | strings (TR/EN, hata metinleri), themes, fonts (TTF), 69 ikon, 75 lig rozeti, 11 imleç, 28 ses olayı + 28 efekt + 2 müzik | `matris/hat-d.md` | Hat D |
| Durumlar | 144 etkin + 7 kaldırılmış durum | `DURUMLAR.md` (`durumlar.json`) | Faz 2 |
| Animasyon | css.json (56 kullanılan keyframe), tween3d.json, easings.json | `matris/anim.md` | Faz 2 |
| İstemci altyapısı | D-60…D-62 | aşağıda | Faz 2 |

## İstemci altyapısı (D-60…D-62)

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı | Durum | v4 sonrası |
|---|---|---|---|---|
| D-60 | Ortak saat: tek sanal saat + seed'li mulberry32; Unity `GameClock` + seed'li RNG (`DECISIONS.md` D-60, `g101-clock.js`) | Yok: animasyonlar `Time.time`/`Time.deltaTime`, arka plan `GalaxyBackground.TimeState`; rastgelelik `UnityEngine.Random` (örn. `AuthFlow.cs:54` misafir adı) | eksik | `Assets/Galaxy101/Scripts/Core/GameClock.cs` (mulberry32 birebir, `Stream(name)` = `G101Clock.rng`); animasyonların bu saate bağlanması Faz 2 durum döngüsünde |
| D-61 | `?ft`: yola varınca ft ms sonra kanonik 100000+ft anında donma (`DECISIONS.md` D-61, `g101-state.js`) | Yok (eski yakalama sürücüsü sabit `Settle` beklemeleri, `GalaxyCaptureDriver.cs`) | eksik | `GameClock.Freeze(ft)` + `G101StateRouter` (`-g101Ft`) |
| D-62 | Öğe kimlikleri `data-g101-id` (menu./lobby./ranked./table./app.); Unity eleman adları aynı olacak (`DECISIONS.md` D-62) | Unity adları serbest (`friend-query`, `draft-frame-1`, `profile-save` …) | farklı | Yerleşim dökümü (`G101Dump.Layout`) adları yazar; ad eşleşmesi durum durum (`compare_states.py` "named x/y") |
| states/README | `?state=ID` yönlendirici, adımlar acct/logout/go/waitFor/set/call/table/waitDeal/masa/wait/click/type/scenario/waitMy/waitPen, scSel/scDrag/scSwap/scDiscardCode/scOpen/scDiscard | Yok (`GalaxyCaptureDriver` yalnız eski 54 menü karesi; masa kareleri PlayMode testlerinde ortam değişkenleriyle) | eksik | `Assets/Galaxy101/Scripts/Diagnostics/G101StateRouter*.cs` (`-g101State/-g101Seed/-g101Theme/-g101Lang/-g101Ft/-g101Out`), `scripts/Capture-G101States.ps1` |
| layout dump | `dump=1`: kutu + hesaplanmış stil | `Tests/PlayMode/UiDump.cs` yalnız metin | farklı | `G101Dump.Layout` + `G101Dump.Texts` (çalışma zamanında, Development) |

## Özet sayılar

Hat raporları birleşince doldurulur (Faz 1 sonu).
