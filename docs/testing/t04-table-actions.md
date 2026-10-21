# T04 — Açılış, taş işleme ve okey değiştirme

Durum: Tamamlandı. Tarih: 2026-10-20.

## Uygulanan davranış

`TableEvaluator.Evaluate(drawnState, actions)` sıralı masa eylemlerini özel bir aday kopyaya uygular. Başarıda değiştirilemez yeni RoundState döner; herhangi bir hata tüm planı reddeder. Kaynak durum hiçbir aşamada değişmez. Kurallar mevcut MeldValidator üzerinden doğrulanır; Unity bağımlılığı veya yeni paket yoktur.

- İlk seri açılışı yeni gruplarla en az 101; ilk çift açılışı en az beş çift gerektirir. İşlenen taş açılış toplamına katılmaz. Gerçek okeyin ilan edilmiş karşılığı toplama girer.
- Açılış türü değişmez. Açılmış oyuncu aynı türde yeni gruplar için yeniden 101/beş çift koşuluna tabi değildir. `OpenGroups` aynı türde birden fazla grubu, `AddGroup` açılış sonrası tek grubu ekleyebilir.
- Oyuncu kendi açılışını tamamlamadan gruba taş ekleyemez veya okey alamaz. Aynı plan içinde önce açılış, ardından işleme/değişim geçerlidir.
- Seri açan, herhangi bir oyuncu çift açmışsa kendi adına yeni çiftler ekleyebilir; açılışı Series kalır. Çift açan yeni seri/sayı grubu açamaz. Çiftler üç taşa genişletilemez.
- Çift açanın seri/sayı gruplarına işlemesi tur toplamında en fazla iki fiziksel taştır. Bu gruplardaki okey değişimi bir hak tüketir. Bir eylemde iki taş eklemek iki hak tüketir; bütçe sonraki aday değerlendirmesinde de korunur.
- Yeni çiftler ve çift grubundaki okey değişimleri bu bütçeyi tüketmez. Seri açanın seri/sayı gruplarına işlemesinde sayısal sınır yoktur; SeriesTilesUsed yine işlenen taşları sayar.
- Sıralı per uçları temsil edilen en küçük/en büyük sayıyla belirlenir; kullanıcı seçme sırasına güvenilmez. Prepend yönü doğrulanır, geçerli genişletme sonrası sıra artan biçimde tutulur. Mevcut taşların kimlik/karşılık eşleşmesi değişmez.
- Okey yerine yalnız aynı sabit karşılığa sahip, eldeki joker olmayan fiziksel taş konabilir. Sahte okey yalnız normal karşılığı eşleşiyorsa geçerlidir. Geri alınan aynı fiziksel okey elde kalabilir veya aynı planda yeni karşılıkla tekrar kullanılabilir; seri/sayı grubuna tekrar işlenirse bir hak daha tüketir.
- Yeni grup kimlikleri mevcut en yüksek kimlikten devam eder; sahip aktif oyuncudur. Genişletilen/değiştirilen grubun kimliği ve sahibi korunur. Kimlik alanı dolarsa taşma yerine InvalidState döner.
- Eksik/çoğaltılmış fiziksel kimlik içeren kaynak durum, bozuk mevcut grup, eksik eylem listesi ve yanlış evre reddedilir. Beklenen oyun hataları RuleResult/ErrorCode ile bildirilir.

## Dosyalar

- [TableEvaluator](../../Packages/com.okey101.core/Runtime/Rules/TableEvaluator.cs)
- [Açılış testleri ve ortak test yardımcıları](../../Packages/com.okey101.core/Tests/Editor/OpeningTests.cs)
- [İşleme testleri](../../Packages/com.okey101.core/Tests/Editor/ProcessingTests.cs)
- [Okey değiştirme testleri](../../Packages/com.okey101.core/Tests/Editor/JokerSwapTests.cs)

Unity bu dört kaynak dosyanın `.meta` dosyalarını üretti. Önceki model, validator, puanlama ve test betikleri değiştirilmedi.

## Gözlenen doğrulama

```powershell
& .\scripts\Test-Unity.ps1 -Platform EditMode -Filter 'Okey101.Core.Tests.OpeningTests;Okey101.Core.Tests.ProcessingTests;Okey101.Core.Tests.JokerSwapTests'
& .\scripts\Test-Unity.ps1 -Platform EditMode
```

- İlk RED: **57 test, 55 başarısız**. Derlenen minimum API yalnız InvalidState dönüyordu; masa durumuna ve hata kodlarına ilişkin assertion'lar başarısız oldu. Geçersiz kaynak ve kimlik alanı tükenmesi testleri minimum API'de zaten geçiyordu.
- İlk uygulama sonrası dar GREEN: **57/57**.
- Ek kapsam: aynı tur açılıp okey değiştirme; aynı çiftteki iki fiziksel okeyin ayrı ayrı geri alınması; sayı grubundaki değişim bütçesi; art arda yeni çift ekleme; sahte yüz beyanı ve bozuk mevcut grubun reddi.
- Son dar GREEN: **64/64** — OpeningTests 23, ProcessingTests 23, JokerSwapTests 18.
- Tam EditMode: **161/161 geçti; 0 başarısız, 0 atlanan, 0 belirsiz.** Önceki 97 test de geçti. Son Unity günlüğünde C# derleyici hata/uyarısı bulunmadı.

[İlk RED sonucu](../../artifacts/test-results/20261020T170508897Z-8728a58f/results.xml)

[İlk GREEN sonucu](../../artifacts/test-results/20261020T170622692Z-9fd53023/results.xml)

[Son dar sonuç](../../artifacts/test-results/20261020T170741299Z-efd0adf6/results.xml)

[Tam NUnit sonucu](../../artifacts/test-results/20261020T170758604Z-eaf74441/results.xml)

[Tam Unity günlüğü](../../artifacts/test-results/20261020T170758604Z-eaf74441/editor.log)

Test fixture'ları RoundTestBuilder ile 106 kimliği tam birer konuma yerleştirir. Başarılı sonuçlarda StateInvariant yeniden doğrulanır. Başarılı ve hatalı planlarda kaynak durumun bütün taş konumları, grup sahipleri, kimlikleri, okey karşılıkları ve tur bilgileri doğrudan karşılaştırılır; hata sonrası yeniden denemede grup kimliği de kaymamış olur.

## T05/T06 sınırı

Bu API yalnız masa taslağını doğrular. Son taşı atma, yandan alınan kesin fiziksel taşı kullanma, ceza oluşumu/onayı T05'te; sıra/sürüm/komut sequence ilerletme ve nihai el sonucu T06'da uygulanacak. T04 başarısı tek başına tamamlanabilir tam tur anlamına gelmez.

PendingDraw, OpeningAtTurnStart, IsInitialTurn, gösterge/deste/atıklar, ceza puanları, aktif oyuncu, tur numarası, sürüm ve sequence bilgileri korunur. Açılış/işleme taslağı kesinleşmiş çekişi geri almaz.

PlayMode, Windows build ve görsel/oynanabilir masa kabulü çalıştırılmadı. Git init/commit/push işlemi yapılmadı. Sıradaki görev T05.
