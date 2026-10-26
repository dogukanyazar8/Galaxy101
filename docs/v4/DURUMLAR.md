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
| 01 | menu | Ana menü, Satürn, Tekli seçili | ✓ · tekrar Δ12042px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor | [run4] metin 32/37 (kayık 4, eksik 0, fazla 1) · ad 0/42 · UI piksel %10.67 |
| 02 | menu | Ana menü · tema mars | ✓ · tekrar Δ26458px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor | [run4] metin 32/37 (kayık 4, eksik 0, fazla 1) · ad 0/42 · UI piksel %10.12 |
| 03 | menu | Ana menü · tema neptun | ✓ · tekrar Δ13356px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor | [run4] metin 32/37 (kayık 4, eksik 0, fazla 1) · ad 0/42 · UI piksel %10.35 |
| 04 | menu | Ana menü · tema jupiter | ✓ · tekrar Δ16965px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor | [run4] metin 32/37 (kayık 4, eksik 0, fazla 1) · ad 0/42 · UI piksel %10.39 |
| 05 | menu | Ana menü · tema venus | ✓ · tekrar Δ16832px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor | [run4] metin 32/37 (kayık 4, eksik 0, fazla 1) · ad 0/42 · UI piksel %10.31 |
| 06 | menu | Eşli modu seçili | ✓ · tekrar Δ4156px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor | [run4] metin 32/37 (kayık 4, eksik 0, fazla 1) · ad 0/42 · UI piksel %10.46 |
| 10 | menu | Ayarlar → Oyun | ✓ · tekrar Δ353px | Scripts/Screens/SettingsController.cs | bekliyor | [run4] metin 57/66 (kayık 6, eksik 2, fazla 3) · ad 0/81 · UI piksel %9.78 |
| 11 | menu | Ayarlar → Ses | ✓ · tekrar Δ288px | Scripts/Screens/SettingsController.cs | bekliyor | [run4] metin 54/63 (kayık 6, eksik 2, fazla 3) · ad 0/80 · UI piksel %9.04 |
| 12 | menu | Ayarlar → Görüntü | ✓ · tekrar Δ76px | Scripts/Screens/SettingsController.cs | bekliyor | [run4] metin 56/77 (kayık 12, eksik 8, fazla 4) · ad 0/85 · UI piksel %15.74 |
| 13 | menu | Ayarlar → Dil | ✓ · tekrar Δ442px | Scripts/Screens/SettingsController.cs | bekliyor | [run4] metin 41/56 (kayık 12, eksik 2, fazla 3) · ad 0/72 · UI piksel %13.89 |
| 14 | menu | Ayarlar → Hesap | ✓ · tekrar Δ353px | Scripts/Screens/SettingsController.cs | bekliyor | [run4] metin 51/64 (kayık 10, eksik 2, fazla 3) · ad 0/74 · UI piksel %16.89 |
| 15 | menu | Profili düzenle | ✓ · tekrar Δ125px | Scripts/Dialogs/EditProfileController.cs | bekliyor | [run4] metin 51/83 (kayık 24, eksik 7, fazla 5) · ad 0/112 · UI piksel %28.47 |
| 16 | menu | Profili düzenle · Altın çerçeve seçili | ✓ · tekrar Δ156px | Scripts/Dialogs/EditProfileController.cs | bekliyor | [run4] metin 51/83 (kayık 24, eksik 7, fazla 5) · ad 0/112 · UI piksel %29.16 |
| 17 | menu | Profili düzenle · geçersiz ad | ✓ · tekrar Δ36px | Scripts/Dialogs/EditProfileController.cs | bekliyor | [run4] metin 51/83 (kayık 24, eksik 7, fazla 5) · ad 0/112 · UI piksel %28.44 |
| 18 | menu | Ayarlar → Hesap · kaydedildi (altın çerçeve) | ✓ · tekrar Δ297px | Scripts/Dialogs/EditProfileController.cs | bekliyor | [run4] metin 51/64 (kayık 10, eksik 2, fazla 3) · ad 0/74 · UI piksel %16.99 |
| 19 | menu | Çıkış onayı | ✓ · tekrar Δ195px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor | [run4] metin 54/68 (kayık 11, eksik 2, fazla 3) · ad 0/79 · UI piksel %4.93 |
| 20 | menu | Çıkış yapıldı | ✓ · tekrar Δ37px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor | [run4] metin 54/67 (kayık 10, eksik 2, fazla 3) · ad 0/78 · UI piksel %3.19 |
| 21 | menu | Hesap silme onayı (pasif) | ✓ · tekrar Δ52px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor | [run4] metin 57/72 (kayık 11, eksik 3, fazla 4) · ad 0/81 · UI piksel %4.84 |
| 22 | menu | SİL yazıldı (aktif) | ✓ · tekrar Δ56px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bekliyor | [run4] metin 57/72 (kayık 11, eksik 3, fazla 5) · ad 0/81 · UI piksel %4.85 |
| 30 | menu | Mağaza → Çip | ✓ · tekrar Δ735px | Scripts/Screens/ShopController.cs | bekliyor | [run4] metin 58/64 (kayık 4, eksik 1, fazla 2) · ad 0/70 · UI piksel %10.02 |
| 31 | menu | Mağaza → Elmas | ✓ · tekrar Δ948px | Scripts/Screens/ShopController.cs | bekliyor | [run4] metin 58/64 (kayık 4, eksik 1, fazla 2) · ad 0/70 · UI piksel %9.26 |
| 32 | menu | Mağaza → Görünüm | ✓ · tekrar Δ14330px | Scripts/Screens/ShopController.cs | bekliyor | [run4] metin 55/61 (kayık 4, eksik 1, fazla 3) · ad 0/67 · UI piksel %10.85 |
| 40 | menu | Arkadaşlar → Çevrimiçi | ✓ · tekrar Δ98px | Scripts/Screens/FriendsController.cs | bekliyor | [run4] metin 53/60 (kayık 6, eksik 0, fazla 1) · ad 0/66 · UI piksel %6.28 |
| 41 | menu | Arkadaşlar → Tümü | ✓ · tekrar Δ50px | Scripts/Screens/FriendsController.cs | bekliyor | [run4] metin 68/75 (kayık 6, eksik 0, fazla 1) · ad 0/78 · UI piksel %7.13 |
| 42 | menu | "ay" araması | ✓ · tekrar Δ120px | Scripts/Screens/FriendsController.cs | bekliyor | [run4] metin 43/50 (kayık 6, eksik 0, fazla 2) · ad 0/59 · UI piksel %4.61 |
| 43 | menu | İstekler | ✓ · tekrar Δ128px | Scripts/Screens/FriendsController.cs | bekliyor | [run4] metin 50/57 (kayık 6, eksik 0, fazla 1) · ad 0/63 · UI piksel %6.39 |
| 44 | menu | Arkadaş ekle — boş | ✓ · tekrar Δ34px | Scripts/Dialogs/AddFriendController.cs | bekliyor | [run4] metin 61/68 (kayık 6, eksik 0, fazla 1) · ad 0/75 · UI piksel %4.55 |
| 45 | menu | Arkadaş ekle · MK-3321 bulundu | ✓ · tekrar Δ239px | Scripts/Dialogs/AddFriendController.cs | bekliyor | [run4] metin 64/73 (kayık 7, eksik 1, fazla 2) · ad 0/78 · UI piksel %5.22 |
| 46 | menu | Arkadaş ekle · istek gönderildi | ✓ · tekrar Δ41px | Scripts/Dialogs/AddFriendController.cs | bekliyor | [run4] metin 64/73 (kayık 7, eksik 1, fazla 2) · ad 0/78 · UI piksel %5.18 |
| 47 | menu | Arkadaş ekle · bulunamadı | ✓ · tekrar Δ78px | Scripts/Dialogs/AddFriendController.cs | bekliyor | [run4] metin 61/68 (kayık 6, eksik 0, fazla 2) · ad 0/75 · UI piksel %5.3 |
| 50 | menu | Liderlik → Haftalık | ✓ · tekrar Δ7px | Scripts/Screens/LeaderboardController.cs | bekliyor | [run4] metin 85/98 (kayık 12, eksik 0, fazla 1) · ad 0/93 · UI piksel %9.67 |
| 51 | menu | Liderlik → Sezon | ✓ · tekrar Δ83px | Scripts/Screens/LeaderboardController.cs | bekliyor | [run4] metin 85/98 (kayık 12, eksik 0, fazla 1) · ad 0/93 · UI piksel %9.96 |
| 52 | menu | Liderlik → Arkadaşlar | ✓ · tekrar Δ16px | Scripts/Screens/LeaderboardController.cs | bekliyor | [run4] metin 69/78 (kayık 8, eksik 0, fazla 1) · ad 0/77 · UI piksel %7.41 |
| 60 | menu | Günlük ödül | ✓ · tekrar Δ1268px | Scripts/Screens (DailyReward panel, MenuView) | bekliyor | [run4] metin 72/80 (kayık 6, eksik 1, fazla 2) · ad 0/80 · UI piksel %9.54 |
| 61 | menu | Gün toplandı | ✓ · tekrar Δ13938px | Scripts/Screens (DailyReward panel, MenuView) | bekliyor | [run4] metin 71/80 (kayık 5, eksik 3, fazla 3) · ad 0/80 · UI piksel %9.31 |
| 70 | menu | Kurallar 1 | ✓ · tekrar Δ966px | Scripts/Screens (Rules panel, MenuView) | bekliyor | [run4] metin 55/61 (kayık 5, eksik 0, fazla 1) · ad 0/74 · UI piksel %8.09 |
| 71 | menu | Kurallar 2 | ✓ · tekrar Δ108px | Scripts/Screens (Rules panel, MenuView) | bekliyor | [run4] metin 50/55 (kayık 4, eksik 0, fazla 1) · ad 0/68 · UI piksel %6.83 |
| 72 | menu | Kurallar 3 | ✓ · tekrar Δ81px | Scripts/Screens (Rules panel, MenuView) | bekliyor | [run4] metin 59/64 (kayık 4, eksik 0, fazla 1) · ad 0/77 · UI piksel %9.42 |
| 73 | menu | Kurallar 4 | ✓ · tekrar Δ51px | Scripts/Screens (Rules panel, MenuView) | bekliyor | [run4] metin 55/61 (kayık 5, eksik 0, fazla 1) · ad 0/74 · UI piksel %8.29 |
| 74 | menu | Kurallar 5 | ✓ · tekrar Δ17px | Scripts/Screens (Rules panel, MenuView) | bekliyor | [run4] metin 56/61 (kayık 4, eksik 0, fazla 1) · ad 0/74 · UI piksel %7.86 |
| 75 | menu | Kurallar 6 | ✓ · tekrar Δ21px | Scripts/Screens (Rules panel, MenuView) | bekliyor | [run4] metin 51/56 (kayık 4, eksik 0, fazla 1) · ad 0/69 · UI piksel %7.36 |
| 80 | menu | Profilim → Genel | ✓ · tekrar Δ67px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor | [run4] metin 70/96 (kayık 9, eksik 16, fazla 7) · ad 0/105 · UI piksel %20.91 |
| 81 | menu | Profilim → Rozetler | ✓ · tekrar Δ199px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor | [run4] metin 51/83 (kayık 31, eksik 0, fazla 1) · ad 0/90 · UI piksel %12.81 |
| 82 | menu | Profilim → Maç geçmişi | ✓ · tekrar Δ144px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor | [run4] metin 56/97 (kayık 35, eksik 5, fazla 4) · ad 0/104 · UI piksel %12.75 |
| 83 | menu | Profili paylaş | ✓ · tekrar Δ24px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor | [run4] metin 84/129 (kayık 26, eksik 18, fazla 8) · ad 0/139 · UI piksel %8.61 |
| 84 | menu | Bağlantı kopyalandı | ✓ · tekrar Δ6839px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor | [run4] metin 82/129 (kayık 26, eksik 20, fazla 10) · ad 0/139 · UI piksel %10.69 |
| 90 | menu | EN — Ayarlar → Dil | ✓ · tekrar Δ19px | Scripts/Screens/SettingsController.cs | bekliyor | [run4] metin 38/56 (kayık 12, eksik 5, fazla 6) · ad 0/72 · UI piksel %14.24 |
| 91 | menu | EN — Ayarlar → Oyun | ✓ · tekrar Δ86px | Scripts/Screens/SettingsController.cs | bekliyor | [run4] metin 52/66 (kayık 7, eksik 6, fazla 7) · ad 0/81 · UI piksel %10.01 |
| 92 | menu | EN — Ana menü, Satürn, Tekli seçili | ✓ · tekrar Δ1564px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor | [run4] metin 29/37 (kayık 4, eksik 3, fazla 4) · ad 0/42 · UI piksel %9.46 |
| 93 | menu | EN — Mağaza → Çip | ✓ · tekrar Δ574px | Scripts/Screens/ShopController.cs | bekliyor | [run4] metin 52/64 (kayık 7, eksik 4, fazla 5) · ad 0/70 · UI piksel %10.14 |
| 94 | menu | EN — Arkadaşlar → Çevrimiçi | ✓ · tekrar Δ115px | Scripts/Screens/FriendsController.cs | bekliyor | [run4] metin 47/60 (kayık 6, eksik 6, fazla 7) · ad 0/66 · UI piksel %6.37 |
| 95 | menu | EN — Günlük ödül | ✓ · tekrar Δ560px | Scripts/Screens (DailyReward panel, MenuView) | bekliyor | [run4] metin 64/80 (kayık 6, eksik 9, fazla 10) · ad 0/80 · UI piksel %10.35 |
| 96 | menu | EN — Kurallar 1 | ✓ · tekrar Δ50px | Scripts/Screens (Rules panel, MenuView) | bekliyor | [run4] metin 43/61 (kayık 14, eksik 3, fazla 4) · ad 0/74 · UI piksel %9.59 |
| 97 | menu | EN — Profilim → Genel | ✓ · tekrar Δ46px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor | [run4] metin 66/96 (kayık 10, eksik 19, fazla 10) · ad 0/105 · UI piksel %20.78 |
| S01 | acilis | Açılış / yükleme (GALAXY + 1 0 1, ilerleme) | ✓ · tekrar Δ631px | Scripts/Screens/AuthFlow.cs | bekliyor | [run4] metin 3/10 (kayık 3, eksik 3, fazla 36) · ad 0/14 · UI piksel %46.68 |
| S02 | acilis | Giriş seçimi | ✓ · tekrar Δ706px | Scripts/Screens/AuthFlow.cs | bekliyor | [run4] metin 13/19 (kayık 5, eksik 0, fazla 33) · ad 1/18 · UI piksel %10.2 |
| S03 | acilis | Giriş yap formu | ✓ · tekrar Δ384px | Scripts/Screens/AuthFlow.cs | bekliyor | [run4] metin 12/17 (kayık 4, eksik 0, fazla 33) · ad 1/24 · UI piksel %9.36 |
| S04 | acilis | Kayıt ol formu | ✓ · tekrar Δ1468px | Scripts/Screens/AuthFlow.cs | bekliyor | [run4] metin 10/16 (kayık 5, eksik 0, fazla 33) · ad 1/27 · UI piksel %10.31 |
| S05 | acilis | Misafir girişi formu | ✓ · tekrar Δ1140px | Scripts/Screens/AuthFlow.cs | bekliyor | [run4] metin 9/15 (kayık 5, eksik 0, fazla 34) · ad 1/27 · UI piksel %9.78 |
| S06 | menu | Ana menü | ✓ · tekrar Δ2398px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | bekliyor | [run4] metin 32/37 (kayık 4, eksik 0, fazla 1) · ad 0/42 · UI piksel %9.91 |
| S07 | menu | Ayarlar → Görüntü | ✓ · tekrar Δ410px | Scripts/Screens/SettingsController.cs | bekliyor | [run4] metin 56/77 (kayık 12, eksik 8, fazla 4) · ad 0/85 · UI piksel %15.74 |
| S08 | menu | Ayarlar → Oyun | ✓ · tekrar Δ147px | Scripts/Screens/SettingsController.cs | bekliyor | [run4] metin 57/66 (kayık 6, eksik 2, fazla 3) · ad 0/81 · UI piksel %9.78 |
| S09 | lobi | Lobi masa listesi | ✓ · tekrar Δ367px | Scripts/Screens/LobbyScreen*.cs | bekliyor | [run4] metin 135/155 (kayık 14, eksik 6, fazla 36) · ad 1/92 · UI piksel %13.84 |
| S10 | lobi | Masa aç formu | ✓ · tekrar Δ64px | Scripts/Screens/LobbyScreen*.cs | bekliyor | [run4] metin 160/224 (kayık 42, eksik 22, fazla 49) · ad 1/162 · UI piksel %19.57 |
| S11 | lobi | Şifreli masa PIN | ✓ · tekrar Δ213px | Scripts/Screens/LobbyScreen*.cs | bekliyor | [run4] metin 139/159 (kayık 14, eksik 6, fazla 36) · ad 1/101 · UI piksel %11.79 |
| S12 | lobi | Kodla katıl | ✓ · tekrar Δ117px | Scripts/Screens/LobbyScreen*.cs | bekliyor | [run4] metin 139/159 (kayık 14, eksik 6, fazla 36) · ad 1/99 · UI piksel %11.94 |
| S13 | lobi | Bekleme odası (kurucu) | ✓ · tekrar Δ1712px | Scripts/Screens/LobbyScreen*.cs | bekliyor | [run4] metin 23/49 (kayık 13, eksik 13, fazla 5) · ad 1/56 · UI piksel %21.09 |
| S14 | lobi | Bekleme odası · arkadaş davet (Arkadaşlar) | ✓ · tekrar Δ1745px | Scripts/Screens/LobbyScreen*.cs | bekliyor | [run4] metin 23/49 (kayık 13, eksik 13, fazla 47) · ad 1/56 · UI piksel %50.74 |
| S15 | lobi | Bekleme odası · davet (Son oynadıkların) | ✓ · tekrar Δ1219px | Scripts/Screens/LobbyScreen*.cs | bekliyor | [run4] metin 23/49 (kayık 13, eksik 13, fazla 35) · ad 1/56 · UI piksel %50.68 |
| S16 | dereceli | Dereceli ana ekran | ✓ · tekrar Δ1605px | Scripts/Screens/RankedQueue.cs (yalnız kuyruk; dereceli ana ekranı yok) | bekliyor |  |
| G01 | dagitim | Masa yükleniyor | ✓ · tekrar Δ1317px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor |  |
| G02 | dagitim | Bot balyaları diziyor | ✓ · tekrar Δ4920px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G03 | dagitim | Sen diziyorsun | ✓ · tekrar Δ48354px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G04 | dagitim | Zar at düğmesi | ✓ · tekrar Δ5309px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G05 | dagitim | Zar yuvarlanıyor | ✓ · tekrar Δ4836px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G06 | dagitim | 1. zar · Tekrar at | ✓ · tekrar Δ5391px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G07 | dagitim | Gösterge açılıyor | ✓ · tekrar Δ10001px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G08 | dagitim | Gösterge → okey kartı, fazla taş | ✓ · tekrar Δ9675px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G09 | dagitim | Balyalar dağıtılıyor | ✓ · tekrar Δ3865px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G10 | dagitim | Kalan balyalar ortaya | ✓ · tekrar Δ42477px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G11 | dagitim | Istakalar doluyor | ✓ · tekrar Δ56290px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G12 | dagitim | Oyun başladı (22 taş) | ✓ · tekrar Δ7569px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor |  |
| G13 | oyun | El açıldı: perler ortada | ✓ · tekrar Δ4801px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G14 | oyun | İşleme testi (herkes açık) | ✓ · tekrar Δ3048px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G15 | oyun | Altın işleme göstergeleri | ✓ · tekrar Δ5367px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G16 | oyun | Yeşil okey alma göstergesi (Y4 seçili) | ✓ · tekrar Δ6661px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G17 | oyun | Okey alındı (Y4 ↔ okey) | ✓ · tekrar Δ15548px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G18 | oyun | İşleme yapıldı + Geri topla | ✓ · tekrar Δ8622px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G19 | oyun | Geri toplandı | ✓ · tekrar Δ2396px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G20 | oyun | Hatalı açma (turuncu Aç) | ✓ · tekrar Δ6904px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G21 | oyun | Hatalı per masada | ✓ · tekrar Δ8976px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G22 | oyun | Ceza tostu | ✓ · tekrar Δ6312px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G23 | oyun | İşlek taş testi | ✓ · tekrar Δ2212px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G24 | oyun | İşlek taş cezası (B4 atıldı) | ✓ · tekrar Δ3132px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G25 | oyun | Elden bit düğmesi | ✓ · tekrar Δ4374px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G26 | oyun | Taş fırlatılıyor | ✓ · tekrar Δ3204px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G27 | oyun | Nükleer patlama | ✓ · tekrar Δ4339px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G28 | oyun | ELDEN BİTTİN | ✓ · tekrar Δ17439px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G29 | oyun | El sonu (kazandın) | ✓ · tekrar Δ4869px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G30 | oyun | Sıra sende | ✓ · tekrar Δ10568px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G31 | oyun | Soldan alındı + Geri bırak | ✓ · tekrar Δ8990px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G32 | oyun | Masa ayarları | ✓ · tekrar Δ574px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G33 | oyun | Masadan kalk onayı | ✓ · tekrar Δ6751px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| G34 | oyun | El sonu (Ayşe kazandı) | ✓ · tekrar Δ2147px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T01 | masa | Bekleme: 2/4 oyuncu | ✓ · tekrar Δ2409px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T02 | masa | Masa doldu, geri sayım | ✓ · tekrar Δ5550px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T03 | masa | Dağıtım: girdap, karma | ✓ · tekrar Δ4530px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T04 | masa | Dağıtım bitti, İlk hamle senin | ✓ · tekrar Δ36071px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T05 | masa | Çekme aşaması, Ayşe'nin perleri | ✓ · tekrar Δ10377px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T06 | masa | Çay ısmarlandı bildirimi | ✓ · tekrar Δ9892px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T07 | masa | Oyuncu kartı (Zeynep), hızlı ikram | ✓ · tekrar Δ5394px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T08 | masa | Hediye paneli · İkram | ✓ · tekrar Δ3275px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T09 | masa | Hediye paneli · Hediye (Pasta) | ✓ · tekrar Δ4522px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T10 | masa | Hediye paneli · Emoji | ✓ · tekrar Δ6835px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T11 | masa | Roket uçuşu | ✓ · tekrar Δ333754px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T12 | masa | Roket indi + teşekkür balonu | ✓ · tekrar Δ4521px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor |  |
| T13 | masa | Sohbet paneli | ✓ · tekrar Δ3030px | Table/Scripts/Hud/TableHud.Social.cs | bekliyor |  |
| T14 | masa | El açıldı · Yere aç / İşle | ✓ · tekrar Δ3339px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T15 | masa | Ayarlar · Görünüm | ✓ · tekrar Δ1895px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T16 | masa | Masa ayarları · Oyun | ✓ · tekrar Δ4027px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T17 | masa | Masa ayarları · Ses | ✓ · tekrar Δ913px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T18 | masa | Masa ayarları · Bildirimler | ✓ · tekrar Δ1556px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T19 | masa | Tema: Ceviz salon + Klasik ceviz ıstaka | ✓ · tekrar Δ17451px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T20 | masa | Tema: Hologram + Neon cam ıstaka | ✓ · tekrar Δ27754px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T21 | masa | Tema: Andromeda masa + ıstaka | ✓ · tekrar Δ8418px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T22 | masa | Kuşbakışı görünüm (Andromeda) | ✓ · tekrar Δ24252px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor |  |
| T23 | masa | Bitiş anı: Ayşe bitirdi | ✓ · tekrar Δ4648px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T24 | masa | El sonu tablosu | ✓ · tekrar Δ684px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T25 | masa | Masadan kalk onayı | ✓ · tekrar Δ8886px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T26 | masa | Bağlantı koptu | ✓ · tekrar Δ4172px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T27 | masa | EN — oyun sırasında | ✓ · tekrar Δ8171px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T28 | masa | Botlara karşı: başlık, ANTRENMAN, el sayacı | ✓ · tekrar Δ9183px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T30 | masa | Botlara karşı · masadan kalk onayı | ✓ · tekrar Δ5009px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T33 | masa | Okey rozeti masada | — | Table/Scripts/Hud/TableHud.cs + Game/RackInput.cs okey ipucu (ShowOkeyTip) | kaldırıldı | kaldırıldı: masada okey temsil rozeti yok; okeyin değeri perdeki konumundan çıkar (R-13) · Unity karşılığı kaldırılacak |
| T34 | masa | Ceza: attığın K9 ile bot açtı (+90, R-43) | ✓ · tekrar Δ7345px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T35 | masa | Eksik açılış cezası (+101) | ✓ · tekrar Δ5636px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T36 | masa | Ceza: masadaki okeyin alındı (+101, R-44) | ✓ · tekrar Δ10893px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T38 | masa | Yerden alındı + Geri bırak | ✓ · tekrar Δ7296px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T39 | masa | Soldan alınan taş halkaya geri sürükleniyor | ✓ · tekrar Δ8596px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T40 | masa | Mesaj kataloğu | — | — | kaldırıldı | kaldırıldı: Mesaj kataloğu bir referans sayfası: Referans - Masa Ek Durumlar |
| T41 | masa | Mesaj kuyruğu | — | — | kaldırıldı | kaldırıldı: Mesaj kuyruğu referans sayfasında; oyunda tek satırlık sistem mesajı + tost |
| T42 | masa | El sonu · botlara karşı | ✓ · tekrar Δ569px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T43 | masa | El sonu · çevrimiçi masa | ✓ · tekrar Δ1102px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T44 | masa | El sonu puansız (çiftle bitiş) | — | Table/Scripts/Hud/TableHud.cs el sonu | kaldırıldı | kaldırıldı: Çiftle bitiş artık puanlanır (×2, R-50…R-56); "Bu el puanlanmadı" yok · Unity karşılığı kaldırılacak |
| T45 | masa | Maç sonu · kazandın | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd + Game/TableGame.cs ShowMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok; maç sonu lobide tost (dereceli: S16 sonuç) · Unity karşılığı kaldırılacak |
| T46 | masa | Maç sonu · 2. sıra | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok (T45 ile aynı) · Unity karşılığı kaldırılacak |
| T47 | masa | Okeyi al: Y4 sürükleniyor, uygun hedefler parlıyor | ✓ · tekrar Δ5011px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T48 | masa | Okey alma ipucu | — | Table/Scripts/Game/TableGame.cs ShowOkeyTip | kaldırıldı | kaldırıldı: Okeye uzun basma ipucu yok · Unity karşılığı kaldırılacak |
| T49 | masa | Okeyi al: yanlış taşla deneme (K5) | ✓ · tekrar Δ7553px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor |  |
| T50 | masa | Menü → masa yükleme ekranı | ✓ · tekrar Δ7250px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor |  |
| T51 | masa | Masa açılamadı hatası | ✓ · tekrar Δ522px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor |  |
