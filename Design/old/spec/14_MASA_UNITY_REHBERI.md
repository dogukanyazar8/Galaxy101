# 14 · Oyun Masası — Unity Uygulama Rehberi

## 1. Teknoloji
- Unity 6 LTS (ya da 2022.3 LTS), **URP**, Forward+, MSAA 4×, HDR açık, **Bloom** (eşik 1.0, yoğunluk 0.6, dağılım 0.7), Tonemapping ACES, pozlama +0.05.
- 3D içe aktarma: **glTFast** (GLB) ya da Blender'dan FBX.
- Animasyon: kod tabanlı tween (DOTween ya da kendi `Tween` yardımcın). Prototipteki süre ve eğriler birebir.
- Arayüz: UI Toolkit (ana menüyle aynı USS token'ları ve bileşenleri). 3D çapalara bağlı öğeler (avatarlar, etiketler) her karede `Camera.WorldToScreenPoint` + panel koordinatına çevrilir.
- Parçacıklar: VFX Graph ya da Shuriken (patlama, halka dalgası, kıvılcım, duman, buhar, konfeti).

## 2. Sahne hiyerarşisi
```
TableScene
├─ BackgroundCamera (depth −1): Gradient + Starfield + Planet (ana menüdeki sistemler)
├─ MainCamera (CameraRig: Player / Top / Lobby / Win görünümleri, nefes, sarsıntı)
├─ Lighting (Directional+shadow, Spot, 2 Point, Reflection Probe)
├─ Room (oda_istasyon.glb)
├─ Table (TableSkinSwitcher: 4 prefab ya da malzeme seti)
├─ Seats[4] (yaw 0/90/180/−90)
│   ├─ Rack (RackSkinSwitcher: 6 prefab) → 30 Slot transform
│   ├─ Chair (rakipler)
│   ├─ DiscardPile, GiftSlots[3], MeldArea
│   └─ AvatarAnchor
├─ Deck (GPU instanced taşlar)
├─ Indicator, DiscardZone, SideRing, HoloRing
├─ TilePool (110 TileView)
├─ GiftSystem (prefab'lar + uçuş/iniş/idle)
└─ UI (UIDocument: HUD, paneller, pencereler)
```

## 3. Kod modülleri (öneri)
| Sınıf | Görev | Prototip karşılığı |
|---|---|---|
| `TableGameModel` | rows[2][15], opp yuvaları, discard[4], melds[4], deck, indicator, phase, active, opened[4], openMode | DC `state` |
| `TableRules` | `EvalHand`, `PairsOf`, `ExtendSide`, `Addables`, puanlama | `evalHand`, `pairsOf`, `extendSide`, `addables`, `endHand` |
| `TurnController` | tur sırası, süre, otomatik oyun, bot rakipler (zamanlamalar 10 §4) | `startTurn`, `runOpps`, `oppDraw`, `oppIsle`, `oppDiscard` |
| `RackPlanner` | tek taş ve per yerleştirme planları (11 §3–4) | `singlePlan`, `groupPlan`, `planRow` |
| `TileView` | hedef poz + tween (slide/travel/deal), pop, yeşil/sarı çubuk, kaldırma | `tiles`, `startTween`, `frame` |
| `BoardView` | modeli 3D'ye senkronlar (id → poz) | `sync`, `poseFor` |
| `RackInput` | raycast, hover/seçim/çift tık, tek ve toplu sürükleme, canlı önizleme | `onDown/onMove/onUp`, `previewGroup` |
| `GiftSystem` | yuva yönetimi, uçuş, roket turu, iniş, idle, temizlik | `sendGift`, `tickGifts` |
| `TableFx` | dağıtım karması, bitiş kutlaması, halka/patlama | `dealFx`, `celebrate`, `burst`, `wave` |
| `SkinService` | 4 masa + 6 ıstaka + 5 taş görünümü × 5 gezegen rengi | `applyLook`, `setTileSkin` (tiles3d.js), `buildRacks` (racks3d.js) |
| `TableHUD` | üst bar, toast, oyuncu paneli, aksiyonlar, çapalı avatarlar | template |
| `GiftPanel`, `ChatPanel`, `PlayerCard`, `TableSettings`, `ResultWindow`, `LeaveDialog`, `NetDialog`, `FinishBanner` | pencereler | template + `*Vals` |
| `ITableService` | sunucu olayları (oturma, dağıtım, hamle, ikram, sohbet, bağlantı) — şimdilik mock | demo zamanlayıcıları |

Model, görünümden bağımsız olmalı: her taşın kalıcı bir **id**'si vardır, görünüm yalnızca "bu id şu yerde" bilgisini alır ve farkı tween'ler (prototipteki `sync` mantığı). Bu sayede çekme, atma, alma, işleme, sıralama aynı mekanizmayla animasyonlanır.

## 4. Malzemeler (Shader Graph)
- `M_Felt`: albedo doku (tema renkleriyle üretilen ya da pişirilmiş), Hologram/Andromeda için emissive aynı doku.
- `M_Iridescent`: Fresnel → thin-film rampası (mor `#6B4FE0` → mavi `#2F7AD6` → pembe `#C56FD0`), metal 0.85–1, pürüzlülük 0.14–0.18.
- `M_GlassTea`: şeffaf, Fresnel kenar, env yansıması ×1.8; içindeki çay opak + emissive (`#C03A0C`, emissive `#A32C04` ×0.62).
- `M_Emissive_Theme`: renk `--a1`, yoğunluk 2.4, `sin` nabız.
- Taş: Complex Lit (clearcoat 0.7), yüz için atlas + alfa.

## 5. Performans
- Deste GPU instancing (tek draw call). Taş havuzu (110).
- Gölge: yalnızca masa, taşlar, ıstakalar, ikramlar; 2048 harita.
- Ekran görünmezken (uygulama arka planda) render ve parçacıklar durur.
- Hedef 60 FPS (orta seviye mobil 30 FPS: gölge 1024, MSAA 2×, bloom yarım çözünürlük).

## 6. Çözünürlük
Tasarım birimi 1280×720. UI Toolkit PanelSettings: Scale With Screen Size, referans 1280×720, Match 0.5. 3D kamera FOV sabit (40°); 16:9 dışı oranlarda yatay FOV korunacak şekilde dikey FOV hesaplanır (21:9'da masa kırpılmasın).
