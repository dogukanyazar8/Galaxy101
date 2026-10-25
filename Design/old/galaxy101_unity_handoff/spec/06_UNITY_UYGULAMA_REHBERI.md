# 06 — Unity Uygulama Rehberi

## 1. Sürüm ve paketler

- Unity 6 LTS (6000.0+) önerilir; 2022.3 LTS da olur. URP.
- Paketler: `com.unity.localization`, `com.unity.vectorgraphics` (SVG ikonlar için, isteğe bağlı), `com.unity.inputsystem`. Tween için PrimeTween veya DOTween (isteğe bağlı; USS transition çoğu durum için yeterli).
- Hedef: Windows (öncelik), sonra Android/iOS.

## 2. Klasör yapısı

```
Assets/
  Galaxy101/
    Art/
      Fonts/            Montserrat-*.ttf, Baloo2-*.ttf + FontAsset'ler (Türkçe karakter seti)
      Icons/            *.svg (24×24) veya @2x PNG
      Planets/          saturn.png, mars.png, neptune.png, jupiter.png, venus.png (equirect, editörde üretilmiş)
      Sprites/          star.png (radyal), hard-shadow 9-slice, glow 9-slice, coin, gem, chest
    Shaders/
      Planet.shader     (bkz. 04 §3)
      PlanetRing.hlsl   (Satürn)
      StarfieldAdditive.shader
    UI/
      PanelSettings.asset     (Scale With Screen Size, 1280×720, match 0.5)
      Styles/
        Base.uss              tipografi, butonlar, kartlar, sekmeler, toggle, slider, input, scrollbar
        Theme.Saturn.uss … Theme.Venus.uss   (sadece custom property'ler: --bg1, --a1 …)
      Screens/
        MainMenu.uxml
        Panels/ Settings.uxml, Shop.uxml, Friends.uxml, Leaderboard.uxml, DailyReward.uxml, Rules.uxml, Profile.uxml
        Dialogs/ EditProfile.uxml, Confirm.uxml, Done.uxml, AddFriend.uxml, ShareProfile.uxml
      Components/ ModeCard.uxml, NavButton.uxml, Toggle.uxml, GalaxySlider.uxml, Segmented.uxml, CurrencyPill.uxml, Avatar.uxml, Tile.uxml
    Scripts/
      Core/        ThemeService, LocalizationBridge, ModalStack, NumberFormat
      Screens/     MainMenuController, SettingsController, ShopController, FriendsController, LeaderboardController, DailyRewardController, RulesController, ProfileController
      Dialogs/     EditProfileController, ConfirmController, AddFriendController, ShareController
      Components/  GalaxySlider (sürükle+tık+klavye), HardShadow, GlowElement, ShineSweep, FloatAnim
      Background/  PlanetRenderer (MonoBehaviour + Material), Starfield, ShootingStar
      Services/    I*Service + Mock*Service (content.json okur)
      Editor/      PlanetTextureBaker (planet-renderer.js'deki gen() fonksiyonlarının C# portu)
    Data/
      content.json, themes.json  (spec/data'dan kopya)
    Localization/
      String Tables: UI (TR, EN)  ← strings_tr_en.csv
```

## 3. Sahne düzeni

```
MainMenu (Scene)
├─ Background Camera (ortografik, URP Base)
│   ├─ BackgroundGradient (quad, radyal degrade shader, tema renkleri)
│   ├─ Starfield (3 ParticleSystem + ShootingStar)
│   └─ PlanetGroup (konum: tasarımda 640,300; -16° dönüş; 14 sn sallanma)
│        ├─ Halo (sprite)
│        ├─ Planet (quad + Planet.shader; Satürn'de 2.4× genişlik, halka dahil)
├─ UI Document (MainMenu.uxml, PanelSettings, sort order üstte)
└─ Services (Mock)
```

Alternatif: gezegen ve yıldızları bir `RenderTexture`'a çizip UI Toolkit'te arka plan `VisualElement`'ine verin. Her iki durumda 1280×720 tasarım koordinatlarını ekran ölçeğine eşleyin.

## 4. CSS → UI Toolkit eşlemesi

| CSS (prototip) | Unity UI Toolkit |
|---|---|
| `position:absolute; left/top` | `position: absolute; left/top` (USS) |
| flex, gap | flex var; **gap yok** → çocuklara `margin` veya Unity 6'da yoksa yardımcı class `.gap-12 > *` |
| CSS grid | yok → yatay/dikey flex + sabit genişlikli sütunlar |
| `border-radius` | `border-radius` |
| `box-shadow` (sert) | arkada `translate`'li kardeş eleman (`HardShadow` bileşeni) |
| `box-shadow` (glow) / `filter: blur` | 9-slice yumuşak sprite veya özel mesh (`GlowElement`) |
| `inset` gölgeler (seçili halka) | `border` + iç eleman |
| `linear/radial/conic-gradient` | Vector API ile `generateVisualContent` (Painter2D) veya önceden hazırlanmış degrade sprite'ları |
| metin degradesi (GALAXY) | TextCore gradient (vertex color) + outline/underlay; veya logo sprite/mesh |
| `filter: saturate/brightness` (seçilmemiş mod) | `-unity-background-image-tint-color` + metin rengi kısma; veya opak siyah overlay %22 |
| `backdrop-filter: blur` | arka plan RenderTexture'ının bulanık kopyası veya yarı saydam koyu katman (kabul edilebilir yaklaşım) |
| `transition` | USS `transition-property/duration/timing-function` |
| `@keyframes` (sonsuz) | C# `schedule.Execute(...).Every(16)` veya tween |
| `:hover`/`:active` | USS `:hover`, `:active` pseudo-class |
| `scrollbar-gutter: stable` | ScrollView verticalScrollerVisibility = AlwaysVisible (veya sabit sağ padding) |
| `<input>` | `TextField` (USS ile stilize) |
| `cubic-bezier(.3,1.5,.6,1)` | USS `cubic-bezier` desteklenir (Unity 2022.2+) |

## 5. Tema

- `ThemeService.SetPlanet(name)`: kök `VisualElement`'ten eski `theme-*` class'ını kaldırıp yenisini ekler; arka plan malzemelerinin renklerini (`bg1..3`, `pg`, `atm`, `st`) ve gezegen dokusunu/parametrelerini günceller; kaydeder.
- USS örneği:
```css
.theme-saturn { --bg1:#2A2F6E; --bg2:#161A45; --bg3:#0A0C28; --pn:#232A66; --pnH:#2D3680; --pnS:#0B0D2B; --dp:#0B0E2E; --sd:#10133A; --bd:#2E3680; --a1:#FFD964; --a2:#F5A623; --aS:#B86E0F; --aT:#3A1F00; --lS:#8A4E06; --mt:#A9AEDD; --ic:#D3D6FF; }
.btn-primary { background-color: var(--a2); color: var(--aT); border-radius: 14px; }
```
- Değerler: `themes.json` → `cssVars`. rgba değerlerini USS'te `rgba(r,g,b,a)` olarak yazın.

## 6. Yerelleştirme

- `strings_tr_en.csv`'yi Localization String Table'a aktarın (anahtar sütunu yoksa `tr` metnini slug'layarak anahtar üretin; örn. `menu.play_now`).
- Arayüzdeki her metin `LocalizedString` ile bağlanır; dil değişince anında güncellenir.
- Sayı/para/yüzde biçimleri `NumberFormat` yardımcı sınıfında (`CultureInfo("tr-TR")` / `("en-US")`, yüzde işareti konumu elle).

## 7. Özel bileşenler

- **GalaxySlider**: `PointerDown` → `CapturePointer`, değer = (x − ray.left)/ray.width; `PointerMove` capture'dayken güncelle (geçiş kapalı); `PointerUp` → release. Klavye okları ±1. Sürüklerken topuz scale 1.06 + 4 px hale.
- **ModeCard**: seçili/pasif/hover görünümleri (02 §7); seçili beyaz çerçeve kartın alt sert gölgesini de sarar.
- **NavButton**: degrade kare + ikon + etiket + rozet (rozet karenin içinde konumlanır).
- **CurrencyPill**: ikon + `ShineSweep` (maskeli) + değer + "+" butonu.
- **Avatar**: çerçeve türü (Klasik halka / Altın / Galaksi), renk, baş harfler, seviye rozeti; 3 boyut (62, 110–124, 40–58).
- **Tile** (okey taşı): yüzey/kenar/rakam renkleri, vurgu halkası, etiket.

## 8. Test

- Editörde Game view: 1280×720, 1920×1080, 2560×1440 — öğeler aynı oranda, keskin.
- Her ekranı `screenshots/` ile yan yana karşılaştır.
- `07_KABUL_KONTROL_LISTESI.md`.
