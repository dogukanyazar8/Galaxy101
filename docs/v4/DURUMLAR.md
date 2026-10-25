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
- Metin kutuları: `tools/compare_ui.py` ±3 px konum, genişlik/yükseklik ±%6 (en az 3 px), yazı boyutu ±0,5 px. "eşleşti" için referanstaki hiçbir metin eksik olmayacak ve en çok %5'i eşik dışında kalabilir.
- Yerleşim dökümü: aynı `data-g101-id` adlı öğeler için kutu ±3 px; renk, font, harf aralığı, köşe birebir.
- Piksel farkı: yalnız referansın UI öğe kutularında, gri ton farkı > 24 olan pikseller; tam ekran katmanlar ve Unity filigranı (UF-01) dışarıda. Referansın kendi tekrar farkı bu ölçüde %0. Eşik %12: yapısı birebir aynı bir karede (durum 40, run3) kalan fark yalnız yazı kenar yumuşatmasıdır (Chrome ↔ Unity). 3D masa kareleri ayrıca göz kontrolü + `tween3d`/`materials`/`lighting` değerleriyle.
- "eşleşti": metin ve öğe eşikleri içinde; "bilinçli fark": nedeni `UNITY_FARKLARI.md`'de; "eksik": `EKSIKLER.md`'de kayıtlı.

## Tablo

Ref: `artifacts/v4/ref/<ID>.png` + `.layout.json` (✓ çekildi; `tekrar Δ` = aynı adresin ikinci açılışıyla farklı piksel sayısı, EK-001).

| Kimlik | Grup | Başlık | Ref | Unity karşılığı | Durum | Not |
|---|---|---|---|---|---|---|
| 01 | menu | Ana menü, Satürn, Tekli seçili | ✓ · tekrar Δ12329px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 02 | menu | Ana menü · tema mars | ✓ · tekrar Δ27162px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 03 | menu | Ana menü · tema neptun | ✓ · tekrar Δ12090px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 04 | menu | Ana menü · tema jupiter | ✓ · tekrar Δ13011px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 05 | menu | Ana menü · tema venus | ✓ · tekrar Δ13644px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 06 | menu | Eşli modu seçili | ✓ · tekrar Δ881px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 10 | menu | Ayarlar → Oyun | ✓ · tekrar Δ164px | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 11 | menu | Ayarlar → Ses | ✓ · tekrar Δ161px | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 12 | menu | Ayarlar → Görüntü | ✓ · tekrar Δ202px | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 13 | menu | Ayarlar → Dil | ✓ · tekrar Δ455px | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 14 | menu | Ayarlar → Hesap | ✓ · tekrar Δ461px | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 15 | menu | Profili düzenle | ✓ · tekrar Δ112px | Scripts/Dialogs/EditProfileController.cs | bekliyor |  |
| 16 | menu | Profili düzenle · Altın çerçeve seçili | ✓ · tekrar Δ60px | Scripts/Dialogs/EditProfileController.cs | bekliyor |  |
| 17 | menu | Profili düzenle · geçersiz ad | ✓ · tekrar Δ96px | Scripts/Dialogs/EditProfileController.cs | bekliyor |  |
| 18 | menu | Ayarlar → Hesap · kaydedildi (altın çerçeve) | ✓ · tekrar Δ134px | Scripts/Dialogs/EditProfileController.cs | bekliyor |  |
| 19 | menu | Çıkış onayı | ✓ · tekrar Δ0px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor |  |
| 20 | menu | Çıkış yapıldı | ✓ · tekrar Δ15px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor |  |
| 21 | menu | Hesap silme onayı (pasif) | ✓ · tekrar Δ91px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor |  |
| 22 | menu | SİL yazıldı (aktif) | ✓ · tekrar Δ23px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor |  |
| 30 | menu | Mağaza → Çip | ✓ · tekrar Δ862px | Scripts/Screens/ShopController.cs | bekliyor |  |
| 31 | menu | Mağaza → Elmas | ✓ · tekrar Δ982px | Scripts/Screens/ShopController.cs | bekliyor |  |
| 32 | menu | Mağaza → Görünüm | ✓ · tekrar Δ825px | Scripts/Screens/ShopController.cs | bekliyor |  |
| 40 | menu | Arkadaşlar → Çevrimiçi | ✓ · tekrar Δ16px | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 41 | menu | Arkadaşlar → Tümü | ✓ · tekrar Δ57px | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 42 | menu | "ay" araması | ✓ · tekrar Δ141px | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 43 | menu | İstekler | ✓ · tekrar Δ77px | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 44 | menu | Arkadaş ekle — boş | ✓ · tekrar Δ14px | Scripts/Dialogs/AddFriendController.cs | bekliyor |  |
| 45 | menu | Arkadaş ekle · MK-3321 bulundu | ✓ · tekrar Δ173px | Scripts/Dialogs/AddFriendController.cs | bekliyor |  |
| 46 | menu | Arkadaş ekle · istek gönderildi | ✓ · tekrar Δ70px | Scripts/Dialogs/AddFriendController.cs | bekliyor |  |
| 47 | menu | Arkadaş ekle · bulunamadı | ✓ · tekrar Δ94px | Scripts/Dialogs/AddFriendController.cs | bekliyor |  |
| 50 | menu | Liderlik → Haftalık | ✓ · tekrar Δ25px | Scripts/Screens/LeaderboardController.cs | bekliyor |  |
| 51 | menu | Liderlik → Sezon | ✓ · tekrar Δ88px | Scripts/Screens/LeaderboardController.cs | bekliyor |  |
| 52 | menu | Liderlik → Arkadaşlar | ✓ · tekrar Δ3px | Scripts/Screens/LeaderboardController.cs | bekliyor |  |
| 60 | menu | Günlük ödül | ✓ · tekrar Δ808px | Scripts/Screens (DailyReward panel, MenuView) | bekliyor |  |
| 61 | menu | Gün toplandı | ✓ · tekrar Δ13477px | Scripts/Screens (DailyReward panel, MenuView) | bekliyor |  |
| 70 | menu | Kurallar 1 | ✓ · tekrar Δ1000px | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 71 | menu | Kurallar 2 | ✓ · tekrar Δ156px | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 72 | menu | Kurallar 3 | ✓ · tekrar Δ403px | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 73 | menu | Kurallar 4 | ✓ · tekrar Δ1430px | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 74 | menu | Kurallar 5 | ✓ · tekrar Δ85px | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 75 | menu | Kurallar 6 | ✓ · tekrar Δ155px | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 80 | menu | Profilim → Genel | ✓ · tekrar Δ96px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 81 | menu | Profilim → Rozetler | ✓ · tekrar Δ148px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 82 | menu | Profilim → Maç geçmişi | ✓ · tekrar Δ97px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 83 | menu | Profili paylaş | ✓ · tekrar Δ56px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 84 | menu | Bağlantı kopyalandı | ✓ · tekrar Δ6838px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| 90 | menu | EN — Ayarlar → Dil | ✓ · tekrar Δ81px | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 91 | menu | EN — Ayarlar → Oyun | ✓ · tekrar Δ66px | Scripts/Screens/SettingsController.cs | bekliyor |  |
| 92 | menu | EN — Ana menü, Satürn, Tekli seçili | ✓ · tekrar Δ1541px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| 93 | menu | EN — Mağaza → Çip | ✓ · tekrar Δ790px | Scripts/Screens/ShopController.cs | bekliyor |  |
| 94 | menu | EN — Arkadaşlar → Çevrimiçi | ✓ · tekrar Δ36px | Scripts/Screens/FriendsController.cs | bekliyor |  |
| 95 | menu | EN — Günlük ödül | ✓ · tekrar Δ772px | Scripts/Screens (DailyReward panel, MenuView) | bekliyor |  |
| 96 | menu | EN — Kurallar 1 | ✓ · tekrar Δ79px | Scripts/Screens (Rules panel, MenuView) | bekliyor |  |
| 97 | menu | EN — Profilim → Genel | ✓ · tekrar Δ87px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor |  |
| S01 | acilis | Açılış / yükleme (GALAXY + 1 0 1, ilerleme) | ✓ · tekrar Δ724px | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S02 | acilis | Giriş seçimi | ✓ · tekrar Δ1000px | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S03 | acilis | Giriş yap formu | ✓ · tekrar Δ461px | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S04 | acilis | Kayıt ol formu | ✓ · tekrar Δ3150px | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S05 | acilis | Misafir girişi formu | ✓ · tekrar Δ2177px | Scripts/Screens/AuthFlow.cs | bekliyor |  |
| S06 | menu | Ana menü | ✓ · tekrar Δ1663px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor |  |
| S07 | menu | Ayarlar → Görüntü | ✓ · tekrar Δ464px | Scripts/Screens/SettingsController.cs | bekliyor |  |
| S08 | menu | Ayarlar → Oyun | ✓ · tekrar Δ194px | Scripts/Screens/SettingsController.cs | bekliyor |  |
| S09 | lobi | Lobi masa listesi | ✓ · tekrar Δ303px | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S10 | lobi | Masa aç formu | ✓ · tekrar Δ64px | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S11 | lobi | Şifreli masa PIN | ✓ · tekrar Δ242px | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S12 | lobi | Kodla katıl | ✓ · tekrar Δ126px | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S13 | lobi | Bekleme odası (kurucu) | ✓ · tekrar Δ156px | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S14 | lobi | Bekleme odası · arkadaş davet (Arkadaşlar) | ✓ · tekrar Δ1838px | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S15 | lobi | Bekleme odası · davet (Son oynadıkların) | ✓ · tekrar Δ2026px | Scripts/Screens/LobbyScreen*.cs | bekliyor |  |
| S16 | dereceli | Dereceli ana ekran | ✓ · tekrar Δ1433px | Scripts/Screens/RankedQueue.cs (yalnız kuyruk; dereceli ana ekranı yok) | bekliyor |  |
| G01 | dagitim | Masa yükleniyor | ✓ · tekrar Δ1317px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor |  |
| G02 | dagitim | Bot balyaları diziyor | ✓ · tekrar Δ5530px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G03 | dagitim | Sen diziyorsun | ✓ · tekrar Δ5537px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G04 | dagitim | Zar at düğmesi | ✓ · tekrar Δ4178px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G05 | dagitim | Zar yuvarlanıyor | ✓ · tekrar Δ46556px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G06 | dagitim | 1. zar · Tekrar at | ✓ · tekrar Δ1298px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G07 | dagitim | Gösterge açılıyor | ✓ · tekrar Δ9691px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G08 | dagitim | Gösterge → okey kartı, fazla taş | ✓ · tekrar Δ8843px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G09 | dagitim | Balyalar dağıtılıyor | ✓ · tekrar Δ3607px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G10 | dagitim | Kalan balyalar ortaya | ✓ · tekrar Δ4217px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G11 | dagitim | Istakalar doluyor | ✓ · tekrar Δ5596px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G12 | dagitim | Oyun başladı (22 taş) | ✓ · tekrar Δ5340px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G13 | oyun | El açıldı: perler ortada | ✓ · tekrar Δ4308px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G14 | oyun | İşleme testi (herkes açık) | ✓ · tekrar Δ1943px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G15 | oyun | Altın işleme göstergeleri | ✓ · tekrar Δ7388px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G16 | oyun | Yeşil okey alma göstergesi (Y4 seçili) | ✓ · tekrar Δ10852px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G17 | oyun | Okey alındı (Y4 ↔ okey) | ✓ · tekrar Δ15290px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G18 | oyun | İşleme yapıldı + Geri topla | ✓ · tekrar Δ9585px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G19 | oyun | Geri toplandı | ✓ · tekrar Δ6480px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G20 | oyun | Hatalı açma (turuncu Aç) | ✓ · tekrar Δ7547px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G21 | oyun | Hatalı per masada | ✓ · tekrar Δ4897px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G22 | oyun | Ceza tostu | ✓ · tekrar Δ11380px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G23 | oyun | İşlek taş testi | ✓ · tekrar Δ6357px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G24 | oyun | İşlek taş cezası (B4 atıldı) | ✓ · tekrar Δ6054px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G25 | oyun | Elden bit düğmesi | ✓ · tekrar Δ3310px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G26 | oyun | Taş fırlatılıyor | ✓ · tekrar Δ3400px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G27 | oyun | Nükleer patlama | ✓ · tekrar Δ4812px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G28 | oyun | ELDEN BİTTİN | ✓ · tekrar Δ19594px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G29 | oyun | El sonu (kazandın) | ✓ · tekrar Δ5583px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G30 | oyun | Sıra sende | ✓ · tekrar Δ10791px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G31 | oyun | Soldan alındı + Geri bırak | ✓ · tekrar Δ9404px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G32 | oyun | Masa ayarları | ✓ · tekrar Δ567px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G33 | oyun | Masadan kalk onayı | ✓ · tekrar Δ1017px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G34 | oyun | El sonu (Ayşe kazandı) | ✓ · tekrar Δ2164px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T01 | masa | Bekleme: 2/4 oyuncu | ✓ · tekrar Δ2329px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T02 | masa | Masa doldu, geri sayım | ✓ · tekrar Δ4933px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T03 | masa | Dağıtım: girdap, karma | ✓ · tekrar Δ3642px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T04 | masa | Dağıtım bitti, İlk hamle senin | ✓ · tekrar Δ6184px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T05 | masa | Çekme aşaması, Ayşe'nin perleri | ✓ · tekrar Δ5887px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T06 | masa | Çay ısmarlandı bildirimi | ✓ · tekrar Δ10580px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T07 | masa | Oyuncu kartı (Zeynep), hızlı ikram | ✓ · tekrar Δ3663px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T08 | masa | Hediye paneli · İkram | ✓ · tekrar Δ3610px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T09 | masa | Hediye paneli · Hediye (Pasta) | ✓ · tekrar Δ4297px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T10 | masa | Hediye paneli · Emoji | ✓ · tekrar Δ3669px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T11 | masa | Roket uçuşu | ✓ · tekrar Δ321716px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T12 | masa | Roket indi + teşekkür balonu | ✓ · tekrar Δ6893px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T13 | masa | Sohbet paneli | ✓ · tekrar Δ1192px | Table/Scripts/Hud/TableHud.Social.cs | bekliyor |  |
| T14 | masa | El açıldı · Yere aç / İşle | ✓ · tekrar Δ4723px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T15 | masa | Ayarlar · Görünüm | ✓ · tekrar Δ257px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T16 | masa | Masa ayarları · Oyun | ✓ · tekrar Δ3835px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T17 | masa | Masa ayarları · Ses | ✓ · tekrar Δ375px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T18 | masa | Masa ayarları · Bildirimler | ✓ · tekrar Δ652px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T19 | masa | Tema: Ceviz salon + Klasik ceviz ıstaka | ✓ · tekrar Δ14798px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T20 | masa | Tema: Hologram + Neon cam ıstaka | ✓ · tekrar Δ26755px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T21 | masa | Tema: Andromeda masa + ıstaka | ✓ · tekrar Δ8954px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T22 | masa | Kuşbakışı görünüm (Andromeda) | ✓ · tekrar Δ4356px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T23 | masa | Bitiş anı: Ayşe bitirdi | ✓ · tekrar Δ4582px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T24 | masa | El sonu tablosu | ✓ · tekrar Δ961px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T25 | masa | Masadan kalk onayı | ✓ · tekrar Δ6520px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T26 | masa | Bağlantı koptu | ✓ · tekrar Δ2379px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T27 | masa | EN — oyun sırasında | ✓ · tekrar Δ7393px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T28 | masa | Botlara karşı: başlık, ANTRENMAN, el sayacı | ✓ · tekrar Δ5931px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T30 | masa | Botlara karşı · masadan kalk onayı | ✓ · tekrar Δ5622px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T33 | masa | Okey rozeti masada | — | Table/Scripts/Hud/TableHud.cs + Game/RackInput.cs okey ipucu (ShowOkeyTip) | kaldırıldı | kaldırıldı: masada okey temsil rozeti yok; okeyin değeri perdeki konumundan çıkar (R-13) · Unity karşılığı kaldırılacak |
| T34 | masa | Ceza: attığın K9 ile bot açtı (+90, R-43) | ✓ · tekrar Δ7635px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T35 | masa | Eksik açılış cezası (+101) | ✓ · tekrar Δ12524px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T36 | masa | Ceza: masadaki okeyin alındı (+101, R-44) | ✓ · tekrar Δ9543px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T38 | masa | Yerden alındı + Geri bırak | ✓ · tekrar Δ5760px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T39 | masa | Soldan alınan taş halkaya geri sürükleniyor | ✓ · tekrar Δ6351px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T40 | masa | Mesaj kataloğu | — | — | kaldırıldı | kaldırıldı: Mesaj kataloğu bir referans sayfası: Referans - Masa Ek Durumlar |
| T41 | masa | Mesaj kuyruğu | — | — | kaldırıldı | kaldırıldı: Mesaj kuyruğu referans sayfasında; oyunda tek satırlık sistem mesajı + tost |
| T42 | masa | El sonu · botlara karşı | ✓ · tekrar Δ480px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T43 | masa | El sonu · çevrimiçi masa | ✓ · tekrar Δ671px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T44 | masa | El sonu puansız (çiftle bitiş) | — | Table/Scripts/Hud/TableHud.cs el sonu | kaldırıldı | kaldırıldı: Çiftle bitiş artık puanlanır (×2, R-50…R-56); "Bu el puanlanmadı" yok · Unity karşılığı kaldırılacak |
| T45 | masa | Maç sonu · kazandın | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd + Game/TableGame.cs ShowMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok; maç sonu lobide tost (dereceli: S16 sonuç) · Unity karşılığı kaldırılacak |
| T46 | masa | Maç sonu · 2. sıra | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok (T45 ile aynı) · Unity karşılığı kaldırılacak |
| T47 | masa | Okeyi al: Y4 sürükleniyor, uygun hedefler parlıyor | ✓ · tekrar Δ4900px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T48 | masa | Okey alma ipucu | — | Table/Scripts/Game/TableGame.cs ShowOkeyTip | kaldırıldı | kaldırıldı: Okeye uzun basma ipucu yok · Unity karşılığı kaldırılacak |
| T49 | masa | Okeyi al: yanlış taşla deneme (K5) | ✓ · tekrar Δ5680px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T50 | masa | Menü → masa yükleme ekranı | ✓ · tekrar Δ3722px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor |  |
| T51 | masa | Masa açılamadı hatası | ✓ · tekrar Δ0px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor |  |
