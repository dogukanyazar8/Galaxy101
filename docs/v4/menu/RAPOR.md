# Faz 2 · Menü hattı · rapor

Dal `v4/menu` (taban `main` 9172811; `v4/data` ve sonraki `main` birleştirildi), worktree `C:/GD/Okey101-v4/menu`. Unity çalıştırılmadı (tek süreç kilidi): her değer prototip kaynağından (`G101-Menu.dc.html`, `g101-rank.js`, `g101-settings.js`, `g101-state.js`, paket) ve `artifacts/v4/ref` / `artifacts/v4/unity/run3–run4` dökümlerinden türetildi; her commit'ten önce `tools/v4/unity_compile.py` → `Galaxy101.PlayMode.Tests`, `Galaxy101.Service.Tests`, `Galaxy101.Editor.Tests`: **OK (0 errors)**. Yeni Unity kareleri orkestratörün yakalamasıyla doğrulanacak.

Kalan farkların kaynağı: `EKSIKLER.md` (EK-601…614), `UNITY_FARKLARI.md` (UF-601…607).

## 1. Ortak değişiklikler (bütün menü durumları)

| Konu | Önce | Şimdi | Kaynak |
|---|---|---|---|
| Lig rozeti | 18 px altın baklava + "II" | 24 px `G101Emblem` (paket PNG'si, hat D) + 8 px boşluk; ad `RankLabel(idx)` lig renginde (#FFD45C), satır kırılmaz | G101-Menu 66–68, `rk` (1547–1550) |
| Avatar çerçevesi | v3: Klasik / Altın konik + çiviler / Galaksi | `frameOf` / `frameOpts`: Klasik konik yüz ya da lig halkası (`G101Ring`, EK-601) + ışıma; v4 iç boşlukları (S 5/9, 110: 7/15, M 8/14, L 9/17, çip 5/7) ve boşluk renkleri (gap / gapS); kayıtlı 1 = Altın lig çerçevesi | 1239–1253 |
| Animasyon saati | Gerçek zaman; donukken de akıyordu | Donukken 100000 ms (g101-state.js `freeze()` ms'siz, EK-602); `AnimateElement` her animasyona kendi kanonik zamanını verir | g101-clock.js, g101-state.js 118 |
| Logo taşları | v3 `floatA` 3,2 sn, −14 px | `g101TileIn` + `g101Float` 6 sn, gecikme 0/−2/−4 sn, −7 px, cubic-bezier(.45,0,.55,1) | 21–23, 90–92 |
| Pencere kartları | 980 × 610 kenarlık dahil | İçerik kutusu: 982 × 612 (1 px kenarlık dışarıda) → run4'teki +1 px x/y kayması gider; Ayarlar kartı v4'te kenarlıksız ve 10 px pnS çıkıntısız | 183, 398, 768 |
| %50 rozeti | Nunito 900 | Nunito 700 (prototip yalnız 700'ü yükler) | vendor/fonts.css |
| D-62 adları | `play-now`, `mode-3`, `settings-open`, … | `menu.play`, `menu.m.pick[i]`, `menu.openSettings`, `menu.t.rank`, `menu.closeSettings#2`, `menu.pf.*`, `menu.cf.*`, `menu.sh.*`, `menu.af.*`, `menu.r.*`, `menu.h.*`, `menu.f.*`, `menu.o.pick[i]`, … Ayar denetimleri testlerin ve masa HUD'unun kullandığı `setting-<anahtar>` adlarını korur; satır yazıları D-62 kimliklerini taşır | DECISIONS D-62 |
| İngilizce sabitler | "LV 24", "14,233", "5 s" | Prototip gibi "SV 24", "14.233", "5 sn" (EK-611) | 62, `M`, `sl('warn')` |

## 2. Durumlar

"Beklenen" sütunu, orkestratörün eşleşme kuralına (eksik referans metni yok, ≤ %5 metin > 3 px, UI piksel ≤ %12) göre tahmindir; doğrulama yeni yakalamayla.

| Durum | Değişen | Kalan bilinen fark | Beklenen |
|---|---|---|---|
| 01 | Lig rozeti + "Altın II" konumu, logo taşı fazı, D-62 adları | "1" "0" "1" kutu genişliği (UF-602) — 3/37 > %5 | UF-602 dışında eşleşir |
| 02–05 | 01 ile aynı (tema renkleri değişmedi) | UF-602 | 01 gibi |
| 06 | 01 ile aynı; Dereceli seçili | UF-602 | 01 gibi |
| 10 / S08 | Altı sekme (Masa eklendi, Dil/Hesap −50 px kayması gider), "Oyundan çık" (künye kalktı), kenarlıksız kart, başlık alt metni tek satır, D-62 adları | — | eşleşir |
| 11 | 10 ile aynı | — | eşleşir |
| 12 / S07 | 10 + İmleç satırı (Normal / Büyük / Sistem), 236 × 44 adımlayıcılar (‹ › 38 px, mt, %30 uçta) | Görüntü modu / çözünürlük / Hz cihaza bağlı (UF-607); İmleç donanım imleci (UF-605) | metin eşleşir, 3 değer farkı (UF-607) |
| 13 | 10 + dil kartlarına 6 px üst/alt dolgu (−5 px kayması), D-62 adları | — | eşleşir |
| 14 | 10 + 62 px çerçeveli hesap avatarı (64 px düz disk yerine), Google satırı 19,2 px satırlar, butonlar `align-items: stretch`, 12 px boşluk | ±1–3 px kalabilir | eşleşir |
| 15 | Profili düzenle: 7 çerçeve kutusu (Klasik + 6 lig; Elmas/Usta/Galaksi kilitli, kilit rozeti), 110 px önizleme halkayla, 4 × 28 renk ızgarası (sd + beyaz halka), v4 dolguları → kart yüksekliği 446 → ~484 (başlık +20 px kayması gider), alt bant kesilmez | — | eşleşir |
| 16 | 15 + Altın (menu.o.pick[3]) seçili: lig halkası + ışıma | Işıma yöntemi (UF-604) | eşleşir |
| 17 | 15 + geçersiz ad "A" | — | eşleşir |
| 18 | 15 → Altın kaydedildi: hesap kartı ve üst çubukta altın lig halkası | UF-604 | eşleşir |
| 19 | Onay penceresi adları, hover 1.2 | Gövde h (UF-603) | eşleşir (araç farkı hariç) |
| 20 | Veda: D-62 adları | — | eşleşir |
| 21 | 19 ile aynı | "125.400 çip ve 340 elmas" ↔ gerçek bakiye (EK-605) | 1 metin farkı |
| 22 | 21 + "SİL" (`menu.cf.onType`) | EK-605 | 1 metin farkı |
| 30, 31 | Kart 982 × 612 | Geri sayım (EK-003) | eşleşir (sayaç hariç) |
| 32 | 30 + taş görünümleri v4: Altın taş siyah oniks + altın kenar, "7" çizgi yolu (yazı değil), 11 px nokta | EK-003; veri dosyası v3 (EK-613) | eşleşir (sayaç hariç) |
| 40–44 | Kod şeridinde kopyala simgesinden önceki 10 px boşluk ("Kodun" / "DY-4821" +10 px kayması), D-62 adları | — | eşleşir |
| 45, 46 | 40 + bulunan oyuncu satırında "#" ve kod ayrı metin | — | eşleşir |
| 47 | 40 ile aynı | — | eşleşir |
| 50, 51 | Satırlarda 24 px rozet ("Altın I" −16 px kayması), lcol #FFD45C / #7FE6FF, ben satırı rozeti | — | eşleşir |
| 52 | 50 ile aynı | — | eşleşir |
| 60 | Bugün kartının zıplama fazı (EK-602 → −4 px, "5. Gün" +5 px kayması gider) | Yenilenme sayacı (EK-003) | eşleşir (sayaç hariç) |
| 61 | Yönlendirici `dayClaimed` kurar, ödül vermez (çip 10.000, EK-610); "1" rozeti gizlenir (EK-609 kararı) | EK-003 | eşleşir (sayaç hariç) |
| 70–75 | Kart 982 genişlik → kural sütunu 595 px (73'teki gereksiz satır kırılması gider) | Çok satırlı metin yüksekliği (UF-603) | eşleşir (araç farkı hariç) |
| 80 | 124 px halkalı avatar, "Bu ay +%3", 60 px rozetli sezon kartı ("Altın II", zirve "Altın II", "72 / 100 LP", "Altın I'e 28 LP", lig çubuğu), 7 çerçeve kutusu ("Lig çerçeveleri derecelide açılır", Takılı / Sahipsin / Kilitli) | — | eşleşir |
| 81 | Kart genişliği | Çok satırlı rozet açıklamaları (UF-603) | metin konumları ±; araç farkı |
| 82 | Maç geçmişi v4 ızgarası (44/1fr/110/110/90, 9 px 14 px dolgu, 14 px aralık), adlar `menu.STR.modes`'tan (EK-606), sıra rozeti 800 12px | EK-606 bilinçli kopya | eşleşir |
| 83 | Paylaş kartı 26 px dolgu, 104 px avatar "SEVİYE 24", "· " + "Sezon 3" iki metin, rakam kutuları 17,6 px satır, uygulama kutuları 46 px + çıkıntı; arkadaki Profilim 80 gibi | — | eşleşir |
| 84 | 83 + yönlendirici `menu.af.copy` → "Kopyalandı" (EK-608) | — | eşleşir |
| 90 | 13 + İngilizce sabitler (EK-611) | — | eşleşir |
| 91 | 10 + "5 sn" (EK-611) | UF-603 (açıklama) | eşleşir |
| 92 | 01 + "SV 24", "14.233" | UF-602 | 01 gibi |
| 93 | 30 + "Buy" düğmesi dolguyla boyutlanır (104 px alt sınır kalktı: fiyatlar −23 px kayması) | EK-003 | eşleşir (sayaç hariç) |
| 94 | 40 + EN | run4'te liste ilk satırdan sonra boş ve başka panellerin metinleri var: dil değişiminden sonraki yeniden kurulum zamanlaması (yönlendiricinin `OpenMenu`'su, bu hattın dışında) — yeniden yakalamada bakılmalı | eşleşmeli |
| 95 | 60 + EN | 94'teki zamanlama; EK-003 | eşleşmeli |
| 96 | 70 + EN | UF-603 | araç farkı hariç |
| 97 | 80 + EN | — | eşleşir |
| S06 | 01 ile aynı | UF-602 | 01 gibi |
| S07 | 12 ile aynı | UF-607 | 12 gibi |
| S08 | 10 ile aynı | — | eşleşir |

## 3. Yeni / değişen dosyalar

- `Scripts/Core/MenuView.Rank.cs` (yeni): lig tablosu (`ranks.json`), `RankIndex/RankAt/RankLabel/RankInk`, `Emblem`, `LeagueRing`, çerçeve kimlikleri, `FrameLook`, örnek derece (EK-604).
- `Scripts/Core/GalaxyCursor.cs` (yeni): İmleç tercihi (`galaxy101.cursor`) ve donanım imleci (UF-605).
- `Scripts/Screens/SettingsMasa.cs` (yeni): Masa sekmesi — beş açılır bölüm (Görüş açısı açık), görüş çizimleri (Painter2D), masa / ıstaka / taş önizlemeleri, masa bildirimleri; `TableSettings` üzerinden masa ile ortak (EK-612, UF-606).
- `Scripts/Core/MenuView.cs`: `Avatar` v4, `bright-NNN` hover parlaklığı, `QuitGame`, `AnimateElement` donuk saat.
- `Scripts/Screens/MenuView.Main.cs`, `MenuView.Modals.cs`, `SettingsController.cs`, `ProfileController.cs`, `LeaderboardController.cs`, `FriendsController.cs`, `ShopController.cs`, `ShopArtwork.cs`, `MainMenuController.cs`; `Dialogs/ConfirmController.cs` (Oyundan çık), `DoneController.cs` ("Görüşmek üzere"), `EditProfileController.cs`, `ShareController.cs`, `AddFriendController.cs`, `PanelUi.cs` (`Ring`); `Core/ModalStack.cs` (`quit`, `ClosePanel`); `Services/MockProfileService.cs` (`winRateMonth`).
- `Resources/Galaxy101/UI/Rank/ring_<lig>_<boyut>@2x.png` (36 dosya) + `docs/v4/menu/tools/league_rings.py`.
- `Scripts/Diagnostics/G101StateRouter.Menu.cs`: 16/18 `menu.o.pick[3]` + `menu.pf.save`, 17 `menu.pf.onName`, 20 `menu.cf.go`, 22 `menu.cf.onType`, 42 `menu.fr.onQ`, 45–47 `menu.af.onQ/find/send`, 61 `DayClaimed` (ödülsüz), 84 `menu.af.copy`.
- Testler: `NativePanelTests`, `MenuRuntimeTests`, `ModalStateRuntimeTests`, `DisplaySettingsTests`, `LobbyTests`, `RebuildStabilityTests` — yeni adlar; altı ayar sekmesi; yedi çerçeve (Elmas/Usta/Galaksi kilitli) — eski iddia üç çerçeve ve seviye 30 kilidiydi.

## 4. Orkestratöre notlar

- **Donma anı (EK-602):** referans kareleri 100000 ms'de; `GameClock.Freeze(hold)` 100000 + hold veriyor. Menü kendini 100000'e sabitledi; masa ve diğer hatlar için `Freeze(0)` daha doğru olur.
- **Döküm aracı (UF-602, UF-603):** `G101Dump` döndürülmüş metinlerde AABB, çok satırlı etiketlerde gerçek yükseklik ölçmüyor; bu iki fark ana menünün görünür olduğu her durumda "off" sayısını artırıyor.
- **94/95 (run4):** İngilizce panellerde döküm yeniden kurulumun ortasında alınmış görünüyor (liste boş, başka panellerin metinleri) — bu hattın değiştirmediği `OpenMenu(theme, lang)` adımı.
- **Eski yakalama planı (EK-614):** `GalaxyCaptureDriver` eski adları kullanıyor.
- **İmleç:** menü açılışında `GalaxyCursor.Apply()` paketin okunu donanım imleci yapar (prototipte varsayılan "Normal").

## 6. Run8 sonrası düzeltmeler (main fd64806 üzerine)

Run8: 50 durum eşleşti, 7 bilinçli fark; kuralı geçemeyen 12 durum için (`artifacts/v4/unity/run8/compare.json`; döküm artık döndürülmüş ve satır kırılmış metni doğru ölçüyor):

| Durum | Run8 farkı | Neden | Düzeltme | Kaynak |
|---|---|---|---|---|
| 60, 61, 95 | Görev satırları dx +4, dy −1; "15.000" dy −4 | Liste kaydırıcısının 4 px sağ dolgusu yoktu; "tamamlandı" rozeti 26 px (27,2 olmalı); 7. gün kartında 2 px kenarlık (inset halka değil) ve 15px/1 yerine doğal satır | Liste içeriğine `padding-right: 4`; rozet 800 12px, dolgu 4px 10px, 19,2 px satır, köşe 8; başlık 20px/1.2; 7. gün halkası katmansız (`inset 0 0 0 2px a1` + `0 0 20px gl`); tutarlar 15 px satır; bugün kartına `0 0 26px rgba(60,203,110,.55)` ışıması | G101-Menu 667–716 |
| 70, 72, 74, 96 | Örnek kutusu içeriği dy +4; EN "Opening your hand" tek satır, altındaki başlıklar −10 px | Örnek açıklamasında `margin-top: 6` yoktu ve satırı 22,4 px (14px/1.5 = 21 olmalı); bölüm başlıkları 19,2 px sabit yükseklikteydi, kırılamıyordu | Açıklama 6 px üstte, 21 px satır; "ÖRNEK" doğal satır; bölüm başlığı 16px/1.2 ile kırılır, düğme en az 50 px | G101-Menu 722–752 |
| 81 | Rozet ızgarası dy −5/−6/−13, +4/+5 | Kartlar içeriği üstten diziyordu; CSS'te her satırın kartları aynı yükseklikte ve içerik ortada | Kart = satır, `align-items: center`; ad 15px/1.2, açıklama 12 px (19,2 px satır, 2 px üst), ilerleme satırı 6 px üstte | G101-Menu 505–516 |
| S02–S05 | Yan logo "1" taşları dy +9/+11; slogan h +4; S04 koşullar metni dy +5 | Rakam taşın üstüne hizalıydı (font Npx/1 ortalar); süzülme gerçek zamandan kosinüsle (donukken 100000 ms, `g101Float` eğrisi değil); slogan 20 px doğal satırla (29 olmalı); alan başlıkları 20 px (19,2 olmalı: alan başına +1) | Rakam ortada; `g101Float` cubic-bezier(.45,0,.55,1), gecikme 0/−2/−4 sn, donukken 100000 ms; `g101TileIn` saydamlığı eğriyle; slogan 20px/1.45 (29 px); alan başlık satırı 19,2 px | Gerçek Ana Oyun 34, 89–100, 158, 210 |

### 6.1 Run12 sonrası

| Durum | Run12 farkı | Neden | Düzeltme |
|---|---|---|---|
| 60, 61, 95 | 4.–7. gün kartlarının metinleri dx +4…+6 | Günler USS'teki `flex-basis: 0` ile değil içerik genişliğinden büyüyordu: 1–6. günler 117–118 px, 7. gün 168 px (tasarım: `repeat(6, minmax(0,1fr)) 1.5fr` → 116,27 ve 174,4) | Her güne satır içi `flex-basis: 0`, `min-width: 0` |
| 81 | 2. ve 3. satır rozetleri dy +4…+7 | Tek satırlık ad ve açıklama 19 / 20 px doğal satırla duruyordu (CSS 18 / 19,2): her kart satırı 2–3 px uzadı | `MenuView.CssLines`: n satır × tam satır yüksekliği (n, etiket genişliğindeki metinden), negatif kenar boşluğu yok; rozet adı 18 px, açıklama 19,2 px |

## 5. Commit'ler

| Commit | İçerik |
|---|---|
| `5f6a647` | `v4/data` birleştirildi (metinler, temalar, fontlar, ikonlar, lig rozetleri, imleçler, ses) |
| `2d02a40` | `main` birleştirildi (kural taşıması, karşılaştırma araçları, döküm düzeltmesi, testler Resources'tan okur) |
| `3f3ab23` | Lig rozetleri ve halkaları, avatarlar, üst çubuk, liderlik, logo animasyonu, Ayarlar (6 sekme, Oyundan çık, İmleç, Masa), Profili düzenle, Profilim, Paylaş |
| `752adc2` | Yönlendirici ve testler D-62 adlarıyla |
| `e1d6708` | Donuk saat 100000 ms, içerik kutulu kartlar, arkadaşlar kod şeridi ve ekle penceresi, günlük ödül durumu, alt menü adları |
| `119d6c9` | Mağaza taş görünümleri, "Buy" düğmesi, İngilizce sabitler |
| `137ae5f` | `docs/v4/menu/RAPOR.md`, `EKSIKLER.md`, `UNITY_FARKLARI.md` |
| `fd64806` | (orkestratör) `main`: run8, `G101Dump` dönüşüm ve satır kırılımı ölçümü; `MenuView.Main.cs` rozet ve `ShopController.cs` "Satın al" satırlarında yorumun yuttuğu kod düzeltildi |
| `5043d5e` | Run8 sonrası: günlük ödül, kurallar, rozetler, giriş ekranları (§6) |
| (bu commit) | Run12 sonrası: gün kartı genişlikleri, rozet satır yükseklikleri (§6.1) |
