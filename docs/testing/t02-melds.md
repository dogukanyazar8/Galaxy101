# T02 — Perler, çiftler ve okey karşılıkları

Durum: Tamamlandı. Tarih: 2026-10-20.

## Uygulanan davranış

- `MeldValidator.Validate`: sıralı per, aynı sayılı grup ve çift doğrulaması; geçerli grupta kesin sayı toplamı.
- 3–13 taşlık aynı renk sıralı per; 13'ten 1'e dönüş yok. Taş seçme sırası üyeliği değiştirmez; girdi yerinde sıralanmaz.
- Aynı sayılı grupta 3/4 farklı renk; çiftte tamamen eşleşen iki renk/sayı.
- Gerçek okeyin açıkça belirtilen karşılığı doğrulanır. Sahte okey yalnız kendi normal değeriyle kullanılır.
- Fiziksel taş tekrarı, normal taşın başka yüzle gösterilmesi, eksik/geçersiz grup reddedilir.
- `MeldCandidateGenerator.Generate`: elin tüm geçerli alt grupları, fiziksel kopyaları ve joker karşılıkları. Doğal karşılık eldeyken okey kullanılan alternatifler de korunur.
- İki gerçek okeyden çift için 52 renk/sayı karşılığı bulunur; aynı çiftin taş sırası nedeniyle tekrar üretilmesi engellenir.
- Öncelik: toplam azalan; eşitlikte kanonik renk dizisi, fiziksel kimlik ve karşılık sırası. Elin giriş sırası sonucu değiştirmez.
- Aday üretim girdisi en fazla 22 farklı fiziksel taştır. Geçersiz programlama girdisi exception ile; beklenen geçersiz grup RuleResult/ErrorCode ile bildirilir.

## Dosyalar

- [Doğrulama](../../Packages/com.okey101.core/Runtime/Rules/MeldValidator.cs)
- [Aday üretimi](../../Packages/com.okey101.core/Runtime/Rules/MeldCandidateGenerator.cs)
- [Doğrulama testleri](../../Packages/com.okey101.core/Tests/Editor/MeldValidatorTests.cs)
- [Aday testleri](../../Packages/com.okey101.core/Tests/Editor/MeldCandidateTests.cs)

## Gözlenen testler

```powershell
& .\scripts\Test-Unity.ps1 -Platform EditMode -Filter 'Okey101.Core.Tests.MeldValidatorTests;Okey101.Core.Tests.MeldCandidateTests'
& .\scripts\Test-Unity.ps1 -Platform EditMode
```

- İlk doğrulama RED: 15 test, 10 başarısız; geçerli gruplar reddediliyor ve ilgili hata kodları oluşmuyordu.
- Doğrulama uygulandıktan sonra aday üretimi RED: toplam 30 testte adaylara ait 14 başarısızlık; üretici henüz boş dönüyordu.
- Uygulama sonrası ilk dar kontrol: 30/30 geçti.
- Ek sınır testleri: aynı sayıyla eksik sıra tamamlama reddi; 1–13'ün 66 alt aralığı; yoğun 22 taşlık elde tam aday sayısı.
- Son tam EditMode çalışması: **54/54 geçti; 0 başarısız, 0 atlanan, 0 belirsiz.** T02'nin 33 testi ve T01'in 21 testi birlikte çalıştı.

[Son NUnit sonucu](../../artifacts/test-results/20261020T164317311Z-26cdef92/results.xml)
[Son Unity günlüğü](../../artifacts/test-results/20261020T164317311Z-26cdef92/editor.log)

## Yoğun el ölçümü

Kırmızı 1–10'un ikişer kopyası ve iki gerçek okeyle **124.724** farklı sıralı per adayı üretilir. Beklenen sayı fiziksel seçimlerin bağımsız kombinatorik sayımıyla sabitlenmiştir; aday sınırı veya geçerli sonuç eleme kullanılmaz.

Editor testinde renk sıralamasının enum `CompareTo(object)` kullanması boxing oluşturuyordu. Aynı sıralamayı koruyan tamsayı karşılaştırmasıyla ölçüm 1.963 ms'den 531 ms'ye indi. Süre tanısal çıktıdır; dalgalanabilen bir zaman eşiği test başarısını belirlemez. Bu, paketlenmiş Windows oyununun FPS ölçümü değildir.

Unity'nin bu çalışmadaki `GC.GetAllocatedBytesForCurrentThread` sayacı gerçek nesne üretimine rağmen 0 bildirdi. Bu nedenle tahsis ölçümü olarak kullanılmadı; yanıltıcı sayaç çıktısı kaldırıldı.

## Sonraki görevler için sözleşme

T04, sıralı grubun uçlarını temsil edilen sayılardan veya kanonik dizilimden belirlemeli; kullanıcı seçim sırasına güvenmemeli. T05'in kesin araması, T09'un strateji bütçesinden bağımsız kalmalı. Yoğun el üretimi görüntü döngüsünde çalıştırılmamalı; onaylı tasarımdaki iş kuyruğu sınırı T07/T09'da uygulanacak.

Bu görev masa işlemesi, açılış eşiği, puanlama, bot veya görüntü üretmez. PlayMode ve Windows build kontrolleri çalıştırılmadı. Bağımlılık, Git commit veya push yok.
