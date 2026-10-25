# v4 · durum tablosu

Üretildi: `python tools/v4/states_table.py` (girdi: `states.json`, `artifacts/v4/ref/capture_report.json`, elle tutulan `docs/v4/durumlar.json`). Bu dosyayı elle düzenleme.

Durum kataloğu: 151 kimlik, 144 etkin, 7 kaldırılmış.

| Durum | Sayı |
|---|---|
| eşleşti | 0 |
| bilinçli fark | 0 |
| eksik | 0 |
| kaldırıldı | 7 |
| bekliyor | 144 |

## Eşikler ve yöntem

- Referans: `artifacts/v4/ref/<ID>.png` (Chrome, GPU, 1280×720, seed 42, ft = katalog `hold`); Unity: `scripts/Capture-G101States.ps1` (Development player, pencereli 1280×720, aynı ID/seed/ft).
- Metin kutuları: `tools/compare_ui.py` ±3 px konum, genişlik/yükseklik ±%6 (en az 3 px), yazı boyutu ±0,5 px.
- Yerleşim dökümü: aynı `data-g101-id` adlı öğeler için kutu ±3 px; renk, font, harf aralığı, köşe birebir.
- Piksel farkı: arka plan tuvali (yıldız + gezegen) maskelenir (EK-001); 3D masa kareleri yalnız göz kontrolü + `tween3d`/`materials`/`lighting` değerleri.
- "eşleşti": metin ve öğe eşikleri içinde; "bilinçli fark": nedeni `UNITY_FARKLARI.md`'de; "eksik": `EKSIKLER.md`'de kayıtlı.

## Tablo

Ref: `artifacts/v4/ref/<ID>.png` + `.layout.json` (✓ çekildi; `tekrar Δ` = aynı adresin ikinci açılışıyla farklı piksel sayısı, EK-001).

| Kimlik | Grup | Başlık | Ref | Unity karşılığı | Durum | Not |
|---|---|---|---|---|---|---|
| 01 | menu | Ana menü, Satürn, Tekli seçili | ✓ | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 02 | menu | Ana menü · tema mars | ✓ | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 03 | menu | Ana menü · tema neptun | ✓ | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 04 | menu | Ana menü · tema jupiter | ✓ | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 05 | menu | Ana menü · tema venus | ✓ | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 06 | menu | Eşli modu seçili | ✓ | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 10 | menu | Ayarlar → Oyun | ✓ | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 11 | menu | Ayarlar → Ses | ✓ | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 12 | menu | Ayarlar → Görüntü | ✓ | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 13 | menu | Ayarlar → Dil | ✓ | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 14 | menu | Ayarlar → Hesap | ✓ | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 15 | menu | Profili düzenle | ✓ | Scripts/Dialogs/EditProfileController.cs | bekliyor |  |
| 16 | menu | Profili düzenle · Altın çerçeve seçili | ✓ | Scripts/Dialogs/EditProfileController.cs | bekliyor |  |
| 17 | menu | Profili düzenle · geçersiz ad | ✓ | Scripts/Dialogs/EditProfileController.cs | bekliyor |  |
| 18 | menu | Ayarlar → Hesap · kaydedildi (altın çerçeve) | ✓ | Scripts/Dialogs/EditProfileController.cs | bekliyor |  |
| 19 | menu | Çıkış onayı | ✓ | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor |  |
| 20 | menu | Çıkış yapıldı | ✓ | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor |  |
| 21 | menu | Hesap silme onayı (pasif) | ✓ | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor |  |
| 22 | menu | SİL yazıldı (aktif) | ✓ | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor |  |
| 30 | menu | Mağaza → Çip | ✓ | Scripts/Screens/ShopController.cs | bekliyor |  |
| 31 | menu | Mağaza → Elmas | ✓ | Scripts/Screens/ShopController.cs | bekliyor |  |
| 32 | menu | Mağaza → Görünüm | ✓ | Scripts/Screens/ShopController.cs | bekliyor |  |
| 40 | menu | Arkadaşlar → Çevrimiçi | ✓ | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 41 | menu | Arkadaşlar → Tümü | ✓ | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 42 | menu | "ay" araması | ✓ | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 43 | menu | İstekler | ✓ | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 44 | menu | Arkadaş ekle — boş | ✓ | Scripts/Dialogs/AddFriendController.cs | bekliyor |  |
| 45 | menu | Arkadaş ekle · MK-3321 bulundu | ✓ | Scripts/Dialogs/AddFriendController.cs | bekliyor |  |
| 46 | menu | Arkadaş ekle · istek gönderildi | ✓ | Scripts/Dialogs/AddFriendController.cs | bekliyor |  |
| 47 | menu | Arkadaş ekle · bulunamadı | ✓ | Scripts/Dialogs/AddFriendController.cs | bekliyor |  |
| 50 | menu | Liderlik → Haftalık | ✓ | Scripts/Screens/LeaderboardController.cs | bekliyor |  |
| 51 | menu | Liderlik → Sezon | ✓ | Scripts/Screens/LeaderboardController.cs | bekliyor |  |
| 52 | menu | Liderlik → Arkadaşlar | ✓ | Scripts/Screens/LeaderboardController.cs | bekliyor |  |
| 60 | menu | Günlük ödül | ✓ | Scripts/Screens (DailyReward panel, MenuView) | bekliyor |  |
| 61 | menu | Gün toplandı | ✓ | Scripts/Screens (DailyReward panel, MenuView) | bekliyor |  |
| 70 | menu | Kurallar 1 | ✓ | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 71 | menu | Kurallar 2 | ✓ | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 72 | menu | Kurallar 3 | ✓ | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 73 | menu | Kurallar 4 | ✓ | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 74 | menu | Kurallar 5 | ✓ | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 75 | menu | Kurallar 6 | ✓ | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 80 | menu | Profilim → Genel | ✓ | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 81 | menu | Profilim → Rozetler | ✓ | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 82 | menu | Profilim → Maç geçmişi | ✓ | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 83 | menu | Profili paylaş | ✓ | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 84 | menu | Bağlantı kopyalandı | ✓ | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 90 | menu | EN — Ayarlar → Dil | ✓ | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 91 | menu | EN — Ayarlar → Oyun | ✓ | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 92 | menu | EN — Ana menü, Satürn, Tekli seçili | ✓ | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 93 | menu | EN — Mağaza → Çip | ✓ | Scripts/Screens/ShopController.cs | bekliyor |  |
| 94 | menu | EN — Arkadaşlar → Çevrimiçi | ✓ | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 95 | menu | EN — Günlük ödül | ✓ | Scripts/Screens (DailyReward panel, MenuView) | bekliyor |  |
| 96 | menu | EN — Kurallar 1 | ✓ | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 97 | menu | EN — Profilim → Genel | ✓ | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| S01 | acilis | Açılış / yükleme (GALAXY + 1 0 1, ilerleme) | ✓ | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S02 | acilis | Giriş seçimi | ✓ | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S03 | acilis | Giriş yap formu | ✓ | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S04 | acilis | Kayıt ol formu | ✓ | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S05 | acilis | Misafir girişi formu | ✓ | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S06 | menu | Ana menü | ✓ | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| S07 | menu | Ayarlar → Görüntü | ✓ | Scripts/Screens/SettingsController.cs | bekliyor |  |
| S08 | menu | Ayarlar → Oyun | ✓ | Scripts/Screens/SettingsController.cs | bekliyor |  |
| S09 | lobi | Lobi masa listesi | ✓ | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S10 | lobi | Masa aç formu | ✓ | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S11 | lobi | Şifreli masa PIN | ✓ | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S12 | lobi | Kodla katıl | ✓ | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S13 | lobi | Bekleme odası (kurucu) | ✓ | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S14 | lobi | Bekleme odası · arkadaş davet (Arkadaşlar) | ✓ | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S15 | lobi | Bekleme odası · davet (Son oynadıkların) | ✓ | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S16 | dereceli | Dereceli ana ekran | ✓ | Scripts/Screens/RankedQueue.cs (yalnız kuyruk; dereceli ana ekranı yok) | bekliyor |  |
| G01 | dagitim | Masa yükleniyor | ✓ | Scripts/Screens/TableLoadingOverlay.cs | bekliyor |  |
| G02 | dagitim | Bot balyaları diziyor | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G03 | dagitim | Sen diziyorsun | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G04 | dagitim | Zar at düğmesi | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G05 | dagitim | Zar yuvarlanıyor | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G06 | dagitim | 1. zar · Tekrar at | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G07 | dagitim | Gösterge açılıyor | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G08 | dagitim | Gösterge → okey kartı, fazla taş | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G09 | dagitim | Balyalar dağıtılıyor | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G10 | dagitim | Kalan balyalar ortaya | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G11 | dagitim | Istakalar doluyor | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G12 | dagitim | Oyun başladı (22 taş) | ✓ | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G13 | oyun | El açıldı: perler ortada | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G14 | oyun | İşleme testi (herkes açık) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G15 | oyun | Altın işleme göstergeleri | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G16 | oyun | Yeşil okey alma göstergesi (Y4 seçili) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G17 | oyun | Okey alındı (Y4 ↔ okey) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G18 | oyun | İşleme yapıldı + Geri topla | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G19 | oyun | Geri toplandı | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G20 | oyun | Hatalı açma (turuncu Aç) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G21 | oyun | Hatalı per masada | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G22 | oyun | Ceza tostu | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G23 | oyun | İşlek taş testi | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G24 | oyun | İşlek taş cezası (B4 atıldı) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G25 | oyun | Elden bit düğmesi | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G26 | oyun | Taş fırlatılıyor | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G27 | oyun | Nükleer patlama | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G28 | oyun | ELDEN BİTTİN | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G29 | oyun | El sonu (kazandın) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G30 | oyun | Sıra sende | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G31 | oyun | Soldan alındı + Geri bırak | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G32 | oyun | Masa ayarları | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G33 | oyun | Masadan kalk onayı | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G34 | oyun | El sonu (Ayşe kazandı) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T01 | masa | Bekleme: 2/4 oyuncu | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T02 | masa | Masa doldu, geri sayım | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T03 | masa | Dağıtım: girdap, karma | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T04 | masa | Dağıtım bitti, İlk hamle senin | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T05 | masa | Çekme aşaması, Ayşe'nin perleri | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T06 | masa | Çay ısmarlandı bildirimi | ✓ | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T07 | masa | Oyuncu kartı (Zeynep), hızlı ikram | ✓ | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T08 | masa | Hediye paneli · İkram | ✓ | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T09 | masa | Hediye paneli · Hediye (Pasta) | ✓ | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T10 | masa | Hediye paneli · Emoji | ✓ | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T11 | masa | Roket uçuşu | ✓ | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T12 | masa | Roket indi + teşekkür balonu | ✓ | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T13 | masa | Sohbet paneli | ✓ | Table/Scripts/Hud/TableHud.Social.cs | bekliyor |  |
| T14 | masa | El açıldı · Yere aç / İşle | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T15 | masa | Ayarlar · Görünüm | ✓ | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T16 | masa | Masa ayarları · Oyun | ✓ | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T17 | masa | Masa ayarları · Ses | ✓ | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T18 | masa | Masa ayarları · Bildirimler | ✓ | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T19 | masa | Tema: Ceviz salon + Klasik ceviz ıstaka | ✓ | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T20 | masa | Tema: Hologram + Neon cam ıstaka | ✓ | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T21 | masa | Tema: Andromeda masa + ıstaka | ✓ | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T22 | masa | Kuşbakışı görünüm (Andromeda) | ✓ | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T23 | masa | Bitiş anı: Ayşe bitirdi | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T24 | masa | El sonu tablosu | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T25 | masa | Masadan kalk onayı | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T26 | masa | Bağlantı koptu | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T27 | masa | EN — oyun sırasında | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T28 | masa | Botlara karşı: başlık, ANTRENMAN, el sayacı | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T30 | masa | Botlara karşı · masadan kalk onayı | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T33 | masa | Okey rozeti masada | — | Table/Scripts/Hud/TableHud.cs + Game/RackInput.cs okey ipucu (ShowOkeyTip) | kaldırıldı | kaldırıldı: masada okey temsil rozeti yok; okeyin değeri perdeki konumundan çıkar (R-13) · Unity karşılığı kaldırılacak |
| T34 | masa | Ceza: attığın K9 ile bot açtı (+90, R-43) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T35 | masa | Eksik açılış cezası (+101) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T36 | masa | Ceza: masadaki okeyin alındı (+101, R-44) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T38 | masa | Yerden alındı + Geri bırak | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T39 | masa | Soldan alınan taş halkaya geri sürükleniyor | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T40 | masa | Mesaj kataloğu | — | — | kaldırıldı | kaldırıldı: Mesaj kataloğu bir referans sayfası: Referans - Masa Ek Durumlar |
| T41 | masa | Mesaj kuyruğu | — | — | kaldırıldı | kaldırıldı: Mesaj kuyruğu referans sayfasında; oyunda tek satırlık sistem mesajı + tost |
| T42 | masa | El sonu · botlara karşı | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T43 | masa | El sonu · çevrimiçi masa | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T44 | masa | El sonu puansız (çiftle bitiş) | — | Table/Scripts/Hud/TableHud.cs el sonu | kaldırıldı | kaldırıldı: Çiftle bitiş artık puanlanır (×2, R-50…R-56); "Bu el puanlanmadı" yok · Unity karşılığı kaldırılacak |
| T45 | masa | Maç sonu · kazandın | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd + Game/TableGame.cs ShowMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok; maç sonu lobide tost (dereceli: S16 sonuç) · Unity karşılığı kaldırılacak |
| T46 | masa | Maç sonu · 2. sıra | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok (T45 ile aynı) · Unity karşılığı kaldırılacak |
| T47 | masa | Okeyi al: Y4 sürükleniyor, uygun hedefler parlıyor | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T48 | masa | Okey alma ipucu | — | Table/Scripts/Game/TableGame.cs ShowOkeyTip | kaldırıldı | kaldırıldı: Okeye uzun basma ipucu yok · Unity karşılığı kaldırılacak |
| T49 | masa | Okeyi al: yanlış taşla deneme (K5) | ✓ | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T50 | masa | Menü → masa yükleme ekranı | ✓ | Scripts/Screens/TableLoadingOverlay.cs | bekliyor |  |
| T51 | masa | Masa açılamadı hatası | ✓ | Scripts/Screens/TableLoadingOverlay.cs | bekliyor |  |
