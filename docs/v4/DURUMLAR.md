# v4 · durum tablosu

Üretildi: `python tools/v4/states_table.py` (girdi: `states.json`, `artifacts/v4/ref/capture_report.json`, elle tutulan `docs/v4/durumlar.json`). Bu dosyayı elle düzenleme.

Durum kataloğu: 151 kimlik, 144 etkin, 7 kaldırılmış.

| Durum | Sayı |
|---|---|
| eşleşti | 63 |
| bilinçli fark | 7 |
| eksik | 0 |
| kaldırıldı | 7 |
| bekliyor | 74 |

## Eşikler ve yöntem

- Referans: `artifacts/v4/ref/<ID>.png` (Chrome, GPU, 1280×720, seed 42, ft = katalog `hold`); Unity: `scripts/Capture-G101States.ps1` (Development player, pencereli 1280×720, aynı ID/seed/ft).
- Metin kutuları: `tools/compare_ui.py` ±3 px konum, genişlik/yükseklik ±%6 (en az 3 px), yazı boyutu ±0,5 px. "eşleşti" için referanstaki hiçbir metin eksik olmayacak ve en çok %5'i eşik dışında kalabilir.
- Yerleşim dökümü: aynı `data-g101-id` adlı öğeler için kutu ±3 px; renk, font, harf aralığı, köşe birebir.
- Piksel farkı: yalnız referansın UI öğe kutularında, gri ton farkı > 24 olan pikseller; tam ekran katmanlar, boyanmayan `auth.side` yerleşim sütunu (logo/metin çocukları dahil kalır) ve Unity filigranı (UF-01) dışarıda. Referansın kendi tekrar farkı bu ölçüde %0. Eşik %12: yapısı birebir aynı bir karede (durum 40, run3) kalan fark yalnız yazı kenar yumuşatmasıdır (Chrome ↔ Unity). 3D masa kareleri ayrıca göz kontrolü + `tween3d`/`materials`/`lighting` değerleriyle.
- "eşleşti": metin ve öğe eşikleri içinde; "bilinçli fark": nedeni `UNITY_FARKLARI.md`'de; "eksik": `EKSIKLER.md`'de kayıtlı.

## Tablo

Ref: `artifacts/v4/ref/<ID>.png` + `.layout.json` (✓ çekildi; `tekrar Δ` = aynı adresin ikinci açılışıyla farklı piksel sayısı, EK-001).

| Kimlik | Grup | Başlık | Ref | Unity karşılığı | Durum | Not |
|---|---|---|---|---|---|---|
| 01 | menu | Ana menü, Satürn, Tekli seçili | ✓ · tekrar Δ21490px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [safe18-all-tr] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.75 |
| 02 | menu | Ana menü · tema mars | ✓ · tekrar Δ21016px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [safe18-all-tr] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.57 |
| 03 | menu | Ana menü · tema neptun | ✓ · tekrar Δ22786px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [safe18-all-tr] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.81 |
| 04 | menu | Ana menü · tema jupiter | ✓ · tekrar Δ23884px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [safe18-all-tr] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.84 |
| 05 | menu | Ana menü · tema venus | ✓ · tekrar Δ17506px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [safe18-all-tr] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.79 |
| 06 | menu | Eşli modu seçili | ✓ · tekrar Δ2759px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [safe18-all-tr] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.5 |
| 10 | menu | Ayarlar → Oyun | ✓ · tekrar Δ72px | Scripts/Screens/SettingsController.cs | eşleşti | [safe18-all-tr] metin 65/66 (kayık 0, eksik 0, fazla 0) · ad 65/81 · UI piksel %5.92 |
| 11 | menu | Ayarlar → Ses | ✓ · tekrar Δ533px | Scripts/Screens/SettingsController.cs | eşleşti | [safe18-all-tr] metin 62/63 (kayık 0, eksik 0, fazla 0) · ad 64/80 · UI piksel %5.21 |
| 12 | menu | Ayarlar → Görüntü | ✓ · tekrar Δ150px | Scripts/Screens/SettingsController.cs | bilinçli fark | UF-607 (menü hattı): "Yerel · Monitör en fazla …" gerçek monitörden okunur; prototip sabit 1280×720 yazar [safe18-all-tr] metin 74/77 (kayık 1, eksik 1, fazla 1) · ad 67/85 · UI piksel %10.9 |
| 13 | menu | Ayarlar → Dil | ✓ · tekrar Δ867px | Scripts/Screens/SettingsController.cs | eşleşti | [safe18-all-tr] metin 55/56 (kayık 0, eksik 0, fazla 0) · ad 61/72 · UI piksel %3.0 |
| 14 | menu | Ayarlar → Hesap | ✓ · tekrar Δ365px | Scripts/Screens/SettingsController.cs | eşleşti | [safe18-all-tr] metin 63/64 (kayık 0, eksik 0, fazla 0) · ad 61/74 · UI piksel %5.75 |
| 15 | menu | Profili düzenle | ✓ · tekrar Δ184px | Scripts/Dialogs/EditProfileController.cs | eşleşti | [safe18-all-tr] metin 82/83 (kayık 0, eksik 0, fazla 1) · ad 98/112 · UI piksel %2.91 |
| 16 | menu | Profili düzenle · Altın çerçeve seçili | ✓ · tekrar Δ11538px | Scripts/Dialogs/EditProfileController.cs | eşleşti | [safe18-all-tr] metin 82/83 (kayık 0, eksik 0, fazla 1) · ad 98/112 · UI piksel %3.03 |
| 17 | menu | Profili düzenle · geçersiz ad | ✓ · tekrar Δ10902px | Scripts/Dialogs/EditProfileController.cs | eşleşti | [safe18-all-tr] metin 82/83 (kayık 0, eksik 0, fazla 1) · ad 98/112 · UI piksel %2.69 |
| 18 | menu | Ayarlar → Hesap · kaydedildi (altın çerçeve) | ✓ · tekrar Δ242px | Scripts/Dialogs/EditProfileController.cs | eşleşti | [safe18-all-tr] metin 63/64 (kayık 0, eksik 0, fazla 0) · ad 61/74 · UI piksel %5.77 |
| 19 | menu | Çıkış onayı | ✓ · tekrar Δ70px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | eşleşti | [safe18-all-tr] metin 67/68 (kayık 0, eksik 0, fazla 0) · ad 65/79 · UI piksel %3.1 |
| 20 | menu | Çıkış yapıldı | ✓ · tekrar Δ47px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | eşleşti | [safe18-all-tr] metin 66/67 (kayık 0, eksik 0, fazla 0) · ad 64/78 · UI piksel %1.22 |
| 21 | menu | Hesap silme onayı (pasif) | ✓ · tekrar Δ28px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bilinçli fark | EK-605 (menü hattı): prototip hesap silme metninde DEMO-04 sabiti "125.400 çip ve 340 elmas" yazar; Unity gerçek bakiyeyi gösterir [safe18-all-tr] metin 70/72 (kayık 0, eksik 1, fazla 1) · ad 67/81 · UI piksel %3.64 |
| 22 | menu | SİL yazıldı (aktif) | ✓ · tekrar Δ78px | Scripts/Dialogs/ConfirmController.cs, DoneController.cs | bilinçli fark | EK-605 (menü hattı): prototip hesap silme metninde DEMO-04 sabiti "125.400 çip ve 340 elmas" yazar; Unity gerçek bakiyeyi gösterir [safe18-all-tr] metin 70/72 (kayık 0, eksik 1, fazla 2) · ad 67/81 · UI piksel %3.68 |
| 30 | menu | Mağaza → Çip | ✓ · tekrar Δ988px | Scripts/Screens/ShopController.cs | eşleşti | [safe18-all-tr] metin 62/64 (kayık 1, eksik 0, fazla 0) · ad 33/70 · UI piksel %6.03 |
| 31 | menu | Mağaza → Elmas | ✓ · tekrar Δ485px | Scripts/Screens/ShopController.cs | eşleşti | [safe18-all-tr] metin 62/64 (kayık 1, eksik 0, fazla 0) · ad 33/70 · UI piksel %5.65 |
| 32 | menu | Mağaza → Görünüm | ✓ · tekrar Δ14166px | Scripts/Screens/ShopController.cs | eşleşti | [safe18-all-tr] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 33/67 · UI piksel %6.8 |
| 40 | menu | Arkadaşlar → Çevrimiçi | ✓ · tekrar Δ191px | Scripts/Screens/FriendsController.cs | eşleşti | [safe18-all-tr] metin 59/60 (kayık 0, eksik 0, fazla 0) · ad 48/66 · UI piksel %3.61 |
| 41 | menu | Arkadaşlar → Tümü | ✓ · tekrar Δ14067px | Scripts/Screens/FriendsController.cs | eşleşti | [safe18-all-tr] metin 71/75 (kayık 3, eksik 0, fazla 0) · ad 60/78 · UI piksel %4.06 |
| 42 | menu | "ay" araması | ✓ · tekrar Δ185px | Scripts/Screens/FriendsController.cs | eşleşti | [safe18-all-tr] metin 49/50 (kayık 0, eksik 0, fazla 1) · ad 41/59 · UI piksel %2.89 |
| 43 | menu | İstekler | ✓ · tekrar Δ67px | Scripts/Screens/FriendsController.cs | eşleşti | [safe18-all-tr] metin 56/57 (kayık 0, eksik 0, fazla 0) · ad 44/63 · UI piksel %3.64 |
| 44 | menu | Arkadaş ekle — boş | ✓ · tekrar Δ17px | Scripts/Dialogs/AddFriendController.cs | eşleşti | [safe18-all-tr] metin 67/68 (kayık 0, eksik 0, fazla 0) · ad 56/75 · UI piksel %4.03 |
| 45 | menu | Arkadaş ekle · MK-3321 bulundu | ✓ · tekrar Δ196px | Scripts/Dialogs/AddFriendController.cs | eşleşti | [safe18-all-tr] metin 72/73 (kayık 0, eksik 0, fazla 1) · ad 59/78 · UI piksel %4.58 |
| 46 | menu | Arkadaş ekle · istek gönderildi | ✓ · tekrar Δ8390px | Scripts/Dialogs/AddFriendController.cs | eşleşti | [safe18-all-tr] metin 72/73 (kayık 0, eksik 0, fazla 1) · ad 59/78 · UI piksel %4.53 |
| 47 | menu | Arkadaş ekle · bulunamadı | ✓ · tekrar Δ8297px | Scripts/Dialogs/AddFriendController.cs | eşleşti | [safe18-all-tr] metin 67/68 (kayık 0, eksik 0, fazla 1) · ad 56/75 · UI piksel %4.77 |
| 50 | menu | Liderlik → Haftalık | ✓ · tekrar Δ107px | Scripts/Screens/LeaderboardController.cs | eşleşti | [safe18-all-tr] metin 97/98 (kayık 0, eksik 0, fazla 0) · ad 76/93 · UI piksel %4.97 |
| 51 | menu | Liderlik → Sezon | ✓ · tekrar Δ83px | Scripts/Screens/LeaderboardController.cs | eşleşti | [safe18-all-tr] metin 97/98 (kayık 0, eksik 0, fazla 0) · ad 76/93 · UI piksel %5.15 |
| 52 | menu | Liderlik → Arkadaşlar | ✓ · tekrar Δ13326px | Scripts/Screens/LeaderboardController.cs | eşleşti | [safe18-all-tr] metin 77/78 (kayık 0, eksik 0, fazla 0) · ad 60/77 · UI piksel %4.09 |
| 60 | menu | Günlük ödül | ✓ · tekrar Δ14384px | Scripts/Screens (DailyReward panel, MenuView) | eşleşti | [safe18-all-tr] metin 79/80 (kayık 0, eksik 0, fazla 0) · ad 32/80 · UI piksel %5.89 |
| 61 | menu | Gün toplandı | ✓ · tekrar Δ13893px | Scripts/Screens (DailyReward panel, MenuView) | eşleşti | [safe18-all-tr] metin 79/80 (kayık 0, eksik 0, fazla 0) · ad 32/80 · UI piksel %5.91 |
| 70 | menu | Kurallar 1 | ✓ · tekrar Δ989px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [safe18-all-tr] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 53/74 · UI piksel %4.7 |
| 71 | menu | Kurallar 2 | ✓ · tekrar Δ93px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [safe18-all-tr] metin 54/55 (kayık 0, eksik 0, fazla 0) · ad 51/68 · UI piksel %4.81 |
| 72 | menu | Kurallar 3 | ✓ · tekrar Δ97px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [safe18-all-tr] metin 63/64 (kayık 0, eksik 0, fazla 0) · ad 53/77 · UI piksel %4.87 |
| 73 | menu | Kurallar 4 | ✓ · tekrar Δ100px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [safe18-all-tr] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 53/74 · UI piksel %4.22 |
| 74 | menu | Kurallar 5 | ✓ · tekrar Δ162px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [safe18-all-tr] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 53/74 · UI piksel %3.92 |
| 75 | menu | Kurallar 6 | ✓ · tekrar Δ117px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [safe18-all-tr] metin 55/56 (kayık 0, eksik 0, fazla 0) · ad 51/69 · UI piksel %5.53 |
| 80 | menu | Profilim → Genel | ✓ · tekrar Δ39px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [safe18-all-tr] metin 93/94 (kayık 0, eksik 0, fazla 0) · ad 88/105 · UI piksel %8.78 |
| 81 | menu | Profilim → Rozetler | ✓ · tekrar Δ707px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [safe18-all-tr] metin 82/83 (kayık 0, eksik 0, fazla 0) · ad 73/90 · UI piksel %7.22 |
| 82 | menu | Profilim → Maç geçmişi | ✓ · tekrar Δ1007px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [safe18-all-tr] metin 96/97 (kayık 0, eksik 0, fazla 0) · ad 87/104 · UI piksel %5.57 |
| 83 | menu | Profili paylaş | ✓ · tekrar Δ6738px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [safe18-all-tr] metin 124/127 (kayık 2, eksik 0, fazla 0) · ad 121/139 · UI piksel %6.13 |
| 84 | menu | Bağlantı kopyalandı | ✓ · tekrar Δ6881px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [safe18-all-tr] metin 124/127 (kayık 2, eksik 0, fazla 0) · ad 121/139 · UI piksel %6.13 |
| 90 | menu | EN — Ayarlar → Dil | ✓ · tekrar Δ260px | Scripts/Screens/SettingsController.cs | eşleşti | [safe18-all-tr] metin 55/56 (kayık 0, eksik 0, fazla 0) · ad 61/72 · UI piksel %3.43 |
| 91 | menu | EN — Ayarlar → Oyun | ✓ · tekrar Δ136px | Scripts/Screens/SettingsController.cs | eşleşti | [safe18-all-tr] metin 65/66 (kayık 0, eksik 0, fazla 0) · ad 65/81 · UI piksel %6.11 |
| 92 | menu | EN — Ana menü, Satürn, Tekli seçili | ✓ · tekrar Δ3164px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [safe18-all-tr] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %8.88 |
| 93 | menu | EN — Mağaza → Çip | ✓ · tekrar Δ966px | Scripts/Screens/ShopController.cs | eşleşti | [safe18-all-tr] metin 62/64 (kayık 1, eksik 0, fazla 0) · ad 33/70 · UI piksel %5.99 |
| 94 | menu | EN — Arkadaşlar → Çevrimiçi | ✓ · tekrar Δ161px | Scripts/Screens/FriendsController.cs | eşleşti | [safe18-all-tr] metin 58/60 (kayık 1, eksik 0, fazla 0) · ad 48/66 · UI piksel %3.28 |
| 95 | menu | EN — Günlük ödül | ✓ · tekrar Δ14235px | Scripts/Screens (DailyReward panel, MenuView) | eşleşti | [safe18-all-tr] metin 79/80 (kayık 0, eksik 0, fazla 0) · ad 32/80 · UI piksel %6.39 |
| 96 | menu | EN — Kurallar 1 | ✓ · tekrar Δ364px | Scripts/Screens (Rules panel, MenuView) | eşleşti | [safe18-all-tr] metin 60/61 (kayık 0, eksik 0, fazla 0) · ad 53/74 · UI piksel %4.98 |
| 97 | menu | EN — Profilim → Genel | ✓ · tekrar Δ28px | Scripts/Screens/ProfileController.cs (+ ShareProfile) | eşleşti | [safe18-all-tr] metin 93/94 (kayık 0, eksik 0, fazla 0) · ad 88/105 · UI piksel %8.5 |
| S01 | acilis | Açılış / yükleme (GALAXY + 1 0 1, ilerleme) | ✓ · tekrar Δ3357px | Scripts/Screens/AuthFlow.cs | bilinçli fark | UNITY_FARKLARI UF-02 / EKSIKLER EK-004: ilerleme yüzdesi, mesajı ve ipucu yükleme olaylarına ve çağrı sırasına bağlı [safe18-all-tr] metin 3/10 (kayık 3, eksik 3, fazla 36) · ad 0/14 · UI piksel %46.67 |
| S02 | acilis | Giriş seçimi | ✓ · tekrar Δ1148px | Scripts/Screens/AuthFlow.cs | eşleşti | [safe18-all-tr] metin 18/19 (kayık 0, eksik 0, fazla 33) · ad 1/18 · UI piksel %9.67 |
| S03 | acilis | Giriş yap formu | ✓ · tekrar Δ1828px | Scripts/Screens/AuthFlow.cs | eşleşti | [safe18-all-tr] metin 16/17 (kayık 0, eksik 0, fazla 33) · ad 1/24 · UI piksel %6.43 |
| S04 | acilis | Kayıt ol formu | ✓ · tekrar Δ2651px | Scripts/Screens/AuthFlow.cs | eşleşti | [safe18-all-tr] metin 15/16 (kayık 0, eksik 0, fazla 33) · ad 1/27 · UI piksel %6.13 |
| S05 | acilis | Misafir girişi formu | ✓ · tekrar Δ963px | Scripts/Screens/AuthFlow.cs | eşleşti | [safe18-all-tr] metin 14/15 (kayık 0, eksik 0, fazla 34) · ad 1/27 · UI piksel %7.23 |
| S06 | menu | Ana menü | ✓ · tekrar Δ1685px | Scripts/Core/MenuView.cs, Scripts/Screens/MenuView.Main.cs | eşleşti | [safe18-all-tr] metin 36/37 (kayık 0, eksik 0, fazla 0) · ad 32/42 · UI piksel %9.45 |
| S07 | menu | Ayarlar → Görüntü | ✓ · tekrar Δ547px | Scripts/Screens/SettingsController.cs | bilinçli fark | UF-607 (menü hattı): "Yerel · Monitör en fazla …" gerçek monitörden okunur; prototip sabit 1280×720 yazar [safe18-all-tr] metin 74/77 (kayık 1, eksik 1, fazla 1) · ad 67/85 · UI piksel %10.9 |
| S08 | menu | Ayarlar → Oyun | ✓ · tekrar Δ17049px | Scripts/Screens/SettingsController.cs | eşleşti | [safe18-all-tr] metin 65/66 (kayık 0, eksik 0, fazla 0) · ad 65/81 · UI piksel %5.92 |
| S09 | lobi | Lobi masa listesi | ✓ · tekrar Δ255px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [safe18-all-tr] metin 155/155 (kayık 0, eksik 0, fazla 33) · ad 91/92 · UI piksel %11.89 |
| S10 | lobi | Masa aç formu | ✓ · tekrar Δ16px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [safe18-all-tr] metin 224/224 (kayık 0, eksik 0, fazla 42) · ad 161/162 · UI piksel %3.95 |
| S11 | lobi | Şifreli masa PIN | ✓ · tekrar Δ93px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [safe18-all-tr] metin 159/159 (kayık 0, eksik 0, fazla 33) · ad 100/101 · UI piksel %0.84 |
| S12 | lobi | Kodla katıl | ✓ · tekrar Δ1392px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [safe18-all-tr] metin 159/159 (kayık 0, eksik 0, fazla 33) · ad 98/99 · UI piksel %1.09 |
| S13 | lobi | Bekleme odası (kurucu) | ✓ · tekrar Δ939px | Scripts/Screens/LobbyScreen*.cs | eşleşti | [safe18-all-tr] metin 48/49 (kayık 1, eksik 0, fazla 0) · ad 55/56 · UI piksel %7.59 |
| S14 | lobi | Bekleme odası · arkadaş davet (Arkadaşlar) | ✓ · tekrar Δ2128px | Scripts/Screens/LobbyScreen*.cs | bilinçli fark | EKSIKLER EK-007 / lobi EK-801: paket karesi davet panelini açmıyor; Unity başlıktaki paneli açar (doğru kare docs/v4/lobby/ref/S14) [safe18-all-tr] metin 48/49 (kayık 1, eksik 0, fazla 44) · ad 55/56 · UI piksel %47.75 |
| S15 | lobi | Bekleme odası · davet (Son oynadıkların) | ✓ · tekrar Δ5858px | Scripts/Screens/LobbyScreen*.cs | bilinçli fark | EKSIKLER EK-007 / lobi EK-801: paket karesi davet panelini açmıyor; Unity başlıktaki paneli açar (doğru kare docs/v4/lobby/ref/S15) [safe18-all-tr] metin 48/49 (kayık 1, eksik 0, fazla 33) · ad 55/56 · UI piksel %47.59 |
| S16 | dereceli | Dereceli ana ekran | ✓ · tekrar Δ1598px | Scripts/Screens/RankedQueue.cs (yalnız kuyruk; dereceli ana ekranı yok) | eşleşti | [safe18-all-tr] metin 48/48 (kayık 0, eksik 0, fazla 0) · ad 48/49 · UI piksel %6.58 |
| G01 | dagitim | Masa yükleniyor | ✓ · tekrar Δ1309px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor | [safe18-all-tr] metin 26/30 (kayık 2, eksik 2, fazla 2) · ad 26/41 · UI piksel %4.44 |
| G02 | dagitim | Bot balyaları diziyor | ✓ · tekrar Δ5188px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 26/28 (kayık 0, eksik 2, fazla 2) · ad 17/37 · UI piksel %12.68 |
| G03 | dagitim | Sen diziyorsun | ✓ · tekrar Δ4978px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 25/28 (kayık 0, eksik 3, fazla 5) · ad 17/37 · UI piksel %12.22 |
| G04 | dagitim | Zar at düğmesi | ✓ · tekrar Δ2869px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 25/28 (kayık 0, eksik 3, fazla 5) · ad 17/37 · UI piksel %14.67 |
| G05 | dagitim | Zar yuvarlanıyor | ✓ · tekrar Δ46456px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 25/28 (kayık 0, eksik 3, fazla 5) · ad 17/37 · UI piksel %15.11 |
| G06 | dagitim | 1. zar · Tekrar at | ✓ · tekrar Δ4562px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 25/28 (kayık 0, eksik 3, fazla 5) · ad 17/37 · UI piksel %14.77 |
| G07 | dagitim | Gösterge açılıyor | ✓ · tekrar Δ60352px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 26/32 (kayık 1, eksik 5, fazla 5) · ad 17/41 · UI piksel %15.97 |
| G08 | dagitim | Gösterge → okey kartı, fazla taş | ✓ · tekrar Δ5854px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 28/32 (kayık 0, eksik 4, fazla 4) · ad 17/41 · UI piksel %14.42 |
| G09 | dagitim | Balyalar dağıtılıyor | ✓ · tekrar Δ4372px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 28/32 (kayık 1, eksik 3, fazla 3) · ad 17/41 · UI piksel %14.36 |
| G10 | dagitim | Kalan balyalar ortaya | ✓ · tekrar Δ4698px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 28/32 (kayık 1, eksik 3, fazla 3) · ad 17/41 · UI piksel %12.87 |
| G11 | dagitim | Istakalar doluyor | ✓ · tekrar Δ5574px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 19/32 (kayık 9, eksik 4, fazla 4) · ad 17/41 · UI piksel %38.52 |
| G12 | dagitim | Oyun başladı (22 taş) | ✓ · tekrar Δ72214px | Table/Scripts/Game/TableCeremony.cs, Hud/TableHud.Deal.cs | bekliyor | [safe18-all-tr] metin 28/30 (kayık 0, eksik 2, fazla 2) · ad 23/38 · UI piksel %15.79 |
| G13 | oyun | El açıldı: perler ortada | ✓ · tekrar Δ4639px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 31/33 (kayık 1, eksik 1, fazla 1) · ad 25/40 · UI piksel %15.86 |
| G14 | oyun | İşleme testi (herkes açık) | ✓ · tekrar Δ3509px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/34 (kayık 6, eksik 0, fazla 0) · ad 25/40 · UI piksel %17.39 |
| G15 | oyun | Altın işleme göstergeleri | ✓ · tekrar Δ4575px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/34 (kayık 6, eksik 0, fazla 0) · ad 25/40 · UI piksel %14.62 |
| G16 | oyun | Yeşil okey alma göstergesi (Y4 seçili) | ✓ · tekrar Δ3904px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/34 (kayık 6, eksik 0, fazla 0) · ad 25/40 · UI piksel %14.67 |
| G17 | oyun | Okey alındı (Y4 ↔ okey) | ✓ · tekrar Δ16884px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 35/36 (kayık 1, eksik 0, fazla 0) · ad 27/42 · UI piksel %13.16 |
| G18 | oyun | İşleme yapıldı + Geri topla | ✓ · tekrar Δ3085px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 33/34 (kayık 1, eksik 0, fazla 0) · ad 25/40 · UI piksel %14.89 |
| G19 | oyun | Geri toplandı | ✓ · tekrar Δ2855px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/34 (kayık 5, eksik 1, fazla 1) · ad 25/40 · UI piksel %14.2 |
| G20 | oyun | Hatalı açma (turuncu Aç) | ✓ · tekrar Δ7076px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 30/32 (kayık 1, eksik 1, fazla 1) · ad 24/39 · UI piksel %13.8 |
| G21 | oyun | Hatalı per masada | ✓ · tekrar Δ3838px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/33 (kayık 4, eksik 1, fazla 2) · ad 24/39 · UI piksel %13.82 |
| G22 | oyun | Ceza tostu | ✓ · tekrar Δ9020px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 30/32 (kayık 1, eksik 1, fazla 2) · ad 24/39 · UI piksel %15.46 |
| G23 | oyun | İşlek taş testi | ✓ · tekrar Δ6029px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/34 (kayık 6, eksik 0, fazla 0) · ad 25/40 · UI piksel %15.63 |
| G24 | oyun | İşlek taş cezası (B4 atıldı) | ✓ · tekrar Δ4094px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 27/32 (kayık 3, eksik 2, fazla 3) · ad 23/38 · UI piksel %12.71 |
| G25 | oyun | Elden bit düğmesi | ✓ · tekrar Δ19579px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 30/31 (kayık 0, eksik 1, fazla 1) · ad 24/39 · UI piksel %14.09 |
| G26 | oyun | Taş fırlatılıyor | ✓ · tekrar Δ4280px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 30/31 (kayık 0, eksik 1, fazla 1) · ad 24/39 · UI piksel %14.48 |
| G27 | oyun | Nükleer patlama | ✓ · tekrar Δ5804px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 30/31 (kayık 0, eksik 1, fazla 1) · ad 24/39 · UI piksel %13.73 |
| G28 | oyun | ELDEN BİTTİN | ✓ · tekrar Δ19872px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 29/31 (kayık 1, eksik 1, fazla 1) · ad 24/39 · UI piksel %14.54 |
| G29 | oyun | El sonu (kazandın) | ✓ · tekrar Δ5701px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 29/31 (kayık 1, eksik 1, fazla 1) · ad 24/39 · UI piksel %17.01 |
| G30 | oyun | Sıra sende | ✓ · tekrar Δ14166px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/31 (kayık 1, eksik 2, fazla 3) · ad 23/39 · UI piksel %16.91 |
| G31 | oyun | Soldan alındı + Geri bırak | ✓ · tekrar Δ14967px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 29/33 (kayık 2, eksik 2, fazla 7) · ad 25/41 · UI piksel %15.87 |
| G32 | oyun | Masa ayarları | ✓ · tekrar Δ2687px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 83/88 (kayık 3, eksik 2, fazla 32) · ad 70/85 · UI piksel %6.58 |
| G33 | oyun | Masadan kalk onayı | ✓ · tekrar Δ11697px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 27/34 (kayık 5, eksik 2, fazla 2) · ad 23/43 · UI piksel %10.17 |
| G34 | oyun | El sonu (Ayşe kazandı) | ✓ · tekrar Δ2274px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 61/68 (kayık 5, eksik 2, fazla 1) · ad 59/75 · UI piksel %7.03 |
| T01 | masa | Bekleme: 2/4 oyuncu | ✓ · tekrar Δ2723px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 18/19 (kayık 1, eksik 0, fazla 0) · ad 17/26 · UI piksel %9.19 |
| T02 | masa | Masa doldu, geri sayım | ✓ · tekrar Δ5250px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 16/26 (kayık 10, eksik 0, fazla 0) · ad 19/36 · UI piksel %43.94 |
| T03 | masa | Dağıtım: girdap, karma | ✓ · tekrar Δ2048px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 26/28 (kayık 0, eksik 2, fazla 2) · ad 17/37 · UI piksel %13.98 |
| T04 | masa | Dağıtım bitti, İlk hamle senin | ✓ · tekrar Δ5518px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 26/30 (kayık 2, eksik 2, fazla 2) · ad 23/38 · UI piksel %14.95 |
| T05 | masa | Çekme aşaması, Ayşe'nin perleri | ✓ · tekrar Δ12249px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/31 (kayık 1, eksik 2, fazla 3) · ad 23/39 · UI piksel %14.01 |
| T06 | masa | Çay ısmarlandı bildirimi | ✓ · tekrar Δ9093px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [safe18-all-tr] metin 28/31 (kayık 1, eksik 2, fazla 3) · ad 23/39 · UI piksel %14.08 |
| T07 | masa | Oyuncu kartı (Zeynep), hızlı ikram | ✓ · tekrar Δ8203px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [safe18-all-tr] metin 44/47 (kayık 1, eksik 2, fazla 4) · ad 39/54 · UI piksel %11.67 |
| T08 | masa | Hediye paneli · İkram | ✓ · tekrar Δ7176px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [safe18-all-tr] metin 55/59 (kayık 2, eksik 2, fazla 3) · ad 56/72 · UI piksel %7.52 |
| T09 | masa | Hediye paneli · Hediye (Pasta) | ✓ · tekrar Δ5543px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [safe18-all-tr] metin 51/55 (kayık 2, eksik 2, fazla 3) · ad 52/68 · UI piksel %8.14 |
| T10 | masa | Hediye paneli · Emoji | ✓ · tekrar Δ5838px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [safe18-all-tr] metin 52/65 (kayık 11, eksik 2, fazla 3) · ad 62/78 · UI piksel %7.1 |
| T11 | masa | Roket uçuşu | ✓ · tekrar Δ261144px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [safe18-all-tr] metin 30/34 (kayık 2, eksik 2, fazla 3) · ad 23/42 · UI piksel %17.81 |
| T12 | masa | Roket indi + teşekkür balonu | ✓ · tekrar Δ9570px | Table/Scripts/Game/TableGifts.cs, Hud/TableHud.Gifts.cs | bekliyor | [safe18-all-tr] metin 28/31 (kayık 1, eksik 2, fazla 2) · ad 23/39 · UI piksel %16.37 |
| T13 | masa | Sohbet paneli | ✓ · tekrar Δ3972px | Table/Scripts/Hud/TableHud.Social.cs | bekliyor | [safe18-all-tr] metin 46/49 (kayık 1, eksik 2, fazla 3) · ad 46/61 · UI piksel %7.65 |
| T14 | masa | El açıldı · Yere aç / İşle | ✓ · tekrar Δ3915px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 31/33 (kayık 1, eksik 1, fazla 1) · ad 25/40 · UI piksel %13.03 |
| T15 | masa | Ayarlar · Görünüm | ✓ · tekrar Δ1022px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [safe18-all-tr] metin 83/88 (kayık 3, eksik 2, fazla 32) · ad 70/85 · UI piksel %6.63 |
| T16 | masa | Masa ayarları · Oyun | ✓ · tekrar Δ2449px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [safe18-all-tr] metin 51/55 (kayık 2, eksik 2, fazla 2) · ad 55/70 · UI piksel %4.95 |
| T17 | masa | Masa ayarları · Ses | ✓ · tekrar Δ1689px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [safe18-all-tr] metin 48/52 (kayık 2, eksik 2, fazla 2) · ad 56/71 · UI piksel %6.19 |
| T18 | masa | Masa ayarları · Bildirimler | ✓ · tekrar Δ2346px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [safe18-all-tr] metin 43/47 (kayık 2, eksik 2, fazla 2) · ad 50/65 · UI piksel %4.84 |
| T19 | masa | Tema: Ceviz salon + Klasik ceviz ıstaka | ✓ · tekrar Δ14955px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [safe18-all-tr] metin 29/31 (kayık 1, eksik 1, fazla 2) · ad 23/39 · UI piksel %11.85 |
| T20 | masa | Tema: Hologram + Neon cam ıstaka | ✓ · tekrar Δ25799px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [safe18-all-tr] metin 29/31 (kayık 1, eksik 1, fazla 2) · ad 23/39 · UI piksel %14.54 |
| T21 | masa | Tema: Andromeda masa + ıstaka | ✓ · tekrar Δ10346px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [safe18-all-tr] metin 29/31 (kayık 1, eksik 1, fazla 2) · ad 23/39 · UI piksel %14.38 |
| T22 | masa | Kuşbakışı görünüm (Andromeda) | ✓ · tekrar Δ5317px | Table/Scripts/Hud/TableHud.Windows.cs, Core/TableSettings.cs | bekliyor | [safe18-all-tr] metin 29/31 (kayık 1, eksik 1, fazla 2) · ad 23/39 · UI piksel %16.4 |
| T23 | masa | Bitiş anı: Ayşe bitirdi | ✓ · tekrar Δ6455px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 29/33 (kayık 2, eksik 2, fazla 2) · ad 23/42 · UI piksel %28.37 |
| T24 | masa | El sonu tablosu | ✓ · tekrar Δ3153px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 61/68 (kayık 5, eksik 2, fazla 1) · ad 59/75 · UI piksel %6.95 |
| T25 | masa | Masadan kalk onayı | ✓ · tekrar Δ8869px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 27/34 (kayık 5, eksik 2, fazla 2) · ad 23/43 · UI piksel %10.29 |
| T26 | masa | Bağlantı koptu | ✓ · tekrar Δ5520px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 30/36 (kayık 5, eksik 1, fazla 1) · ad 29/44 · UI piksel %13.52 |
| T27 | masa | EN — oyun sırasında | ✓ · tekrar Δ12860px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 26/31 (kayık 1, eksik 4, fazla 5) · ad 23/39 · UI piksel %14.35 |
| T28 | masa | Botlara karşı: başlık, ANTRENMAN, el sayacı | ✓ · tekrar Δ32733px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 24/31 (kayık 5, eksik 2, fazla 4) · ad 23/39 · UI piksel %15.82 |
| T30 | masa | Botlara karşı · masadan kalk onayı | ✓ · tekrar Δ8555px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 23/34 (kayık 9, eksik 2, fazla 3) · ad 23/43 · UI piksel %11.15 |
| T33 | masa | Okey rozeti masada | — | Table/Scripts/Hud/TableHud.cs + Game/RackInput.cs okey ipucu (ShowOkeyTip) | kaldırıldı | kaldırıldı: masada okey temsil rozeti yok; okeyin değeri perdeki konumundan çıkar (R-13) · Unity karşılığı kaldırılacak |
| T34 | masa | Ceza: attığın K9 ile bot açtı (+90, R-43) | ✓ · tekrar Δ9560px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 26/35 (kayık 8, eksik 1, fazla 4) · ad 23/42 · UI piksel %16.65 |
| T35 | masa | Eksik açılış cezası (+101) | ✓ · tekrar Δ7821px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/33 (kayık 4, eksik 1, fazla 2) · ad 24/39 · UI piksel %14.57 |
| T36 | masa | Ceza: masadaki okeyin alındı (+101, R-44) | ✓ · tekrar Δ10113px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 27/36 (kayık 8, eksik 1, fazla 3) · ad 23/42 · UI piksel %14.81 |
| T38 | masa | Yerden alındı + Geri bırak | ✓ · tekrar Δ14244px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 29/33 (kayık 2, eksik 2, fazla 7) · ad 25/41 · UI piksel %15.88 |
| T39 | masa | Soldan alınan taş halkaya geri sürükleniyor | ✓ · tekrar Δ9928px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 32/36 (kayık 2, eksik 2, fazla 7) · ad 25/44 · UI piksel %15.51 |
| T40 | masa | Mesaj kataloğu | — | — | kaldırıldı | kaldırıldı: Mesaj kataloğu bir referans sayfası: Referans - Masa Ek Durumlar |
| T41 | masa | Mesaj kuyruğu | — | — | kaldırıldı | kaldırıldı: Mesaj kuyruğu referans sayfasında; oyunda tek satırlık sistem mesajı + tost |
| T42 | masa | El sonu · botlara karşı | ✓ · tekrar Δ4261px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 57/68 (kayık 9, eksik 2, fazla 2) · ad 59/75 · UI piksel %6.87 |
| T43 | masa | El sonu · çevrimiçi masa | ✓ · tekrar Δ1293px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 61/68 (kayık 5, eksik 2, fazla 1) · ad 59/75 · UI piksel %6.91 |
| T44 | masa | El sonu puansız (çiftle bitiş) | — | Table/Scripts/Hud/TableHud.cs el sonu | kaldırıldı | kaldırıldı: Çiftle bitiş artık puanlanır (×2, R-50…R-56); "Bu el puanlanmadı" yok · Unity karşılığı kaldırılacak |
| T45 | masa | Maç sonu · kazandın | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd + Game/TableGame.cs ShowMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok; maç sonu lobide tost (dereceli: S16 sonuç) · Unity karşılığı kaldırılacak |
| T46 | masa | Maç sonu · 2. sıra | — | Table/Scripts/Hud/TableHud.cs BuildMatchEnd | kaldırıldı | kaldırıldı: Ayrı maç sonu ekranı yok (T45 ile aynı) · Unity karşılığı kaldırılacak |
| T47 | masa | Okeyi al: Y4 sürükleniyor, uygun hedefler parlıyor | ✓ · tekrar Δ4881px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 27/34 (kayık 6, eksik 1, fazla 1) · ad 25/40 · UI piksel %14.97 |
| T48 | masa | Okey alma ipucu | — | Table/Scripts/Game/TableGame.cs ShowOkeyTip | kaldırıldı | kaldırıldı: Okeye uzun basma ipucu yok · Unity karşılığı kaldırılacak |
| T49 | masa | Okeyi al: yanlış taşla deneme (K5) | ✓ · tekrar Δ7299px | Table/Scripts/Game/TableGame.cs, Hud/TableHud.cs | bekliyor | [safe18-all-tr] metin 28/34 (kayık 5, eksik 1, fazla 1) · ad 25/40 · UI piksel %14.42 |
| T50 | masa | Menü → masa yükleme ekranı | ✓ · tekrar Δ7217px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor | [safe18-all-tr] metin 25/30 (kayık 2, eksik 3, fazla 3) · ad 26/41 · UI piksel %5.62 |
| T51 | masa | Masa açılamadı hatası | ✓ · tekrar Δ658px | Scripts/Screens/TableLoadingOverlay.cs | bekliyor | [safe18-all-tr] metin 7/10 (kayık 1, eksik 2, fazla 26) · ad 13/14 · UI piksel %4.57 |
