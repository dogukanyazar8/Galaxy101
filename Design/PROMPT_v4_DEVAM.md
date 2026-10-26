# Galaxy 101 · v4 devam görevi (kalan işler)

Bu dosyanın çizgiden sonraki kısmı yeni bir Claude Code oturumuna olduğu gibi verilir. Çalışma klasörü: `C:\GD\Okey 101`. Bu görev, `Design/PROMPT_v4_OTONOM_GELISTIRME.md`'nin devamıdır; oradaki bütün kurallar burada da bağlayıcıdır.

---

## Görev

Sen bu deponun kıdemli Unity istemci ve .NET sunucu geliştiricisisin. Galaxy 101 (101 Okey) v4 çalışmasının yarısı bitti. Kalan işleri **otonom, sırayla, en yüksek kalitede ve dikkatle** bitir; sonunda tek bir rapor ver. Kullanıcıya ara soru sorma: belirsizliği kayda geçir ve sonraki işe geç.

Asıl görev metni, kurallar ve kaynak önceliği: `Design/PROMPT_v4_OTONOM_GELISTIRME.md`. **Önce onu baştan sona oku.** Sonra `docs/v4/ILERLEME.md`, `docs/v4/KARARLAR.md`, `docs/v4/RAPOR.md` (taslak), `docs/v4/DURUMLAR.md`, `docs/v4/EKSIKLER.md` ve `docs/v4/UNITY_FARKLARI.md`'yi oku. Hatların raporları: `docs/v4/{hat-a,hat-b,hat-c,hat-d,menu,lobby,ranked,audio,table}/RAPOR.md`.

## Değişmez kurallar (özet; tam hali asıl görevde)

- **Uydurma yok.** Her değer pakette bir kaynağa dayanır (dosya + anahtar/satır/durum kimliği). Kaynak yoksa yapma ya da mevcut davranışı koru; bir `EKSIKLER` kaydı aç (kimlik, grup T/K/D/U/P, ne eksik, nerelere bakıldı, etkisi, geçici durum).
- **Dış dünyaya giden işlem yok:** push, deploy, Railway, gerçek ödeme, e-posta, mağaza. Yerel commit serbest; yerel Docker serbest.
- **Her adım kanıtla biter:** derleme temiz, ilgili testler yeşil, görsel karşılaştırma yapılmış, yerel commit atılmış. "Bitti" demeden önce komut çıktısıyla doğrula. Sayıları tahmin etme; koş ve oku.
- **Unity tek süreç.** Aynı anda yalnız bir Unity editörü, batch işlemi ya da Development oyuncusu çalışır, yalnız ana kopyada ve sırayla. Kod yazımı worktree'lerde paralel olabilir (K-11); Unity doğrulaması ana kopyada.
- **Sıfırdan yazma yok;** mevcut kodu düzelt. Bir parçayı baştan yazman gerekirse gerekçesini `KARARLAR.md`'ye yaz.
- **Gizli bilgi** sunucuda kalır; istemci rakip ıstakasını ve deste sırasını asla almaz.
- `docs/v4/ILERLEME.md`'yi her kilometre taşında güncelle. Bağlam sıfırlanırsa oradan devam et.
- Commit mesajları İngilizce ve şu satırla biter: `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Şu anki durum (26 Ekim 2026, 08:30)

- `main` = `aed3a0d`, temiz. Faz 0 ve Faz 1 tamam. Faz 2'de menü, giriş, lobi, Dereceli, ses, imleç ve masa (v4 çalışma zamanı) ana kopyada.
- Durum tablosu: 59 eşleşti, 7 bilinçli fark, 78 bekliyor (7 kaldırılmış). Masa dışı 70 durumun 66'sı tamam. Menünün son düzeltmesi (60, 61, 95, 81) birleşti ama Unity'de doğrulanmadı.
- Masa hattı `v4/table` (worktree `C:/GD/Okey101-v4/table`): `b6c4733` henüz `main`'de değil. Worktree'de 3 commit edilmemiş dosya var (`TableGame.cs`, `TableHud.cs`, `TableScene.cs`). Bunlar, bilgisayar kapanınca yarıda kalan işin parçası. **Körü körüne commit etme:** önce `git diff` ile oku, derle; tutarlı ve tamamsa commit et, değilse tamamla ya da geri al ve kararını yaz.
- Son test sayıları: Node 106/106 + 26/26; `dotnet test tools/v4/nunit/CoreV4Tests.csproj` 791/791; masa istemcisi `dotnet test tools/v4/nunit-table/TableClientTests.csproj` 38/38; sunucu xUnit 130/130; Unity EditMode 1006/1006; PlayMode 147 testin 144'ü geçti. İki canlı masa testi kırmızı (düzeltmesi `b6c4733`'te), biri `GALAXY_AUDIT_DIR` olmadığı için atlandı.
- Bilgisayar yeniden başladı. Docker Desktop'ı ve yerel sunucuyu yeniden başlat: `scripts/Start-LocalServer.ps1` (port 5173, arka planda çalıştır).

## Araçlar (hepsi depoda)

- Unity'siz derleme: `python tools/v4/unity_compile.py --project <Galaxy101.PlayMode.Tests | Galaxy101.Service.Tests | Galaxy101.Editor.Tests | Okey101.PlayMode.Tests>`. Worktree için `--repo <worktree> --csproj-from "C:/GD/Okey 101"`.
- Unity testleri: `scripts/Test-Unity.ps1 -Platform EditMode|PlayMode -Graphics [-Filter "A;B"]`.
- Development derlemesi: `scripts/Build-Windows.ps1 -Configuration Development`.
- Durum yakalama: `scripts/Capture-G101States.ps1 -BuildDirectory <build> -OutputDirectory artifacts/v4/unity/runN -States all|<id,id> [-Lang en] [-Theme mars] [-Size 1920x1080] [-FpsSeconds 5]`.
- Karşılaştırma ve tablo: `uv run --with pillow python tools/v4/compare_states.py <runDir>`, sonra `python tools/v4/apply_compare.py <runDir>/compare.json` (DURUMLAR.md'yi de yeniden üretir).
- Referans kareler: `artifacts/v4/ref/<ID>.png|.layout.json|.text.json`, `ref/en/`, `ref/themes/<tema>/`. Yeniden çekmek gerekirse `tools/ref_capture.py`.
- Matris ve eksik özeti: `python tools/v4/matrix_summary.py --write`. Paket testleri: `node tools/v4/run_package_tests.js`.
- Sunucu testleri (kilitli bin'e yazmamak için): `dotnet build Server/tests/Galaxy101.Server.Tests/Galaxy101.Server.Tests.csproj -p:OutDir=<geçici>/`, sonra `dotnet test <geçici>/Galaxy101.Server.Tests.dll`.

## Öğrenilen tuzaklar (tekrarlama)

1. **Satır ortası yorum:** bu kod yoğun tek satırlık C# yazılmış. `//` koyduğun yerden sonrası yorum olur. İki kez kod yuttu (rozet sınıfı, mağaza fontu). Yorum her zaman satırın sonunda. Commit öncesi taramak için: `grep -nE '//[^"]*\)[a-z]+\.(style|Add|AddToClassList)'`.
2. **Yönlendirici sanal ortamda koşar** (`GalaxyApi.Override` reddedilen port). Yerel sunucu açıkken hatırlanan oturum giriş durumlarını atlıyordu; bu korumayı bozma.
3. **Donma anı:** referanslar 100000 ms'de donmuş (EK-005). Unity'de `GameClock.Freeze(0)`. Donmuş saat hiçbir şeyi ilerletmemeli; masa hattı bunu düzeltti, koru.
4. Dil ve tema menü kurulmadan önce yalıtılmış kayda yazılır. Sonradan değişirse giriş katmanı düşer.
5. Ana kopyanın Unity csproj'larında yeni asmdef'ler olmayabilir. `unity_compile.py` bunları şablondan üretir; gerçek sonuç Unity derlemesidir.
6. Satır sonu: `text=auto` olduğu için kopya karşılaştırmaları içeriğe bakmalı, bayta değil.
7. UI Toolkit `letterSpacing`'i bu projede `MenuView.Tracking(el, px, fontSize)` ile verilir.
8. `UF-04`: filtreli kart yeniden kurulurken UI Toolkit motor günlüğü yazar; yalnız test ortamında. Yeni bir `ignoreFailingMessages` ekleme; önce nedenini ara.
9. Alt ajanlar Unity çalıştıramaz. Görsel doğrulama ve Unity testleri hep sende.
10. Ajanın yaptığı işi, birleştirmeden önce diff'ini okuyarak ve derleyerek kontrol et. Rapor kanıt değildir.

## Kalan işler (bu sırayla)

### 1 · Masa hattını kapat
1. `v4/table` worktree'sindeki 3 dosyayı incele, sonuçlandır, commit et. `git merge main` yap, derle (4 test projesi).
2. `dotnet test tools/v4/nunit-table/TableClientTests.csproj` (yerel sunucu açıkken canlı testler dahil) ve `CoreV4Tests` koş.
3. `main`'e birleştir. Unity'de EditMode ve PlayMode koş (yerel sunucu açık). Hedef: hiç kırmızı yok. Atlanan tek test `HighDpiAuditCaptures` olmalı (ortam değişkeni ister).

### 2 · Masa durumlarının görsel eşitliği (G01–G34, T01–T51)
Kalan en büyük iş. Her durum için döngü (asıl görev Faz 2):
1. Prototipte durumun yolunu (`states.json`), `G101-Masa.dc.html` kodunu, yerleşim dökümünü, `anim/css.json`, `tween3d.json`, `materials.json`, metin anahtarlarını oku.
2. Uygula. Öğe adları `data-g101-id` ile aynı olsun (D-62).
3. Development derle ve yakala. `compare_states.py` + `apply_compare.py` çalıştır. Kare ve yan yana görüntüye kendi gözünle bak.
4. Kabul: eşik içinde ya da nedeni `UNITY_FARKLARI`/`EKSIKLER`'de kayıtlı.

Öncelik sırası (run11 ölçümü):
- G32/T15 masa ayarları "Görünüm" (88 metnin 57'si kayık).
- G34/T24/T42/T43 el sonu paneli (10 eksik, 34 kayık).
- T16–T18 ayar sekmeleri.
- G11, T02, G17 (piksel farkı yüksek).
- T07/T13 (oyuncu kartı, sohbet), T08/T09/T10 (hediye paneli), T51 (masa açılamadı).
- Sonra G01–G12 dağıtım töreni ve kalan oyun durumları.

3D sahne yerleşim dökümünde yok. Onu `tween3d.json`, `materials.json`, `lighting.json`, `sahne_tam` GLB'si ve referans kareyle doğrula. Hat C raporundaki Unity adımları masa varlıkları içindir: v4 GLB'leri, `nukeLight` yüksekliği 0,3 m, `mid` kamera görünümü, andromeda holo renkleri.

UI kutularındaki piksel farkı 3D arka plan yüzünden eşiği aşıyorsa, eşiği gevşetmeden önce farkın kaynağını ölç. Gerekiyorsa masa durumları için ayrı, ölçülmüş ve gerekçesi yazılmış bir eşik koy (`DURUMLAR.md` "Eşikler").

Hız için masa HUD işini parçalara bölüp worktree'lerde alt ajanlara verebilirsin (bir parça = bir dosya grubu, iki ajan aynı dosyaya dokunmaz). Her birleşmeden sonra kendin derle, yakala, karşılaştır.

### 3 · Menünün son dört durumu
60, 61, 95 ve 81 için düzeltme `aed3a0d`'de. Yakalayıp doğrula; kalan farkı kapat.

### 4 · EN ve tema doğrulaması
EN için bütün durumları `-Lang en` ile yakala ve `--ref artifacts/v4/ref/en` ile karşılaştır. Renk taşıyan durumlarda en az bir tema (`-Theme mars`) için `ref/themes/mars` ile karşılaştır. Farkları kapat ya da kayda geçir.

### 5 · Faz 3 uçtan uca (Windows build + yerel sunucu)
- Akışlar: misafir → menü → lobi → masa aç → bekleme odası → maç → maç sonu (masa kapanır, lobide tost) → lobi. Ayrıca eşli, katlamalı (ortak seçeneği açık ve kapalı), dereceli, izleme, oyuna katıl, kopma ve dönme ("Deneme n/5", 3 sn, "Lobiye dön", `snapshot.screen`), masadan kalk, hediye, sohbet, mağaza (sınırına kadar), günlük ödül, arkadaş, liderlik.
- Mümkün olan her akışı otomatik yap. Tercih: canlı sunucuya karşı PlayMode testi, `TableLiveServerTests` deseni, sunucu yoksa Inconclusive. Otomatikleşmeyen adımlar için Development oyuncusunu `-galaxyServer http://localhost:5173` ile çalıştır ve kanıt topla (kare + günlük).
- Ses: her olay doğru anda, doğru kanalda ve düzeyde çalıyor; müzik ekran geçişinde değişiyor. Ses servisinin olay günlüğüyle doğrula.
- Çözünürlük ve FPS bir kez daha: 1280×720, 1920×1080, 2560×1440, 21:9; `-FpsSeconds 5` ile 1080p'de ≥60 FPS.
- Sonuçları `docs/v4/FAZ3.md`'ye yaz.

### 6 · Son koşu ve rapor (Faz 4)
1. Hepsini son kez koş: Node paket testleri, `CoreV4Tests`, `TableClientTests`, sunucu xUnit, Docker imajı + `/health`, Unity EditMode ve PlayMode, 144 durumun tamamı.
2. `matrix_summary.py --write` çalıştır.
3. `docs/v4/RAPOR.md`'nin §2 (test sayıları, gerçek komut çıktılarıyla), §3 (144 durum: eşleşti / bilinçli fark / eksik) ve §7'sini doldur. Bilinçli farkları ve grupladığın EKSIKLER'i güncelle. "Pakette yok" sunucu özelliklerini ve Railway hazırlık listesini koru, güncelle.
4. Kullanıcıya kısa bir özet ver.

## Bitti tanımı

- Paketin 106 vektörü ve 26 akışı C#'ta yeşil. Bütün mevcut testler yeşil (Unity EditMode ve PlayMode dahil, yerel sunucu açıkken; atlanan yalnız ortam isteyen testler).
- 144 etkin durumun hepsi `DURUMLAR.md`'de **eşleşti**, **bilinçli fark** (UNITY_FARKLARI'nda gerekçeli) ya da **eksik** (EKSIKLER'de kayıtlı). "bekliyor" kalmayacak.
- Faz 3 akışları kanıtlı. `RAPOR.md` tamam.
- Bütün iş yerel `main`'de commit'li. Push yok, deploy yok.

## Kullanıcı kararı bekleyenler (karar verme, raporda listele)

- EK-211: bekleme odasında sahip çıkınca masa kapanır (paket) ↔ "sahiplik ilk insana geçer" (eski karar).
- K-10: "Botlara karşı" açık (paket) ↔ "YAKINDA" (eski karar).
- EK-305: masa modelleri cilalı mevcut GLB'ler mi, ham v4 GLB'leri mi.
- EK-413: taş sesleri gerçek kayıt mı, sentez mi.
- Hat EKSIKLER dosyalarındaki diğer K kayıtları.
