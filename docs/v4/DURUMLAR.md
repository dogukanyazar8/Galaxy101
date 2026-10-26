# v4 · durum tablosu

Üretildi: `python tools/v4/states_table.py` (girdi: `states.json`, `artifacts/v4/ref/capture_report.json`, elle tutulan `docs/v4/durumlar.json`). Bu dosyayı elle düzenleme.

Durum kataloğu: 151 kimlik, 144 etkin, 7 kaldırılmış.

| Durum | Sayı |
|---|---|
| eşleşti | 59 |
| bilinçli fark | 7 |
| eksik | 0 |
| kaldırıldı | 7 |
| bekliyor | 78 |

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
| 01 | menu | Ana menü, Satürn, Tekli seçili | ✓ · tekrar Δ21490px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [run12] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %11.28 |
| 02 | menu | Ana menü · tema mars | ✓ · tekrar Δ21016px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [run12] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %10.98 |
| 03 | menu | Ana menü · tema neptun | ✓ · tekrar Δ22786px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [run12] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.64 |
| 04 | menu | Ana menü · tema jupiter | ✓ · tekrar Δ23884px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [run12] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.68 |
| 05 | menu | Ana menü · tema venus | ✓ · tekrar Δ17506px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [run12] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.65 |
| 06 | menu | Eşli modu seçili | ✓ · tekrar Δ2759px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [run12] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.5 |
| 10 | menu | Ayarlar → Oyun | ✓ · tekrar Δ72px | Scripts/Screens/SettingsController.cs | eşleşti | [run12] metin 65/66 (kayık 0, eksik 0, fazla 0) · ad 65/81 · UI piksel %5.91 |
| 11 | menu | Ayarlar → Ses | ✓ · tekrar Δ533px | Scripts/Screens/SettingsController.cs | eşleşti | [run12] metin 62/63 (kayık 0, eksik 0, fazla 0) · ad 64/80 · UI piksel %5.21 |
| 12 | menu | Ayarlar → Görüntü | ✓ · tekrar Δ150px | Scripts/Screens/SettingsController.cs | bilinçli fark | UF-607 (menü hattı): "Yerel · Monitör en fazla …" gerçek monitörden okunur; prototip sabit 1280×720 yazar [run12] metin 74/77 (kayık 1, eksik 1, fazla 1) · ad 67/85 · UI piksel %10.9 |
| 13 | menu | Ayarlar → Dil | ✓ · tekrar Δ867px | Scripts/Screens/SettingsController.cs | eşleşti | [run12] metin 55/56 (kayık 0, eksik 0, fazla 0) · ad 61/72 · UI piksel %3.0 |
| 14 | menu | Ayarlar → Hesap | ✓ · tekrar Δ365px | Scripts/Screens/SettingsController.cs | eşleşti | [run12] metin 63/64 (kayık 0, eksik 0, fazla 0) · ad 61/74 · UI piksel %5.75 |
| 15 | menu | Profili düzenle | ✓ · tekrar Δ184px | Scripts/Dialogs/EditProfileController.cs | eşleşti | [run12] metin 82/83 (kayık 0, eksik 0, fazla 1) · ad 98/112 · UI piksel %2.91 |
| 16 | menu | Profili düzenle · Altın çerçeve seçili | ✓ · tekrar Δ11538px | Scripts/Dialogs/EditProfileController.cs | eşleşti | [run12] metin 82/83 (kayık 0, eksik 0, fazla 1) · ad 98/112 · UI piksel %3.03 |
| 17 | menu | Profili düzenle · geçersiz ad | ✓ · tekrar Δ10902px | Scripts/Dialogs/EditProfileController.cs | eşleşti | [run12] metin 82/83 (kayık 0, eksik 0, fazla 1) · ad 98/112 · UI piksel %2.68 |
| 18 | menu | Ayarlar → Hesap · kaydedildi (altın çerçeve) | ✓ · tekrar Δ242px | Scripts/Dialogs/EditProfileController.cs | eşleşti | [run12] metin 63/64 (kayık 0, eksik 0, fazla 0) · ad 61/74 · UI piksel %5.76 |
| 19 | menu | Çıkış onayı | ✓ · tekrar Δ70px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | eşleşti | [run12] metin 67/68 (kayık 0, eksik 0, fazla 0) · ad 65/79 · UI piksel %3.11 |
| 20 | menu | Çıkış yapıldı | ✓ · tekrar Δ47px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | eşleşti | [run12] metin 66/67 (kayık 0, eksik 0, fazla 0) · ad 64/78 · UI piksel %1.22 |
| 21 | menu | Hesap silme onayı (pasif) | ✓ · tekrar Δ28px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bilinçli fark | EK-605 (menü hattı): prototip hesap silme metninde DEMO-04 sabiti "125.400 çip ve 340 elmas" yazar; Unity gerçek bakiyeyi gösterir [run12] metin 70/72 (kayık 0, eksik 1, fazla 1) · ad 67/81 · UI piksel %3.63 |
| 22 | menu | SİL yazıldı (aktif) | ✓ · tekrar Δ78px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bilinçli fark | EK-605 (menü hattı): prototip hesap silme metninde DEMO-04 sabiti "125.400 çip ve 340 elmas" yazar; Unity gerçek bakiyeyi gösterir [run12] metin 70/72 (kayık 0, eksik 1, fazla 2) · ad 67/81 · UI piksel %3.67 |
| 30 | menu | Mağaza → Çip | ✓ · tekrar Δ988px | Scripts/Screens/ShopController.cs | eşleşti | [run12] metin 63/64 (kayık 0, eksik 0, fazla 0) · ad 33/70 · UI piksel %6.03 |
| 31 | menu | Mağaza → Elmas | ✓ · tekrar Δ485px | Scripts/Screens/ShopController.cs | eşleşti | [run12] metin 63/64 (kayık 0, eksik 0, fazla 0) · ad 33/70 · UI piksel %5.65 |
| 32 | menu | Mağaza → Görünüm | ✓ · tekrar Δ14166px | Scripts/Screens/ShopController.cs | eşleşti | [run12] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 33/67 · UI piksel %6.8 |
| 40 | menu | Arkadaşlar → Çevrimiçi | ✓ · tekrar Δ191px | Scripts/Screens/FriendsController.cs | eşleşti | [run12] metin 59/60 (kayık 0, eksik 0, fazla 0) · ad 48/66 · UI piksel %3.6 |
| 41 | menu | Arkadaşlar → Tümü | ✓ · tekrar Δ14067px | Scripts/Screens/FriendsController.cs | eşleşti | [run12] metin 71/75 (kayık 3, eksik 0, fazla 0) · ad 60/78 · UI piksel %4.05 |
| 42 | menu | "ay" araması | ✓ · tekrar Δ185px | Scripts/Screens/FriendsController.cs | eşleşti | [run12] metin 49/50 (kayık 0, eksik 0, fazla 1) · ad 41/59 · UI piksel %2.88 |
| 43 | menu | İstekler | ✓ · tekrar Δ67px | Scripts/Screens/FriendsController.cs | eşleşti | [run12] metin 56/57 (kayık 0, eksik 0, fazla 0) · ad 44/63 · UI piksel %3.63 |
| 44 | menu | Arkadaş ekle — boş | ✓ · tekrar Δ17px | Scripts/Dialogs/AddFriendController.cs | eşleşti | [run12] metin 67/68 (kayık 0, eksik 0, fazla 0) · ad 56/75 · UI piksel %4.03 |
| 45 | menu | Arkadaş ekle · MK-3321 bulundu | ✓ · tekrar Δ196px | Scripts/Dialogs/AddFriendController.cs | eşleşti | [run12] metin 72/73 (kayık 0, eksik 0, fazla 1) · ad 59/78 · UI piksel %4.59 |
| 46 | menu | Arkadaş ekle · istek gönderildi | ✓ · tekrar Δ8390px | Scripts/Dialogs/AddFriendController.cs | eşleşti | [run12] metin 72/73 (kayık 0, eksik 0, fazla 1) · ad 59/78 · UI piksel %4.53 |
| 47 | menu | Arkadaş ekle · bulunamadı | ✓ · tekrar Δ8297px | Scripts/Dialogs/AddFriendController.cs | eşleşti | [run12] metin 67/68 (kayık 0, eksik 0, fazla 1) · ad 56/75 · UI piksel %4.78 |
| 50 | menu | Liderlik → Haftalık | ✓ · tekrar Δ107px | Scripts/Screens/LeaderboardController.cs | eşleşti | [run12] metin 97/98 (kayık 0, eksik 0, fazla 0) · ad 76/93 · UI piksel %4.97 |
| 51 | menu | Liderlik → Sezon | ✓ · tekrar Δ83px | Scripts/Screens/LeaderboardController.cs | eşleşti | [run12] metin 97/98 (kayık 0, eksik 0, fazla 0) · ad 76/93 · UI piksel %5.14 |
| 52 | menu | Liderlik → Arkadaşlar | ✓ · tekrar Δ13326px | Scripts/Screens/LeaderboardController.cs | eşleşti | [run12] metin 77/78 (kayık 0, eksik 0, fazla 0) · ad 60/77 · UI piksel %4.08 |
| 60 | menu | Günlük ödül | ✓ · tekrar Δ14384px | Scripts/Screens (DailyReward panel, MenuView) | bekliyor | [run12] metin 72/80 (kayık 7, eksik 0, fazla 0) · ad 32/80 · UI piksel %6.74 |
| 61 | menu | Gün toplandı | ✓ · tekrar Δ13893px | Scripts/Screens (DailyReward panel, MenuView) | bekliyor | [run12] metin 72/80 (kayık 7, eksik 0, fazla 0) · ad 32/80 · UI piksel %6.68 |
| 70 | menu | Kurallar 1 | ✓ · tekrar Δ989px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [run12] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 53/74 · UI piksel %4.7 |
| 71 | menu | Kurallar 2 | ✓ · tekrar Δ93px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [run12] metin 54/55 (kayık 0, eksik 0, fazla 0) · ad 51/68 · UI piksel %4.81 |
| 72 | menu | Kurallar 3 | ✓ · tekrar Δ97px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [run12] metin 63/64 (kayık 0, eksik 0, fazla 0) · ad 53/77 · UI piksel %4.86 |
| 73 | menu | Kurallar 4 | ✓ · tekrar Δ100px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [run12] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 53/74 · UI piksel %4.22 |
| 74 | menu | Kurallar 5 | ✓ · tekrar Δ162px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [run12] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 53/74 · UI piksel %3.92 |
| 75 | menu | Kurallar 6 | ✓ · tekrar Δ117px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [run12] metin 55/56 (kayık 0, eksik 0, fazla 0) · ad 51/69 · UI piksel %5.52 |
| 80 | menu | Profilim → Genel | ✓ · tekrar Δ39px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [run12] metin 93/94 (kayık 0, eksik 0, fazla 0) · ad 88/105 · UI piksel %8.77 |
| 81 | menu | Profilim → Rozetler | ✓ · tekrar Δ707px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | bekliyor | [run12] metin 65/83 (kayık 17, eksik 0, fazla 0) · ad 73/90 · UI piksel %10.03 |
| 82 | menu | Profilim → Maç geçmişi | ✓ · tekrar Δ1007px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [run12] metin 96/97 (kayık 0, eksik 0, fazla 0) · ad 87/104 · UI piksel %5.57 |
| 83 | menu | Profili paylaş | ✓ · tekrar Δ6738px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [run12] metin 124/127 (kayık 2, eksik 0, fazla 0) · ad 121/139 · UI piksel %6.13 |
| 84 | menu | Bağlantı kopyalandı | ✓ · tekrar Δ6881px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [run12] metin 124/127 (kayık 2, eksik 0, fazla 0) · ad 121/139 · UI piksel %6.13 |
| 90 | menu | EN — Ayarlar → Dil | ✓ · tekrar Δ260px | Scripts/Screens/SettingsController.cs | eşleşti | [run12] metin 55/56 (kayık 0, eksik 0, fazla 0) · ad 61/72 · UI piksel %3.43 |
| 91 | menu | EN — Ayarlar → Oyun | ✓ · tekrar Δ136px | Scripts/Screens/SettingsController.cs | eşleşti | [run12] metin 65/66 (kayık 0, eksik 0, fazla 0) · ad 65/81 · UI piksel %6.1 |
| 92 | menu | EN — Ana menü, Satürn, Tekli seçili | ✓ · tekrar Δ3164px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [run12] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %8.71 |
| 93 | menu | EN — Mağaza → Çip | ✓ · tekrar Δ966px | Scripts/Screens/ShopController.cs | eşleşti | [run12] metin 63/64 (kayık 0, eksik 0, fazla 0) · ad 33/70 · UI piksel %5.98 |
| 94 | menu | EN — Arkadaşlar → Çevrimiçi | ✓ · tekrar Δ161px | Scripts/Screens/FriendsController.cs | eşleşti | [run12] metin 58/60 (kayık 1, eksik 0, fazla 0) · ad 48/66 · UI piksel %3.28 |
| 95 | menu | EN — Günlük ödül | ✓ · tekrar Δ14235px | Scripts/Screens (DailyReward panel, MenuView) | bekliyor | [run12] metin 72/80 (kayık 7, eksik 0, fazla 0) · ad 32/80 · UI piksel %7.32 |
| 96 | menu | EN — Kurallar 1 | ✓ · tekrar Δ364px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [run12] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 53/74 · UI piksel %4.98 |
| 97 | menu | EN — Profilim → Genel | ✓ · tekrar Δ28px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [run12] metin 93/94 (kayık 0, eksik 0, fazla 0) · ad 88/105 · UI piksel %8.5 |
| S01 | acilis | Açılış / yükleme (GALAXY + 1 0 1, ilerleme) | ✓ · tekrar Δ3357px | Scripts/Screens/AuthFlow.cs | bilinçli fark | UNITY_FARKLARI UF-02 / EKSIKLER EK-004: ilerleme yüzdesi, mesajı ve ipucu yükleme olaylarına ve çağrı sırasına bağlı [run12] metin 3/10 (kayık 3, eksik 3, fazla 36) · ad 0/14 · UI piksel %46.72 |
| S02 | acilis | Giriş seçimi | ✓ · tekrar Δ1148px | Scripts/Screens/AuthFlow.cs | eşleşti | [run12] metin 18/19 (kayık 0, eksik 0, fazla 33) · ad 1/18 · UI piksel %9.46 |
| S03 | acilis | Giriş yap formu | ✓ · tekrar Δ1828px | Scripts/Screens/AuthFlow.cs | eşleşti | [run12] metin 16/17 (kayık 0, eksik 0, fazla 33) · ad 1/24 · UI piksel %8.19 |
| S04 | acilis | Kayıt ol formu | ✓ · tekrar Δ2651px | Scripts/Screens/AuthFlow.cs | eşleşti | [run12] metin 15/16 (kayık 0, eksik 0, fazla 33) · ad 1/27 · UI piksel %8.04 |
| S05 | acilis | Misafir girişi formu | ✓ · tekrar Δ963px | Scripts/Screens/AuthFlow.cs | eşleşti | [run12] metin 14/15 (kayık 0, eksik 0, fazla 34) · ad 1/27 · UI piksel %8.54 |
| S06 | menu | Ana menü | ✓ · tekrar Δ1685px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [run12] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.32 |
| S07 | menu | Ayarlar → Görüntü | ✓ · tekrar Δ547px | Scripts/Screens/SettingsController.cs | bilinçli fark | UF-607 (menü hattı): "Yerel · Monitör en fazla …" gerçek monitörden okunur; prototip sabit 1280×720 yazar [run12] metin 74/77 (kayık 1, eksik 1, fazla 1) · ad 67/85 · UI piksel %10.9 |
| S08 | menu | Ayarlar → Oyun | ✓ · tekrar Δ17049px | Scripts/Screens/SettingsController.cs | eşleşti | [run12] metin 65/66 (kayık 0, eksik 0, fazla 0) · ad 65/81 · UI piksel %5.91 |
| S09 | lobi | Lobi masa listesi | ✓ · tekrar Δ255px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [run12] metin 155/155 (kayık 0, eksik 0, fazla 33) · ad 91/92 · UI piksel %11.82 |
| S10 | lobi | Masa aç formu | ✓ · tekrar Δ16px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [run12] metin 224/224 (kayık 0, eksik 0, fazla 42) · ad 161/162 · UI piksel %3.94 |
| S11 | lobi | Şifreli masa PIN | ✓ · tekrar Δ93px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [run12] metin 159/159 (kayık 0, eksik 0, fazla 33) · ad 100/101 · UI piksel %0.84 |
| S12 | lobi | Kodla katıl | ✓ · tekrar Δ1392px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [run12] metin 159/159 (kayık 0, eksik 0, fazla 33) · ad 98/99 · UI piksel %1.09 |
| S13 | lobi | Bekleme odası (kurucu) | ✓ · tekrar Δ939px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [run12] metin 48/49 (kayık 1, eksik 0, fazla 0) · ad 55/56 · UI piksel %7.59 |
| S14 | lobi | Bekleme odası · arkadaş davet (Arkadaşlar) | ✓ · tekrar Δ2128px | Scripts/Screens/LobbyScreen*.cs | bilinçli fark | EKSIKLER EK-007 / lobi EK-801: paket karesi davet panelini açmıyor; Unity başlıktaki paneli açar (doğru kare docs/v4/lobby/ref/S14) [run12] metin 48/49 (kayık 1, eksik 0, fazla 44) · ad 55/56 · UI piksel %47.74 |
| S15 | lobi | Bekleme odası · davet (Son oynadıkların) | ✓ · tekrar Δ5858px | Scripts/Screens/LobbyScreen*.cs | bilinçli fark | EKSIKLER EK-007 / lobi EK-801: paket karesi davet panelini açmıyor; Unity başlıktaki paneli açar (doğru kare docs/v4/lobby/ref/S15) [run12] metin 48/49 (kayık 1, eksik 0, fazla 33) · ad 55/56 · UI piksel %47.59 |
| S16 | dereceli | Dereceli ana ekran | ✓ · tekrar Δ1598px | Scripts/Screens/RankedQueue.cs (yalnız kuyruk; dereceli ana ekranı yok) | eşleşti | [run12] metin 48/48 (kayık 0, eksik 0, fazla 0) · ad 48/49 · UI piksel %6.57 |
| G01 | dagitim | Masa yükleniyor | ✓ · tekrar Δ1309px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor | [run11] metin 22/30 (kayık 5, eksik 3, fazla 3) · ad 22/41 · UI piksel %13.0 |
| G02 | dagitim | Bot balyaları diziyor | ✓ · tekrar Δ5188px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 22/28 (kayık 4, eksik 2, fazla 2) · ad 17/37 · UI piksel %14.06 |
| G03 | dagitim | Sen diziyorsun | ✓ · tekrar Δ4978px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 21/28 (kayık 4, eksik 3, fazla 5) · ad 17/37 · UI piksel %14.26 |
| G04 | dagitim | Zar at düğmesi | ✓ · tekrar Δ2869px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 21/28 (kayık 4, eksik 3, fazla 5) · ad 17/37 · UI piksel %15.35 |
| G05 | dagitim | Zar yuvarlanıyor | ✓ · tekrar Δ46456px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 21/28 (kayık 4, eksik 3, fazla 5) · ad 17/37 · UI piksel %15.86 |
| G06 | dagitim | 1. zar · Tekrar at | ✓ · tekrar Δ4562px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 21/28 (kayık 4, eksik 3, fazla 5) · ad 17/37 · UI piksel %15.31 |
| G07 | dagitim | Gösterge açılıyor | ✓ · tekrar Δ60352px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 22/32 (kayık 5, eksik 5, fazla 5) · ad 17/41 · UI piksel %14.65 |
| G08 | dagitim | Gösterge → okey kartı, fazla taş | ✓ · tekrar Δ5854px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 24/32 (kayık 4, eksik 4, fazla 4) · ad 17/41 · UI piksel %15.33 |
| G09 | dagitim | Balyalar dağıtılıyor | ✓ · tekrar Δ4372px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 24/32 (kayık 5, eksik 3, fazla 3) · ad 17/41 · UI piksel %14.68 |
| G10 | dagitim | Kalan balyalar ortaya | ✓ · tekrar Δ4698px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 24/32 (kayık 5, eksik 3, fazla 3) · ad 17/41 · UI piksel %13.05 |
| G11 | dagitim | Istakalar doluyor | ✓ · tekrar Δ5574px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 15/32 (kayık 13, eksik 4, fazla 4) · ad 17/41 · UI piksel %38.77 |
| G12 | dagitim | Oyun başladı (22 taş) | ✓ · tekrar Δ72214px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [run11] metin 23/30 (kayık 5, eksik 2, fazla 2) · ad 23/38 · UI piksel %17.63 |
| G13 | oyun | El açıldı: perler ortada | ✓ · tekrar Δ4639px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/33 (kayık 8, eksik 1, fazla 2) · ad 25/40 · UI piksel %17.64 |
| G14 | oyun | İşleme testi (herkes açık) | ✓ · tekrar Δ3509px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/34 (kayık 12, eksik 1, fazla 1) · ad 25/40 · UI piksel %17.18 |
| G15 | oyun | Altın işleme göstergeleri | ✓ · tekrar Δ4575px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/34 (kayık 11, eksik 2, fazla 1) · ad 25/40 · UI piksel %20.79 |
| G16 | oyun | Yeşil okey alma göstergesi (Y4 seçili) | ✓ · tekrar Δ3904px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/34 (kayık 12, eksik 1, fazla 1) · ad 25/40 · UI piksel %16.13 |
| G17 | oyun | Okey alındı (Y4 ↔ okey) | ✓ · tekrar Δ16884px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 19/36 (kayık 14, eksik 3, fazla 3) · ad 26/42 · UI piksel %24.86 |
| G18 | oyun | İşleme yapıldı + Geri topla | ✓ · tekrar Δ3085px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 23/34 (kayık 10, eksik 1, fazla 2) · ad 25/40 · UI piksel %17.47 |
| G19 | oyun | Geri toplandı | ✓ · tekrar Δ2855px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/34 (kayık 11, eksik 2, fazla 2) · ad 25/40 · UI piksel %19.19 |
| G20 | oyun | Hatalı açma (turuncu Aç) | ✓ · tekrar Δ7076px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/32 (kayık 7, eksik 1, fazla 1) · ad 24/39 · UI piksel %19.12 |
| G21 | oyun | Hatalı per masada | ✓ · tekrar Δ3838px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/33 (kayık 11, eksik 1, fazla 2) · ad 24/39 · UI piksel %17.87 |
| G22 | oyun | Ceza tostu | ✓ · tekrar Δ9020px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 25/32 (kayık 6, eksik 1, fazla 2) · ad 24/39 · UI piksel %18.07 |
| G23 | oyun | İşlek taş testi | ✓ · tekrar Δ6029px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/34 (kayık 12, eksik 1, fazla 1) · ad 25/40 · UI piksel %17.31 |
| G24 | oyun | İşlek taş cezası (B4 atıldı) | ✓ · tekrar Δ4094px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 22/32 (kayık 8, eksik 2, fazla 3) · ad 23/38 · UI piksel %15.12 |
| G25 | oyun | Elden bit düğmesi | ✓ · tekrar Δ19579px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/31 (kayık 6, eksik 1, fazla 1) · ad 24/39 · UI piksel %16.68 |
| G26 | oyun | Taş fırlatılıyor | ✓ · tekrar Δ4280px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/31 (kayık 6, eksik 1, fazla 1) · ad 24/39 · UI piksel %16.71 |
| G27 | oyun | Nükleer patlama | ✓ · tekrar Δ5804px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/31 (kayık 6, eksik 1, fazla 1) · ad 24/39 · UI piksel %17.44 |
| G28 | oyun | ELDEN BİTTİN | ✓ · tekrar Δ19872px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/31 (kayık 6, eksik 1, fazla 1) · ad 24/39 · UI piksel %19.07 |
| G29 | oyun | El sonu (kazandın) | ✓ · tekrar Δ5701px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/31 (kayık 6, eksik 1, fazla 1) · ad 24/39 · UI piksel %17.98 |
| G30 | oyun | Sıra sende | ✓ · tekrar Δ14166px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 25/31 (kayık 4, eksik 2, fazla 3) · ad 23/39 · UI piksel %15.05 |
| G31 | oyun | Soldan alındı + Geri bırak | ✓ · tekrar Δ14967px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/33 (kayık 7, eksik 2, fazla 10) · ad 25/41 · UI piksel %16.85 |
| G32 | oyun | Masa ayarları | ✓ · tekrar Δ2687px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 27/88 (kayık 57, eksik 4, fazla 33) · ad 23/85 · UI piksel %26.88 |
| G33 | oyun | Masadan kalk onayı | ✓ · tekrar Δ11697px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 25/34 (kayık 7, eksik 2, fazla 2) · ad 23/43 · UI piksel %10.62 |
| G34 | oyun | El sonu (Ayşe kazandı) | ✓ · tekrar Δ2274px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/68 (kayık 34, eksik 10, fazla 8) · ad 26/75 · UI piksel %19.75 |
| T01 | masa | Bekleme: 2/4 oyuncu | ✓ · tekrar Δ2723px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 14/19 (kayık 5, eksik 0, fazla 0) · ad 17/26 · UI piksel %11.28 |
| T02 | masa | Masa doldu, geri sayım | ✓ · tekrar Δ5250px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 13/26 (kayık 13, eksik 0, fazla 0) · ad 19/36 · UI piksel %32.68 |
| T03 | masa | Dağıtım: girdap, karma | ✓ · tekrar Δ2048px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 22/28 (kayık 4, eksik 2, fazla 2) · ad 17/37 · UI piksel %15.22 |
| T04 | masa | Dağıtım bitti, İlk hamle senin | ✓ · tekrar Δ5518px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/30 (kayık 4, eksik 2, fazla 2) · ad 23/38 · UI piksel %14.95 |
| T05 | masa | Çekme aşaması, Ayşe'nin perleri | ✓ · tekrar Δ12249px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 25/31 (kayık 4, eksik 2, fazla 3) · ad 23/39 · UI piksel %15.43 |
| T06 | masa | Çay ısmarlandı bildirimi | ✓ · tekrar Δ9093px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [run11] metin 25/31 (kayık 4, eksik 2, fazla 6) · ad 23/39 · UI piksel %15.24 |
| T07 | masa | Oyuncu kartı (Zeynep), hızlı ikram | ✓ · tekrar Δ8203px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [run11] metin 28/47 (kayık 13, eksik 6, fazla 5) · ad 23/54 · UI piksel %19.87 |
| T08 | masa | Hediye paneli · İkram | ✓ · tekrar Δ7176px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [run11] metin 41/59 (kayık 7, eksik 11, fazla 5) · ad 23/72 · UI piksel %13.38 |
| T09 | masa | Hediye paneli · Hediye (Pasta) | ✓ · tekrar Δ5543px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [run11] metin 41/55 (kayık 5, eksik 9, fazla 5) · ad 23/68 · UI piksel %13.43 |
| T10 | masa | Hediye paneli · Emoji | ✓ · tekrar Δ5838px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [run11] metin 48/65 (kayık 13, eksik 4, fazla 3) · ad 23/78 · UI piksel %11.8 |
| T11 | masa | Roket uçuşu | ✓ · tekrar Δ261144px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [run11] metin 16/34 (kayık 16, eksik 2, fazla 2) · ad 23/42 · UI piksel %19.09 |
| T12 | masa | Roket indi + teşekkür balonu | ✓ · tekrar Δ9570px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [run11] metin 25/31 (kayık 4, eksik 2, fazla 2) · ad 23/39 · UI piksel %13.73 |
| T13 | masa | Sohbet paneli | ✓ · tekrar Δ3972px | Table/Scripts/Hud/TableHud.Social.cs | bekliyor | [run11] metin 30/49 (kayık 8, eksik 11, fazla 3) · ad 23/61 · UI piksel %17.2 |
| T14 | masa | El açıldı · Yere aç / İşle | ✓ · tekrar Δ3915px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/33 (kayık 8, eksik 1, fazla 2) · ad 25/40 · UI piksel %15.81 |
| T15 | masa | Ayarlar · Görünüm | ✓ · tekrar Δ1022px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [run11] metin 27/88 (kayık 57, eksik 4, fazla 33) · ad 23/85 · UI piksel %26.91 |
| T16 | masa | Masa ayarları · Oyun | ✓ · tekrar Δ2449px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [run11] metin 28/55 (kayık 25, eksik 2, fazla 3) · ad 23/70 · UI piksel %14.3 |
| T17 | masa | Masa ayarları · Ses | ✓ · tekrar Δ1689px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [run11] metin 28/52 (kayık 22, eksik 2, fazla 3) · ad 23/71 · UI piksel %15.2 |
| T18 | masa | Masa ayarları · Bildirimler | ✓ · tekrar Δ2346px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [run11] metin 28/47 (kayık 17, eksik 2, fazla 3) · ad 23/65 · UI piksel %13.19 |
| T19 | masa | Tema: Ceviz salon + Klasik ceviz ıstaka | ✓ · tekrar Δ14955px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [run11] metin 26/31 (kayık 4, eksik 1, fazla 2) · ad 23/39 · UI piksel %16.1 |
| T20 | masa | Tema: Hologram + Neon cam ıstaka | ✓ · tekrar Δ25799px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [run11] metin 26/31 (kayık 4, eksik 1, fazla 2) · ad 23/39 · UI piksel %18.66 |
| T21 | masa | Tema: Andromeda masa + ıstaka | ✓ · tekrar Δ10346px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [run11] metin 26/31 (kayık 4, eksik 1, fazla 2) · ad 23/39 · UI piksel %18.32 |
| T22 | masa | Kuşbakışı görünüm (Andromeda) | ✓ · tekrar Δ5317px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [run11] metin 26/31 (kayık 4, eksik 1, fazla 2) · ad 23/39 · UI piksel %21.38 |
| T23 | masa | Bitiş anı: Ayşe bitirdi | ✓ · tekrar Δ6455px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/33 (kayık 7, eksik 2, fazla 2) · ad 23/42 · UI piksel %30.06 |
| T24 | masa | El sonu tablosu | ✓ · tekrar Δ3153px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/68 (kayık 34, eksik 10, fazla 8) · ad 26/75 · UI piksel %19.67 |
| T25 | masa | Masadan kalk onayı | ✓ · tekrar Δ8869px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 25/34 (kayık 7, eksik 2, fazla 2) · ad 23/43 · UI piksel %10.61 |
| T26 | masa | Bağlantı koptu | ✓ · tekrar Δ5520px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 27/36 (kayık 8, eksik 1, fazla 1) · ad 29/44 · UI piksel %13.3 |
| T27 | masa | EN — oyun sırasında | ✓ · tekrar Δ12860px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/31 (kayık 3, eksik 4, fazla 5) · ad 23/39 · UI piksel %18.93 |
| T28 | masa | Botlara karşı: başlık, ANTRENMAN, el sayacı | ✓ · tekrar Δ32733px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 20/31 (kayık 9, eksik 2, fazla 4) · ad 23/39 · UI piksel %19.77 |
| T30 | masa | Botlara karşı · masadan kalk onayı | ✓ · tekrar Δ8555px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 20/34 (kayık 12, eksik 2, fazla 3) · ad 23/43 · UI piksel %11.81 |
| T33 | masa | Okey rozeti masada | — | Table/Scripts/Hud/TableHud.cs + Game/RackInput.cs okey ipucu (ShowOkeyTip) | kaldırıldı | kaldırıldı: masada okey temsil rozeti yok; okeyin değeri perdeki konumundan çıkar (R-13) · Unity karşılığı kaldırılacak |
| T34 | masa | Ceza: attığın K9 ile bot açtı (+90, R-43) | ✓ · tekrar Δ9560px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/35 (kayık 8, eksik 3, fazla 6) · ad 23/42 · UI piksel %20.18 |
| T35 | masa | Eksik açılış cezası (+101) | ✓ · tekrar Δ7821px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/33 (kayık 11, eksik 1, fazla 2) · ad 24/39 · UI piksel %18.3 |
| T36 | masa | Ceza: masadaki okeyin alındı (+101, R-44) | ✓ · tekrar Δ10113px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/36 (kayık 12, eksik 3, fazla 5) · ad 23/42 · UI piksel %17.6 |
| T38 | masa | Yerden alındı + Geri bırak | ✓ · tekrar Δ14244px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/33 (kayık 7, eksik 2, fazla 10) · ad 25/41 · UI piksel %18.02 |
| T39 | masa | Soldan alınan taş halkaya geri sürükleniyor | ✓ · tekrar Δ9928px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 27/36 (kayık 7, eksik 2, fazla 7) · ad 25/44 · UI piksel %18.48 |
| T40 | masa | Mesaj kataloğu | — | — | kaldırıldı | kaldırıldı: Mesaj kataloğu bir referans sayfası: Referans - Masa Ek Durumlar |
| T41 | masa | Mesaj kuyruğu | — | — | kaldırıldı | kaldırıldı: Mesaj kuyruğu referans sayfasında; oyunda tek satırlık sistem mesajı + tost |
| T42 | masa | El sonu · botlara karşı | ✓ · tekrar Δ4261px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 20/68 (kayık 38, eksik 10, fazla 9) · ad 26/75 · UI piksel %19.31 |
| T43 | masa | El sonu · çevrimiçi masa | ✓ · tekrar Δ1293px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 24/68 (kayık 34, eksik 10, fazla 8) · ad 26/75 · UI piksel %19.77 |
| T44 | masa | El sonu puansız (çiftle bitiş) | — | Table/Scripts/Hud/TableHud.cs el sonu | kaldırıldı | kaldırıldı: Çiftle bitiş artık puanlanır (×2, R-50…R-56); "Bu el puanlanmadı" yok · Unity karşılığı kaldırılacak |
| T45 | masa | Maç sonu · kazandın | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd + Game/TableGame.cs ShowMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok; maç sonu lobide tost (dereceli: S16 sonuç) · Unity karşılığı kaldırılacak |
| T46 | masa | Maç sonu · 2. sıra | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok (T45 ile aynı) · Unity karşılığı kaldırılacak |
| T47 | masa | Okeyi al: Y4 sürükleniyor, uygun hedefler parlıyor | ✓ · tekrar Δ4881px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/34 (kayık 12, eksik 1, fazla 1) · ad 25/40 · UI piksel %17.45 |
| T48 | masa | Okey alma ipucu | — | Table/Scripts/Game/TableGame.cs ShowOkeyTip | kaldırıldı | kaldırıldı: Okeye uzun basma ipucu yok · Unity karşılığı kaldırılacak |
| T49 | masa | Okeyi al: yanlış taşla deneme (K5) | ✓ · tekrar Δ7299px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [run11] metin 21/34 (kayık 11, eksik 2, fazla 2) · ad 25/40 · UI piksel %16.31 |
| T50 | masa | Menü → masa yükleme ekranı | ✓ · tekrar Δ7217px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor | [run11] metin 22/30 (kayık 6, eksik 2, fazla 2) · ad 22/41 · UI piksel %16.69 |
| T51 | masa | Masa açılamadı hatası | ✓ · tekrar Δ658px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor | [run11] metin 3/10 (kayık 3, eksik 4, fazla 23) · ad 1/14 · UI piksel %17.17 |
